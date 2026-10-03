using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Services;

public static class BrokerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerCore(
        this IServiceCollection services,
        string[]? args,
        SessionRegistry? sessions = null,
        Func<IServiceProvider, bool>? shutdownAllowed = null,
        Action<IServiceProvider>? requestShutdown = null)
    {
        var initial = BrokerOptions.LocalDefault.WithArgs(args);
        var settingsStore = new BrokerSettingsStore(initial.EffectiveSettingsFilePath);
        var options = BrokerOptions.LocalDefault.ApplyPersistedSettings(settingsStore.Load()).WithArgs(args);

        return services.AddNetVsMcpBrokerCore(options, sessions, shutdownAllowed, requestShutdown);
    }

    public static IServiceCollection AddNetVsMcpBrokerCore(
        this IServiceCollection services,
        BrokerOptions options,
        SessionRegistry? sessions = null,
        Func<IServiceProvider, bool>? shutdownAllowed = null,
        Action<IServiceProvider>? requestShutdown = null)
    {
        services.AddNetVsMcpBrokerServices(options, sessions, shutdownAllowed, requestShutdown);
        services.AddSingleton<BestPracticeGuideCatalog>();
        services.AddSingleton(BrokerRuntime.Create);

        return services;
    }
}
