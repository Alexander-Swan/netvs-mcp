using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_expand", Title = "Expand Stopped Debugger Value", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Expands an expression within one validated pause and frame with auto-expand rules disabled. Requires an updated VSIX. Incomplete or duplicate-member results include $value and $expansion with paging and truncation evidence. Compact output.")]
    public Task<ToolResponse<DebugFrameInspectionResult>> DebugExpand(
        [Description("Expression to expand, 1 to 4096 characters.")] string expression,
        [Description("Required stopId from debug_status or a stopped snapshot.")] string expectedStopId,
        [Description("Required generation from the same state as expectedStopId.")] long expectedGeneration,
        [Description("Returned node's 1-based member path, up to eight indices. Reuse only within the same pause. Omit for the root.")] int[]? memberPath = null,
        [Description("Zero-based child offset. Use the node's returned nextOffset to continue.")] int childOffset = 0,
        [Description("Maximum children per node, 1 to 100. Defaults to 20.")] int maxChildren = 20,
        [Description("Maximum expansion depth, 0 to 4. Defaults to 1.")] int maxDepth = 1,
        [Description("Maximum expanded nodes, 1 to 1000. Defaults to 100.")] int maxNodes = 100,
        [Description("Maximum characters per value, 64 to 4096. Defaults to 1024.")] int maxValueCharacters = 1024,
        [Description("Cooperative expansion budget in milliseconds, 1 to 10000. Defaults to 1000. Synchronous engine calls can exceed it.")] int budgetMilliseconds = 1000,
        [Description("Allow debugger function evaluation, which may execute debuggee code. Defaults to false.")] bool allowFunctionEvaluation = false,
        [Description(InspectionFrameDescription)] int stackFrame = 0,
        [Description("Debugger thread ID from debug_get_threads. Omit to use the selected thread. When supplied without a frame, uses frame 1.")] int? threadId = null,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null,
        CancellationToken cancellationToken = default, [Description(InspectionContextDescription)] bool includeContext = false)
    {
        var expand = new DebugExpansionRequest
        {
            Expression = expression, MemberPath = memberPath ?? [], ChildOffset = childOffset, MaxChildren = maxChildren,
            MaxDepth = maxDepth, MaxNodes = maxNodes, MaxValueCharacters = maxValueCharacters,
            BudgetMilliseconds = budgetMilliseconds, AllowFunctionEvaluation = allowFunctionEvaluation
        };
        try
        {
            expand.Validate();
            if (string.IsNullOrWhiteSpace(expectedStopId) || stackFrame < 0 || threadId is <= 0)
                throw new ArgumentException("A current stopId, nonnegative frame index, and positive optional threadId are required.");
        }
        catch (ArgumentException ex) { return Task.FromResult(ToolResponse<DebugFrameInspectionResult>.Fail(ex.Message)); }
        var request = new DebugFrameInspectionRequest
        {
            Expansion = expand, ExpectedStopId = expectedStopId, ExpectedGeneration = expectedGeneration,
            StackFrame = stackFrame, ThreadId = threadId
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DebugExpandAsync(request, ct), cancellationToken,
            unsupportedByVsixMessage: "Bounded expansion and function-evaluation policy require an updated VSIX; no unguarded fallback is used.", includeContext: includeContext);
    }
}
