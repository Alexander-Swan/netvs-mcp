using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "module_list", Title = "Module List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists loaded modules and actual debugger symbol status in a paused Visual Studio Concord session. Returns module paths, versions, addresses, symbol-file paths and loaded/notLoaded/unavailable status. Requires break mode. Engines without Concord support return supported=false. Does not load symbols. Use includeSymbolLoadInformation for symbol-search diagnostics.")]
    public Task<ToolResponse<ModuleListResult>> ModuleList(
        [Description("Optional OS process ID; omit to list all Concord processes in the routed session.")] int? processId = null,
        [Description("Optional case-insensitive substring of the module name or path.")] string? name = null,
        bool includeSymbolLoadInformation = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (processId is <= 0)
            return Task.FromResult(FailWithCode<ModuleListResult>("processId must be positive.", ToolErrorCodes.InvalidRequest));

        var request = new ModuleListRequest { ProcessId = processId, Name = name, IncludeSymbolLoadInformation = includeSymbolLoadInformation };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.ModuleListAsync(request, ct), cancellationToken);
    }
}
