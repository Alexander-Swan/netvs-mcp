using NetVsMcp.Contracts;
using System.IO;

namespace NetVsMcp.Broker.Services;

public interface IVsSessionDispatcher
{
    Task<VsSessionDispatchResult<T>> DispatchAsync<T>(
        RoutingTarget? target,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}

public sealed class VsSessionDispatcher : IVsSessionDispatcher
{
    private const string DocumentPathGuidance = "Use forward slashes in documentPath/path values, for example src/Project/File.cs. If you use Windows backslashes in JSON, escape them as double backslashes.";

    private readonly SessionRegistry _sessions;
    private readonly IVsSessionConnectionMap _connections;

    public VsSessionDispatcher(
        SessionRegistry sessions,
        IVsSessionConnectionMap connections)
    {
        _sessions = sessions;
        _connections = connections;
    }

    public async Task<VsSessionDispatchResult<T>> DispatchAsync<T>(
        RoutingTarget? target,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var route = _sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            return VsSessionDispatchResult<T>.Failed(
                MapRouteFailure(route.FailureReason),
                route.Message ?? "Unable to route Visual Studio session request.",
                candidates: route.Candidates);
        }

        var status = _sessions.ListSessionStatuses()
            .SingleOrDefault(sessionStatus => string.Equals(
                sessionStatus.Session.SessionId,
                route.Session.SessionId,
                StringComparison.OrdinalIgnoreCase));

        if (status?.Health is not SessionHealth.Connected)
        {
            return VsSessionDispatchResult<T>.Failed(
                VsSessionDispatchFailureReason.StaleSession,
                $"Visual Studio session '{route.Session.SessionId}' is not connected.",
                route.Session);
        }

        if (!_connections.TryGet(route.Session.SessionId, out var connection))
        {
            return VsSessionDispatchResult<T>.Failed(
                VsSessionDispatchFailureReason.MissingConnection,
                $"Visual Studio session '{route.Session.SessionId}' has no active RPC connection.",
                route.Session);
        }

        try
        {
            var value = await operation(connection, cancellationToken);
            return VsSessionDispatchResult<T>.Ok(route.Session, value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return VsSessionDispatchResult<T>.Failed(
                VsSessionDispatchFailureReason.RpcFailure,
                $"Visual Studio session '{route.Session.SessionId}' RPC call failed: {AddDocumentPathGuidance(ex)}",
                route.Session);
        }
    }

    private static string AddDocumentPathGuidance(Exception exception)
    {
        var message = exception.Message;
        if (!LooksLikePathParsingFailure(exception, message) || message.Contains(DocumentPathGuidance, StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{message} {DocumentPathGuidance}";
    }

    private static bool LooksLikePathParsingFailure(Exception exception, string message)
    {
        return exception is ArgumentException or NotSupportedException or PathTooLongException
            && message.Contains("path", StringComparison.OrdinalIgnoreCase);
    }

    private static VsSessionDispatchFailureReason MapRouteFailure(RouteFailureReason reason)
    {
        return reason switch
        {
            RouteFailureReason.NoRegisteredSessions => VsSessionDispatchFailureReason.NoRegisteredSessions,
            RouteFailureReason.SessionNotFound => VsSessionDispatchFailureReason.SessionNotFound,
            RouteFailureReason.ProcessIdNotFound => VsSessionDispatchFailureReason.SessionNotFound,
            RouteFailureReason.SolutionPathNotFound => VsSessionDispatchFailureReason.SolutionPathNotFound,
            RouteFailureReason.SolutionNameNotFound => VsSessionDispatchFailureReason.SolutionNameNotFound,
            RouteFailureReason.WorkspacePathNotFound => VsSessionDispatchFailureReason.SolutionPathNotFound,
            RouteFailureReason.Ambiguous => VsSessionDispatchFailureReason.AmbiguousTarget,
            _ => VsSessionDispatchFailureReason.SessionNotFound
        };
    }
}
