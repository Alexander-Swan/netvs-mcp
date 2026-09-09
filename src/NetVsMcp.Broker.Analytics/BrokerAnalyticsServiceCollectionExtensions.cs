using Microsoft.Extensions.DependencyInjection;

namespace NetVsMcp.Broker.Analytics;

public static class BrokerAnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddNetVsMcpBrokerAnalytics(
        this IServiceCollection services,
        string? databasePath)
    {
        services.AddSingleton<IToolUsageAnalyticsService>(_ =>
            new SqliteToolUsageAnalyticsService(databasePath));
        return services;
    }

    public static IServiceCollection AddNetVsMcpBrokerAnalytics(
        this IServiceCollection services,
        Func<IServiceProvider, string?> databasePathFactory)
    {
        services.AddSingleton<IToolUsageAnalyticsService>(provider =>
            new SqliteToolUsageAnalyticsService(databasePathFactory(provider)));
        return services;
    }
}
