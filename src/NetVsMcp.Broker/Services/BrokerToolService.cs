using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

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

    public ToolResponse<IReadOnlyCollection<VsSessionInfo>> VsListSessions()
    {
        return ToolResponse<IReadOnlyCollection<VsSessionInfo>>.Ok(_runtime.Sessions.ListSessions());
    }

    public ToolResponse<BrokerStatus> VsGetStatus()
    {
        return ToolResponse<BrokerStatus>.Ok(_runtime.GetStatus());
    }

    public ToolResponse<BrokerCapabilities> VsGetCapabilities()
    {
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            ToolDescriptors,
            VisualStudioCapabilities);

        return ToolResponse<BrokerCapabilities>.Ok(capabilities);
    }
}
