using NetVsMcp.Contracts;
using NetVsMcp.Broker.Services;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoverageSummaryPathRoundTripsThroughPublicProjection(bool forwardSlashes)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("coverage", "Fixture"));
        var session = runtime.Sessions.ListSessions().Single();
        const string path = @"C:\Fixture\RegressionCoverage\CoverageProbe.cs";
        var report = new CoverageReport("exact-run", "coverlet-cobertura", "collected", 1, 1,
            [new(path, 1, 1, [new(3, 1)])]);
        var fake = new FakeVisualStudioSessionRpc("Program.cs")
        {
            TestResultsReport = new(true, "Completed", [], [], "dotnet-cli", "exact-run", "completed", 0, Coverage: report)
        };
        runtime.Connections.AddOrUpdate(session.SessionId, fake);
        var summary = await runtime.Tools.TestCoverageResults("exact-run", sessionId: session.SessionId);
        var text = new TextContentBlock { Text = JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var output = new CallToolResult { Content = [text] };
        DebuggingToolOutput.Apply(output, "test_coverage_results", @"C:\Fixture\Fixture.slnx");
        var returnedPath = JsonNode.Parse(text.Text)!["coverage"]!["files"]![0]!["path"]!.GetValue<string>();
        Assert.Equal(path, returnedPath);
        var lines = await runtime.Tools.TestCoverageFile("exact-run", forwardSlashes ? returnedPath.Replace('\\', '/') : returnedPath,
            sessionId: session.SessionId);
        Assert.True(lines.Success);
        Assert.Equal(new CoverageLine(3, 1), Assert.Single(lines.Value!.Lines));
        Assert.Equal("exact-run", fake.LastTestResultsRequest!.RunId);
        Assert.Null(fake.LastTestRunRequest);
    }

    [Fact]
    public async Task CoverageNegotiationRejectsOlderExtensionBeforeRunningTests()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("coverage", "Fixture"));
        var session = runtime.Sessions.ListSessions().Single();
        var fake = new FakeVisualStudioSessionRpc("Program.cs")
        {
            CapabilityReport = new(session, "1.2", [], [new("coverage", "unsupported", "Not implemented")])
        };
        runtime.Connections.AddOrUpdate(session.SessionId, fake);
        var result = await runtime.Tools.TestCoverageRun("Fixture.Tests", sessionId: session.SessionId);
        Assert.False(result.Success);
        Assert.Null(fake.LastTestRunRequest);
    }

    [Fact]
    public async Task CoverageKeepsFailureOutcomeAndOmitsLinesFromFileSummary()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("coverage", "Fixture"));
        var session = runtime.Sessions.ListSessions().Single();
        var report = new CoverageReport("exact-run", "coverlet-cobertura", "collected", 1, 2,
            [new("Example.cs", 1, 2, [new(10, 1), new(11, 0)])]);
        var fake = new FakeVisualStudioSessionRpc("Program.cs")
        {
            CapabilityReport = new(session, "1.2", [], [new("coverage", "requiresCollector", "Installed collector required")]),
            TestRunReport = new(true, "A test failed", [], [], "dotnet-cli", "exact-run", "testsFailed", 1, Coverage: report)
        };
        runtime.Connections.AddOrUpdate(session.SessionId, fake);
        var result = await runtime.Tools.TestCoverageRun("Fixture.Tests", sessionId: session.SessionId);
        Assert.True(result.Success);
        Assert.True(fake.LastTestRunRequest!.CollectCoverage);
        Assert.Equal("testsFailed", result.Value!.Outcome);
        Assert.Equal(1, result.Value.RunnerExitCode);
        Assert.Equal("exact-run", result.Value.RunId);
        Assert.Equal("collected", result.Value.Coverage!.Outcome);
        Assert.Empty(result.Value.Coverage.Files.Single().Lines);
        Assert.Equal(2, result.Value.Coverage.LinesValid);
    }
}
