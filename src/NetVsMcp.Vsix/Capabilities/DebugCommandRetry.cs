using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal static class DebugCommandRetry
{
    // EnvDTE reports this rejection while the debugger is still settling a mode
    // transition, even after CurrentMode and OnEnterBreakMode report a pause.
    internal const int MethodUnavailable = unchecked((int)0x89711007);
    internal const int MaxAttempts = 20;

    public static async Task ExecuteAsync(Func<CancellationToken, Task> issue, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? delay = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await issue(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (COMException ex) when (ex.ErrorCode == MethodUnavailable && attempt < MaxAttempts)
            {
                // The caller must revalidate its command and mode on the UI thread
                // before every attempt. Never replay a successfully issued command.
                await (delay is null ? Task.Delay(100, cancellationToken) : delay(cancellationToken)).ConfigureAwait(false);
            }
        }
    }
}
