using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

/// <summary>Correlates commands with subsequent debugger notifications, never with the pause they left.</summary>
internal sealed class DebugExecutionTracker
{
    private readonly object gate = new();
    private readonly string epoch = Guid.NewGuid().ToString("N");
    private TaskCompletionSource<bool> changed = NewSignal();
    private string mode = "dbgDesignMode";
    private string? reason;
    private string? stopId;
    private long generation;
    private long sequence;
    private long version;
    private bool active;
    private bool pending;
    private bool unobservedCommand;
    private long command;

    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Initialize(string initialMode)
    {
        lock (gate)
        {
            mode = initialMode;
            if (mode != "dbgDesignMode") { active = true; generation++; }
            if (mode == "dbgBreakMode") stopId = NextStop();
        }
    }

    public long Begin(bool newSession = false)
    {
        lock (gate)
        {
            if (newSession) { generation++; active = true; }
            pending = true;
            unobservedCommand = true;
            stopId = null;
            reason = null;
            command++;
            return version;
        }
    }

    public void Record(string nextMode, string? breakReason = null, string? liveMode = null)
    {
        TaskCompletionSource<bool> wake;
        lock (gate)
        {
            unobservedCommand = false;
            // Delayed notifications must not resurrect a mode the debugger has already left.
            if (liveMode != null && nextMode != liveMode)
            {
                // Even a delayed resume proves that a formerly issued inspection token may
                // be stale. Do not adopt its obsolete mode. A fresh UI-thread status read
                // can issue a new token without claiming this completed a pending command.
                if (nextMode is "dbgRunMode" or "dbgDesignMode")
                {
                    stopId = null;
                    reason = null;
                    if (nextMode == "dbgDesignMode") generation++;
                }
                return;
            }
            if (!pending && nextMode == mode && breakReason == reason) return;
            version++;
            if (nextMode != "dbgDesignMode" && !active) { generation++; active = true; }
            if (nextMode == "dbgDesignMode") active = false;
            mode = nextMode;
            reason = nextMode == "dbgBreakMode" ? breakReason : null;
            stopId = nextMode == "dbgBreakMode" ? NextStop() : null;
            pending = false;
            wake = changed;
            changed = NewSignal();
        }
        wake.TrySetResult(true);
    }

    public void RejectCommand(string liveMode, long? expectedSequence = null)
    {
        lock (gate)
        {
            // A rejected or canceled readiness wait must not erase a newer command
            // or a stop notification that arrived while the UI thread was yielded.
            if (expectedSequence.HasValue && (!pending || command != expectedSequence.Value)) return;
            // Do not manufacture a new pause from a command rejected before any notification.
            pending = false;
            unobservedCommand = false;
            mode = liveMode;
            stopId = null;
            reason = null;
        }
    }

    public bool CanRetryCommand(long? expectedSequence)
    {
        lock (gate) return pending && unobservedCommand && command == expectedSequence;
    }

    public DebuggerStateInfo State(string liveMode, string? liveReason = null, string? outcome = null, bool refresh = false)
    {
        lock (gate)
        {
            // A live run/design observation also invalidates a stop before a delayed event arrives.
            if (liveMode != "dbgBreakMode") stopId = null;
            if (refresh && !pending && liveMode == "dbgBreakMode" && stopId == null)
            {
                stopId = NextStop();
                reason = liveReason;
            }
            var observed = pending ? "pending" : liveMode switch
            {
                "dbgBreakMode" => stopId is null ? "unknown" : "stopped",
                "dbgRunMode" => "running",
                "dbgDesignMode" => generation == 0 ? "idle" : "exited",
                _ => "unknown"
            };
            return new DebuggerStateInfo(liveMode, pending || liveMode != "dbgBreakMode" ? null : reason ?? liveReason,
                outcome ?? observed, generation, pending || liveMode != "dbgBreakMode" ? null : stopId, command);
        }
    }

    public void Validate(string liveMode, string? expectedStopId, long? expectedGeneration)
    {
        lock (gate)
        {
            if (pending || liveMode != "dbgBreakMode" || stopId == null ||
                (expectedStopId != null && !string.Equals(stopId, expectedStopId, StringComparison.Ordinal)) ||
                (expectedGeneration.HasValue && generation != expectedGeneration))
                throw new StaleDebugContextException("Stale debugger context. Read debug_status or debug_snapshot for the current generation and stopId before inspecting again.");
        }
    }

    internal DebuggerStateInfo ObservedState()
    {
        lock (gate) return State(mode);
    }

    public async Task<bool> WaitAsync(long afterVersion, bool waitForStop, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            while (true)
            {
                Task signal;
                lock (gate)
                {
                    if (version > afterVersion && (!waitForStop || mode is "dbgBreakMode" or "dbgDesignMode")) return true;
                    signal = changed.Task;
                }
                // Task.WaitAsync is unavailable in the VSIX's .NET Framework runtime.
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (deadline.Token.Register(() => canceled.TrySetCanceled()))
                {
                    var completed = await Task.WhenAny(signal, canceled.Task).ConfigureAwait(false);
                    await completed.ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }

    private string NextStop() => epoch + ":" + generation + ":" + ++sequence;
}
