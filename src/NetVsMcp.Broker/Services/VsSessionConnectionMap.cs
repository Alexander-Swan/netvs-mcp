using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface IVsSessionConnectionMap
{
    void AddOrUpdate(string sessionId, IVisualStudioSessionRpc connection);

    bool TryGet(string sessionId, out IVisualStudioSessionRpc connection);

    bool Remove(string sessionId);
}

public sealed class VsSessionConnectionMap : IVsSessionConnectionMap
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IVisualStudioSessionRpc> _connections = new(StringComparer.OrdinalIgnoreCase);

    public void AddOrUpdate(string sessionId, IVisualStudioSessionRpc connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(connection);

        lock (_gate)
        {
            _connections[sessionId] = connection;
        }
    }

    public bool TryGet(string sessionId, out IVisualStudioSessionRpc connection)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(sessionId, out connection!);
        }
    }

    public bool Remove(string sessionId)
    {
        lock (_gate)
        {
            return _connections.Remove(sessionId);
        }
    }
}
