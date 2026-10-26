using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Debugger.Symbols;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    public async Task<DebugSourceEvidenceResult> GetSourceEvidenceAsync(DebugSourceEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedStopId) || request.ExpectedStopId.Length > 200 || request.ExpectedGeneration < 0 ||
            request.ExpectedProcessId < 1 || request.ThreadId is <= 0) throw new ArgumentException("Source evidence requires the current pause/generation and a positive process/thread ID.");
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        if (!debugger.DebuggedProcesses.Cast<EnvDTE.Process>().Any(p => { ThreadHelper.ThrowIfNotOnUIThread(); return p.ProcessID == request.ExpectedProcessId; }))
            throw new ArgumentException("The requested process is not debugged in this session.");
        var threadId = request.ThreadId ?? (debugger.CurrentProcess?.ProcessID == request.ExpectedProcessId ? debugger.CurrentThread?.ID : null);
        DkmSourcePosition? source = null;
        string[] expected = Array.Empty<string>();
        string? message = null;
        try
        {
            source = ReadThreadSource(request.ExpectedProcessId, threadId);
            expected = source?.SourceFileId?.AdditionalChecksums?
                .Where(hash => hash.Algorithm == DkmHashAlgorithmId.SHA256 && hash.Value.Count == 32)
                .Select(hash => SourceChecksumVerifier.Hex(hash.Value.ToArray())).Distinct().Take(9).ToArray() ?? Array.Empty<string>();
            if (expected.Length > 8)
            {
                expected = Array.Empty<string>();
                message = "The engine supplied more than eight distinct SHA-256 source checksums. Comparison is unavailable rather than claiming a mismatch from a truncated set.";
            }
        }
        catch (Exception ex) when (ex is COMException or NotSupportedException or NotImplementedException) { message = ex.Message; }
        var file = source?.DocumentName;
        var line = source is null ? (int?)null : checked((int)source.TextSpan.StartLine);
        var column = source is null ? (int?)null : checked((int)source.TextSpan.StartColumn);
        var disk = new SourceChecksumComparison("unavailable", Message: "The active instruction has no supported SHA-256 source checksum.");
        var editor = disk;
        if (expected.Length > 0 && !string.IsNullOrWhiteSpace(file))
        {
            var componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            var workspace = componentModel?.GetService<VisualStudioWorkspace>();
            var snapshot = workspace?.CurrentSolution;
            var documents = snapshot?.GetDocumentIdsWithFilePath(file).Select(snapshot.GetDocument).Where(d => d is not null).ToArray();
            var document = documents?.Length == 1 ? documents[0] : null;
            await TaskScheduler.Default;
            disk = SourceChecksumVerifier.CompareDisk(file!, expected, cancellationToken);
            if (document is not null)
            {
                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                editor = SourceChecksumVerifier.CompareEditor(text, expected);
            }
            else editor = new("unavailable", Message: documents?.Length > 1 ? "Source belongs to multiple Roslyn documents; no editor snapshot was selected." : "Source is not in the current Roslyn workspace.");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (document is not null && !ReferenceEquals(snapshot, workspace!.CurrentSolution))
                editor = editor with { Status = "stale", Message = "The workspace changed during inspection. The checksum describes the returned text version; read again before acting." };
        }
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        return new(ReadRunToState(debugger), request.ExpectedProcessId, threadId, file, line, column, "SHA256", expected, disk, editor,
            message ?? (expected.Length == 0 ? "Only engine-supplied SHA-256 document checksums are compared. Missing/legacy checksums are unavailable, never assumed to match." : null));
    }
}
