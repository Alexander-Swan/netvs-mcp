using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class BrokerEventStore : IBrokerEventStore
{
    private const int MaxEventsPerSession = 200;

    private readonly object gate = new();
    private readonly Dictionary<string, SessionQueue> queues = new(StringComparer.OrdinalIgnoreCase);
    private TaskCompletionSource signal = NewSignal();

    public BrokerEvent Publish(BrokerEventNotification notification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Type);

        TaskCompletionSource toRelease;
        BrokerEvent brokerEvent;
        lock (gate)
        {
            var queue = GetOrCreateQueue(notification.SessionId);
            brokerEvent = new BrokerEvent(
                ++queue.LastSequence,
                notification.SessionId,
                notification.Type,
                notification.TimestampUtc ?? DateTimeOffset.UtcNow,
                notification.Summary,
                notification.Data);

            queue.Events.Enqueue(brokerEvent);
            while (queue.Events.Count > MaxEventsPerSession)
            {
                queue.Events.Dequeue();
            }

            toRelease = signal;
            signal = NewSignal();
        }

        toRelease.TrySetResult();
        return brokerEvent;
    }

    public BrokerEventsResult List(
        string sessionId,
        long sinceSequence,
        IReadOnlyCollection<string>? eventTypes,
        int maxEvents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfNegative(sinceSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEvents, 1);

        lock (gate)
        {
            return CreateResultLocked(sessionId, sinceSequence, eventTypes, maxEvents, timedOut: false);
        }
    }

    public async Task<BrokerEventsResult> WaitAsync(
        string sessionId,
        long sinceSequence,
        IReadOnlyCollection<string>? eventTypes,
        int maxEvents,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfNegative(sinceSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEvents, 1);
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be zero or greater.");
        }

        var timeoutAt = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            Task signalTask;
            lock (gate)
            {
                var result = CreateResultLocked(sessionId, sinceSequence, eventTypes, maxEvents, timedOut: false);
                if (result.Events.Count > 0)
                {
                    return result;
                }

                if (timeout == TimeSpan.Zero)
                {
                    return CreateResultLocked(sessionId, sinceSequence, eventTypes, maxEvents, timedOut: true);
                }

                var remaining = timeoutAt - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return CreateResultLocked(sessionId, sinceSequence, eventTypes, maxEvents, timedOut: true);
                }

                signalTask = signal.Task;
            }

            var waitTime = timeoutAt - DateTimeOffset.UtcNow;
            var delay = Task.Delay(waitTime > TimeSpan.Zero ? waitTime : TimeSpan.Zero, cancellationToken);
            var completed = await Task.WhenAny(signalTask, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (gate)
                {
                    return CreateResultLocked(sessionId, sinceSequence, eventTypes, maxEvents, timedOut: true);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private BrokerEventsResult CreateResultLocked(
        string sessionId,
        long sinceSequence,
        IReadOnlyCollection<string>? eventTypes,
        int maxEvents,
        bool timedOut)
    {
        var queue = GetOrCreateQueue(sessionId);
        var filters = NormalizeEventTypes(eventTypes);
        var events = queue.Events
            .Where(e => e.Sequence > sinceSequence)
            .Where(e => filters is null || filters.Contains(e.Type))
            .Take(maxEvents)
            .ToArray();
        var nextSequence = events.Length > 0
            ? events.Max(e => e.Sequence)
            : Math.Max(sinceSequence, queue.LastSequence);

        return new BrokerEventsResult(events, timedOut, nextSequence);
    }

    private SessionQueue GetOrCreateQueue(string sessionId)
    {
        if (!queues.TryGetValue(sessionId, out var queue))
        {
            queue = new SessionQueue();
            queues.Add(sessionId, queue);
        }

        return queue;
    }

    private static HashSet<string>? NormalizeEventTypes(IReadOnlyCollection<string>? eventTypes)
    {
        if (eventTypes is null || eventTypes.Count == 0)
        {
            return null;
        }

        return eventTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class SessionQueue
    {
        public long LastSequence { get; set; }

        public Queue<BrokerEvent> Events { get; } = new();
    }
}
