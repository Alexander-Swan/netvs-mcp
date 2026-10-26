using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal enum HotReloadBaselineAttempt
{
    NotReady,
    Accepted,
    Unavailable
}

internal static class HotReloadBaseline
{
    internal const int MaxAttempts = 50;

    internal static async Task<bool> WaitForInitializationAsync(Task<bool> started, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            if (await Task.WhenAny(started, Task.Delay(timeout, deadline.Token)).ConfigureAwait(false) == started)
                return await started.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return false; }
        finally { deadline.Cancel(); }
    }

    internal static HotReloadBaselineAttempt Attempt(Func<bool> isCurrentSession, Func<bool> isAvailable, Action issue)
    {
        if (!isCurrentSession()) return HotReloadBaselineAttempt.Unavailable;
        try
        {
            if (!isAvailable()) return HotReloadBaselineAttempt.NotReady;
        }
        catch (ArgumentException) { return HotReloadBaselineAttempt.Unavailable; }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Availability queries cannot have applied changes.
            return HotReloadBaselineAttempt.NotReady;
        }
        try { issue(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return IsRejectedBeforeExecution(ex) ? HotReloadBaselineAttempt.NotReady : HotReloadBaselineAttempt.Unavailable;
        }
        return HotReloadBaselineAttempt.Accepted;
    }

    // The callback checks session identity before every query/issue. Accepted requests
    // are never replayed: ExecuteCommand does not expose asynchronous apply completion.
    internal static async Task InitializeAsync(Func<CancellationToken, Task<HotReloadBaselineAttempt>> attempt,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? delay = null)
    {
        delay ??= ct => Task.Delay(100, ct);
        for (var index = 0; index < MaxAttempts; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await attempt(cancellationToken) != HotReloadBaselineAttempt.NotReady) return;
            if (index + 1 < MaxAttempts) await delay(cancellationToken);
        }
    }

    internal static bool IsRejectedBeforeExecution(Exception exception) =>
        exception is System.Runtime.InteropServices.COMException com && com.ErrorCode == unchecked((int)0x89711007) ||
        exception is InvalidOperationException or System.Runtime.InteropServices.COMException &&
        exception.Message.IndexOf("Debug.ApplyCodeChanges", StringComparison.Ordinal) >= 0 &&
        exception.Message.IndexOf("not available", StringComparison.OrdinalIgnoreCase) >= 0;
}
