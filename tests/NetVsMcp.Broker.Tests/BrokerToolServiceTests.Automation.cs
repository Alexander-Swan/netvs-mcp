using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task AutomationTools_RouteThroughVsixSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var console = await runtime.Tools.ConsoleRead(sessionId: "vs-1");
        var ui = await runtime.Tools.UiFindElements("name=Run", sessionId: "vs-1");
        var web = await runtime.Tools.WebNavigate("http://localhost:5000", sessionId: "vs-1");
        var blazor = await runtime.Tools.WebBlazorInspect(sessionId: "vs-1");
        var blazorReady = await runtime.Tools.WebBlazorWaitReady(sessionId: "vs-1");
        var blazorComponents = await runtime.Tools.WebBlazorGetComponents(sessionId: "vs-1");
        var blazorState = await runtime.Tools.WebBlazorGetState("#app", sessionId: "vs-1");
        var blazorEvent = await runtime.Tools.WebBlazorTriggerEvent("#submit", "click", sessionId: "vs-1");
        var blazorCircuit = await runtime.Tools.WebBlazorInspectCircuit(sessionId: "vs-1");
        var blazorWasm = await runtime.Tools.WebBlazorInspectWasm(sessionId: "vs-1");

        Assert.True(console.Success);
        Assert.Equal("console_read", console.Value!.Metadata!["toolName"]);
        Assert.True(ui.Success);
        Assert.Equal("ui_find_elements", ui.Value!.Metadata!["toolName"]);
        Assert.True(web.Success);
        Assert.Equal("web_navigate", web.Value!.Metadata!["toolName"]);
        Assert.True(blazor.Success);
        Assert.Equal("web_blazor_inspect", blazor.Value!.Metadata!["toolName"]);
        Assert.True(blazorReady.Success);
        Assert.Equal("web_blazor_wait_ready", blazorReady.Value!.Metadata!["toolName"]);
        Assert.True(blazorComponents.Success);
        Assert.Equal("web_blazor_get_components", blazorComponents.Value!.Metadata!["toolName"]);
        Assert.True(blazorState.Success);
        Assert.Equal("web_blazor_get_state", blazorState.Value!.Metadata!["toolName"]);
        Assert.True(blazorEvent.Success);
        Assert.Equal("web_blazor_trigger_event", blazorEvent.Value!.Metadata!["toolName"]);
        Assert.True(blazorCircuit.Success);
        Assert.Equal("web_blazor_inspect_circuit", blazorCircuit.Value!.Metadata!["toolName"]);
        Assert.True(blazorWasm.Success);
        Assert.Equal("web_blazor_inspect_wasm", blazorWasm.Value!.Metadata!["toolName"]);
    }
}
