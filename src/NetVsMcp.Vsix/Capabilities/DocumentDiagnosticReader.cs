using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using DocumentDiagnostic = NetVsMcp.Contracts.DocumentDiagnostic;

namespace NetVsMcp.Vsix;

internal static class DocumentDiagnosticReader
{
    public static async Task<DocumentDiagnosticsResult> ReadAsync(DocumentDiagnosticsRequest request, CancellationToken cancellationToken)
    {
        if (request.Offset < 0 || request.MaxItems is < 1 or > 200) throw new ArgumentException("offset must be nonnegative and maxItems must be 1–200.");
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
        var path = DocumentPathResolver.Resolve(dte, request.Path, allowActiveDocument: false);
        var componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
        var workspace = componentModel?.GetService<VisualStudioWorkspace>()
            ?? throw new NotSupportedException("Roslyn workspace is unavailable; use Error List diagnostics with mixed provenance.");
        var snapshot = workspace.CurrentSolution;
        var linkedDocuments = snapshot.GetDocumentIdsWithFilePath(path);
        var documents = linkedDocuments.Select(snapshot.GetDocument).Where(d => d is not null)
            .Where(d => string.IsNullOrWhiteSpace(request.ProjectName) ||
                string.Equals(d!.Project.Name, request.ProjectName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Project.FilePath, request.ProjectName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (documents.Length == 0) throw new FileNotFoundException("Document was not found in the selected Roslyn project/workspace.", path);
        if (documents.Length != 1) throw new ArgumentException("Document belongs to multiple projects; supply an unambiguous projectName or project file path.");
        var document = documents[0]!;
        var result = await ReadDocumentAsync(document, request, () => ReferenceEquals(snapshot, workspace.CurrentSolution), cancellationToken).ConfigureAwait(false);
        return result with { LinkedFile = linkedDocuments.Length > 1 };
    }

    internal static async Task<DocumentDiagnosticsResult> ReadDocumentAsync(Microsoft.CodeAnalysis.Document document,
        DocumentDiagnosticsRequest request, Func<bool> isCurrent, CancellationToken cancellationToken)
    {
        if (request.Offset < 0 || request.MaxItems is < 1 or > 200) throw new ArgumentException("Invalid diagnostic page limits.");
        var path = document.FilePath ?? request.Path;
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var version = TextContentVersion.Create(text.ToString());
        var project = document.Project;
        if (request.ExpectedTextVersion is not null && request.ExpectedTextVersion != version)
            return new(path, version, project.Id.Id.ToString("N"), project.Name, project.Language, false, false,
                request.Offset, 0, null, Array.Empty<DocumentDiagnostic>(), Message: "The workspace text does not match the requested version. Read the current document or retry once the live workspace catches up.");
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotSupportedException("The selected document has no compiler semantic model; build and analyzer diagnostics are separate capabilities.");
        var diagnostics = model.GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => d.Location.IsInSource && (d.Severity == DiagnosticSeverity.Error ||
                request.IncludeWarnings && d.Severity == DiagnosticSeverity.Warning))
            .OrderBy(d => d.Location.SourceSpan.Start).ThenBy(d => d.Id, StringComparer.Ordinal).ToArray();
        if (request.Offset > diagnostics.Length) throw new ArgumentException("offset exceeds the diagnostic count.");
        var items = new System.Collections.Generic.List<DocumentDiagnostic>();
        var characters = 0;
        foreach (var diagnostic in diagnostics.Skip(request.Offset).Take(request.MaxItems))
        {
            var location = diagnostic.Location.GetLineSpan();
            var message = diagnostic.GetMessage();
            var size = Math.Min(message.Length, 4096) + (location.Path?.Length ?? 0) + 128;
            if (items.Count > 0 && characters + size > 65536) break;
            characters += size;
            items.Add(new DocumentDiagnostic(diagnostic.Id, diagnostic.Severity.ToString(), message.Length > 4096 ? message.Substring(0, 4096) : message,
                location.Path, location.StartLinePosition.Line + 1, location.StartLinePosition.Character + 1,
                location.EndLinePosition.Line + 1, location.EndLinePosition.Character + 1, message.Length > 4096));
        }
        var current = isCurrent();
        return new(path, version, project.Id.Id.ToString("N"), project.Name, project.Language, true, current,
            request.Offset, diagnostics.Length, request.Offset + items.Count < diagnostics.Length ? request.Offset + items.Count : null, items,
            Message: current ? null : "Workspace changed while computing diagnostics; these results describe the returned text version. Request fresh diagnostics before acting.");
    }
}
