using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Services;

public static class BrokerCoreServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerCore(
        this IServiceCollection services,
        string[]? args,
        SessionRegistry? sessions = null)
    {
        var initial = BrokerOptions.LocalDefault.WithArgs(args);
        var settingsStore = new BrokerSettingsStore(initial.EffectiveSettingsFilePath);
        var options = BrokerOptions.LocalDefault.ApplyPersistedSettings(settingsStore.Load()).WithArgs(args);

        return services.AddNetVsMcpBrokerCore(options, sessions);
    }

    public static IServiceCollection AddNetVsMcpBrokerCore(
        this IServiceCollection services,
        BrokerOptions options,
        SessionRegistry? sessions = null)
    {
        services.AddNetVsMcpBrokerServices(options, sessions);
        services.AddSingleton<BestPracticeGuideCatalog>();
        services.AddSingleton(BrokerRuntime.Create);

        return services;
    }
}
