using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerToolServiceTests
{
    [Fact]
    public void VsListSessions_ReturnsRegisteredSessions()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsListSessions();

        Assert.True(response.Success);
        Assert.Single(response.Value!);
        Assert.Equal("NetVsMcp", response.Value!.Single().SolutionName);
    }

    [Fact]
    public void VsGetCapabilities_ReturnsInitialBrokerTools()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.VsGetCapabilities();

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Tools, tool => tool.Name == "vs_list_sessions");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_status");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_capabilities");
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_get_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_select_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_ping", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_active", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_document_symbols", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_solution", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_status", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "errors_list", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "output_read", RequiresVisualStudioSession: true });
        Assert.All(
            response.Value.Tools.Where(tool => tool.Name.StartsWith("vs_", StringComparison.Ordinal)),
            tool => Assert.False(tool.RequiresVisualStudioSession));
    }

    [Fact]
    public void VsGetSession_SelectsBySessionId()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other"));

        var response = runtime.Tools.VsGetSession(sessionId: "vs-2");

        Assert.True(response.Success);
        Assert.Equal("vs-2", response.Value!.Session.SessionId);
        Assert.Equal(SessionHealth.Connected, response.Value.Health);
    }

    [Fact]
    public void VsGetSession_SelectsByNormalizedSolutionPath()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: false));

        var response = runtime.Tools.VsGetSession(solutionPath: @"c:/code/NetVsMcp/../NetVsMcp/NetVsMcp.slnx");

        Assert.True(response.Success);
        Assert.Equal("vs-1", response.Value!.Session.SessionId);
    }

    [Fact]
    public void VsSelectSession_SelectsBySolutionName()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other"));

        var response = runtime.Tools.VsSelectSession(solutionName: "Other");

        Assert.True(response.Success);
        Assert.Equal("vs-2", response.Value!.SessionId);
        Assert.Equal("Other", response.Value.SolutionName);
    }

    [Fact]
    public void VsSelectSession_ReturnsAmbiguousFailureWithCandidates()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.slnx", isActive: false));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.slnx", isActive: false));

        var response = runtime.Tools.VsSelectSession(solutionName: "Shared");

        Assert.False(response.Success);
        Assert.Equal("Ambiguous", response.Metadata!["failureReason"]);
        Assert.Equal("2", response.Metadata["candidateCount"]);
        Assert.Equal("vs-1,vs-2", response.Metadata["candidateSessionIds"]);
    }

    [Fact]
    public void VsGetSession_ReturnsNoSessionsFailure()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.VsGetSession();

        Assert.False(response.Success);
        Assert.Equal("NoRegisteredSessions", response.Metadata!["failureReason"]);
    }

    [Fact]
    public void VsPing_ReturnsBrokerHealthWithoutTarget()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsPing();

        Assert.True(response.Success);
        Assert.Equal(BrokerOptions.LocalDefault.McpEndpoint, response.Value!.McpEndpoint);
        Assert.Equal(1, response.Value.RegisteredSessionCount);
        Assert.Null(response.Value.TargetSession);
    }

    [Fact]
    public void VsPing_ReturnsTargetStatus_WhenTargetIsSupplied()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsPing(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("vs-1", response.Value!.TargetSession!.Session.SessionId);
    }

    [Fact]
    public async Task DocumentActive_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DocumentActive(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Editor.cs", response.Value);
    }

    [Fact]
    public async Task CodeDocumentSymbols_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeDocumentSymbols(
            documentPath: @"C:\Code\NetVsMcp\Editor.cs",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(@"C:\Code\NetVsMcp\Editor.cs", session.LastSymbolsDocumentPath);
        Assert.Equal(["Editor", "Editor.Run"], response.Value);
    }

    [Fact]
    public async Task DocumentActive_ReturnsAmbiguousFailureWithCandidates()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.slnx", isActive: false));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.slnx", isActive: false));

        var response = await runtime.Tools.DocumentActive(solutionName: "Shared");

        Assert.False(response.Success);
        Assert.Equal("AmbiguousTarget", response.Metadata!["failureReason"]);
        Assert.Equal("2", response.Metadata["candidateCount"]);
        Assert.Equal("vs-1,vs-2", response.Metadata["candidateSessionIds"]);
    }

    [Fact]
    public async Task DocumentActive_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.DocumentActive(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

    [Fact]
    public async Task CodeDocumentSymbols_RequiresDocumentPath()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.CodeDocumentSymbols("");

        Assert.False(response.Success);
        Assert.Equal("Document path is required.", response.Message);
    }

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
    public async Task ErrorsList_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ErrorsList(
            includeWarnings: false,
            maxItems: 25,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.False(session.LastErrorListRequest!.IncludeWarnings);
        Assert.Equal(25, session.LastErrorListRequest.MaxItems);
        var item = Assert.Single(response.Value!.Items);
        Assert.Equal("Build failed.", item.Description);
        Assert.Equal("Error", item.Level);
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
    public async Task BuildStatus_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.BuildStatus(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

    private static BrokerRuntime CreateRuntime()
    {
        return new BrokerRuntime(BrokerOptions.LocalDefault, new SessionRegistry());
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return CreateRegistration(
            sessionId,
            solutionName,
            $@"C:\Code\{solutionName}\{solutionName}.slnx",
            isActive: true);
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        string solutionPath,
        bool isActive)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: 1234,
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: solutionPath,
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: isActive,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private sealed class FakeVisualStudioSessionRpc : IVisualStudioSessionRpc
    {
        private readonly string _activeDocument;

        public FakeVisualStudioSessionRpc(string activeDocument)
        {
            _activeDocument = activeDocument;
        }

        public string? LastSymbolsDocumentPath { get; private set; }

        public BuildSolutionRequest? LastBuildSolutionRequest { get; private set; }

        public ErrorListRequest? LastErrorListRequest { get; private set; }

        public OutputReadRequest? LastOutputReadRequest { get; private set; }

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<string?>.Ok(_activeDocument));
        }

        public Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
            string documentPath,
            CancellationToken cancellationToken)
        {
            LastSymbolsDocumentPath = documentPath;
            IReadOnlyCollection<string> symbols = ["Editor", "Editor.Run"];
            return Task.FromResult(ToolResponse<IReadOnlyCollection<string>>.Ok(symbols));
        }

        public Task<BuildSolutionResult> BuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            LastBuildSolutionRequest = request;
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildStatusInfo("Idle", 0));
        }

        public Task<ErrorListResult> ErrorsListAsync(
            ErrorListRequest request,
            CancellationToken cancellationToken)
        {
            LastErrorListRequest = request;
            IReadOnlyCollection<ErrorListItemInfo> items =
            [
                new(
                    Description: "Build failed.",
                    File: @"C:\Code\NetVsMcp\Program.cs",
                    Line: 12,
                    Column: 8,
                    Level: "Error",
                    Project: "NetVsMcp")
            ];

            return Task.FromResult(new ErrorListResult(items));
        }

        public Task<OutputReadResult> OutputReadAsync(
            OutputReadRequest request,
            CancellationToken cancellationToken)
        {
            LastOutputReadRequest = request;
            return Task.FromResult(new OutputReadResult(request.PaneName, "Build output", false));
        }
    }
}
