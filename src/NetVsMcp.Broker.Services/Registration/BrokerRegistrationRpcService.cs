using NetVsMcp.Contracts;
using System.Reflection;

namespace NetVsMcp.Broker.Services;

internal sealed class BrokerRegistrationRpcService : IBrokerRegistrationRpc
{
    private readonly SessionRegistry _sessions;
    private readonly IVsSessionConnectionMap? _connections;
    private readonly IVisualStudioSessionRpc? _sessionConnection;
    private readonly Func<bool> _shutdownAllowed;
    private readonly Action _requestShutdown;
    private readonly HashSet<string> _registeredSessionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public BrokerRegistrationRpcService(
        SessionRegistry sessions,
        IVsSessionConnectionMap? connections = null,
        IVisualStudioSessionRpc? sessionConnection = null,
        Func<bool>? shutdownAllowed = null,
        Action? requestShutdown = null)
    {
        _sessions = sessions;
        _connections = connections;
        _sessionConnection = sessionConnection;
        _shutdownAllowed = shutdownAllowed ?? (() => false);
        _requestShutdown = requestShutdown ?? (() => { });
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
        if (!IsCompatibleProtocol(registration.ProtocolVersion))
        {
            return Task.FromResult(ProtocolMismatch(registration.ProtocolVersion));
        }

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
        if (!IsCompatibleProtocol(update.ProtocolVersion))
        {
            return Task.FromResult(ProtocolMismatch(update.ProtocolVersion));
        }

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
            RequestShutdownIfAllowedAndNoSessionsRemain();
        }

        return Task.FromResult(response);
    }

    public Task<ToolResponse> ShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = RequestShutdownIfAllowedAndNoSessionsRemain();
        if (!response.Success)
        {
            return Task.FromResult(response);
        }

        return Task.FromResult(response);
    }

    public void RemoveRegisteredConnections()
    {
        var removedAny = false;
        foreach (var sessionId in RegisteredSessionIds)
        {
            var response = _sessions.Unregister(sessionId);
            removedAny = removedAny || response.Success;
            RemoveConnection(sessionId);
        }

        if (removedAny)
        {
            RequestShutdownIfAllowedAndNoSessionsRemain();
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

    private ToolResponse RequestShutdownIfAllowedAndNoSessionsRemain()
    {
        if (!_shutdownAllowed())
        {
            return ToolResponse.Fail("Broker shutdown is not available for this broker launch mode.");
        }

        if (_sessions.ListSessions().Count > 0)
        {
            return ToolResponse.Fail("Broker shutdown skipped because Visual Studio sessions are still registered.");
        }

        _requestShutdown();
        return ToolResponse.Ok("Broker shutdown requested.");
    }

    private static bool IsCompatibleProtocol(string? protocolVersion)
    {
        if (string.IsNullOrWhiteSpace(protocolVersion))
        {
            return false;
        }

        var majorText = protocolVersion.Split('.')[0];
        return int.TryParse(majorText, out var major) && major == VsRpcProtocol.CurrentMajorVersion;
    }

    private static ToolResponse ProtocolMismatch(string? protocolVersion)
    {
        var brokerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        return new ToolResponse(
            false,
            $"Visual Studio extension RPC protocol '{protocolVersion ?? "unknown"}' is not compatible with broker protocol '{VsRpcProtocol.CurrentVersion}'. Install the latest NetVsMcp Broker and try again.",
            new Dictionary<string, string>
            {
                ["error_code"] = ToolErrorCodes.ProtocolMismatch,
                ["vsix_protocol"] = protocolVersion ?? string.Empty,
                ["broker_protocol"] = VsRpcProtocol.CurrentVersion,
                ["broker_version"] = brokerVersion,
                ["download_url"] = "https://github.com/Alexander-Swan/netvs-mcp/releases/latest"
            });
    }
}
