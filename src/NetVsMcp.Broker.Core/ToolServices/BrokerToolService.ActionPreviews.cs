using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "code_actions_preview", Title = "Preview Code Action", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Prepares the original actionId and returns text edits across affected existing source documents without applying them. Apply the same ID and position using code_actions_apply to use this exact transformation. Any workspace change invalidates the preview. IDs expire after two minutes. Structural/custom operations are explicitly unsupported; requires an updated extension.")]
    public Task<ToolResponse<CodeActionsApplyResult>> CodeActionsPreview(string documentPath, int line, int column,
        string actionId, int? endLine = null, int? endColumn = null,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateCodePosition(documentPath, line, column);
        if (error != null || string.IsNullOrWhiteSpace(actionId))
            return Task.FromResult(ToolResponse<CodeActionsApplyResult>.Fail(error ?? "actionId is required."));
        var request = new CodeActionsApplyRequest
        {
            DocumentPath = documentPath.Trim(), Line = line, Column = column,
            EndLine = endLine, EndColumn = endColumn, ActionId = actionId, PreviewOnly = true
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.CodeActionsPreviewAsync(request, ct), cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
}
