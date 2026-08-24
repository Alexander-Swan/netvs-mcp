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

        Assert.True(console.Success);
        Assert.Equal("console_read", console.Value!.Metadata!["toolName"]);
        Assert.True(ui.Success);
        Assert.Equal("ui_find_elements", ui.Value!.Metadata!["toolName"]);
        Assert.True(web.Success);
        Assert.Equal("web_navigate", web.Value!.Metadata!["toolName"]);
    }
}
