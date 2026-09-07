using System.Diagnostics;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker.Services;

public sealed class StartupUpdateCheckService
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TrayIconController _tray;

    public StartupUpdateCheckService(MainWindowViewModel viewModel, TrayIconController tray)
    {
        _viewModel = viewModel;
        _tray = tray;
    }

    public void Start()
    {
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            Trace.WriteLine("NetVsMcp broker checking for updates at startup.");
            await _viewModel.CheckForUpdatesAsync();

            if (_viewModel.UpdateAvailable)
            {
                Trace.WriteLine($"NetVsMcp broker update available at startup: v{_viewModel.UpdateVersionText}.");
                _tray.ShowUpdateAvailableBalloon(_viewModel.UpdateVersionText);
            }
            else
            {
                Trace.WriteLine("NetVsMcp broker is up to date at startup.");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp broker startup update check failed: {ex}");
        }
    }
}
