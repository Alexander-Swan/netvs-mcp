using System;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>Retains only the notification immediately preceding the current exception pause.</summary>
internal sealed class DebugExceptionTracker
{
    private readonly object gate = new();
    private DebugExceptionEvidence? pending;
    private DebugExceptionEvidence? current;
    private long? generation, command;
    private string? stopId;

    internal void Capture(string? category, string? type, string? description, int? code, bool unhandled,
        int? processId, int? threadId, DebuggerStateInfo state, string? notificationKind = null, string? processingState = null, string? message = null)
    {
        lock (gate)
        {
            var truncated = false;
            string? Clip(string? value, int limit)
            {
                if (value is null || value.Length <= limit) return value;
                truncated = true;
                return value.Substring(0, limit);
            }
            var clippedCategory = Clip(category, 256);
            var clippedType = Clip(type, 1024);
            var clippedDescription = Clip(description, 4096);
            var clippedMessage = Clip(message, 4096);
            pending = new(clippedCategory, clippedType, clippedDescription, code.HasValue ? "0x" + unchecked((uint)code.Value).ToString("X8") : null,
                notificationKind ?? (unhandled ? "notHandled" : "thrown"), unhandled ? false : null, processId, threadId, DateTimeOffset.UtcNow, truncated,
                ProcessingState: processingState, Message: clippedMessage);
            generation = state.Generation;
            command = state.ExecutionSequence;
        }
    }

    internal void Bind(DebuggerStateInfo state)
    {
        lock (gate)
        {
            current = state.Mode == "dbgBreakMode" && state.BreakReason == "exception" &&
                state.Generation == generation && state.ExecutionSequence == command ? pending : null;
            stopId = state.StopId;
            pending = null;
        }
    }

    internal DebugExceptionEvidence? Read(DebuggerStateInfo state)
    {
        lock (gate) return state.Mode == "dbgBreakMode" &&
            state.BreakReason == "exception" && state.StopId is not null && state.StopId == stopId && state.Generation == generation
            ? current : null;
    }

    internal void Clear() { lock (gate) { pending = current = null; stopId = null; generation = command = null; } }
}
