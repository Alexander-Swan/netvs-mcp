using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class VsSessionDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ReturnsNoRegisteredSessions_WhenRegistryIsEmpty()
    {
        var dispatcher = CreateDispatcher(new SessionRegistry(), new VsSessionConnectionMap());

        var result = await dispatcher.DispatchAsync(
            null,
            static (_, _) => Task.FromResult("unused"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.NoRegisteredSessions, result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsAmbiguousTarget_WhenRegistryCannotChooseSession()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.sln", isActive: false));
        var dispatcher = CreateDispatcher(registry, new VsSessionConnectionMap());

        var result = await dispatcher.DispatchAsync(
            new RoutingTarget(SolutionName: "Shared"),
            static (_, _) => Task.FromResult("unused"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.AmbiguousTarget, result.FailureReason);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsStaleSession_WhenResolvedSessionIsStale()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new SessionRegistry(() => now);
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        now = now.AddMinutes(1);
        var dispatcher = CreateDispatcher(registry, new VsSessionConnectionMap());

        var result = await dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static (_, _) => Task.FromResult("unused"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.StaleSession, result.FailureReason);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsMissingConnection_WhenSessionHasNoConnection()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var dispatcher = CreateDispatcher(registry, new VsSessionConnectionMap());

        var result = await dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static (_, _) => Task.FromResult("unused"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.MissingConnection, result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_InvokesConnection_WhenSessionIsConnected()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        var result = await dispatcher.DispatchAsync(
            new RoutingTarget(SolutionName: "NetVsMcp"),
            static async (connection, cancellationToken) =>
                (await connection.GetActiveDocumentAsync(cancellationToken)).Value,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Program.cs", result.Value);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    private static VsSessionDispatcher CreateDispatcher(
        SessionRegistry registry,
        IVsSessionConnectionMap connections)
    {
        return new VsSessionDispatcher(registry, connections);
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
            return Task.FromResult(ToolResponse<string?>.Ok(_activeDocument));
        }

        public Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
            string documentPath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

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
    }
}
