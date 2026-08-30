using System.Windows;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void CopyConfig_Click(object sender, RoutedEventArgs e) => _viewModel.CopyMcpConfig();

    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.Refresh();

    private void CopyEndpoint_Click(object sender, RoutedEventArgs e) => _viewModel.CopyEndpoint();

    private void CopyWebAutomationEndpoint_Click(object sender, RoutedEventArgs e) => _viewModel.CopyWebAutomationEndpoint();

    private void CopyPipe_Click(object sender, RoutedEventArgs e) => _viewModel.CopyPipeName();

    private void ToggleAutostart_Click(object sender, RoutedEventArgs e) => _viewModel.ToggleAutostart();

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => _viewModel.OpenLogsFolder();

    private void OpenVisualStudioExtensionSetup_Click(object sender, RoutedEventArgs e) => _viewModel.OpenVisualStudioExtensionSetupPage();

    private void ApplySettings_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyStartupSettings();

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
