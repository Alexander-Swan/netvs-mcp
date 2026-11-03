using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal static class DebugCommandExecution
{
    public static async Task<long> IssueAsync(DebugExecutionTracker execution,
        Func<CancellationToken, Action, Task> runInContext, Func<string> readMode, Action issue,
        bool newSession, CancellationToken cancellationToken, Action? validateContext = null,
        Func<CancellationToken, Task>? delay = null)
    {
        var initialMode = string.Empty;
        long before = 0;
        long? sequence = null;
        await runInContext(cancellationToken, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            initialMode = readMode();
            validateContext?.Invoke();
            before = execution.Begin(newSession);
            sequence = execution.ObservedState().ExecutionSequence;
        });
        try
        {
            await DebugCommandRetry.ExecuteAsync(ct => runInContext(ct, () =>
            {
                if (!execution.CanRetryCommand(sequence) || readMode() != initialMode)
                    throw new InvalidOperationException("Debugger state changed while waiting for command readiness; the command was not retried. Read debug_status before issuing another command.");
                validateContext?.Invoke();
                issue();
            }), cancellationToken, delay);
        }
        catch
        {
            await runInContext(CancellationToken.None, () => execution.RejectCommand(readMode(), sequence));
            throw;
        }
        return before;
    }
}
