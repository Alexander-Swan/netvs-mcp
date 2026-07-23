using System.Windows;
using NetVsMcp.Broker.Services;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker;

public partial class App : System.Windows.Application
{
    private BrokerRuntime? _runtime;
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;
    private AutostartService? _autostart;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sessions = new SessionRegistry();
        _autostart = new AutostartService();
        _runtime = new BrokerRuntime(BrokerOptions.FromEnvironmentAndArgs(e.Args), sessions);
        await _runtime.StartAsync(CancellationToken.None);

        var viewModel = new MainWindowViewModel(_runtime, _autostart);
        _mainWindow = new MainWindow(viewModel);
        _trayIcon = new TrayIconController(_runtime, viewModel, () => _mainWindow);
        _mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_runtime is not null)
        {
            await _runtime.StopAsync();
        }

        base.OnExit(e);
    }
}
