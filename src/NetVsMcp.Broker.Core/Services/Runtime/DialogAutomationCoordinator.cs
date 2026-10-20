using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

// UIA providers can block. Bound callers' waits and allow only one active worker, including
// automatic probes, so an unresponsive provider cannot accumulate active automation calls.
internal sealed class DialogAutomationCoordinator(IDialogAutomationService backend)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<IReadOnlyList<DialogInfo>> ListAsync(int processId, CancellationToken cancellationToken) =>
        RunAsync(ct => backend.List(processId, ct), cancellationToken);

    public Task<bool> RespondAsync(int processId, string dialogId, string buttonId, CancellationToken cancellationToken) =>
        RunAsync(ct => backend.Respond(processId, dialogId, buttonId, ct), cancellationToken);

    private async Task<T> RunAsync<T>(Func<CancellationToken, T> operation, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;
        var work = Task.Run(async () =>
        {
            await gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                return operation(token);
            }
            finally { gate.Release(); }
        }, token);
        // Observe late failures even if the UIA provider outlives the caller's timeout.
        _ = work.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        try { return await work.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        finally { linked.Cancel(); }
    }
}
