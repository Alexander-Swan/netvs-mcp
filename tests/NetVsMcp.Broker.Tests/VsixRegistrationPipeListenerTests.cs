using System.IO.Pipes;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Tests;

public sealed class VsixRegistrationPipeListenerTests
{
    [Fact]
    public async Task RegisteredPipeSession_IsAddedToConnectionMapAndRemovedOnDisconnect()
    {
        var pipeName = $"netvs-mcp-test-{Guid.NewGuid():N}";
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        await using var listener = new VsixRegistrationPipeListener(
            new BrokerOptions("http://127.0.0.1:0", $@"\\.\pipe\{pipeName}"),
            registry,
            connections);

        await listener.StartAsync(CancellationToken.None);

        await using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(5000);

        using var jsonRpc = new JsonRpc(clientPipe);
        jsonRpc.AddLocalRpcTarget<IVisualStudioSessionRpc>(
            new FakeVisualStudioSessionRpc("Program.cs"),
            options: null);
        var registration = jsonRpc.Attach<IBrokerRegistrationRpc>();
        jsonRpc.StartListening();

        var response = await registration.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(connections.TryGet("vs-1", out _));

        var dispatcher = new VsSessionDispatcher(registry, connections);
        var dispatch = await dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static async (connection, cancellationToken) =>
                (await connection.GetActiveDocumentAsync(cancellationToken)).Value,
            CancellationToken.None);

        Assert.True(dispatch.Success);
        Assert.Equal("Program.cs", dispatch.Value);

        jsonRpc.Dispose();
        clientPipe.Dispose();

        await WaitForAsync(() => !connections.TryGet("vs-1", out _));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            await Task.Delay(25, cancellation.Token);
        }
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

        public Task<TaskListResult> TaskListGetAsync(
            TaskListRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TaskListMutationResult> TaskListAddAsync(
            TaskListAddRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TaskListMutationResult> TaskListRemoveAsync(
            TaskListMutationRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TaskListMutationResult> TaskListSetCheckedAsync(
            TaskListSetCheckedRequest request,
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
