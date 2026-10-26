using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal static class SessionEventReconciliation
{
    internal static async Task<VsSessionEventSnapshot> CaptureAsync(SessionRegistry sessions,
        IVsSessionConnectionMap connections, string sessionId, IVisualStudioSessionRpc connection, CancellationToken ct)
    {
        lock (sessions.RegistrationGate) ValidateConnection();
        var snapshot = await connection.SessionSnapshotAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        lock (sessions.RegistrationGate)
        {
            ValidateConnection();
            if (snapshot.SessionId != sessionId || snapshot.Events.SessionId != sessionId || snapshot.Events.RequiresReconciliation)
                throw new InvalidOperationException("The authoritative snapshot does not match this session. Request a fresh snapshot; no command was replayed.");
            sessions.Events.Reconcile(snapshot.Events);
        }
        return snapshot with
        {
            Debugger = snapshot.Debugger with
            {
                EventCursor = new(sessionId, snapshot.Events.Epoch, snapshot.Events.Watermark)
            }
        };

        void ValidateConnection()
        {
            if (!connections.TryGet(sessionId, out var current) ||
                !ReferenceEquals(current, CoordinatedSessionRpc.Unwrap(connection)))
                throw new InvalidOperationException("Session connection changed while reconciling. Request a fresh snapshot; no command was replayed.");
        }
    }
}
