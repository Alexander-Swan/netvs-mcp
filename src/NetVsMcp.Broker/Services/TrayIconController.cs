using System.Drawing;
using System.IO;
using System.Windows;
using NetVsMcp.Broker.ViewModels;
using NetVsMcp.Contracts;
using Forms = System.Windows.Forms;

namespace NetVsMcp.Broker.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly BrokerRuntime _runtime;
    private readonly MainWindowViewModel _viewModel;
    private readonly Func<Window> _windowFactory;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconController(BrokerRuntime runtime, MainWindowViewModel viewModel, Func<Window> windowFactory)
    {
        _runtime = runtime;
        _viewModel = viewModel;
        _windowFactory = windowFactory;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = "NetVsMcp: starting",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowStatusWindow();
        _runtime.Sessions.SessionsChanged += (_, _) => UpdateStatus();
        _runtime.Sessions.SessionConnected += OnSessionConnected;
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        var sessions = _runtime.Sessions.ListSessions();
        var suffix = sessions.Count == 1 ? "1 VS instance connected" : $"{sessions.Count} VS instances connected";
        var header = $"NetVsMcp {_viewModel.Version} — {suffix}";
        var recentIds = sessions
            .OrderByDescending(s => s.LastSeenUtc)
            .Take(3)
            .Select(s => s.SessionId);
        var text = sessions.Count == 0 ? header : header + "\n" + string.Join("\n", recentIds);
        _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _runtime.Sessions.SessionConnected -= OnSessionConnected;
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Status Window", null, (_, _) => ShowStatusWindow());
        menu.Items.Add("Copy MCP Config", null, (_, _) => _viewModel.CopyMcpConfig());
        menu.Items.Add("Refresh", null, (_, _) => Refresh());
        var autostartItem = new Forms.ToolStripMenuItem(BuildAutostartMenuText());
        autostartItem.Click += (_, _) =>
        {
            _viewModel.ToggleAutostart();
            autostartItem.Text = BuildAutostartMenuText();
        };
        menu.Items.Add(autostartItem);
        menu.Items.Add("Open Logs Folder", null, (_, _) => _viewModel.OpenLogsFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        return menu;
    }

    private void Refresh()
    {
        _viewModel.Refresh();
        UpdateStatus();
    }

    private string BuildAutostartMenuText() => $"Start at Login: {_viewModel.AutostartStatus}";

    private void OnSessionConnected(object? sender, SessionConnectedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            UpdateStatus();
            _notifyIcon.ShowBalloonTip(
                5000,
                "Visual Studio connected",
                BuildSessionConnectedMessage(e.Session),
                Forms.ToolTipIcon.Info);
        });
    }

    private static string BuildSessionConnectedMessage(VsSessionInfo session)
    {
        var name = string.IsNullOrWhiteSpace(session.SolutionName)
            ? Path.GetFileNameWithoutExtension(session.SolutionPath) ?? "Visual Studio"
            : session.SolutionName;

        return $"{name} registered with NetVsMcp Broker. Process id: {session.ProcessId}.";
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
