using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal enum BrokerConnectivityIssue
{
    NotInstalled,
    NotRunning,
    UpdateRequired
}

internal sealed class BrokerConnectionException : Exception
{
    public BrokerConnectionException(
        BrokerConnectivityIssue issue,
        string message,
        Exception? innerException = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        : base(message, innerException)
    {
        Issue = issue;
        Metadata = metadata;
    }

    public BrokerConnectivityIssue Issue { get; }

    public IReadOnlyDictionary<string, string>? Metadata { get; }
}

internal interface IBrokerInstallationDetector
{
    bool IsInstalled();
}

internal sealed class BrokerInstallationDetector : IBrokerInstallationDetector
{
    private const string BrokerRegistryKeyPath = @"Software\NetVsMcp\Broker";
    private const string BrokerInstalledValueName = "Installed";

    public bool IsInstalled()
    {
        if (IsInstalledInRegistry())
        {
            return true;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var installDirectory = Path.Combine(localAppData, "NetVsMcp", "Broker");
        var brokerExecutablePath = Path.Combine(installDirectory, "NetVsMcp.Broker.exe");
        var startMenuShortcutPath = Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "NetVsMcp", "NetVsMcp Broker.lnk");

        return File.Exists(brokerExecutablePath) ||
            Directory.Exists(installDirectory) ||
            File.Exists(startMenuShortcutPath);
    }

    private static bool IsInstalledInRegistry()
    {
        try
        {
            var registryType = Type.GetType("Microsoft.Win32.Registry, mscorlib", throwOnError: false);
            var currentUser = registryType?.GetProperty("CurrentUser")?.GetValue(null);
            if (currentUser is null)
            {
                return false;
            }

            using var brokerKey = currentUser.GetType()
                .GetMethod("OpenSubKey", [typeof(string), typeof(bool)])
                ?.Invoke(currentUser, [BrokerRegistryKeyPath, false]) as IDisposable;
            var installed = brokerKey?.GetType()
                .GetMethod("GetValue", [typeof(string)])
                ?.Invoke(brokerKey, [BrokerInstalledValueName]);

            return installed is int installedValue && installedValue == 1;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class BrokerNotificationContent
{
    public BrokerNotificationContent(
        BrokerConnectivityIssue issue,
        string title,
        string linkText,
        string linkUrl,
        string stateKey)
    {
        Issue = issue;
        Title = title;
        LinkText = linkText;
        LinkUrl = linkUrl;
        StateKey = stateKey;
    }

    public BrokerConnectivityIssue Issue { get; }

    public string Title { get; }

    public string LinkText { get; }

    public string LinkUrl { get; }

    public string StateKey { get; }
}

internal static class BrokerNotificationContentFactory
{
    public const string BrokerReleasesUrl = "https://github.com/Alexander-Swan/netvs-mcp/releases/latest";

    public static BrokerNotificationContent Create(BrokerConnectivityIssue issue)
    {
        return issue switch
        {
            BrokerConnectivityIssue.NotInstalled => new(
                issue,
                "NetVsMcp Broker is required to use this extension. Install the broker, then reopen Visual Studio or start it from the Start menu.",
                "Download NetVsMcp Broker",
                BrokerReleasesUrl,
                "broker-not-installed"),
            BrokerConnectivityIssue.NotRunning => new(
                issue,
                "NetVsMcp Broker is installed but not running. Start NetVsMcp Broker from the Start menu, or reinstall it if needed.",
                "Open Broker Download Page",
                BrokerReleasesUrl,
                "broker-not-running"),
            BrokerConnectivityIssue.UpdateRequired => new(
                issue,
                "This NetVsMcp extension needs a newer compatible NetVsMcp Broker. Update the broker, then restart Visual Studio if the warning does not clear automatically.",
                "Download Latest Broker",
                BrokerReleasesUrl,
                "broker-update-required"),
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null)
        };
    }
}

internal interface IBrokerNotificationService
{
    void Show(BrokerConnectivityIssue issue);

    void Clear();
}

internal sealed class BrokerStatusInfoBarService : IBrokerNotificationService, IVsInfoBarUIEvents, IDisposable
{
    private readonly AsyncPackage package;
    private readonly object gate = new();

    private IVsInfoBarUIElement? infoBarElement;
    private uint infoBarCookie;
    private string? currentStateKey;
    private string? currentLinkUrl;

    public BrokerStatusInfoBarService(AsyncPackage package)
    {
        this.package = package;
    }

    public void Show(BrokerConnectivityIssue issue)
    {
        _ = package.JoinableTaskFactory.RunAsync(async delegate
        {
            await ShowAsync(issue);
        });
    }

    public void Clear()
    {
        _ = package.JoinableTaskFactory.RunAsync(async delegate
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            RemoveInfoBar();
        });
    }

    public void Dispose()
    {
        package.JoinableTaskFactory.Run(async delegate
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            RemoveInfoBar();
        });
    }

    void IVsInfoBarUIEvents.OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var url = currentLinkUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            VsShellUtilities.OpenSystemBrowser(url);
        }
    }

    void IVsInfoBarUIEvents.OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (ReferenceEquals(infoBarElement, infoBarUIElement))
        {
            RemoveInfoBar();
        }
    }

    private async Task ShowAsync(BrokerConnectivityIssue issue)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        var content = BrokerNotificationContentFactory.Create(issue);
        if (string.Equals(currentStateKey, content.StateKey, StringComparison.Ordinal))
        {
            return;
        }

        var shell = await package.GetServiceAsync(typeof(SVsShell)) as IVsShell;
        var factory = await package.GetServiceAsync(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
        if (shell is null || factory is null)
        {
            return;
        }

        shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var hostObject);
        if (hostObject is not IVsInfoBarHost host)
        {
            return;
        }

        RemoveInfoBar();

        var model = new InfoBarModel(
            [new InfoBarTextSpan(content.Title)],
            [new InfoBarHyperlink(content.LinkText)],
            KnownMonikers.StatusWarning,
            isCloseButtonVisible: true);

        var element = factory.CreateInfoBar(model);
        element.Advise(this, out infoBarCookie);
        host.AddInfoBar(element);

        lock (gate)
        {
            infoBarElement = element;
            currentStateKey = content.StateKey;
            currentLinkUrl = content.LinkUrl;
        }
    }

    private void RemoveInfoBar()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        IVsInfoBarUIElement? element;
        uint cookie;

        lock (gate)
        {
            element = infoBarElement;
            cookie = infoBarCookie;
            infoBarElement = null;
            infoBarCookie = 0;
            currentStateKey = null;
            currentLinkUrl = null;
        }

        if (element is null)
        {
            return;
        }

        if (cookie != 0)
        {
            element.Unadvise(cookie);
        }

        element.Close();
    }
}
