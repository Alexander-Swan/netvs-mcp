using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface ISessionManifestService
{
    string SessionsDirectory { get; }
    void Sync(IReadOnlyCollection<VsSessionInfo> sessions);
    int CleanupStale(DateTimeOffset staleBeforeUtc);
}
