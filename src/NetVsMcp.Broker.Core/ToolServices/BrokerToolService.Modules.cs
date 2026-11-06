using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "module_list", Title = "Module List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists loaded modules and actual debugger symbol status in a paused Visual Studio Concord session. Reports loaded binaries and their actual symbol associations. Requires break mode. Engines without Concord support fail explicitly. Does not load symbols. Use includeSymbolLoadInformation for symbol-search diagnostics. Compact output. Returns processes grouped by processId, each with modules containing path/symbols. symbols is .pdb for the matching adjacent PDB, another path for a different PDB, or an explicit status. Symbol diagnostics remain when requested; sizes, versions, addresses, GUIDs and duplicate names are omitted. Dumps without OS IDs retain processUniqueId. Returns up to maxItems modules across all processes. Follow nextOffset while paused.")]
    public Task<ToolResponse<ModuleListResult>> ModuleList(
        [Description("Optional OS process ID; omit to list all Concord processes in the routed session.")] int? processId = null,
        [Description("Optional case-insensitive substring of the module name or path.")] string? name = null,
        bool includeSymbolLoadInformation = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default, int offset = 0, int maxItems = 100)
    {
        if (processId is <= 0)
            return Task.FromResult(FailWithCode<ModuleListResult>("processId must be positive.", ToolErrorCodes.InvalidRequest));

        if (offset < 0 || maxItems is < 1 or > 500)
            return Task.FromResult(FailWithCode<ModuleListResult>("offset must be nonnegative and maxItems must be 1–500.", ToolErrorCodes.InvalidRequest));

        var request = new ModuleListRequest { ProcessId = processId, Name = name, IncludeSymbolLoadInformation = includeSymbolLoadInformation };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.ModuleListAsync(request, ct), cancellationToken);
    }
}
