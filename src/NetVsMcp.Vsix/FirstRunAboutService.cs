using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace NetVsMcp.Vsix;

/// <summary>
/// Shows the About dialog once per installed/updated version, so users see download and project
/// information the first time a new NetVsMcp release loads in a given Visual Studio profile.
/// </summary>
internal static class FirstRunAboutService
{
    private const string CollectionPath = "NetVsMcp\\About";
    private const string LastShownVersionProperty = "LastShownVersion";

    public static void ShowIfNeeded(AsyncPackage package)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            var settingsManager = new ShellSettingsManager(package);
            WritableSettingsStore store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!store.CollectionExists(CollectionPath))
            {
                store.CreateCollection(CollectionPath);
            }

            var lastShownVersion = store.PropertyExists(CollectionPath, LastShownVersionProperty)
                ? store.GetString(CollectionPath, LastShownVersionProperty)
                : null;

            if (lastShownVersion == AboutInfo.Version)
            {
                return;
            }

            store.SetString(CollectionPath, LastShownVersionProperty, AboutInfo.Version);

            using var dialog = new AboutDialog();
            dialog.ShowDialog(new Win32WindowHandle(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle));
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            // Non-critical: never block extension load because the About dialog couldn't show.
            System.Diagnostics.Trace.WriteLine($"NetVsMcp: failed to show About dialog: {ex}");
        }
    }

    private sealed class Win32WindowHandle : IWin32Window
    {
        public Win32WindowHandle(IntPtr handle) => Handle = handle;

        public IntPtr Handle { get; }
    }
}
