using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>Compares immutable source versions, not incidental Solution wrapper identity.</summary>
internal sealed class WorkspaceActionSnapshot
{
    private readonly Workspace workspace;
    private readonly string versions;
    private sealed class CachedTextVersion(string value) { public string Value { get; } = value; }
    private static readonly ConditionalWeakTable<SourceText, CachedTextVersion> TextVersions = new();
    private WorkspaceActionSnapshot(Workspace workspace, string versions) { this.workspace = workspace; this.versions = versions; }
    public static Task<WorkspaceActionSnapshot> CaptureAsync(Solution solution, CancellationToken cancellationToken) =>
        Task.Run(() => CaptureCoreAsync(solution, cancellationToken), cancellationToken);

    private static async Task<WorkspaceActionSnapshot> CaptureCoreAsync(Solution solution, CancellationToken cancellationToken)
    {
        var signature = new StringBuilder(solution.Id.Id.ToString("N"));
        foreach (var project in solution.Projects.OrderBy(p => p.Id.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            signature.Append('|').Append(project.Id.Id).Append(':').Append(project.Version);
            foreach (var document in project.Documents.Cast<TextDocument>().Concat(project.AdditionalDocuments)
                .Concat(project.AnalyzerConfigDocuments).OrderBy(d => d.Id.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var version = TextVersions.GetValue(text, value => new CachedTextVersion(TextContentVersion.Create(value.ToString())));
                AppendDocument(signature, document.Id, text, version.Value);
            }
        }
        return new(solution.Workspace, signature.ToString());
    }

    public override bool Equals(object? obj) => obj is WorkspaceActionSnapshot other &&
        ReferenceEquals(workspace, other.workspace) && string.Equals(versions, other.versions, StringComparison.Ordinal);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(workspace) ^ StringComparer.Ordinal.GetHashCode(versions);

    public bool MatchesCurrent(Solution current)
    {
        if (!ReferenceEquals(workspace, current.Workspace)) return false;
        var signature = new StringBuilder(current.Id.Id.ToString("N"));
        foreach (var project in current.Projects.OrderBy(p => p.Id.Id))
        {
            signature.Append('|').Append(project.Id.Id).Append(':').Append(project.Version);
            foreach (var document in project.Documents.Cast<TextDocument>().Concat(project.AdditionalDocuments)
                .Concat(project.AnalyzerConfigDocuments).OrderBy(d => d.Id.Id))
            {
                // No asynchronous work or uncached hashing at the final mutation boundary.
                if (!document.TryGetText(out var text) || !TextVersions.TryGetValue(text, out var version)) return false;
                AppendDocument(signature, document.Id, text, version.Value);
            }
        }
        return string.Equals(versions, signature.ToString(), StringComparison.Ordinal);
    }

    private static void AppendDocument(StringBuilder signature, DocumentId id, SourceText text, string version) =>
        signature.Append('|').Append(id.Id).Append(':').Append(version).Append(':').Append(text.Encoding?.WebName)
            .Append(':').Append(text.Encoding is null ? "" : BitConverter.ToString(text.Encoding.GetPreamble()));
}
