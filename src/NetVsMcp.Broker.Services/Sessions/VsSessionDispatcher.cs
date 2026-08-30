using NetVsMcp.Contracts;
using System.Diagnostics;
using System.IO;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Services;

internal sealed class VsSessionDispatcher : IVsSessionDispatcher
{
    private const string DocumentPathGuidance = "Use forward slashes in documentPath/path values, for example src/Project/File.cs. If you use Windows backslashes in JSON, escape them as double backslashes.";

    // Applies whenever a caller doesn't specify its own timeout. Generous enough not to
    // false-positive on legitimately slow operations (a real build/test/restore can take
    // minutes), but still finite - a VSIX-side call that hangs (see the watch_add design-mode
    // hang this was added after) now fails cleanly instead of blocking the caller forever.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

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
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
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

        using var dispatchLease = _sessions.BeginDispatch(route.Session.SessionId);
        try
        {
            var effectiveTimeout = timeout ?? DefaultTimeout;
            var operationTask = InvokeSafely(operation, connection, cancellationToken);

            // Cancelling a StreamJsonRpc call's token only sends a "$/cancelRequest" notification -
            // the local await does NOT unblock until the *server* actually responds to it. A VSIX-
            // side handler stuck in a synchronous, cancellation-oblivious COM call (e.g. attaching
            // over a remote debugger transport to an unreachable host) never observes that
            // notification, so it never responds, so passing a cancelled token here would hang for
            // exactly as long as that COM call does - which is what happened in practice with a
            // hung SSH attach. Race with a plain delay instead, so this always returns within
            // effectiveTimeout regardless of whether the far side ever cooperates, and abandon
            // (rather than keep awaiting) the operation if the delay wins.
            var delayTask = Task.Delay(effectiveTimeout, cancellationToken);
            var firstCompleted = await Task.WhenAny(operationTask, delayTask);

            if (firstCompleted == delayTask)
            {
                ObserveAbandonedOperation(operationTask, route.Session.SessionId);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return VsSessionDispatchResult<T>.Failed(
                    VsSessionDispatchFailureReason.OperationTimedOut,
                    $"Visual Studio session '{route.Session.SessionId}' did not respond within {effectiveTimeout.TotalSeconds:0}s.",
                    route.Session);
            }

            var value = await operationTask;
            return VsSessionDispatchResult<T>.Ok(route.Session, value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RemoteMethodNotFoundException)
        {
            return VsSessionDispatchResult<T>.Failed(
                VsSessionDispatchFailureReason.UnsupportedByVsix,
                $"Your VSIX doesn't support this tool yet, reinstall the extension. (Visual Studio session '{route.Session.SessionId}' has no matching RPC method.)",
                route.Session);
        }
        catch (Exception ex)
        {
            return VsSessionDispatchResult<T>.Failed(
                VsSessionDispatchFailureReason.RpcFailure,
                $"Visual Studio session '{route.Session.SessionId}' RPC call failed: {AddDocumentPathGuidance(ex)}",
                route.Session);
        }
    }

    // A delegate that throws synchronously (rather than returning a faulted Task) would
    // otherwise escape uncaught, since the initial invocation happens before the try below -
    // normalize it into a faulted Task so every failure mode flows through the same handling.
    private static Task<T> InvokeSafely<T>(
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        IVisualStudioSessionRpc connection,
        CancellationToken cancellationToken)
    {
        try
        {
            return operation(connection, cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    // The abandoned task keeps running in the background (there's no safe way to force-abort a
    // synchronous COM call on the VSIX's STA thread from here); this just prevents an unobserved
    // task exception if/when it eventually faults, and gives a trace breadcrumb rather than
    // silently swallowing it.
    private static void ObserveAbandonedOperation<T>(Task<T> operationTask, string sessionId)
    {
        _ = operationTask.ContinueWith(
            t => Trace.WriteLine($"NetVsMcp: dispatch to session '{sessionId}' timed out, then later faulted: {t.Exception?.GetBaseException().Message}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
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
