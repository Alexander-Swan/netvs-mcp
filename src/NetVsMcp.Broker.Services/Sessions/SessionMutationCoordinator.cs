using System.Reflection;
using System.Runtime.ExceptionServices;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class SessionMutationConflictException(string message) : Exception(message);

/// <summary>Rejects overlapping commands; retains ownership until the actual RPC finishes.</summary>
internal sealed class SessionMutationCoordinator
{
    private sealed class State { public bool Paused; public string? Active; }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
    private readonly object gate = new();
    private readonly Dictionary<string, State> sessions = new(StringComparer.OrdinalIgnoreCase);

    public (bool Paused, string? Active) Inspect(string sessionId, bool? paused = null)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var state)) sessions.Add(sessionId, state = new());
            if (paused is not null) state.Paused = paused.Value;
            return (state.Paused, state.Active);
        }
    }

    public IDisposable Enter(string sessionId, string method)
    {
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var state)) sessions.Add(sessionId, state = new());
            if (state.Paused) throw new SessionMutationConflictException("Automation mutations are paused for this session. Resume with session_automation_control after the developer is ready.");
            if (state.Active is not null) throw new SessionMutationConflictException($"Session is busy with {state.Active}. Inspect its result before retrying; commands are not queued or replayed.");
            state.Active = method;
            return new Lease(() => { lock (gate) state.Active = null; });
        }
    }
}

// A conservative boundary: unknown/new RPC methods are exclusive until explicitly reviewed.
public class CoordinatedSessionRpc : DispatchProxy
{
    private static readonly HashSet<string> Reads = new(StringComparer.Ordinal)
    {
        "GetStatusAsync", "SessionCapabilitiesAsync", "SessionSnapshotAsync", "DebugStatusAsync", "DebugExceptionAsync", "DebugSourceEvidenceAsync", "DebugGetModeAsync", "BuildStatusAsync",
        "GetActiveDocumentAsync", "ListDocumentSymbolsAsync", "DocumentReadAsync", "DocumentDiagnosticsAsync", "DocumentListAsync", "SelectionGetAsync",
        "WindowListAsync", "EditListPendingAsync", "SolutionInfoAsync", "ProjectListAsync", "ProjectInfoAsync",
        "StartupProjectGetAsync", "TestResultsAsync", "TestResultsPageAsync", "CodeFindReferencesAsync", "CodeFindImplementationsAsync",
        "CodeWorkspaceSymbolsAsync", "CallHierarchyGetAsync", "CodeActionsListAsync", "CodeActionsPreviewAsync",
        "CodeRenameSymbolPreviewAsync", "NugetListAsync", "NugetSearchAsync", "BuildConfigurationGetAsync",
        "ErrorsListAsync", "TaskListGetAsync", "OutputReadAsync", "OutputListPanesAsync", "BreakpointListAsync", "BreakpointDiagnosticsAsync",
        "ModuleListAsync", "ProcessListLocalAsync", "ProcessListDebuggedAsync", "ExceptionSettingsGetAsync",
        "ConsoleReadAsync", "ConsoleGetInfoAsync", "DiagnosticsBindingErrorsAsync", "UiCaptureWindowAsync", "UiCaptureRegionAsync",
        "UiSnapshotAsync", "UiGetTreeAsync", "UiFindElementsAsync", "UiGetElementAsync", "UiWaitForElementAsync", "UiWaitIdleAsync",
        "WebStatusAsync", "WebScreenshotAsync", "WebDomGetAsync", "WebDomQueryAsync", "WebConsoleAsync",
        "WebBlazorInspectAsync", "WebBlazorWaitReadyAsync", "WebBlazorGetComponentsAsync", "WebBlazorGetStateAsync",
        "WebBlazorInspectCircuitAsync", "WebBlazorInspectWasmAsync", "WebNetworkAsync",
        // Recovery controls must remain callable while the command they interrupt is in flight.
        "BuildCancelAsync", "DebugBreakAsync"
    };
    private IVisualStudioSessionRpc target = null!;
    private SessionMutationCoordinator coordinator = null!;
    private string sessionId = null!;

    internal static IVisualStudioSessionRpc Wrap(IVisualStudioSessionRpc target, string sessionId, SessionMutationCoordinator coordinator)
    {
        var proxy = Create<IVisualStudioSessionRpc, CoordinatedSessionRpc>();
        var state = (CoordinatedSessionRpc)proxy;
        state.target = target; state.sessionId = sessionId; state.coordinator = coordinator;
        return proxy;
    }

    internal static IVisualStudioSessionRpc Unwrap(IVisualStudioSessionRpc connection) =>
        connection is CoordinatedSessionRpc proxy ? proxy.target : connection;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (Reads.Contains(method.Name)) return InvokeTarget(method, args);
        var lease = coordinator.Enter(sessionId, method.Name);
        try
        {
            var task = InvokeTarget(method, args)!;
            var resultType = method.ReturnType.GetGenericArguments().Single();
            return typeof(CoordinatedSessionRpc).GetMethod(nameof(HoldAsync), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(resultType).Invoke(null, [task, lease]);
        }
        catch { lease.Dispose(); throw; }
    }

    private object? InvokeTarget(MethodInfo method, object?[]? args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    private static async Task<T> HoldAsync<T>(Task<T> work, IDisposable lease)
    { using (lease) return await work.ConfigureAwait(false); }
}
