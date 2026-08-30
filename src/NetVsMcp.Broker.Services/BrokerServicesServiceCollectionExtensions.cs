using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Services;

public static class BrokerServicesServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerServices(
        this IServiceCollection services,
        BrokerOptions options,
        SessionRegistry? sessions = null)
    {
        services.AddSingleton(options);
        services.AddSingleton(sessions ?? new SessionRegistry());
        services.AddSingleton<IVsSessionConnectionMap, VsSessionConnectionMap>();
        services.AddSingleton<IVsSessionDispatcher>(provider =>
            new VsSessionDispatcher(
                provider.GetRequiredService<SessionRegistry>(),
                provider.GetRequiredService<IVsSessionConnectionMap>()));
        services.AddSingleton(provider =>
            new VisualStudioLauncher(provider.GetRequiredService<SessionRegistry>()));
        services.AddSingleton(provider =>
            new BrokerRegistrationRpcService(
                provider.GetRequiredService<SessionRegistry>(),
                provider.GetRequiredService<IVsSessionConnectionMap>()));
        services.AddSingleton<IAuditLogService>(provider =>
            new AuditLogService(provider.GetRequiredService<BrokerOptions>().EffectiveLogsDirectory));
        services.AddSingleton<ISessionManifestService>(provider =>
            new SessionManifestService(provider.GetRequiredService<BrokerOptions>().EffectiveSessionsDirectory));
        services.AddSingleton<IBrokerSettingsStore>(provider =>
            new BrokerSettingsStore(provider.GetRequiredService<BrokerOptions>().EffectiveSettingsFilePath));

        return services;
    }
}
