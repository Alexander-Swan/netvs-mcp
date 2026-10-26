using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    private static DebugRunToResult RunToReport(string outcome = "reachedLocation", bool clean = true) =>
        new(new("dbgBreakMode", StopId: "next", Generation: 1), outcome, "owner", "run-to-owner", "Program.cs", 10, 1,
            "Program.cs", 10, 1, new(1, clean ? 1 : 0, clean ? 0 : 1, [], clean), null);

    [Fact]
    public async Task RunToRejectsInvalidGuardsAndNeverFallsBackOnOlderExtensions()
    {
        var runtime = CreateRuntime();
        Assert.False((await runtime.Tools.DebugRunTo("Program.cs", 0, "pause", 1, 123)).Success);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        { RunToHandler = (_, _) => throw MissingFrameInspectionMethod() });
        var response = await runtime.Tools.DebugRunTo("Program.cs", 10, "pause", 1, 123, sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
    }

    [Theory]
    [InlineData("reachedLocation", true)]
    [InlineData("alreadyAtLocation", true)]
    [InlineData("reachedLocation", false)]
    [InlineData("stoppedElsewhere", true)]
    [InlineData("outcomeUnknown", true)]
    [InlineData("waitCanceled", true)]
    [InlineData("contextChanged", true)]
    public async Task RunToPreservesObservedOutcomeAndCleanup(string outcome, bool clean)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var calls = 0;
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            RunToHandler = (request, _) =>
            {
                calls++;
                Assert.Equal("pause", request.ExpectedStopId);
                Assert.Equal(1, request.ExpectedGeneration);
                Assert.Equal(123, request.ExpectedProcessId);
                return Task.FromResult(RunToReport(outcome, clean));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var result = await runtime.Tools.DebugRunTo("Program.cs", 10, "pause", 1, 123, sessionId: "vs-1");
        Assert.True(result.Success);
        Assert.Equal(outcome, result.Value!.Outcome);
        Assert.Equal(clean, result.Value.Cleanup.Complete);
        Assert.Equal(1, calls);
        Assert.Equal(0, session.DebugExecutionCommands);
    }
}
