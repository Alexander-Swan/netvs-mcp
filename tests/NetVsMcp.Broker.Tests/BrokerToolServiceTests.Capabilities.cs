using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task CapabilityDiscoveryRoutesAndPreservesUnsupportedReasons()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var report = new SessionCapabilityReport(runtime.Sessions.ListSessions().Single(), "1.2", ["C#"],
            [new("testExplorer", "unsupported", "CLI backend only", "test_run")]);
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs") { CapabilityReport = report });
        var response = await runtime.Tools.SessionCapabilities(sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.NotNull(response.Value!.BrokerVersion);
        Assert.Equal("CLI backend only", Assert.Single(response.Value.Features).Reason);
    }

    [Fact]
    public async Task CapabilityDiscoveryDoesNotInferSupportForAnOldExtension()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));
        var response = await runtime.Tools.SessionCapabilities(sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
    }

    [Fact]
    public void DoctorIdentifiesMissingSelectedSession()
    {
        var runtime = CreateRuntime();
        var response = runtime.Tools.NetVsDoctor(sessionId: "missing");
        Assert.Contains(response.Value!.Checks, check => check.Name == "selected_session" && !check.Passed);
    }
}
