using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData("", 1, 123, null)]
    [InlineData("pause", -1, 123, null)]
    [InlineData("pause", 1, 0, null)]
    [InlineData("pause", 1, 123, 0)]
    public async Task SourceEvidenceRejectsInvalidPauseAndProcessArguments(string stop, long generation, int process, int? thread)
    {
        var response = await CreateRuntime().Tools.DebugSourceEvidence(stop, generation, process, thread);
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }

    [Fact]
    public async Task SourceEvidenceNeverFallsBackToUnguardedLegacyInspection()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        { SourceEvidenceHandler = (_, _) => throw MissingFrameInspectionMethod() });
        var result = await runtime.Tools.DebugSourceEvidence("pause", 1, 123, sessionId: "vs-1");
        Assert.False(result.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, result.Metadata!["error_code"]);
    }

    [Fact]
    public async Task SourceEvidencePreservesExplicitRoutingAndRemainsReadableWhileMutationsArePaused()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Second"));
        var calls = 0;
        runtime.Connections.AddOrUpdate("vs-2", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            SourceEvidenceHandler = (request, _) =>
            {
                Assert.Equal(new DebugSourceEvidenceRequest("exact", 2, 456, 789), request);
                calls++;
                return Task.FromResult(new DebugSourceEvidenceResult(new("dbgBreakMode", Generation: 2, StopId: "exact"),
                    456, 789, "Editor.cs", 10, 1, "SHA256", [], new("unavailable"), new("unavailable")));
            }
        });
        Assert.True(runtime.Tools.SessionAutomationControl("vs-2", true).Success);
        var result = await runtime.Tools.DebugSourceEvidence("exact", 2, 456, 789, sessionId: "vs-2");
        Assert.True(result.Success);
        Assert.Equal(1, calls);
        Assert.Equal("unavailable", result.Value!.Disk.Status);
    }
}
