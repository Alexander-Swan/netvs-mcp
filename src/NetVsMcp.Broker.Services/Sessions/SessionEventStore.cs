using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class SessionEventStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, SessionEventBuffer> streams = new(StringComparer.OrdinalIgnoreCase);

    public SessionEventBuffer Accept(VsEventBatch batch)
    {
        lock (gate)
        {
            if (!streams.TryGetValue(batch.SessionId, out var stream))
            {
                stream = new(batch.SessionId, epoch: batch.Epoch);
                streams.Add(batch.SessionId, stream);
            }
            stream.Import(batch);
            return stream;
        }
    }

    public SessionEventBuffer? Find(string sessionId)
    { lock (gate) return streams.TryGetValue(sessionId, out var stream) ? stream : null; }

    internal void Reconcile(VsEventBatch snapshot)
    {
        lock (gate) Accept(snapshot).Reconcile(snapshot);
    }

    public void Remove(string sessionId)
    {
        lock (gate)
            if (streams.Remove(sessionId, out var stream)) stream.Invalidate();
    }
}
