using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DebugSelectFrame_RoutesToTheRequestedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Second"));
        var first = new FakeVisualStudioSessionRpc("First.cs");
        var second = new FakeVisualStudioSessionRpc("Second.cs");
        runtime.Connections.AddOrUpdate("vs-1", first);
        runtime.Connections.AddOrUpdate("vs-2", second);

        var response = await runtime.Tools.DebugSelectFrame(2, sessionId: "vs-2");

        Assert.True(response.Success);
        Assert.Equal(2, second.LastDebugSelectFrameRequest!.FrameIndex);
        Assert.Null(first.LastDebugSelectFrameRequest);
        Assert.Equal("dbgBreakMode", response.Value!.State.Mode);
        Assert.Equal(2, response.Value.Frame.FrameIndex);
        Assert.True(response.Value.Frame.IsCurrent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DebugSelectFrame_InvalidIndexFailsBeforeRouting(int index)
    {
        var response = await CreateRuntime().Tools.DebugSelectFrame(index);
        Assert.False(response.Success);
        Assert.Contains("1 or greater", response.Message);
    }

    [Fact]
    public async Task DebugSelectFrame_InvalidSessionDoesNotFallBack()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugSelectFrame(2, sessionId: "missing");

        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.SessionRoutingFailed, response.Metadata!["error_code"]);
        Assert.Null(session.LastDebugSelectFrameRequest);
    }

    [Fact]
    public async Task DebugSelectFrame_RunningDebuggerReportsFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugSelectFrameHandler = (_, _) => throw new InvalidOperationException("The debugger must be paused before selecting a stack frame.")
        });

        var response = await runtime.Tools.DebugSelectFrame(2, sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Contains("paused", response.Message);
    }

}
