using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerRegistrationRpcServiceTests
{
    [Fact]
    public async Task RegisterAsync_AddsSessionToRegistry()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);

        var response = await service.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(registry.ListSessions());
        Assert.Equal("NetVsMcp", registry.ListSessions().Single().SolutionName);
    }

    [Fact]
    public async Task RegisterAsync_AddsSessionConnection_WhenConnectionIsAvailable()
    {
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var sessionConnection = new FakeVisualStudioSessionRpc();
        var service = new BrokerRegistrationRpcService(registry, connections, sessionConnection);

        var response = await service.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(connections.TryGet("vs-1", out var connection));
        Assert.Same(sessionConnection, connection);
    }

    [Fact]
    public async Task RegisterAsync_RejectsIncompatibleProtocolVersion()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);

        var response = await service.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp") with { ProtocolVersion = "2.0" },
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.ProtocolMismatch, response.Metadata!["error_code"]);
        Assert.Empty(registry.ListSessions());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "OldName"), CancellationToken.None);

        var response = await service.UpdateAsync(
            new VsSessionUpdate(
                SessionId: "vs-1",
                SolutionName: "NewName",
                SolutionPath: @"C:\Code\NewName\NewName.slnx",
                ActiveDocument: "Services\\BrokerRuntime.cs",
                DebuggerMode: DebuggerMode.Break,
                IsActiveWindow: false,
                Capabilities: [VsCapability.Debugger]),
            CancellationToken.None);

        var session = registry.ListSessions().Single();
        Assert.True(response.Success);
        Assert.Equal("NewName", session.SolutionName);
        Assert.Equal(DebuggerMode.Break, session.DebuggerMode);
        Assert.Contains(VsCapability.Debugger, session.Capabilities);
    }

    [Fact]
    public async Task UpdateAsync_PreservesExistingSessionConnection()
    {
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var sessionConnection = new FakeVisualStudioSessionRpc();
        var service = new BrokerRegistrationRpcService(registry, connections, sessionConnection);
        await service.RegisterAsync(CreateRegistration("vs-1", "OldName"), CancellationToken.None);

        var response = await service.UpdateAsync(
            new VsSessionUpdate(
                SessionId: "vs-1",
                SolutionName: "NewName",
                SolutionPath: @"C:\Code\NewName\NewName.slnx",
                ActiveDocument: "Program.cs",
                DebuggerMode: DebuggerMode.Design,
                IsActiveWindow: true),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(connections.TryGet("vs-1", out var connection));
        Assert.Same(sessionConnection, connection);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.UpdateAsync(
            new VsSessionUpdate(
                SessionId: "missing",
                SolutionName: "Missing",
                SolutionPath: null,
                ActiveDocument: null,
                DebuggerMode: DebuggerMode.Unknown,
                IsActiveWindow: false),
            CancellationToken.None);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task HeartbeatAsync_RefreshesStaleSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);
        Assert.Equal(SessionHealth.Stale, registry.ListSessionStatuses(DateTimeOffset.UtcNow.AddMinutes(1)).Single().Health);

        var response = await service.HeartbeatAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(SessionHealth.Connected, registry.ListSessionStatuses().Single().Health);
    }

    [Fact]
    public async Task HeartbeatAsync_PreservesExistingSessionConnection()
    {
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var sessionConnection = new FakeVisualStudioSessionRpc();
        var service = new BrokerRegistrationRpcService(registry, connections, sessionConnection);
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);

        var response = await service.HeartbeatAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(connections.TryGet("vs-1", out var connection));
        Assert.Same(sessionConnection, connection);
    }

    [Fact]
    public async Task HeartbeatAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.HeartbeatAsync("missing", CancellationToken.None);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task UnregisterAsync_RemovesExistingSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);

        var response = await service.UnregisterAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Empty(registry.ListSessions());
    }

    [Fact]
    public async Task UnregisterAsync_RemovesExistingSessionConnection()
    {
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var service = new BrokerRegistrationRpcService(
            registry,
            connections,
            new FakeVisualStudioSessionRpc());
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);

        var response = await service.UnregisterAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(connections.TryGet("vs-1", out _));
    }

    [Fact]
    public async Task RemoveRegisteredConnections_RemovesConnectionsForDisconnectedPipe()
    {
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var service = new BrokerRegistrationRpcService(
            registry,
            connections,
            new FakeVisualStudioSessionRpc());
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);
        await service.RegisterAsync(CreateRegistration("vs-2", "Other"), CancellationToken.None);

        service.RemoveRegisteredConnections();

        Assert.False(connections.TryGet("vs-1", out _));
        Assert.False(connections.TryGet("vs-2", out _));
        Assert.Equal(2, registry.ListSessions().Count);
    }

    [Fact]
    public async Task UnregisterAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.UnregisterAsync("missing", CancellationToken.None);

        Assert.False(response.Success);
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: 1234,
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: $@"C:\Code\{solutionName}\{solutionName}.slnx",
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private sealed class FakeVisualStudioSessionRpc : IVisualStudioSessionRpc
    {
        public Task<UnsupportedToolResult> PlannedToolAsync(PlannedToolRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
            string documentPath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentCloseResult> DocumentCloseAsync(DocumentCloseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TextSearchResult> EditorFindAsync(EditorFindRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TextSearchResult> FindInFilesAsync(FindInFilesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExecuteCommandResult> ExecuteCommandAsync(ExecuteCommandRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WindowActivateResult> WindowActivateAsync(WindowActivateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ToolWindowResult> ToolWindowShowAsync(ToolWindowRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ToolWindowResult> ToolWindowHideAsync(ToolWindowRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentReadResult> DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EditorDocumentInfo> DocumentOpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentMutationResult> DocumentWriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentMutationResult> DocumentSaveAsync(DocumentSaveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentMutationResult> EditorInsertAsync(EditorInsertRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentMutationResult> EditorReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EditorDocumentInfo> EditorGotoLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SelectionInfo> SelectionSetAsync(SelectionSetRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DocumentCleanupResult> DocumentCleanupAsync(DocumentCleanupRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EditPreviewResult> EditPreviewAsync(EditPreviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EditDecisionResult> EditApproveAsync(EditDecisionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EditDecisionResult> EditRejectAsync(EditDecisionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SolutionInfoResult> SolutionOpenAsync(SolutionOpenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectInfo> SolutionAddProjectAsync(SolutionAddProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectInfo> SolutionRemoveProjectAsync(ProjectInfoRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectInfo?> ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectInfo> ProjectAddFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectReferenceResult> ProjectAddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StartupProjectResult> StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestRunAsync(TestRunRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FindReferencesResult> CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FindImplementationsResult> CodeFindImplementationsAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PackageRestoreResult> PackageRestoreAsync(PackageRestoreRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetListResult> NugetListAsync(NugetListRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildSolutionResult> BuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ErrorListResult> ErrorsListAsync(
            ErrorListRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OutputReadResult> OutputReadAsync(
            OutputReadRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggerStateInfo> DebugStepAsync(
            DebugStepRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BreakpointInfo> BreakpointSetAsync(
            BreakpointSetRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BreakpointRemoveResult> BreakpointRemoveAsync(
            BreakpointRemoveRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BreakpointEnableResult> BreakpointEnableAsync(
            BreakpointEnableRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<EvaluateExpressionResult> DebugEvaluateAsync(
            EvaluateExpressionRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
