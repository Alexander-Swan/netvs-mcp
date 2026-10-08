using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerEventStoreTests
{
    [Fact]
    public void List_ReturnsMatchingEventsAfterSequence()
    {
        var store = new BrokerEventStore();
        store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildStarted));
        store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildCompleted));
        store.Publish(CreateEvent("vs-2", BrokerEventTypes.BuildCompleted));

        var result = store.List("vs-1", sinceSequence: 1, [BrokerEventTypes.BuildCompleted], maxEvents: 10);

        var evt = Assert.Single(result.Events);
        Assert.Equal(2, evt.Sequence);
        Assert.Equal(BrokerEventTypes.BuildCompleted, evt.Type);
        Assert.Equal(2, result.NextSequence);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task WaitAsync_ReturnsWhenMatchingEventArrives()
    {
        var store = new BrokerEventStore();
        var wait = store.WaitAsync(
            "vs-1",
            sinceSequence: 0,
            [BrokerEventTypes.BuildCompleted],
            maxEvents: 10,
            timeout: TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(wait.IsCompleted);
        store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildStarted));
        Assert.False(wait.IsCompleted);
        store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildCompleted));

        var result = await wait;

        var evt = Assert.Single(result.Events);
        Assert.Equal(BrokerEventTypes.BuildCompleted, evt.Type);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task WaitAsync_TimesOutWithNextSequence()
    {
        var store = new BrokerEventStore();
        store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildStarted));

        var result = await store.WaitAsync(
            "vs-1",
            sinceSequence: 0,
            [BrokerEventTypes.BuildCompleted],
            maxEvents: 10,
            timeout: TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.Empty(result.Events);
        Assert.True(result.TimedOut);
        Assert.Equal(1, result.NextSequence);
    }

    [Fact]
    public void Publish_BoundsPerSessionQueue()
    {
        var store = new BrokerEventStore();
        for (var i = 0; i < 205; i++)
        {
            store.Publish(CreateEvent("vs-1", BrokerEventTypes.BuildCompleted));
        }

        var result = store.List("vs-1", sinceSequence: 0, eventTypes: null, maxEvents: 250);

        Assert.Equal(200, result.Events.Count);
        Assert.Equal(6, result.Events.Min(evt => evt.Sequence));
        Assert.Equal(205, result.NextSequence);
    }

    private static BrokerEventNotification CreateEvent(string sessionId, string type) =>
        new(
            sessionId,
            type,
            $"{type} summary",
            new Dictionary<string, string> { ["kind"] = type });
}
