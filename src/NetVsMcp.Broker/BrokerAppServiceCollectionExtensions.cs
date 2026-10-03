using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NetVsMcp.Broker.Analytics;
using NetVsMcp.Broker.Services;
using NetVsMcp.Broker.ViewModels;

namespace NetVsMcp.Broker;

public static class BrokerAppServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerApp(
        this IServiceCollection services,
        string[]? args)
    {
        services.AddSingleton<BrokerUpdateAvailability>();
        services.AddNetVsMcpBrokerCore(
            args,
            shutdownAllowed: provider => provider.GetRequiredService<BrokerUpdateAvailability>().IsVsixBundled,
            requestShutdown: _ => System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(System.Windows.Application.Current.Shutdown)));
        services.AddNetVsMcpBrokerAnalytics(provider =>
            provider.GetRequiredService<BrokerOptions>().AnalyticsDatabaseFilePath);
        services.AddSingleton<IAutostartService, AutostartService>();
        services.AddSingleton<UpdateCheckService>();
        services.AddSingleton<StartupUpdateCheckService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<Func<Window>>(provider => () => provider.GetRequiredService<MainWindow>());
        services.AddSingleton<TrayIconController>();

        return services;
    }
}
