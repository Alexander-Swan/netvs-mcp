using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_source_evidence", Title = "Inspect Paused Source Checksums", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compares engine-supplied SHA-256 source checksums for a paused thread's top instruction with the exact local file bytes and immutable Roslyn editor text (including unsaved changes and encoding). Requires current expectedStopId/generation and expectedProcessId; optional threadId selects that process's thread without changing debugger selection. Returns separate disk/editor match, mismatch, unavailable, tooLarge, or stale evidence, expected checksums, source location, and editor text version. Reads at most 8 MiB per source; no symbol loading, source download, function evaluation, path guessing, or execution. Missing/legacy checksums and unsupported engines remain unavailable; loaded symbols or matching filenames alone never prove source matches. Requires an updated VSIX with no compatibility fallback.")]
    public Task<ToolResponse<DebugSourceEvidenceResult>> DebugSourceEvidence(string expectedStopId, long expectedGeneration,
        int expectedProcessId, int? threadId = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedStopId) || expectedStopId.Length > 200 || expectedGeneration < 0 || expectedProcessId < 1 || threadId is <= 0)
            return Task.FromResult(FailWithCode<DebugSourceEvidenceResult>("Supply the current pause/generation and positive process/thread IDs.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DebugSourceEvidenceAsync(new(expectedStopId, expectedGeneration, expectedProcessId, threadId), ct), cancellationToken);
    }
}
