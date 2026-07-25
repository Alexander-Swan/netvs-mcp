using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class CapabilityProfileStoreTests
{
    [Fact]
    public void Load_ReturnsFallbackWhenFileDoesNotExist()
    {
        var store = new CapabilityProfileStore(CreateTempFilePath());

        var profile = store.Load(BrokerCapabilityProfile.Debug);

        Assert.Equal(BrokerCapabilityProfile.Debug, profile);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProfile()
    {
        var store = new CapabilityProfileStore(CreateTempFilePath());

        store.Save(BrokerCapabilityProfile.EditPreview);
        var loaded = store.Load(BrokerCapabilityProfile.Admin);

        Assert.Equal(BrokerCapabilityProfile.EditPreview, loaded);
    }

    [Fact]
    public void Save_PersistsAcrossSeparateStoreInstances()
    {
        var path = CreateTempFilePath();
        new CapabilityProfileStore(path).Save(BrokerCapabilityProfile.ReadOnly);

        var reloaded = new CapabilityProfileStore(path).Load(BrokerCapabilityProfile.Admin);

        Assert.Equal(BrokerCapabilityProfile.ReadOnly, reloaded);
    }

    private static string CreateTempFilePath() => Path.Combine(
        Path.GetTempPath(),
        "NetVsMcp.Broker.Tests",
        Guid.NewGuid().ToString("N"),
        "capability-profile.json");
}
