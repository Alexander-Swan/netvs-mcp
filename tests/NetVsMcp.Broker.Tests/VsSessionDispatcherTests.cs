using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using StreamJsonRpc;

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

    [Fact]
    public async Task DispatchAsync_AddsDocumentPathGuidance_WhenRpcPathParsingFails()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        var result = await dispatcher.DispatchAsync<string>(
            new RoutingTarget(SessionId: "vs-1"),
            static (_, _) => throw new ArgumentException("Illegal characters in path."),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.RpcFailure, result.FailureReason);
        Assert.Contains("Use forward slashes", result.Message, StringComparison.Ordinal);
        Assert.Contains("double backslashes", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsUnsupportedByVsix_WhenRemoteMethodIsMissing()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        // RemoteMethodNotFoundException has no public constructor - StreamJsonRpc throws it
        // internally when the remote target has no matching method. Bypass construction to
        // get a real instance for the catch-clause type check under test.
        var missingMethodException = (RemoteMethodNotFoundException)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RemoteMethodNotFoundException));

        var result = await dispatcher.DispatchAsync<string>(
            new RoutingTarget(SessionId: "vs-1"),
            (_, _) => throw missingMethodException,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.UnsupportedByVsix, result.FailureReason);
        Assert.Contains("reinstall the extension", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsOperationTimedOut_WhenOperationExceedsTimeout()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        // Simulates a hung VSIX-side call (like the watch_add design-mode hang this guard was
        // added after): an operation that never completes on its own, only when its token is
        // cancelled - exactly how a well-behaved async RPC call responds to DispatchAsync's
        // internal timeout tripping.
        var result = await dispatcher.DispatchAsync<string>(
            new RoutingTarget(SessionId: "vs-1"),
            static async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return "unreachable";
            },
            CancellationToken.None,
            timeout: TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.Equal(VsSessionDispatchFailureReason.OperationTimedOut, result.FailureReason);
        Assert.Contains("did not respond within", result.Message, StringComparison.Ordinal);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    [Fact]
    public async Task DispatchAsync_Succeeds_WhenOperationCompletesWithinTimeout()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        var result = await dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static (_, _) => Task.FromResult("fast"),
            CancellationToken.None,
            timeout: TimeSpan.FromMilliseconds(50));

        Assert.True(result.Success);
        Assert.Equal("fast", result.Value);
    }

    [Fact]
    public async Task DispatchAsync_PropagatesCancellation_WhenCallersOwnTokenIsCancelled()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: true));
        var connections = new VsSessionConnectionMap();
        connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = CreateDispatcher(registry, connections);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The caller's own cancellation must still surface as a real OperationCanceledException
        // (not get reinterpreted as an internal timeout, and not get swallowed into a Failed
        // result) - callers/ASP.NET rely on this to distinguish "client went away" from "the
        // operation failed".
        await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return "unreachable";
            },
            cts.Token,
            timeout: TimeSpan.FromMinutes(5)));
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

        public Task<ProjectFileResult> ProjectRemoveFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StartupProjectResult> StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestRunAsync(TestRunRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TestOperationResult> TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FindReferencesResult> CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FindImplementationsResult> CodeFindImplementationsAsync(CodePositionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CodeWorkspaceSymbolsResult> CodeWorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CallHierarchyResult> CallHierarchyGetAsync(CallHierarchyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CodeActionsListResult> CodeActionsListAsync(CodeActionsListRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CodeActionsApplyResult> CodeActionsApplyAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<HotReloadApplyResult> DebugHotReloadApplyAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

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

        public Task<TaskListResult> TaskListGetAsync(TaskListRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskListMutationResult> TaskListAddAsync(TaskListAddRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskListMutationResult> TaskListRemoveAsync(TaskListMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TaskListMutationResult> TaskListSetCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

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

        public Task<DocumentCloseResult> DocumentCloseAsync(DocumentCloseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectReferenceResult> ProjectAddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetListResult> NugetListAsync(NugetListRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutputReadResult> OutputWriteAsync(OutputWriteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebugSetVariableResult> DebugSetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProcessTerminateResult> ProcessTerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ThreadCallStackResult> ThreadGetCallstackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
