using System;
using NetVsMcp.Vsix;
using NetVsMcp.Vsix.Interfaces;

namespace NetVsMcp.Vsix.Tests;

public sealed class BrokerNotificationContentFactoryTests
{
    [Fact]
    public void Create_NotInstalled_UsesExtensionPayloadCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.NotInstalled);

        Assert.Equal(BrokerConnectivityIssue.NotInstalled, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("missing from this Visual Studio extension", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repair", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("install the broker", content.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_NotRunning_UsesBundledStartupCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.NotRunning);

        Assert.Equal(BrokerConnectivityIssue.NotRunning, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("could not be started", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reopen Visual Studio", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("install the broker", content.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_UpdateRequired_UsesExtensionUpdateCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.UpdateRequired);

        Assert.Equal(BrokerConnectivityIssue.UpdateRequired, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("compatible", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Update the NetVsMcp extension", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Download Latest Broker", content.LinkText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrokerInstallationDetector_IsInstalled_WhenBundledBrokerIsAvailable()
    {
        var detector = new BrokerInstallationDetector(new TestBrokerLauncher(isAvailable: true));

        Assert.True(detector.IsInstalled());
    }

    private sealed class TestBrokerLauncher : IBrokerProcessLauncher
    {
        public TestBrokerLauncher(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public System.Threading.Tasks.Task<bool> TryLaunchAsync(System.Threading.CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult(false);
    }
}
