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
}
