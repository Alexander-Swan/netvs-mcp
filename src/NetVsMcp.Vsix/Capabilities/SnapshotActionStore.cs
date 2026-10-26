using System;
using System.Collections.Generic;
using System.Linq;

namespace NetVsMcp.Vsix;

/// <summary>Opaque, bounded, single-use selections bound to an immutable workspace snapshot.</summary>
internal sealed class SnapshotActionStore<T>
{
    private sealed class Entry(object snapshot, string target, T value, DateTimeOffset expires)
    {
        public object Snapshot { get; } = snapshot;
        public string Target { get; } = target;
        public T Value { get; } = value;
        public DateTimeOffset Expires { get; } = expires;
    }
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> now;
    private readonly int capacity;
    private readonly TimeSpan retention;

    public SnapshotActionStore(int capacity = 256, Func<DateTimeOffset>? now = null, TimeSpan? retention = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        this.now = now ?? (() => DateTimeOffset.UtcNow);
        this.retention = retention ?? TimeSpan.FromMinutes(2);
        if (this.retention <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retention));
    }

    public string Add(object snapshot, string target, T value)
    {
        lock (gate)
        {
            foreach (var key in entries.Where(p => p.Value.Expires <= now()).Select(p => p.Key).ToArray()) entries.Remove(key);
            if (entries.Count >= capacity) entries.Remove(entries.OrderBy(p => p.Value.Expires).First().Key);
            var id = Guid.NewGuid().ToString("N");
            entries.Add(id, new(snapshot, target, value, now().Add(this.retention)));
            return id;
        }
    }

    public T Take(string id, object snapshot, string target) => Get(id, snapshot, target, consume: true);

    public T Get(string id, object snapshot, string target, bool consume = false)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry)) throw new StaleActionException("Unknown, evicted, or already used actionId. List actions again.");
            if (consume) entries.Remove(id);
            if (entry.Expires <= now()) throw new StaleActionException("Expired actionId. List actions again.");
            if (entry.Target != target) throw new StaleActionException("Stale actionId: selected document or span changed. List actions again.");
            if (!Equals(entry.Snapshot, snapshot))
                throw new StaleActionException("Stale actionId: workspace source versions changed. List actions again.");
            return entry.Value;
        }
    }
}
