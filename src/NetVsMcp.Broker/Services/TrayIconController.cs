using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace NetVsMcp.Broker.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly BrokerRuntime _runtime;
    private readonly Func<Window> _windowFactory;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconController(BrokerRuntime runtime, Func<Window> windowFactory)
    {
        _runtime = runtime;
        _windowFactory = windowFactory;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "NetVsMcp: starting",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowStatusWindow();
        _runtime.Sessions.SessionsChanged += (_, _) => UpdateStatus();
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        var sessionCount = _runtime.Sessions.ListSessions().Count;
        _notifyIcon.Text = sessionCount switch
        {
            0 => "NetVsMcp: running, no Visual Studio instances connected",
            1 => "NetVsMcp: 1 Visual Studio instance connected",
            _ => $"NetVsMcp: {sessionCount} Visual Studio instances connected"
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Status Window", null, (_, _) => ShowStatusWindow());
        menu.Items.Add("Copy MCP Config", null, (_, _) => System.Windows.Clipboard.SetText(_runtime.Options.McpRegistrationJson));
        menu.Items.Add("Refresh Status", null, (_, _) => UpdateStatus());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        return menu;
    }

    private void ShowStatusWindow()
    {
        var window = _windowFactory();
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
