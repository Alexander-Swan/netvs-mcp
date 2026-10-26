using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "code_actions_revert", Title = "Revert Owned Code Action", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Reverts only verified source changes described by a code-action recoveryId. Receipts are single use, bounded to 32 entries, expire after 15 minutes, and are lost on VS restart. Any later workspace source/project change rejects the receipt; developer-created changes are not overwritten. This is scoped recovery, not the global VS undo stack. Returns observed source versions and any new recovery receipt; inspect partial/unknown outcomes before retrying. Requires an updated extension.")]
    public Task<ToolResponse<CodeActionsApplyResult>> CodeActionsRevert(string recoveryId,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryId)) return Task.FromResult(FailWithCode<CodeActionsApplyResult>("recoveryId is required.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.CodeActionsRevertAsync(new() { RecoveryId = recoveryId }, ct), cancellationToken);
    }

    private static ToolResponse<T> NormalizeActionResponse<T>(ToolResponse<T> response, string? sessionId, bool recovery)
    {
        if (!response.Success)
        {
            if (recovery && response.Metadata?.GetValueOrDefault("error_code") == ToolErrorCodes.StaleAction)
            {
                var metadata = response.Metadata.ToDictionary(p => p.Key, p => p.Value);
                metadata["nextActions"] = "Inspect current buffers and recorded document versions. The receipt expired, was used, or conflicts with later work. Do not replay recovery or use broad IDE undo.";
                return response with { Metadata = metadata };
            }
            return response;
        }
        if (response.Value is not CodeActionsApplyResult { Success: false } value) return response;
        var code = value.Outcome switch
        {
            "unsupported" => ToolErrorCodes.FeatureUnavailable,
            "failedWithChanges" => "partial_application",
            "unknown" => ToolErrorCodes.OutcomeUnknown,
            _ => "action_rejected"
        };
        return response with { Success = false, Message = value.Message,
            Metadata = new Dictionary<string, string>
            {
                ["error_code"] = code,
                ["sessionId"] = sessionId ?? string.Empty,
                ["nextActions"] = value.RecoveryId is not null
                    ? "Review observed changes and versions. If reverting the verified owned changes is intended, call code_actions_revert with recoveryId and the same session. Do not repeat the original apply."
                    : "Inspect current live buffers and returned versions before planning another change. Do not repeat a consumed action ID or blindly undo developer work."
            } };
    }
}
