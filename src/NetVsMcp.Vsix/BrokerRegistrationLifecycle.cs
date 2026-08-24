using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed class BrokerRegistrationLifecycle : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly IVisualStudioSessionSnapshotProvider snapshotProvider;
    private readonly IVisualStudioCapabilityCatalog capabilities;
    private readonly IVisualStudioStateChangeMonitor stateMonitor;
    private readonly IBrokerConnectionFactory connectionFactory;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim stateChanged = new(0, 1);

    private IBrokerConnection? activeConnection;
    private bool disposed;

    public BrokerRegistrationLifecycle(
        IVisualStudioSessionSnapshotProvider snapshotProvider,
        IVisualStudioCapabilityCatalog capabilities,
        IVisualStudioStateChangeMonitor stateMonitor,
        IBrokerConnectionFactory connectionFactory)
    {
        this.snapshotProvider = snapshotProvider;
        this.capabilities = capabilities;
        this.stateMonitor = stateMonitor;
        this.connectionFactory = connectionFactory;
        this.stateMonitor.StateChanged += OnVisualStudioStateChanged;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Task.Run(() => RunConnectionLoopAsync(stop.Token), stop.Token);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        stateMonitor.StateChanged -= OnVisualStudioStateChanged;
        stop.Cancel();

        // Await (with a short timeout) rather than fire-and-forget, so unregistration has a
        // real chance to complete before the process tears down VS - and so this doesn't race
        // the background connection loop's own `finally` (RunConnectionLoopAsync), which can
        // observe the same cancellation and call UnregisterAndDisconnectAsync concurrently.
        // UnregisterAndDisconnectAsync itself guards the shared `activeConnection` field with
        // Interlocked.Exchange so only one caller ever owns and disposes a given connection.
        // Dispose() must stay synchronous (IDisposable), so this uses JoinableTaskFactory.Run
        // (not Task.Wait) to block without risking a UI-thread deadlock.
        ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await UnregisterAndDisconnectAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("NetVsMcp broker disconnect-on-dispose skipped: {0}", ex.Message);
            }
        });

        stateChanged.Dispose();
        stop.Dispose();
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        var reconnectDelay = InitialReconnectDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                activeConnection = await connectionFactory.ConnectAsync(cancellationToken);
                await RegisterAsync(activeConnection, cancellationToken);
                reconnectDelay = InitialReconnectDelay;
                await RunConnectedHeartbeatLoopAsync(activeConnection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("NetVsMcp broker connection failed: {0}", ex);
            }
            finally
            {
                await UnregisterAndDisconnectAsync(CancellationToken.None);
            }

            await DelayBeforeReconnectAsync(reconnectDelay, cancellationToken);
            reconnectDelay = TimeSpan.FromMilliseconds(
                Math.Min(reconnectDelay.TotalMilliseconds * 2, MaxReconnectDelay.TotalMilliseconds));
        }
    }

    private async Task RegisterAsync(IBrokerConnection connection, CancellationToken cancellationToken)
    {
        var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);
        var registration = VsRegistrationRequest.FromSnapshot(snapshot, capabilities);
        await connection.RegisterAsync(registration, cancellationToken);
    }

    private async Task RunConnectedHeartbeatLoopAsync(IBrokerConnection connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && connection.IsConnected)
        {
            await SendHeartbeatAsync(connection, cancellationToken);
            await WaitForNextHeartbeatOrStateChangeAsync(cancellationToken);
        }
    }

    private async Task SendHeartbeatAsync(IBrokerConnection connection, CancellationToken cancellationToken)
    {
        var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);
        await connection.HeartbeatAsync(VsHeartbeatRequest.FromSnapshot(snapshot, capabilities), cancellationToken);
    }

    private async Task UnregisterAndDisconnectAsync(CancellationToken cancellationToken)
    {
        // Interlocked.Exchange makes "take ownership of the current connection and null the
        // field" atomic, so Dispose() (main thread) and RunConnectionLoopAsync's `finally`
        // (background loop) can never both observe the same non-null connection and both try to
        // unregister/dispose it - only one of them wins the exchange and does the work.
        var connection = Interlocked.Exchange(ref activeConnection, null);

        if (connection is null)
        {
            return;
        }

        try
        {
            await connection.UnregisterAsync(SessionIdentity.CurrentProcessSessionId(), cancellationToken);
        }
        catch (Exception ex)
        {
            Trace.TraceInformation("NetVsMcp broker unregister skipped: {0}", ex.Message);
        }
        finally
        {
            connection.Dispose();
        }
    }

    private async Task WaitForNextHeartbeatOrStateChangeAsync(CancellationToken cancellationToken)
    {
        await stateChanged.WaitAsync(HeartbeatInterval, cancellationToken);
    }

    private static async Task DelayBeforeReconnectAsync(TimeSpan reconnectDelay, CancellationToken cancellationToken)
    {
        await Task.Delay(reconnectDelay, cancellationToken);
    }

    private void OnVisualStudioStateChanged(object? sender, VisualStudioStateChangedEventArgs e)
    {
        _ = e;

        if (disposed)
        {
            return;
        }

        if (stateChanged.CurrentCount == 0)
        {
            stateChanged.Release();
        }
    }
}
