using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Services;

public static class BrokerServicesServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerServices(
        this IServiceCollection services,
        BrokerOptions options,
        SessionRegistry? sessions = null,
        Func<IServiceProvider, bool>? shutdownAllowed = null,
        Action<IServiceProvider>? requestShutdown = null)
    {
        services.AddSingleton(options);
        services.AddSingleton(sessions ?? new SessionRegistry());
        services.AddSingleton(provider => new BrokerShutdownPolicy(
            () => shutdownAllowed?.Invoke(provider) ?? false,
            () => requestShutdown?.Invoke(provider)));
        services.AddSingleton<IVsSessionConnectionMap, VsSessionConnectionMap>();
        services.AddSingleton<IVsSessionDispatcher>(provider =>
            new VsSessionDispatcher(
                provider.GetRequiredService<SessionRegistry>(),
                provider.GetRequiredService<IVsSessionConnectionMap>()));
        services.AddSingleton<IBrokerEventStore, BrokerEventStore>();
        services.AddSingleton(provider =>
            new VisualStudioLauncher(provider.GetRequiredService<SessionRegistry>()));
        services.AddSingleton(provider =>
        {
            var shutdownPolicy = provider.GetRequiredService<BrokerShutdownPolicy>();
            return new BrokerRegistrationRpcService(
                provider.GetRequiredService<SessionRegistry>(),
                provider.GetRequiredService<IVsSessionConnectionMap>(),
                events: provider.GetRequiredService<IBrokerEventStore>(),
                shutdownAllowed: shutdownPolicy.IsShutdownAllowed,
                requestShutdown: shutdownPolicy.RequestShutdown);
        });
        services.AddSingleton<IAuditLogService>(provider =>
            new AuditLogService(provider.GetRequiredService<BrokerOptions>().EffectiveLogsDirectory));
        services.AddSingleton<ISessionManifestService>(provider =>
            new SessionManifestService(provider.GetRequiredService<BrokerOptions>().EffectiveSessionsDirectory));
        services.AddSingleton<IBrokerSettingsStore>(provider =>
            new BrokerSettingsStore(provider.GetRequiredService<BrokerOptions>().EffectiveSettingsFilePath));

        return services;
    }
}
