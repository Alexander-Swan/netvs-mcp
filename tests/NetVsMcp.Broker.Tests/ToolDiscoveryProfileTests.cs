using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class ToolDiscoveryProfileTests
{
    [Theory]
    [InlineData("manage")]
    [InlineData("read")]
    [InlineData("edit")]
    [InlineData("build")]
    [InlineData("debug")]
    [InlineData("test")]
    public void RestrictedProfilesRetainModalDialogRecovery(string profile)
    {
        var selected = BrokerToolService.DiscoveryDescriptors
            .Where(t => ToolDiscoveryProfile.Includes(profile, t)).Select(t => t.Name).ToArray();
        Assert.Contains("dialog_list", selected);
        Assert.Contains("dialog_respond", selected);
    }

    [Fact]
    public void DebugProfileRetainsRecoveryAndExcludesEditing()
    {
        var catalog = BrokerToolService.DiscoveryDescriptors;
        var selected = catalog.Where(t => ToolDiscoveryProfile.Includes("debug", t)).Select(t => t.Name).ToArray();
        Assert.Contains("debug_snapshot", selected);
        Assert.Contains("vs_list_sessions", selected);
        Assert.Contains("debug_break", selected);
        Assert.Contains("netvs_get_best_practices", selected);
        Assert.DoesNotContain("document_write", selected);
        Assert.True(selected.Length < catalog.Count);
        Assert.Throws<ArgumentException>(() => ToolDiscoveryProfile.Includes("typo", catalog[0]));
    }

}
