using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class BrokerRegistrationLifecycle : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private readonly IVisualStudioSessionSnapshotProvider snapshotProvider;
    private readonly IVisualStudioCapabilityCatalog capabilities;
    private readonly Timer heartbeatTimer;
    private bool disposed;

    public BrokerRegistrationLifecycle(
        IVisualStudioSessionSnapshotProvider snapshotProvider,
        IVisualStudioCapabilityCatalog capabilities)
    {
        this.snapshotProvider = snapshotProvider;
        this.capabilities = capabilities;
        heartbeatTimer = new Timer(_ => _ = SendHeartbeatAsync(CancellationToken.None));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RegisterAsync(cancellationToken);
        heartbeatTimer.Change(HeartbeatInterval, HeartbeatInterval);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        heartbeatTimer.Dispose();
    }

    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);
        var registration = VsRegistrationRequest.FromSnapshot(snapshot, capabilities);

        // TODO: Agent A owns broker/contracts. Replace this placeholder with
        // StreamJsonRpc over the per-user NetVsMcp named pipe once contracts land.
        _ = registration;
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return;
        }

        var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);

        // TODO: Send state refresh to the broker. This should include solution,
        // active document, active-window state, debugger mode, and capabilities.
        _ = snapshot;
    }
}
