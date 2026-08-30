namespace NetVsMcp.Contracts;

public interface IBrokerRegistrationRpc
{
    Task<ToolResponse> RegisterAsync(VsSessionRegistration registration, CancellationToken cancellationToken);

    Task<ToolResponse> UpdateAsync(VsSessionUpdate update, CancellationToken cancellationToken);

    Task<ToolResponse> HeartbeatAsync(string sessionId, CancellationToken cancellationToken);

    Task<ToolResponse> UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}
