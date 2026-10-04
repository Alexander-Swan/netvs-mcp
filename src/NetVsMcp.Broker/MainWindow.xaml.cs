using System.Windows;
using System.Windows.Interop;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker;

public partial class MainWindow : Window
{
    private const int ShowWindowRestore = 9;

    private readonly MainWindowViewModel _viewModel;
    private bool _isHidingToTray;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideToTray();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (!_isHidingToTray && WindowState == WindowState.Minimized)
        {
            Dispatcher.BeginInvoke(HideToTray);
        }
    }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        BringToForeground();
        Dispatcher.BeginInvoke(BringToForeground);
    }

    public void ShowAgentsTab()
    {
        MainTabs.SelectedIndex = 2;
        RestoreFromTray();
    }

    public void ToggleFromTray()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideToTray();
            return;
        }

        RestoreFromTray();
    }

    private void HideToTray()
    {
        if (!IsVisible)
        {
            ShowInTaskbar = false;
            return;
        }

        _isHidingToTray = true;
        try
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = false;
            Hide();
        }
        finally
        {
            _isHidingToTray = false;
        }
    }

    private void BringToForeground()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, ShowWindowRestore);
            SetForegroundWindow(handle);
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void CopyConfig_Click(object sender, RoutedEventArgs e) => _viewModel.CopyMcpConfig();

    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.Refresh();

    private void CopyEndpoint_Click(object sender, RoutedEventArgs e) => _viewModel.CopyEndpoint();

    private void CopyWebAutomationEndpoint_Click(object sender, RoutedEventArgs e) => _viewModel.CopyWebAutomationEndpoint();

    private void CopyPipe_Click(object sender, RoutedEventArgs e) => _viewModel.CopyPipeName();

    private void ToggleAutostart_Click(object sender, RoutedEventArgs e) => _viewModel.ToggleAutostart();

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => _viewModel.OpenLogsFolder();

    private void OpenVisualStudioExtensionSetup_Click(object sender, RoutedEventArgs e) => _viewModel.OpenVisualStudioExtensionSetupPage();

    private void ApplySettings_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyStartupSettings();

    private void ApplyAnalyticsSettings_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyAnalyticsSettings();

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e) => await _viewModel.CheckForUpdatesAsync();

    private void Exit_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e) => await _viewModel.InstallUpdateAsync();

    private void IgnoreUpdate_Click(object sender, RoutedEventArgs e) => _viewModel.IgnoreUpdate();

    private void RegisterClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClientRegistrationViewModel client })
            _viewModel.RegisterClient(client);
    }

    private void OpenClientConfig_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClientRegistrationViewModel client })
            _viewModel.OpenClientConfig(client);
    }
}
