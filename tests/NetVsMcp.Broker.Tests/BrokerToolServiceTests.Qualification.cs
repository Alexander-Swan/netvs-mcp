using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupMutationOnOlderExtensionNeverFallsBackToNameOrLocation(bool enable)
    {
        var runtime = CreateRuntime();
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs") { GroupMutationUnsupported = true };
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture"));
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var result = enable
            ? await runtime.Tools.BreakpointGroupEnable("owned", false, sessionId: "vs-1")
            : await runtime.Tools.BreakpointGroupRemove("owned", sessionId: "vs-1");
        Assert.False(result.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, result.Metadata!["error_code"]);
        Assert.Contains("No location-based fallback", result.Message);
        Assert.Null(rpc.LastBreakpointRemoveRequest);
        Assert.Null(rpc.LastBreakpointEnableRequest);
    }

}
