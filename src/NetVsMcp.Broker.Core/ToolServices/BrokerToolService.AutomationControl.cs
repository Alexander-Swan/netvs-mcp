using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: false)]
    [McpServerTool(Name = "session_automation_control", Title = "Pause Session Mutations", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Inspects or pauses new automation mutations for an explicitly selected session. Omit paused to inspect; paused=true blocks new commands while preserving reads and build-cancel/debug-break recovery controls. Already-running commands continue and are reported as activeMethod. Pausing does not suspend the developer or debuggee. Resume with paused=false; state lasts for this broker lifetime. Compact output. Setting paused returns true; querying returns paused. Controls automation, not debuggee execution.")]
    public ToolResponse<SessionAutomationControl> SessionAutomationControl(string sessionId, bool? paused = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return ToolResponse<SessionAutomationControl>.Fail("An explicit sessionId is required.");
        var route = _runtime.Sessions.Resolve(CreateTarget(sessionId, null, null));
        if (!route.Success || route.Session is null)
            return new(false, null, route.Message, CreateRouteFailureMetadata(route));
        var state = _runtime.Mutations.Inspect(route.Session.SessionId, paused);
        return ToolResponse<SessionAutomationControl>.Ok(new(route.Session.SessionId, state.Paused, state.Active));
    }
}
