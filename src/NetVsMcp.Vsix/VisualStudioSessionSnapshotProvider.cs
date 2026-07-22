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
        var solutionPath = dte?.Solution?.FullName;
        var activeDocument = dte?.ActiveDocument?.FullName;
        var debuggerMode = dte?.Debugger?.CurrentMode.ToString() ?? "Unknown";

        return new VsSessionSnapshot(
            SessionIdentity.CurrentProcessSessionId(),
            System.Diagnostics.Process.GetCurrentProcess().Id,
            dte?.Version,
            dte?.Edition,
            GetSolutionName(solutionPath),
            solutionPath,
            activeDocument,
            debuggerMode,
            false,
            DateTimeOffset.UtcNow);
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
