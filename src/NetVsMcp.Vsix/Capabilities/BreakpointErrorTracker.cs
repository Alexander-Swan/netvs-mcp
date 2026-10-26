using System;
using System.Collections.Generic;
using System.Linq;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>Historical notifications only: a source location or reused name is never an ownership/correlation key.</summary>
internal sealed class BreakpointErrorTracker
{
    private readonly object gate = new();
    private readonly Queue<BreakpointErrorNotification> notifications = new();
    private long sequence;
    private long? lastDroppedGeneration;
    private bool hasDropped;
    internal void Capture(long? generation, int? processId, string? engineId, string errorType, string? message, uint? rawFlags = null)
    {
        lock (gate)
        {
            var text = message ?? string.Empty;
            notifications.Enqueue(new(++sequence, DateTimeOffset.UtcNow, generation, processId, engineId, errorType,
                text.Length > 2048 ? text.Substring(0, 2048) : text, text.Length > 2048, rawFlags));
            while (notifications.Count > 64) { lastDroppedGeneration = notifications.Dequeue().Generation; hasDropped = true; }
        }
    }
    internal (IReadOnlyList<BreakpointErrorNotification> Items, bool Truncated) Read(long? generation)
    {
        lock (gate)
        {
            var current = notifications.Where(n => n.Generation == generation).ToArray();
            return (current.AsEnumerable().Reverse().Take(32).ToArray(), current.Length > 32 || hasDropped && lastDroppedGeneration == generation);
        }
    }
}
