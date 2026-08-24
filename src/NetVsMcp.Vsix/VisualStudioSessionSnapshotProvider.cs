using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IVisualStudioSessionSnapshotProvider
{
    Task<VsSessionSnapshot> CaptureAsync(CancellationToken cancellationToken);
}

internal sealed class VisualStudioSessionSnapshotProvider : IVisualStudioSessionSnapshotProvider
{
    private readonly AsyncPackage package;

    public VisualStudioSessionSnapshotProvider(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<VsSessionSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;

        // Each of these can throw a transient COMException during a solution-load/close race
        // (e.g. dte.Solution briefly unavailable while a new one is being opened). Read them
        // independently so one field's transient failure doesn't null out the others, and so a
        // blip here degrades this single snapshot rather than throwing out of CaptureAsync -
        // which, via SendHeartbeatAsync, would otherwise be caught only by
        // BrokerRegistrationLifecycle's outer per-connection catch and tear down the whole
        // broker connection (full unregister + backoff) for what should be a one-heartbeat blip.
        var solutionPath = TryRead(() => dte?.Solution?.FullName);
        var activeDocument = TryRead(() => dte?.ActiveDocument?.FullName);
        var debuggerMode = TryRead(() => dte?.Debugger?.CurrentMode.ToString()) ?? "Unknown";

        return new VsSessionSnapshot(
            SessionIdentity.CurrentProcessSessionId(),
            System.Diagnostics.Process.GetCurrentProcess().Id,
            dte?.Version,
            dte?.Edition,
            GetSolutionName(solutionPath),
            solutionPath,
            activeDocument,
            debuggerMode,
            ActiveWindowTracker.IsCurrentProcessForegroundWindow(),
            DateTimeOffset.UtcNow);
    }

    private static string? TryRead(Func<string?> read)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return read();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Trace.TraceInformation("NetVsMcp: transient COM error reading VS session state, skipping this field for one snapshot: {0}", ex.Message);
            return null;
        }
    }

    private static string? GetSolutionName(string? solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(solutionPath);
    }
}
