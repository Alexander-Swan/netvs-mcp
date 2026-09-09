using Microsoft.Extensions.DependencyInjection;
using NetVsMcp.Broker.Analytics;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerAppServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNetVsMcpBrokerApp_ResolvesAnalyticsWithDefaultDatabasePath()
    {
        var services = new ServiceCollection();
        services.AddNetVsMcpBrokerApp([]);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BrokerOptions>();
        var analytics = provider.GetRequiredService<IToolUsageAnalyticsService>();

        Assert.Equal(options.AnalyticsDatabaseFilePath, analytics.DatabasePath);
        Assert.False(string.IsNullOrWhiteSpace(analytics.DatabasePath));
    }
}
