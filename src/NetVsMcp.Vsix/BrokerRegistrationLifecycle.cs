using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
        _ = UnregisterAndDisconnectAsync(CancellationToken.None);
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
        var connection = activeConnection;
        activeConnection = null;

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
