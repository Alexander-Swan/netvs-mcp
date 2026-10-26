using System;

namespace NetVsMcp.Vsix.Tests;

public class SnapshotActionStoreTests
{
    [Fact]
    public void PreviewDoesNotConsumeTheOriginalSelection()
    {
        var store = new SnapshotActionStore<string>();
        var snapshot = new object();
        var id = store.Add(snapshot, "target", "original");
        Assert.Equal("original", store.Get(id, snapshot, "target"));
        Assert.Equal("original", store.Get(id, snapshot, "target"));
        Assert.Equal("original", store.Take(id, snapshot, "target"));
    }

    [Fact]
    public void OriginalSelectionCanOnlyBeConsumedOnce()
    {
        var store = new SnapshotActionStore<string>();
        var snapshot = new object();
        var id = store.Add(snapshot, "file:1:2", "original action");
        Assert.Equal("original action", store.Take(id, snapshot, "file:1:2"));
        Assert.Throws<StaleActionException>(() => store.Take(id, snapshot, "file:1:2"));
    }

    [Fact]
    public void ChangeInAnotherDocumentInvalidatesSelection()
    {
        var store = new SnapshotActionStore<string>();
        var id = store.Add(new object(), "same target", "original action");
        Assert.Throws<StaleActionException>(() => store.Take(id, new object(), "same target"));
    }

    [Fact]
    public void DifferentSelectionCannotUseSameId()
    {
        var store = new SnapshotActionStore<string>();
        var snapshot = new object();
        var id = store.Add(snapshot, "file:1:2", "action");
        Assert.Throws<StaleActionException>(() => store.Take(id, snapshot, "file:2:2"));
    }

    [Fact]
    public void ExpiredSelectionsFailInsteadOfRecomputing()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new SnapshotActionStore<string>(now: () => now);
        var snapshot = new object();
        var id = store.Add(snapshot, "target", "action");
        now = now.AddMinutes(3);
        Assert.Throws<StaleActionException>(() => store.Take(id, snapshot, "target"));
    }

    [Fact]
    public void EvictionRemovesOldestSelection()
    {
        var store = new SnapshotActionStore<string>(capacity: 1);
        var snapshot = new object();
        var old = store.Add(snapshot, "target", "old");
        var current = store.Add(snapshot, "target", "current");
        Assert.Throws<StaleActionException>(() => store.Take(old, snapshot, "target"));
        Assert.Equal("current", store.Take(current, snapshot, "target"));
    }
}
