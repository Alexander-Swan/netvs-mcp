namespace NetVsMcp.Contracts;

/// <summary>One ordered observation from a VS publisher. Epochs change on publisher restart.</summary>
public sealed record VsSessionEvent(string SessionId, string Epoch, long Sequence, string Kind,
    DateTimeOffset TimestampUtc, DebuggerStateInfo? Debugger = null, string? OperationId = null);

public sealed record VsEventBatch(string SessionId, string Epoch, long Watermark,
    bool RequiresReconciliation, IReadOnlyList<VsSessionEvent> Events);

/// <summary>A publisher watermark captured with the enclosing debugger state.</summary>
public sealed record VsSessionEventCursor(string SessionId, string Epoch, long Watermark);

public sealed record VsSessionEventSnapshot(string SessionId, DebuggerStateInfo Debugger, VsEventBatch Events);

/// <summary>
/// Bounded replay with independent cursors. Reading never consumes events for other waiters.
/// A gap is explicit; a current-state snapshot must be used before trusting further events.
/// </summary>
public sealed class SessionEventBuffer
{
    private readonly object gate = new();
    private readonly Queue<VsSessionEvent> events = new();
    private readonly int capacity;
    private readonly string sessionId;
    private string epoch;
    private long watermark;
    private bool invalidated;
    private bool requiresSnapshot;
    private TaskCompletionSource<bool> changed = Signal();

    public SessionEventBuffer(string sessionId, int capacity = 256, string? epoch = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session is required.", nameof(sessionId));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.sessionId = sessionId;
        this.capacity = capacity;
        this.epoch = epoch ?? Guid.NewGuid().ToString("N");
    }

    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public VsSessionEvent Publish(string kind, DebuggerStateInfo? debugger = null, string? operationId = null)
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Kind is required.", nameof(kind));
        TaskCompletionSource<bool> wake;
        VsSessionEvent item;
        lock (gate)
        {
            if (invalidated) throw new InvalidOperationException("The event connection was replaced.");
            item = new(sessionId, epoch, ++watermark, kind, DateTimeOffset.UtcNow, debugger, operationId);
            events.Enqueue(item);
            while (events.Count > capacity) events.Dequeue();
            wake = changed;
            changed = Signal();
        }
        wake.TrySetResult(true);
        return item;
    }

    public VsEventBatch Read(string? expectedEpoch, long afterSequence, int maxItems = 100)
    {
        if (afterSequence < 0) throw new ArgumentOutOfRangeException(nameof(afterSequence));
        if (maxItems < 1 || maxItems > 1000) throw new ArgumentOutOfRangeException(nameof(maxItems));
        lock (gate)
        {
            var gap = invalidated || requiresSnapshot || (expectedEpoch != null && expectedEpoch != epoch) || afterSequence > watermark ||
                afterSequence < (events.Count > 0 ? events.Peek().Sequence - 1 : watermark);
            return new(sessionId, epoch, watermark, gap,
                gap ? Array.Empty<VsSessionEvent>() : events.Where(e => e.Sequence > afterSequence).Take(maxItems).ToArray());
        }
    }

    /// <summary>Transport snapshot. Replays retained items; receivers detect missing prefixes.</summary>
    public VsEventBatch Snapshot()
    {
        lock (gate) return new(sessionId, epoch, watermark, invalidated || requiresSnapshot, events.ToArray());
    }

    /// <summary>Wake existing readers when their connection disappears; never reuse this stream.</summary>
    public void Invalidate()
    {
        TaskCompletionSource<bool> wake;
        lock (gate)
        {
            invalidated = true;
            events.Clear();
            wake = changed;
            changed = Signal();
        }
        wake.TrySetResult(true);
    }

    /// <summary>Accepts only one publisher epoch. A new connection must create a new buffer.</summary>
    public void Import(VsEventBatch batch)
    {
        TaskCompletionSource<bool> wake;
        lock (gate)
        {
            if (invalidated) throw new ArgumentException("The event connection was replaced.");
            if (batch.SessionId != sessionId || batch.Epoch != epoch)
                throw new ArgumentException("Publisher identity does not match the registered stream.");
            if (batch.Watermark < watermark) return; // Late duplicate batch, never rewind.
            var incoming = batch.Events;
            long previous = 0;
            foreach (var item in incoming)
            {
                if (item.SessionId != sessionId || item.Epoch != epoch || item.Sequence <= previous || item.Sequence > batch.Watermark)
                    throw new ArgumentException("Invalid or unordered event batch.");
                previous = item.Sequence;
            }
            if (batch.RequiresReconciliation)
            {
                requiresSnapshot = true;
                // Never present an explicitly incomplete publisher batch as replay.
                events.Clear();
                watermark = batch.Watermark;
                incoming = Array.Empty<VsSessionEvent>();
            }
            foreach (var item in incoming)
            {
                if (item.Sequence <= watermark) continue;
                if (item.Sequence != watermark + 1) events.Clear(); // Preserve a visible gap.
                events.Enqueue(item);
                watermark = item.Sequence;
                while (events.Count > capacity) events.Dequeue();
            }
            if (batch.Watermark > watermark) { events.Clear(); watermark = batch.Watermark; }
            wake = changed;
            changed = Signal();
        }
        wake.TrySetResult(true);
    }

    /// <summary>Clear a transport discontinuity only after an authoritative state snapshot.</summary>
    public void Reconcile(VsEventBatch snapshot)
    {
        if (snapshot.RequiresReconciliation) throw new ArgumentException("An authoritative snapshot cannot be incomplete.");
        lock (gate)
        {
            Import(snapshot);
            requiresSnapshot = false;
        }
    }

    public async Task<VsEventBatch> WaitAsync(string? expectedEpoch, long afterSequence,
        TimeSpan timeout, CancellationToken cancellationToken, int maxItems = 100)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            Task signal;
            lock (gate)
            {
                var batch = Read(expectedEpoch, afterSequence, maxItems);
                if (batch.RequiresReconciliation || batch.Events.Count > 0) return batch;
                signal = changed.Task;
            }
            var canceled = Signal();
            using (deadline.Token.Register(() => canceled.TrySetCanceled()))
            {
                try { await await Task.WhenAny(signal, canceled.Task).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { return Read(expectedEpoch, afterSequence, maxItems); }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
