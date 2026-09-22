using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerUpdateAvailabilityTests
{
    [Fact]
    public void SupportsBrokerUpdates_WhenMarkerFileIsMissing()
    {
        var directory = CreateTempDirectory();
        try
        {
            var availability = new BrokerUpdateAvailability(directory);

            Assert.False(availability.IsVsixBundled);
            Assert.True(availability.SupportsBrokerUpdates);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DisablesBrokerUpdates_WhenMarkerFileExists()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, BrokerUpdateAvailability.VsixBundledMarkerFileName), string.Empty);

            var availability = new BrokerUpdateAvailability(directory);

            Assert.True(availability.IsVsixBundled);
            Assert.False(availability.SupportsBrokerUpdates);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NetVsMcp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
