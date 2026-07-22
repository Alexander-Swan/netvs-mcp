using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Services;

public sealed class VsixRegistrationPipeListener : IAsyncDisposable
{
    private readonly BrokerOptions _options;
    private readonly BrokerRegistrationRpcService _registrationService;
    private readonly List<Task> _clientTasks = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;

    public VsixRegistrationPipeListener(
        BrokerOptions options,
        BrokerRegistrationRpcService registrationService)
    {
        _options = options;
        _registrationService = registrationService;
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
        try
        {
            await using (pipe)
            using (var jsonRpc = JsonRpc.Attach(pipe, _registrationService))
            {
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
