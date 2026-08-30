using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed class GeneralIdeCapabilityService : IGeneralIdeCapabilityService
{
    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "File.Exit",
        "File.CloseSolution",
        "File.ExitVisualStudio"
    };

    private readonly AsyncPackage package;

    public GeneralIdeCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<ExecuteCommandResult> ExecuteCommandAsync(
        ExecuteCommandRequest request,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var commandName = NormalizeCommandName(request.CommandName);
        if (BlockedCommands.Contains(commandName))
        {
            return new ExecuteCommandResult(false, commandName, request.Arguments, "Command is blocked by NetVsMcp safety policy.");
        }

        var dte = await GetDteAsync()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        if (!CommandExists(dte, commandName))
        {
            return new ExecuteCommandResult(false, commandName, request.Arguments, "Visual Studio command was not found.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.Arguments))
            {
                dte.ExecuteCommand(commandName);
            }
            else
            {
                dte.ExecuteCommand(commandName, request.Arguments);
            }

            return new ExecuteCommandResult(true, commandName, request.Arguments, "Command executed.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new ExecuteCommandResult(false, commandName, request.Arguments, ex.Message);
        }
    }

    public async Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var activeWindow = dte.ActiveWindow;
        var windowsCollection = dte.Windows;
        var windows = new List<WindowInfo>(windowsCollection.Count);

        // Iterate by index and skip individual entries that fail to marshal instead of using
        // Cast<Window>()/Select(), which aborts the entire call on the first bad window. Some
        // windows (e.g. orphaned or third-party frames without a valid document interface) can
        // fail QueryInterface (E_NOINTERFACE) partway through enumeration or property access.
        for (var i = 1; i <= windowsCollection.Count; i++)
        {
            var info = TryCreateWindowInfo(windowsCollection, i, activeWindow);
            if (info is not null)
            {
                windows.Add(info);
            }
        }

        return new WindowListResult(windows.ToArray());
    }

    private static WindowInfo? TryCreateWindowInfo(EnvDTE.Windows windowsCollection, int index, Window? activeWindow)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            var window = windowsCollection.Item(index);
            return CreateWindowInfo(window, activeWindow);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or System.Runtime.InteropServices.COMException
            or System.Runtime.InteropServices.InvalidComObjectException
            or InvalidCastException)
        {
            return null;
        }
    }

    public async Task<WindowActivateResult> WindowActivateAsync(
        WindowActivateRequest request,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Caption) && string.IsNullOrWhiteSpace(request.ObjectKind))
        {
            return new WindowActivateResult(false, "Window caption or object kind is required.", null);
        }

        var dte = await GetDteAsync()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var window = FindWindow(dte, request.Caption, request.ObjectKind);
        if (window is null)
        {
            return new WindowActivateResult(false, "Window was not found.", null);
        }

        window.Activate();
        return new WindowActivateResult(true, "Window activated.", CreateWindowInfo(window, dte.ActiveWindow));
    }

    public async Task<ToolWindowResult> ToolWindowShowAsync(
        ToolWindowRequest request,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var window = FindWindow(dte, request.Caption, request.ObjectKind);
        if (window is null)
        {
            return new ToolWindowResult(false, "Tool window was not found.", null);
        }

        SetWindowVisible(window, true);
        window.Activate();
        return new ToolWindowResult(true, "Tool window shown.", CreateWindowInfo(window, dte.ActiveWindow));
    }

    public async Task<ToolWindowResult> ToolWindowHideAsync(
        ToolWindowRequest request,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var window = FindWindow(dte, request.Caption, request.ObjectKind);
        if (window is null)
        {
            return new ToolWindowResult(false, "Tool window was not found.", null);
        }

        SetWindowVisible(window, false);
        return new ToolWindowResult(true, "Tool window hidden.", CreateWindowInfo(window, dte.ActiveWindow));
    }

    private async Task<DTE2?> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE2;
    }

    private static bool CommandExists(DTE2 dte, string commandName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return dte.Commands
            .Cast<Command>()
            .Any(command => CommandMatches(command, commandName));
    }

    private static WindowInfo CreateWindowInfo(Window window, Window? activeWindow)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new WindowInfo(
            GetWindowCaption(window),
            GetWindowKind(window),
            GetWindowObjectKind(window),
            ReferenceEquals(window, activeWindow),
            GetWindowVisible(window));
    }

    private static Window? FindWindow(DTE2 dte, string? caption, string? objectKind)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var normalizedCaption = NormalizeOptional(caption);
        var normalizedObjectKind = NormalizeOptional(objectKind);
        return dte.Windows
            .Cast<Window>()
            .FirstOrDefault(window =>
                (normalizedCaption is not null && string.Equals(GetWindowCaption(window), normalizedCaption, StringComparison.OrdinalIgnoreCase)) ||
                (normalizedObjectKind is not null && string.Equals(GetWindowObjectKind(window), normalizedObjectKind, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? GetWindowCaption(Window window)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return window.Caption;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static string? GetWindowKind(Window window)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return window.Kind.ToString();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static string? GetWindowObjectKind(Window window)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return window.ObjectKind;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static bool GetWindowVisible(Window window)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return window.Visible;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static void SetWindowVisible(Window window, bool visible)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        window.Visible = visible;
    }

    private static bool CommandMatches(Command command, string commandName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return string.Equals(command.Name, commandName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.LocalizedName, commandName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCommandName(string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new ArgumentException("Command name is required.", nameof(commandName));
        }

        return commandName!.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }
}
