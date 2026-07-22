using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRegistrationRpcService : IBrokerRegistrationRpc
{
    private readonly SessionRegistry _sessions;

    public BrokerRegistrationRpcService(SessionRegistry sessions)
    {
        _sessions = sessions;
    }

    public Task<ToolResponse> RegisterAsync(
        VsSessionRegistration registration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.Register(registration));
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
        return Task.FromResult(_sessions.Unregister(sessionId));
    }
}
