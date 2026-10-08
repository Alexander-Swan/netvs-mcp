using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface IBrokerEventStore
{
    BrokerEvent Publish(BrokerEventNotification notification);

    BrokerEventsResult List(
        string sessionId,
        long sinceSequence,
        IReadOnlyCollection<string>? eventTypes,
        int maxEvents);

    Task<BrokerEventsResult> WaitAsync(
        string sessionId,
        long sinceSequence,
        IReadOnlyCollection<string>? eventTypes,
        int maxEvents,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
