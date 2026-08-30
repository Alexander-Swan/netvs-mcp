using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Services;

internal sealed class VsixRegistrationPipeListener : IAsyncDisposable
{
    private readonly BrokerOptions _options;
    private readonly SessionRegistry _sessions;
    private readonly IVsSessionConnectionMap _connections;
    private readonly List<Task> _clientTasks = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;

    public VsixRegistrationPipeListener(
        BrokerOptions options,
        SessionRegistry sessions,
        IVsSessionConnectionMap connections)
    {
        _options = options;
        _sessions = sessions;
        _connections = connections;
    }

    public bool IsRunning => _listenerTask is { IsCompleted: false };

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listenerTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        _listenerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenerTask = Task.Run(() => ListenAsync(_listenerCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_listenerCancellation is null)
        {
            return;
        }

        await _listenerCancellation.CancelAsync();

        if (_listenerTask is not null)
        {
            await AwaitWithoutCancellation(_listenerTask);
        }

        Task[] clientTasks;
        lock (_gate)
        {
            clientTasks = [.. _clientTasks];
            _clientTasks.Clear();
        }

        await Task.WhenAll(clientTasks.Select(AwaitWithoutCancellation));
        _listenerCancellation.Dispose();
        _listenerCancellation = null;
        _listenerTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var pipeName = ToServerPipeName(_options.PipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var clientTask = ServeClientAsync(pipe, cancellationToken);
            TrackClientTask(clientTask);
        }
    }

    private async Task ServeClientAsync(Stream pipe, CancellationToken cancellationToken)
    {
        BrokerRegistrationRpcService? registrationService = null;

        try
        {
            await using (pipe)
            using (var jsonRpc = new JsonRpc(pipe))
            {
                var sessionConnection = jsonRpc.Attach<IVisualStudioSessionRpc>();
                registrationService = new BrokerRegistrationRpcService(
                    _sessions,
                    _connections,
                    sessionConnection);

                jsonRpc.AddLocalRpcTarget(registrationService);
                jsonRpc.StartListening();
                await jsonRpc.Completion.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"VSIX registration pipe client failed: {ex}");
        }
        finally
        {
            registrationService?.RemoveRegisteredConnections();
        }
    }

    private void TrackClientTask(Task clientTask)
    {
        lock (_gate)
        {
            _clientTasks.Add(clientTask);
        }

        _ = clientTask.ContinueWith(
            completed =>
            {
                lock (_gate)
                {
                    _clientTasks.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task AwaitWithoutCancellation(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string ToServerPipeName(string pipeName)
    {
        const string prefix = @"\\.\pipe\";

        return pipeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pipeName[prefix.Length..]
            : pipeName;
    }
}
