using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_expand", Title = "Expand Stopped Debugger Value", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Expands an expression in one validated pause/frame with bounded children, depth, nodes, value length, and cooperative elapsed budget. Obtain expectedStopId/expectedGeneration from debug_status or a stopped snapshot. Function evaluation defaults off and auto-expand rules are disabled; opting in may execute debuggee code. Synchronous engine calls can exceed the budget. Follow a node's memberPath and nextOffset in the SAME pause for more children; requires an updated VSIX.")]
    public Task<ToolResponse<DebugFrameInspectionResult>> DebugExpand(string expression, string expectedStopId, long expectedGeneration,
        int[]? memberPath = null, int childOffset = 0, int maxChildren = 20, int maxDepth = 1, int maxNodes = 100,
        int maxValueCharacters = 1024, int budgetMilliseconds = 1000, bool allowFunctionEvaluation = false,
        int stackFrame = 0, int? threadId = null, string? sessionId = null, string? solutionName = null,
        string? solutionPath = null, CancellationToken cancellationToken = default, bool includeContext = false)
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
