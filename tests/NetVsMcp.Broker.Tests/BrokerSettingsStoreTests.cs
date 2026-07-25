using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerSettingsStoreTests
{
    [Fact]
    public void Load_ReturnsEmptySettingsWhenFileDoesNotExist()
    {
        var store = new BrokerSettingsStore(CreateTempFilePath());

        var settings = store.Load();

        Assert.Null(settings.Port);
        Assert.Null(settings.LogsDirectory);
        Assert.Null(settings.SessionsDirectory);
        Assert.Null(settings.CapabilityProfile);
    }

    [Fact]
    public void Update_ThenLoad_RoundTripsIndividualFields()
    {
        var store = new BrokerSettingsStore(CreateTempFilePath());

        store.Update(s => s with { CapabilityProfile = BrokerCapabilityProfile.EditPreview });
        store.Update(s => s with { Port = 5099 });

        var loaded = store.Load();

        Assert.Equal(BrokerCapabilityProfile.EditPreview, loaded.CapabilityProfile);
        Assert.Equal(5099, loaded.Port);
    }

    [Fact]
    public void Update_PersistsAcrossSeparateStoreInstances()
    {
        var path = CreateTempFilePath();
        new BrokerSettingsStore(path).Update(s => s with { LogsDirectory = @"C:\Logs\netvs-mcp-test" });

        var reloaded = new BrokerSettingsStore(path).Load();

        Assert.Equal(@"C:\Logs\netvs-mcp-test", reloaded.LogsDirectory);
    }

    private static string CreateTempFilePath() => Path.Combine(
        Path.GetTempPath(),
        "NetVsMcp.Broker.Tests",
        Guid.NewGuid().ToString("N"),
        "settings.json");
}
