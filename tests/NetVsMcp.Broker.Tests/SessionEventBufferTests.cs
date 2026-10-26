using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class SessionEventBufferTests
{
    [Fact]
    public async Task InvalidatedStreamWakesWaitersAndCannotYieldOldReplay()
    {
        var stream = new SessionEventBuffer("vs-1", epoch: "old");
        var wait = stream.WaitAsync("old", 0, TimeSpan.FromSeconds(30), default);
        stream.Invalidate();
        var batch = await wait.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(batch.RequiresReconciliation);
        Assert.Empty(batch.Events);
        Assert.True(stream.Read("old", 0).RequiresReconciliation);
        Assert.Throws<InvalidOperationException>(() => stream.Publish("late"));
    }

    [Fact]
    public void PublisherGapFlagCannotBeImportedAsCompleteReplay()
    {
        var sender = new SessionEventBuffer("vs-1", epoch: "epoch");
        sender.Publish("untrusted", new("dbgBreakMode", StopId: "old"));
        var receiver = new SessionEventBuffer("vs-1", epoch: "epoch");
        receiver.Import(sender.Snapshot() with { RequiresReconciliation = true });
        Assert.True(receiver.Read("epoch", 0).RequiresReconciliation);
        Assert.Empty(receiver.Snapshot().Events);
        sender.Publish("snapshot", new("dbgRunMode"));
        receiver.Import(sender.Snapshot());
        Assert.True(receiver.Read("epoch", 1).RequiresReconciliation);
        receiver.Reconcile(sender.Snapshot());
        Assert.Single(receiver.Read("epoch", 1).Events);
    }
    [Fact]
    public void ImportRejectsOtherEpochsAndUnorderedEventsWithoutChangingState()
    {
        var sender = new SessionEventBuffer("vs-1", epoch: "epoch");
        sender.Publish("run");
        sender.Publish("stop");
        var receiver = new SessionEventBuffer("vs-1", epoch: "epoch");
        receiver.Import(sender.Snapshot());
        receiver.Import(sender.Snapshot());
        Assert.Equal(2, receiver.Read(null, 0).Events.Count);
        Assert.Throws<ArgumentException>(() => receiver.Import(sender.Snapshot() with { Epoch = "old" }));
        Assert.Throws<ArgumentException>(() => receiver.Import(sender.Snapshot() with { Events = sender.Snapshot().Events.Reverse().ToArray() }));
        Assert.Equal(2, receiver.Snapshot().Watermark);
    }

    [Fact]
    public void ImportedMissingPrefixCannotLookLikeCompleteHistory()
    {
        var sender = new SessionEventBuffer("vs-1", capacity: 2, epoch: "epoch");
        for (var i = 0; i < 5; i++) sender.Publish("progress");
        var receiver = new SessionEventBuffer("vs-1", epoch: "epoch");
        receiver.Import(sender.Snapshot());
        Assert.True(receiver.Read("epoch", 0).RequiresReconciliation);
        Assert.Equal(2, receiver.Read("epoch", 3).Events.Count);
    }
    [Fact]
    public void CursorExcludesPreCommandEventsAndReplaysFastCompletion()
    {
        var stream = new SessionEventBuffer("vs-1");
        stream.Publish("debugger", new("dbgBreakMode", StopId: "old"));
        var cursor = stream.Read(null, 0);
        stream.Publish("debugger", new("dbgBreakMode", StopId: "new"));
        var batch = stream.Read(cursor.Epoch, cursor.Watermark);
        Assert.Equal("new", Assert.Single(batch.Events).Debugger!.StopId);
    }

    [Fact]
    public void OverflowEpochReplacementAndFutureCursorsRequireReconciliation()
    {
        var stream = new SessionEventBuffer("vs-1", capacity: 2);
        for (var i = 0; i < 3; i++) stream.Publish("debugger");
        var batch = stream.Read(null, 0);
        Assert.True(batch.RequiresReconciliation);
        Assert.Empty(batch.Events);
        Assert.True(stream.Read("previous-publisher", 3).RequiresReconciliation);
        Assert.True(stream.Read(null, 4).RequiresReconciliation);
        Assert.False(stream.Read(batch.Epoch, 1).RequiresReconciliation);
    }

    [Fact]
    public async Task IndependentWaitersObserveTheSameEvent()
    {
        var stream = new SessionEventBuffer("vs-1");
        var first = stream.WaitAsync(null, 0, TimeSpan.FromSeconds(5), CancellationToken.None);
        var second = stream.WaitAsync(null, 0, TimeSpan.FromSeconds(5), CancellationToken.None);
        stream.Publish("buildCompleted", operationId: "build-1");
        Assert.Equal("build-1", Assert.Single((await first).Events).OperationId);
        Assert.Equal("build-1", Assert.Single((await second).Events).OperationId);
    }

    [Fact]
    public async Task CancelingAWaitDoesNotConsumeOrCancelPublication()
    {
        var stream = new SessionEventBuffer("vs-1");
        using var cancellation = new CancellationTokenSource();
        var wait = stream.WaitAsync(null, 0, TimeSpan.FromSeconds(5), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        stream.Publish("completed");
        Assert.Single(stream.Read(null, 0).Events);
    }

    [Fact]
    public async Task TimeoutReturnsAnEmptyObservationWithoutInventingCompletion()
    {
        var stream = new SessionEventBuffer("vs-1");
        var batch = await stream.WaitAsync(null, 0, TimeSpan.FromMilliseconds(10), CancellationToken.None);
        Assert.Empty(batch.Events);
        Assert.Equal(0, batch.Watermark);
        Assert.False(batch.RequiresReconciliation);
    }

    [Fact]
    public void PagingDoesNotAdvancePastUnreadEvents()
    {
        var stream = new SessionEventBuffer("vs-1");
        for (var i = 0; i < 5; i++) stream.Publish("progress");
        var first = stream.Read(null, 0, maxItems: 2);
        var second = stream.Read(first.Epoch, first.Events.Last().Sequence, maxItems: 2);
        Assert.Equal(new long[] { 3, 4 }, second.Events.Select(e => e.Sequence));
        Assert.Equal(5, second.Watermark);
    }
}
