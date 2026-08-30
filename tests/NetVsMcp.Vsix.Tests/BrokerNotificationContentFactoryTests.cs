using System;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class BrokerNotificationContentFactoryTests
{
    [Fact]
    public void Create_NotInstalled_UsesDownloadLinkAndInstallCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.NotInstalled);

        Assert.Equal(BrokerConnectivityIssue.NotInstalled, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("Install", content.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_NotRunning_UsesDownloadLinkAndStartCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.NotRunning);

        Assert.Equal(BrokerConnectivityIssue.NotRunning, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("not running", content.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Start", content.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_UpdateRequired_UsesDownloadLinkAndUpdateCopy()
    {
        var content = BrokerNotificationContentFactory.Create(BrokerConnectivityIssue.UpdateRequired);

        Assert.Equal(BrokerConnectivityIssue.UpdateRequired, content.Issue);
        Assert.Equal(BrokerNotificationContentFactory.BrokerReleasesUrl, content.LinkUrl);
        Assert.Contains("compatible", content.Title, StringComparison.OrdinalIgnoreCase);
    }
}
