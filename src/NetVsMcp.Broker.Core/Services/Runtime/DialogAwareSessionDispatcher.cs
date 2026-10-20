using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

/// <summary>Yields a pending RPC to the client when a modal dialog needs a decision.</summary>
internal sealed class DialogAwareSessionDispatcher(
    IVsSessionDispatcher inner,
    SessionRegistry sessions,
    DialogAutomationCoordinator dialogs,
    TimeSpan? probeInterval = null) : IVsSessionDispatcher
{
    private sealed record Completion(bool Success, JsonElement? Result, string? Message, string? ErrorCode);
    private sealed record Pending(VsSessionInfo Session, Task<Completion> Work)
    {
        public long CompletedUtcTicks;
        public long Sequence { get; } = Interlocked.Increment(ref nextSequence);
    }

    private static long nextSequence;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Pending> pending = new();
    private readonly ConcurrentDictionary<string, string> knownModes = new();
    private readonly ConcurrentDictionary<string, (long Timestamp, IReadOnlyList<DialogInfo> Dialogs)> recentProbes = new();
    private readonly TimeSpan interval = probeInterval ?? TimeSpan.FromSeconds(1);

    public async Task<VsSessionDispatchResult<T>> DispatchAsync<T>(RoutingTarget? target,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var route = sessions.Resolve(target);
        if (!route.Success || route.Session is null)
            return await inner.DispatchAsync(target, operation, cancellationToken, timeout);

        // Pin routing before starting the command, so a change of active VS window cannot
        // make the dialog probe inspect a different process from the original operation.
        var session = route.Session;
        var lifetime = new CancellationTokenSource();
        var registration = cancellationToken.Register(lifetime.Cancel);
        var work = inner.DispatchAsync(new RoutingTarget(SessionId: session.SessionId), operation, lifetime.Token, timeout);
        using var probing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            while (!work.IsCompleted)
            {
                if (await Task.WhenAny(work, Task.Delay(interval, probing.Token)) == work) break;
                cancellationToken.ThrowIfCancellationRequested();
                var probe = ProbeAsync(session.ProcessId, probing.Token);
                if (await Task.WhenAny(work, probe) == work) break;
                var found = await probe;
                if (work.IsCompleted) break;
                if (found.Count == 0) continue;

                Cleanup();
                // Never evict a running command to make room for another continuation.
                cancellationToken.ThrowIfCancellationRequested();
                string id;
                lock (pending)
                {
                    if (pending.Count >= 128) continue;
                    id = Guid.NewGuid().ToString("N");
                    var entry = new Pending(session, CompleteAsync(work));
                    pending[id] = entry;
                    _ = entry.Work.ContinueWith(_ => Interlocked.Exchange(ref entry.CompletedUtcTicks, DateTimeOffset.UtcNow.Ticks),
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                return VsSessionDispatchResult<T>.Failed(VsSessionDispatchFailureReason.DialogBlocked,
                    "Visual Studio has a modal dialog while this operation is pending. Choose an appropriate enabled button with dialog_respond, then call debug_status with this sessionId and operationId to inspect the original outcome. The original command is still running; do not issue it again.",
                    session) with { OperationId = id, Dialogs = found };
            }
            var result = await work;
            if (result.Success) RememberState(session, result.Value);
            return result;
        }
        finally
        {
            // Detach the request token once control is returned for a dialog decision. The
            // inner dispatch retains its original deadline and session dispatch lease.
            probing.Cancel();
            registration.Dispose();
            _ = work.ContinueWith(t =>
            {
                _ = t.Exception;
                lifetime.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    public DebuggerStateInfo ModalState(VsSessionInfo session, IReadOnlyList<DialogInfo> found, string? operationId = null) =>
        new("dbgModalBlock", KnownMode(session), true, found, session.SessionId,
            operationId is null ? null : new DebugOperationInfo(operationId, "running"));

    // Used inside debug wait/settle loops. Probe independently of DTE before each status read,
    // but throttle accessibility traversal so a 50ms debugger poll doesn't scan at 20Hz.
    public async Task<DebuggerStateInfo> ReadDebugStateAsync(VsSessionInfo session, IVisualStudioSessionRpc connection,
        CancellationToken cancellationToken)
    {
        var timestamp = Environment.TickCount64;
        IReadOnlyList<DialogInfo> found;
        if (recentProbes.TryGetValue(session.SessionId, out var cached) && cached.Dialogs.Count == 0 && timestamp - cached.Timestamp < 500)
            found = cached.Dialogs;
        else
        {
            found = await ProbeAsync(session.ProcessId, cancellationToken);
            recentProbes[session.SessionId] = (Environment.TickCount64, found);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (found.Count > 0) return ModalState(session, found);
        var state = await connection.DebugStatusAsync(cancellationToken);
        RememberState(session, state);
        return state;
    }

    public async Task<ToolResponse<DebuggerStateInfo>> GetDebugStatusAsync(RoutingTarget? target, string? operationId,
        CancellationToken cancellationToken)
    {
        Cleanup();
        cancellationToken.ThrowIfCancellationRequested();
        Pending? entry = null;
        if (operationId is not null && !pending.TryGetValue(operationId, out entry))
            return InvalidStatus("Unknown or expired operationId. Completed operations are retained for 10 minutes and are lost when the broker restarts.");
        // An operation ID supplies routing when no routing fields were specified. Explicit
        // routing must agree with its original session; never fetch a different VS instance.
        var route = sessions.Resolve(target ?? (entry is null ? null : new RoutingTarget(SessionId: entry.Session.SessionId)));
        if (!route.Success || route.Session is null)
            return new(false, null, route.Message, BrokerToolService.CreateRouteFailureMetadata(route));
        var session = route.Session;
        if (entry is not null && entry.Session.SessionId != session.SessionId)
            return InvalidStatus("operationId belongs to a different VS session. Use its original sessionId.");
        if (entry is null)
        {
            var latest = pending.Where(p => p.Value.Session.SessionId == session.SessionId)
                .OrderByDescending(p => p.Value.Sequence).FirstOrDefault();
            entry = latest.Value;
            operationId = latest.Key;
        }
        var found = await ProbeAsync(session.ProcessId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        recentProbes[session.SessionId] = (Environment.TickCount64, found);
        DebugOperationInfo? operation = null;
        if (entry is not null)
        {
            if (entry.Work.IsCompleted)
            {
                var completion = await entry.Work;
                operation = new(operationId!, completion.Success ? "completed" : "failed", completion.Success,
                    completion.Result, completion.Message, completion.ErrorCode);
            }
            else operation = new(operationId!, "running");
        }
        if (found.Count > 0)
            return ToolResponse<DebuggerStateInfo>.Ok(ModalState(session, found) with { Operation = operation });
        // Avoid queuing another UI-thread RPC behind any original command still pending.
        if (operation?.ErrorCode == ToolErrorCodes.OperationTimedOut ||
            pending.Values.Any(p => p.Session.SessionId == session.SessionId && !p.Work.IsCompleted))
            return ToolResponse<DebuggerStateInfo>.Ok(new(KnownMode(session), KnownMode(session), true,
                SessionId: session.SessionId, Operation: operation));
        var dispatch = await DispatchAsync(new RoutingTarget(SessionId: session.SessionId),
            static (connection, ct) => connection.DebugStatusAsync(ct), cancellationToken);
        if (dispatch.FailureReason == VsSessionDispatchFailureReason.DialogBlocked)
            return ToolResponse<DebuggerStateInfo>.Ok(ModalState(session, dispatch.Dialogs!, dispatch.OperationId));
        if (!dispatch.Success && operation is not null)
            return new(true, new(KnownMode(session), KnownMode(session), true, SessionId: session.SessionId, Operation: operation),
                "The original outcome is available, but a fresh debugger mode could not be read.", BrokerToolService.CreateFailureMetadata(dispatch));
        if (!dispatch.Success)
            return new(false, null, dispatch.Message, BrokerToolService.CreateFailureMetadata(dispatch));
        return ToolResponse<DebuggerStateInfo>.Ok(dispatch.Value! with { SessionId = session.SessionId, Operation = operation });
    }

    private static ToolResponse<DebuggerStateInfo> InvalidStatus(string message) =>
        new(false, null, message, new Dictionary<string, string> { ["error_code"] = ToolErrorCodes.InvalidRequest });

    private string KnownMode(VsSessionInfo session) => knownModes.TryGetValue(session.SessionId, out var mode) ? mode :
        session.DebuggerMode switch
        {
            DebuggerMode.Run => "dbgRunMode",
            DebuggerMode.Break => "dbgBreakMode",
            DebuggerMode.Design => "dbgDesignMode",
            _ => "Unknown"
        };

    internal void RememberState<T>(VsSessionInfo session, T value)
    {
        var state = value switch
        {
            DebuggerStateInfo direct => direct,
            DebugSnapshotResult snapshot => snapshot.State,
            _ => null
        };
        if (state is not null && state.Mode != "dbgModalBlock" && !state.UnderlyingModeIsStale)
            knownModes[session.SessionId] = state.Mode;
    }

    private async Task<IReadOnlyList<DialogInfo>> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        try { return await dialogs.ListAsync(processId, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return []; }
        catch (Exception ex)
        {
            // Inaccessible/custom UIA providers must not fail an otherwise valid IDE command.
            Trace.WriteLine($"NetVsMcp dialog probe failed for process {processId}: {ex.Message}");
            return [];
        }
    }

    private async Task<Completion> CompleteAsync<T>(Task<VsSessionDispatchResult<T>> work)
    {
        try
        {
            var result = await work;
            if (!result.Success)
                return new(false, null, result.Message, BrokerToolService.MapDispatchFailureToErrorCode(result.FailureReason));
            RememberState(result.Session!, result.Value);
            var payload = JsonSerializer.SerializeToElement(result.Value, JsonOptions);
            // Some RPCs return their own ToolResponse envelope. Preserve that payload and
            // surface its failure as well as transport/dispatch failures.
            if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                var message = payload.TryGetProperty("message", out var text) ? text.GetString() : null;
                return new(false, payload, message, ToolErrorCodes.VisualStudioError);
            }
            return new(true, payload, result.Message, null);
        }
        catch (OperationCanceledException) { return new(false, null, "The original operation was canceled.", "operation_canceled"); }
        catch (Exception ex) { return new(false, null, ex.Message, ToolErrorCodes.RpcFailure); }
    }

    private void Cleanup()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10).Ticks;
        foreach (var pair in pending)
            if (Interlocked.Read(ref pair.Value.CompletedUtcTicks) is var completed && completed > 0 && completed < cutoff)
                pending.TryRemove(pair.Key, out _);
    }
}
