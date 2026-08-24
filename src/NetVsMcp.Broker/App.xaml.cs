using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NetVsMcp.Broker.Services;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker;

public partial class App : System.Windows.Application
{
    // ARCH-3: a second broker instance (autostart + a manual double-click is a plausible real
    // scenario) would otherwise make Kestrel's Listen throw inside async void OnStartup -
    // unhandled, crashing the app ungracefully instead of exiting with a clear message.
    private const string SingleInstanceMutexName = "Global\\NetVsMcp.Broker.SingleInstance";

    private BrokerRuntime? _runtime;
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;
    private AutostartService? _autostart;
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            Trace.WriteLine("NetVsMcp broker: another instance is already running; exiting.");
            System.Windows.MessageBox.Show(
                "NetVsMcp Broker is already running. Check the system tray for its icon.",
                "NetVsMcp Broker",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        try
        {
            var sessions = new SessionRegistry();
            _autostart = new AutostartService();
            var initial = BrokerOptions.LocalDefault.WithArgs(e.Args);
            var settingsStore = new BrokerSettingsStore(initial.EffectiveSettingsFilePath);
            var options = BrokerOptions.LocalDefault.ApplyPersistedSettings(settingsStore.Load()).WithArgs(e.Args);
            _runtime = new BrokerRuntime(options, sessions);
            await _runtime.StartAsync(CancellationToken.None);

            var updateCheckService = new UpdateCheckService();
            var viewModel = new MainWindowViewModel(_runtime, _autostart, updateCheckService);
            _mainWindow = new MainWindow(viewModel);
            _trayIcon = new TrayIconController(_runtime, viewModel, () => _mainWindow);
            _mainWindow.Show();

            _ = CheckForUpdatesOnStartupAsync(viewModel, _trayIcon);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp broker failed to start: {ex}");
            System.Windows.MessageBox.Show(
                $"NetVsMcp Broker failed to start:\n\n{ex.Message}",
                "NetVsMcp Broker",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static async Task CheckForUpdatesOnStartupAsync(MainWindowViewModel viewModel, TrayIconController tray)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        await viewModel.CheckForUpdatesAsync();
        if (viewModel.UpdateAvailable)
            tray.ShowUpdateAvailableBalloon(viewModel.UpdateVersionText);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.WriteLine($"NetVsMcp broker: unhandled UI-thread exception: {e.Exception}");
        System.Windows.MessageBox.Show(
            $"NetVsMcp Broker hit an unexpected error and may be unstable:\n\n{e.Exception.Message}\n\nCheck the broker logs for details.",
            "NetVsMcp Broker",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        // Keep the app alive rather than letting an unhandled UI-thread exception crash the
        // whole tray app - the broker's HTTP/pipe listeners run independently of the dispatcher.
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Trace.WriteLine($"NetVsMcp broker: unhandled exception (terminating={e.IsTerminating}): {exception}");
        // AppDomain.UnhandledException can't stop the process from terminating when
        // IsTerminating is true, but at least surface it instead of silently dying.
        System.Windows.MessageBox.Show(
            $"NetVsMcp Broker hit a fatal error and will exit:\n\n{exception?.Message}\n\nCheck the broker logs for details.",
            "NetVsMcp Broker",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Trace.WriteLine($"NetVsMcp broker: unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_runtime is not null)
        {
            await _runtime.StopAsync();
        }

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
