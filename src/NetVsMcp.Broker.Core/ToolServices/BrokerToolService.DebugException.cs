using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_exception", Title = "Inspect Current Exception Evidence", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads exception notification evidence bound to expectedStopId/expectedGeneration from the current debugger pause. Returns category/type/description/code, throwing process/thread, notification kind, known unhandled status, and an optional throwing-thread top source position. A thrown notification does not prove whether the exception will be handled; handled remains unknown. Never evaluates $exception properties or changes exception settings. Explicit noException/unavailable states distinguish ordinary pauses and engines without captured evidence. Requires an updated VSIX and explicit session routing when multiple sessions exist. Compact output. Returns the requested data directly. Pagination, truncation, unavailable evidence and distinct identities remain explicit.")]
    public Task<ToolResponse<DebugExceptionResult>> DebugException(string expectedStopId, long expectedGeneration,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedStopId) || expectedStopId.Length > 200 || expectedGeneration < 0)
            return Task.FromResult(FailWithCode<DebugExceptionResult>("A current stopId (up to 200 characters) and nonnegative generation are required.", ToolErrorCodes.InvalidRequest));
        var request = new DebugExceptionRequest { ExpectedStopId = expectedStopId, ExpectedGeneration = expectedGeneration };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DebugExceptionAsync(request, ct), cancellationToken);
    }
}
