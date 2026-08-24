namespace NetVsMcp.Contracts;

/// <summary>
/// The full RPC surface the broker calls on a registered VS session to execute MCP tools. Implemented
/// VS-side by <c>VisualStudioCapabilityRpcTarget</c> and its per-category <c>*RpcTarget</c> partners.
/// </summary>
public interface IVisualStudioSessionRpc
{
    Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken);

    Task<ExecuteCommandResult> ExecuteCommandAsync(
        ExecuteCommandRequest request,
        CancellationToken cancellationToken);

    Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken);

    Task<WindowActivateResult> WindowActivateAsync(
        WindowActivateRequest request,
        CancellationToken cancellationToken);

    Task<ToolWindowResult> ToolWindowShowAsync(
        ToolWindowRequest request,
        CancellationToken cancellationToken);

    Task<ToolWindowResult> ToolWindowHideAsync(
        ToolWindowRequest request,
        CancellationToken cancellationToken);

    Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken);

    Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
        string documentPath,
        CancellationToken cancellationToken);

    Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken);

    Task<DocumentCloseResult> DocumentCloseAsync(
        DocumentCloseRequest request,
        CancellationToken cancellationToken);

    Task<TextSearchResult> EditorFindAsync(
        EditorFindRequest request,
        CancellationToken cancellationToken);

    Task<TextSearchResult> FindInFilesAsync(
        FindInFilesRequest request,
        CancellationToken cancellationToken);

    Task<DocumentReadResult> DocumentReadAsync(
        DocumentReadRequest request,
        CancellationToken cancellationToken);

    Task<EditorDocumentInfo> DocumentOpenAsync(
        DocumentOpenRequest request,
        CancellationToken cancellationToken);

    Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken);

    Task<DocumentMutationResult> DocumentWriteAsync(
        DocumentWriteRequest request,
        CancellationToken cancellationToken);

    Task<DocumentMutationResult> DocumentSaveAsync(
        DocumentSaveRequest request,
        CancellationToken cancellationToken);

    Task<DocumentMutationResult> EditorInsertAsync(
        EditorInsertRequest request,
        CancellationToken cancellationToken);

    Task<DocumentMutationResult> EditorReplaceAsync(
        EditorReplaceRequest request,
        CancellationToken cancellationToken);

    Task<EditorDocumentInfo> EditorGotoLineAsync(
        EditorGotoLineRequest request,
        CancellationToken cancellationToken);

    Task<SelectionInfo> SelectionSetAsync(
        SelectionSetRequest request,
        CancellationToken cancellationToken);

    Task<DocumentCleanupResult> DocumentCleanupAsync(
        DocumentCleanupRequest request,
        CancellationToken cancellationToken);

    Task<EditPreviewResult> EditPreviewAsync(
        EditPreviewRequest request,
        CancellationToken cancellationToken);

    Task<EditDecisionResult> EditApproveAsync(
        EditDecisionRequest request,
        CancellationToken cancellationToken);

    Task<EditDecisionResult> EditRejectAsync(
        EditDecisionRequest request,
        CancellationToken cancellationToken);

    Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken);

    Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken);

    Task<SolutionInfoResult> SolutionOpenAsync(
        SolutionOpenRequest request,
        CancellationToken cancellationToken);

    Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken);

    Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken);

    Task<ProjectInfo> SolutionAddProjectAsync(
        SolutionAddProjectRequest request,
        CancellationToken cancellationToken);

    Task<ProjectInfo> SolutionRemoveProjectAsync(
        ProjectInfoRequest request,
        CancellationToken cancellationToken);

    Task<ProjectInfo?> ProjectInfoAsync(
        ProjectInfoRequest request,
        CancellationToken cancellationToken);

    Task<ProjectInfo> ProjectAddFileAsync(
        ProjectFileRequest request,
        CancellationToken cancellationToken);

    Task<ProjectFileResult> ProjectRemoveFileAsync(
        ProjectFileRequest request,
        CancellationToken cancellationToken);

    Task<ProjectReferenceResult> ProjectAddReferenceAsync(
        ProjectReferenceRequest request,
        CancellationToken cancellationToken);

    Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(
        ProjectReferenceRequest request,
        CancellationToken cancellationToken);

    Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken);

    Task<StartupProjectResult> StartupProjectSetAsync(
        StartupProjectSetRequest request,
        CancellationToken cancellationToken);

    Task<TestOperationResult> TestDiscoverAsync(
        TestDiscoverRequest request,
        CancellationToken cancellationToken);

    Task<TestOperationResult> TestRunAsync(
        TestRunRequest request,
        CancellationToken cancellationToken);

    Task<TestDebugResult> TestDebugAsync(
        TestDebugRequest request,
        CancellationToken cancellationToken);

    Task<TestOperationResult> TestResultsAsync(
        TestResultsRequest request,
        CancellationToken cancellationToken);

    Task<GoToDefinitionResult> CodeGoToDefinitionAsync(
        CodePositionRequest request,
        CancellationToken cancellationToken);

    Task<FindReferencesResult> CodeFindReferencesAsync(
        CodePositionRequest request,
        CancellationToken cancellationToken);

    Task<FindImplementationsResult> CodeFindImplementationsAsync(
        CodePositionRequest request,
        CancellationToken cancellationToken);

    Task<CodeWorkspaceSymbolsResult> CodeWorkspaceSymbolsAsync(
        CodeWorkspaceSymbolsRequest request,
        CancellationToken cancellationToken);

    Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(
        RenameSymbolRequest request,
        CancellationToken cancellationToken);

    Task<RenameSymbolApplyResult> CodeRenameSymbolApplyAsync(
        RenameSymbolRequest request,
        CancellationToken cancellationToken);

    Task<CallHierarchyResult> CallHierarchyGetAsync(
        CallHierarchyRequest request,
        CancellationToken cancellationToken);

    Task<CodeActionsListResult> CodeActionsListAsync(
        CodeActionsListRequest request,
        CancellationToken cancellationToken);

    Task<CodeActionsApplyResult> CodeActionsApplyAsync(
        CodeActionsApplyRequest request,
        CancellationToken cancellationToken);

    Task<PackageRestoreResult> PackageRestoreAsync(
        PackageRestoreRequest request,
        CancellationToken cancellationToken);

    Task<NugetListResult> NugetListAsync(
        NugetListRequest request,
        CancellationToken cancellationToken);

    Task<NugetSearchResult> NugetSearchAsync(
        NugetSearchRequest request,
        CancellationToken cancellationToken);

    Task<NugetMutationResult> NugetInstallAsync(
        NugetPackageMutationRequest request,
        CancellationToken cancellationToken);

    Task<NugetMutationResult> NugetUpdateAsync(
        NugetPackageMutationRequest request,
        CancellationToken cancellationToken);

    Task<NugetMutationResult> NugetUninstallAsync(
        NugetPackageMutationRequest request,
        CancellationToken cancellationToken);

    Task<BuildSolutionResult> BuildSolutionAsync(
        BuildSolutionRequest request,
        CancellationToken cancellationToken);

    Task<BuildSolutionResult> BuildProjectAsync(
        BuildProjectRequest request,
        CancellationToken cancellationToken);

    Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken);

    Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken);

    Task<BuildSolutionResult> RebuildSolutionAsync(
        BuildSolutionRequest request,
        CancellationToken cancellationToken);

    Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken);

    Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken);

    Task<BuildConfigurationInfo> BuildConfigurationSetAsync(
        BuildConfigurationSetRequest request,
        CancellationToken cancellationToken);

    Task<ErrorListResult> ErrorsListAsync(
        ErrorListRequest request,
        CancellationToken cancellationToken);

    Task<TaskListResult> TaskListGetAsync(
        TaskListRequest request,
        CancellationToken cancellationToken);

    Task<TaskListMutationResult> TaskListAddAsync(
        TaskListAddRequest request,
        CancellationToken cancellationToken);

    Task<TaskListMutationResult> TaskListRemoveAsync(
        TaskListMutationRequest request,
        CancellationToken cancellationToken);

    Task<TaskListMutationResult> TaskListSetCheckedAsync(
        TaskListSetCheckedRequest request,
        CancellationToken cancellationToken);

    Task<OutputReadResult> OutputReadAsync(
        OutputReadRequest request,
        CancellationToken cancellationToken);

    Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken);

    Task<OutputReadResult> OutputClearAsync(
        OutputPaneRequest request,
        CancellationToken cancellationToken);

    Task<OutputReadResult> OutputWriteAsync(
        OutputWriteRequest request,
        CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken);

    Task<HotReloadApplyResult> DebugHotReloadApplyAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStepAsync(
        DebugStepRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointInfo> BreakpointSetAsync(
        BreakpointSetRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken);

    Task<BreakpointRemoveResult> BreakpointRemoveAsync(
        BreakpointRemoveRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointEnableResult> BreakpointEnableAsync(
        BreakpointEnableRequest request,
        CancellationToken cancellationToken);

    Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken);

    Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken);

    Task<EvaluateExpressionResult> DebugEvaluateAsync(
        EvaluateExpressionRequest request,
        CancellationToken cancellationToken);

    Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken);

    Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken);

    Task<DebugSetVariableResult> DebugSetVariableAsync(
        DebugSetVariableRequest request,
        CancellationToken cancellationToken);

    Task<DebugAttachResult> DebugAttachAsync(
        DebugAttachRequest request,
        CancellationToken cancellationToken);

    Task<ProcessDetachResult> ProcessDetachAsync(
        ProcessDetachRequest request,
        CancellationToken cancellationToken);

    Task<ProcessTerminateResult> ProcessTerminateAsync(
        ProcessTerminateRequest request,
        CancellationToken cancellationToken);

    Task<WatchOperationResult> WatchAddAsync(
        WatchAddRequest request,
        CancellationToken cancellationToken);

    Task<WatchOperationResult> WatchRemoveAsync(
        WatchRemoveRequest request,
        CancellationToken cancellationToken);

    Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken);

    Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken);

    Task<ThreadSwitchResult> ThreadSwitchAsync(
        ThreadSwitchRequest request,
        CancellationToken cancellationToken);

    Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(
        ThreadSetFrozenRequest request,
        CancellationToken cancellationToken);

    Task<ThreadCallStackResult> ThreadGetCallstackAsync(
        ThreadCallStackRequest request,
        CancellationToken cancellationToken);

    Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken);

    Task<ImmediateExecuteResult> ImmediateExecuteAsync(
        ImmediateExecuteRequest request,
        CancellationToken cancellationToken);

    Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(
        ExceptionSettingsRequest request,
        CancellationToken cancellationToken);

    Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(
        ExceptionSettingsRequest request,
        CancellationToken cancellationToken);

    Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken);

    Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken);

    Task<AutomationResult> ConsoleReadAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> DiagnosticsBindingErrorsAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> ConsoleSendAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> ConsoleGetInfoAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiCaptureWindowAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiCaptureRegionAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiSnapshotAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiGetTreeAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiFindElementsAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiGetElementAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiClickAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiDoubleClickAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiRightClickAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiDragAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiSetValueAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiInvokeAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiSendKeysAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiWaitForElementAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> UiWaitIdleAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebConnectAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebDisconnectAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebStatusAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebNavigateAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebScreenshotAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebDomGetAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebDomQueryAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebConsoleAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebJsExecuteAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebNetworkAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebElementClickAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);

    Task<AutomationResult> WebElementSetValueAsync(
        AutomationRequest request,
        CancellationToken cancellationToken);
}
