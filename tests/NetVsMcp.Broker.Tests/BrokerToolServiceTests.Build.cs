using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task BuildSolution_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildSolution(
            waitForBuildToFinish: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.Equal("Done", response.Value!.Status.State);
        Assert.Equal(0, response.Value.LastBuildInfo);
    }

    [Fact]
    public async Task BuildAndGetErrors_BuildsAndReturnsDiagnostics()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildAndGetErrors(includeWarnings: false, maxItems: 10, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.False(session.LastBuildSolutionRequest.CaptureDiagnostics!.IncludeWarnings);
        Assert.Equal(10, session.LastBuildSolutionRequest.CaptureDiagnostics.MaxItems);
        Assert.Null(session.LastErrorListRequest);
        Assert.Equal("Build-specific diagnostic", Assert.Single(response.Value!.Errors.Items).Description);
        Assert.Null(response.Value.Build.Diagnostics);
    }

    [Fact]
    public async Task BuildAndGetErrors_OlderExtensionReportsUnavailableWithoutReadingMixedErrors()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { OmitBuildDiagnostics = true };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildAndGetErrors(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Null(session.LastErrorListRequest);
        Assert.Empty(response.Value!.Errors.Items);
        Assert.NotNull(response.Value.DiagnosticsUnavailable);
    }

    [Fact]
    public async Task BuildStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BuildStatus(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Idle", response.Value!.State);
    }

    [Fact]
    public async Task OutputRead_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OutputRead(
            paneName: "Build",
            maxChars: 100,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Build", session.LastOutputReadRequest!.PaneName);
        Assert.Equal(100, session.LastOutputReadRequest.MaxChars);
        Assert.Equal("Build output", response.Value!.Text);
    }

    [Fact]
    public async Task OutputWrite_RoutesTextToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OutputWrite("hello", paneName: "NetVsMcp", activate: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp", session.LastOutputWriteRequest!.PaneName);
        Assert.True(session.LastOutputWriteRequest.Activate);
        Assert.Contains("hello", response.Value!.Text);
    }

    [Fact]
    public async Task BuildStatus_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.BuildStatus(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

}
