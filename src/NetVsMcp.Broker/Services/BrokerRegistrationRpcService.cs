using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRegistrationRpcService : IBrokerRegistrationRpc
{
    private readonly SessionRegistry _sessions;
    private readonly IVsSessionConnectionMap? _connections;
    private readonly IVisualStudioSessionRpc? _sessionConnection;
    private readonly HashSet<string> _registeredSessionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public BrokerRegistrationRpcService(
        SessionRegistry sessions,
        IVsSessionConnectionMap? connections = null,
        IVisualStudioSessionRpc? sessionConnection = null)
    {
        _sessions = sessions;
        _connections = connections;
        _sessionConnection = sessionConnection;
    }

    public IReadOnlyCollection<string> RegisteredSessionIds
    {
        get
        {
            lock (_gate)
            {
                return _registeredSessionIds.ToArray();
            }
        }
    }

    public Task<ToolResponse> RegisterAsync(
        VsSessionRegistration registration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = _sessions.Register(registration);

        if (response.Success)
        {
            lock (_gate)
            {
                _registeredSessionIds.Add(registration.SessionId);
            }

            if (_sessionConnection is not null)
            {
                _connections?.AddOrUpdate(registration.SessionId, _sessionConnection);
            }
        }

        return Task.FromResult(response);
    }

    public Task<ToolResponse> UpdateAsync(
        VsSessionUpdate update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.Update(update));
    }

    public Task<ToolResponse> HeartbeatAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.Heartbeat(sessionId));
    }

    public Task<ToolResponse> UnregisterAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = _sessions.Unregister(sessionId);

        if (response.Success)
        {
            RemoveConnection(sessionId);
        }

        return Task.FromResult(response);
    }

    public void RemoveRegisteredConnections()
    {
        foreach (var sessionId in RegisteredSessionIds)
        {
            RemoveConnection(sessionId);
        }
    }

    private void RemoveConnection(string sessionId)
    {
        lock (_gate)
        {
            _registeredSessionIds.Remove(sessionId);
        }

        _connections?.Remove(sessionId);
    }
}
