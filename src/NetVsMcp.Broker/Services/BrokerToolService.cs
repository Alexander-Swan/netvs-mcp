using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

[McpServerToolType]
public sealed class BrokerToolService
{
    private static readonly BrokerToolDescriptor[] ToolDescriptors =
    [
        new("vs_list_sessions", "Lists Visual Studio instances registered with the local broker.", false),
        new("vs_get_status", "Returns local broker endpoint, uptime, and registered session status.", false),
        new("vs_get_capabilities", "Lists broker tools and Visual Studio capability categories.", false)
    ];

    private static readonly VsCapability[] VisualStudioCapabilities =
    [
        VsCapability.Editor,
        VsCapability.Navigation,
        VsCapability.Build,
        VsCapability.Debugger,
        VsCapability.Diagnostics,
        VsCapability.Tests,
        VsCapability.ProjectSystem
    ];

    private readonly BrokerRuntime _runtime;

    public BrokerToolService(BrokerRuntime runtime)
    {
        _runtime = runtime;
    }

    [McpServerTool(Name = "vs_list_sessions")]
    [Description("Lists Visual Studio instances registered with the local NetVsMcp broker.")]
    public ToolResponse<IReadOnlyCollection<VsSessionInfo>> VsListSessions()
    {
        return ToolResponse<IReadOnlyCollection<VsSessionInfo>>.Ok(_runtime.Sessions.ListSessions());
    }

    [McpServerTool(Name = "vs_get_status")]
    [Description("Returns local broker endpoint, uptime, registration pipe, and registered Visual Studio session status.")]
    public ToolResponse<BrokerStatus> VsGetStatus()
    {
        return ToolResponse<BrokerStatus>.Ok(_runtime.GetStatus());
    }

    [McpServerTool(Name = "vs_get_capabilities")]
    [Description("Lists NetVsMcp broker tools and Visual Studio capability categories.")]
    public ToolResponse<BrokerCapabilities> VsGetCapabilities()
    {
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            ToolDescriptors,
            VisualStudioCapabilities);

        return ToolResponse<BrokerCapabilities>.Ok(capabilities);
    }
}
