using System.IO;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerUpdateAvailability
{
    public const string VsixBundledMarkerFileName = ".netvsmcp-vsix-bundled-broker";

    public BrokerUpdateAvailability()
        : this(AppContext.BaseDirectory)
    {
    }

    public BrokerUpdateAvailability(string brokerDirectory)
    {
        IsVsixBundled = File.Exists(Path.Combine(brokerDirectory, VsixBundledMarkerFileName));
    }

    public bool IsVsixBundled { get; }

    public bool SupportsBrokerUpdates => !IsVsixBundled;
}
