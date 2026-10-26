using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("pause", -1)]
    public async Task ExceptionInspectionRequiresPauseIdentity(string stop, long generation)
    {
        var response = await CreateRuntime().Tools.DebugException(stop, generation);
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }

    [Fact]
    public async Task ExceptionInspectionPinsPauseAndDoesNotSubstituteAnOlderExtension()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            ExceptionHandler = (request, _) =>
            {
                Assert.Equal("pause", request.ExpectedStopId);
                Assert.Equal(9, request.ExpectedGeneration);
                return Task.FromResult(new DebugExceptionResult(new("dbgBreakMode", Generation: 9, StopId: "pause"),
                    "unavailable", null, null, "No captured engine notification."));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugException("pause", 9, sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("unavailable", response.Value!.EvidenceStatus);
        Assert.Null(response.Value.Exception);
        session.ExceptionHandler = (_, _) => throw MissingFrameInspectionMethod();
        var older = await runtime.Tools.DebugException("pause", 9, sessionId: "vs-1");
        Assert.False(older.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, older.Metadata!["error_code"]);
        session.ExceptionHandler = (_, _) => throw new TypedRemoteFailure(VsRpcErrorCodes.StaleDebuggerContext);
        var stale = await runtime.Tools.DebugException("pause", 9, sessionId: "vs-1");
        Assert.False(stale.Success);
        Assert.Equal(ToolErrorCodes.StaleDebuggerContext, stale.Metadata!["error_code"]);
    }
}
