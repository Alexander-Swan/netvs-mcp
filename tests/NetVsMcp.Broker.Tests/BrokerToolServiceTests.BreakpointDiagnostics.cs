using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task BreakpointDiagnosticsRejectsInvalidPages(int offset, int maximum)
    {
        var response = await CreateRuntime().Tools.BreakpointDiagnostics(offset, maximum);
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BindingReadsRemainAvailableWhilePausedAndOlderExtensionsDoNotInferBinding(bool oldExtension)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            BreakpointDiagnosticsHandler = (_, _) => oldExtension ? throw MissingFrameInspectionMethod() :
                Task.FromResult(new BreakpointDiagnosticsResult(new("dbgDesignMode"), 0, 1, null,
                    [new("bp", "Editor.cs", 1, true, "notDebugging", "pending", null, [], false)], false, [], false, "No inference"))
        });
        runtime.Tools.SessionAutomationControl("vs-1", true);
        var response = await runtime.Tools.BreakpointDiagnostics(sessionId: "vs-1");
        Assert.Equal(!oldExtension, response.Success);
        if (oldExtension) Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        else Assert.Equal("notDebugging", Assert.Single(response.Value!.Breakpoints).BindingState);
    }
}
