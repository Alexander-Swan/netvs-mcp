using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace NetVsMcp.Vsix;

// JSON-RPC error codes in the -32000..-32099 range are reserved for implementation-defined
// server errors per the JSON-RPC 2.0 spec; StreamJsonRpc's own reserved codes stay outside it.
internal static class VsCapabilityRpcErrorCodes
{
    public const int UnhandledCapabilityException = -32050;
}

internal sealed class VisualStudioCapabilityRpcTarget
{
    private readonly EditorRpcTarget editor;
    private readonly GeneralIdeRpcTarget generalIde;
    private readonly NavigationRpcTarget navigation;
    private readonly CodeActionsRpcTarget codeActions;
    private readonly BuildRpcTarget build;
    private readonly DebuggerRpcTarget debugger;
    private readonly IAutomationCapabilityService automation;
    private readonly SolutionRpcTarget solution;
    private readonly IVisualStudioSessionSnapshotProvider snapshotProvider;
    private readonly IVisualStudioCapabilityCatalog capabilities;

    public VisualStudioCapabilityRpcTarget(
        IVisualStudioCapabilityCatalog capabilities,
        IVisualStudioSessionSnapshotProvider snapshotProvider)
    {
        this.capabilities = capabilities;
        this.snapshotProvider = snapshotProvider;
        generalIde = new GeneralIdeRpcTarget(capabilities.GeneralIde);
        editor = new EditorRpcTarget(capabilities.Editor);
        navigation = new NavigationRpcTarget(capabilities.Navigation);
        codeActions = new CodeActionsRpcTarget(capabilities.CodeActions);
        build = new BuildRpcTarget(capabilities.Build);
        debugger = new DebuggerRpcTarget(capabilities.Debugger);
        automation = capabilities.Automation;
        solution = new SolutionRpcTarget(capabilities.Solution);
    }

    public async Task<NetVsMcp.Contracts.ToolResponse<NetVsMcp.Contracts.VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);
            return NetVsMcp.Contracts.ToolResponse<NetVsMcp.Contracts.VsSessionInfo>.Ok(VsContractMapping.ToSessionInfo(snapshot, capabilities));
        }
        catch (Exception ex)
        {
            return NetVsMcp.Contracts.ToolResponse<NetVsMcp.Contracts.VsSessionInfo>.Fail(ex.Message);
        }
    }

    public async Task<NetVsMcp.Contracts.ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var document = await editor.DocumentActiveAsync(cancellationToken);
            return NetVsMcp.Contracts.ToolResponse<string?>.Ok(document?.Path ?? document?.Name);
        }
        catch (Exception ex)
        {
            return NetVsMcp.Contracts.ToolResponse<string?>.Fail(ex.Message);
        }
    }

    public async Task<NetVsMcp.Contracts.ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
        string documentPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new DocumentSymbolsRequest
            {
                DocumentPath = string.IsNullOrWhiteSpace(documentPath) ? null : documentPath
            };
            var result = await navigation.CodeDocumentSymbolsAsync(request, cancellationToken);
            var symbols = result.Symbols.Select(FormatSymbolLabel).ToArray();
            return NetVsMcp.Contracts.ToolResponse<IReadOnlyCollection<string>>.Ok(symbols);
        }
        catch (Exception ex)
        {
            return NetVsMcp.Contracts.ToolResponse<IReadOnlyCollection<string>>.Fail(ex.Message);
        }
    }

    public Task<EditorDocumentInfo?> DocumentActiveAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentActiveAsync(cancellationToken), nameof(DocumentActiveAsync));

    public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentListAsync(cancellationToken), nameof(DocumentListAsync));

    public Task<DocumentCloseResult> DocumentCloseAsync(DocumentCloseRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentCloseAsync(request, cancellationToken), nameof(DocumentCloseAsync));

    public Task<ExecuteCommandResult> ExecuteCommandAsync(ExecuteCommandRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => generalIde.ExecuteCommandAsync(request, cancellationToken), nameof(ExecuteCommandAsync));

    public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => generalIde.WindowListAsync(cancellationToken), nameof(WindowListAsync));

    public Task<WindowActivateResult> WindowActivateAsync(WindowActivateRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => generalIde.WindowActivateAsync(request, cancellationToken), nameof(WindowActivateAsync));

    public Task<ToolWindowResult> ToolWindowShowAsync(ToolWindowRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => generalIde.ToolWindowShowAsync(request, cancellationToken), nameof(ToolWindowShowAsync));

    public Task<ToolWindowResult> ToolWindowHideAsync(ToolWindowRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => generalIde.ToolWindowHideAsync(request, cancellationToken), nameof(ToolWindowHideAsync));

    public Task<DocumentReadResult> DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentReadAsync(request, cancellationToken), nameof(DocumentReadAsync));

    public Task<TextSearchResult> EditorFindAsync(EditorFindRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditorFindAsync(request, cancellationToken), nameof(EditorFindAsync));

    public Task<TextSearchResult> FindInFilesAsync(FindInFilesRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.FindInFilesAsync(request, cancellationToken), nameof(FindInFilesAsync));

    public Task<EditorDocumentInfo> DocumentOpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentOpenAsync(request, cancellationToken), nameof(DocumentOpenAsync));

    public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.SelectionGetAsync(cancellationToken), nameof(SelectionGetAsync));

    public Task<DocumentMutationResult> DocumentWriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentWriteAsync(request, cancellationToken), nameof(DocumentWriteAsync));

    public Task<DocumentMutationResult> DocumentSaveAsync(DocumentSaveRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentSaveAsync(request, cancellationToken), nameof(DocumentSaveAsync));

    public Task<DocumentMutationResult> EditorInsertAsync(EditorInsertRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditorInsertAsync(request, cancellationToken), nameof(EditorInsertAsync));

    public Task<DocumentMutationResult> EditorReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditorReplaceAsync(request, cancellationToken), nameof(EditorReplaceAsync));

    public Task<EditorDocumentInfo> EditorGotoLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditorGotoLineAsync(request, cancellationToken), nameof(EditorGotoLineAsync));

    public Task<SelectionInfo> SelectionSetAsync(SelectionSetRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.SelectionSetAsync(request, cancellationToken), nameof(SelectionSetAsync));

    public Task<DocumentCleanupResult> DocumentCleanupAsync(DocumentCleanupRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.DocumentCleanupAsync(request, cancellationToken), nameof(DocumentCleanupAsync));

    public Task<EditPreviewResult> EditPreviewAsync(EditPreviewRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditPreviewAsync(request, cancellationToken), nameof(EditPreviewAsync));

    public Task<EditDecisionResult> EditApproveAsync(EditDecisionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditApproveAsync(request, cancellationToken), nameof(EditApproveAsync));

    public Task<EditDecisionResult> EditRejectAsync(EditDecisionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditRejectAsync(request, cancellationToken), nameof(EditRejectAsync));

    public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => editor.EditListPendingAsync(cancellationToken), nameof(EditListPendingAsync));

    public Task<DocumentSymbolsResult> CodeDocumentSymbolsAsync(DocumentSymbolsRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeDocumentSymbolsAsync(request, cancellationToken), nameof(CodeDocumentSymbolsAsync));

    public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeGoToDefinitionAsync(request, cancellationToken), nameof(CodeGoToDefinitionAsync));

    public Task<FindReferencesResult> CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeFindReferencesAsync(request, cancellationToken), nameof(CodeFindReferencesAsync));

    public Task<FindImplementationsResult> CodeFindImplementationsAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeFindImplementationsAsync(request, cancellationToken), nameof(CodeFindImplementationsAsync));

    public Task<CodeWorkspaceSymbolsResult> CodeWorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeWorkspaceSymbolsAsync(request, cancellationToken), nameof(CodeWorkspaceSymbolsAsync));

    public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CodeRenameSymbolPreviewAsync(request, cancellationToken), nameof(CodeRenameSymbolPreviewAsync));

    public Task<RenameSymbolApplyResult> CodeRenameSymbolApplyAsync(RenameSymbolRequest request, CancellationToken cancellationToken) =>
        navigation.CodeRenameSymbolApplyAsync(request, cancellationToken);

    public Task<CallHierarchyResult> CallHierarchyGetAsync(CallHierarchyRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => navigation.CallHierarchyGetAsync(request, cancellationToken), nameof(CallHierarchyGetAsync));

    public Task<CodeActionsListResult> CodeActionsListAsync(CodeActionsListRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => codeActions.CodeActionsListAsync(request, cancellationToken), nameof(CodeActionsListAsync));

    public Task<CodeActionsApplyResult> CodeActionsApplyAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => codeActions.CodeActionsApplyAsync(request, cancellationToken), nameof(CodeActionsApplyAsync));

    public Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildSolutionAsync(request, cancellationToken), nameof(BuildSolutionAsync));

    public Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildProjectAsync(request, cancellationToken), nameof(BuildProjectAsync));

    public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildCancelAsync(cancellationToken), nameof(BuildCancelAsync));

    public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => build.CleanSolutionAsync(cancellationToken), nameof(CleanSolutionAsync));

    public Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.RebuildSolutionAsync(request, cancellationToken), nameof(RebuildSolutionAsync));

    public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildStatusAsync(cancellationToken), nameof(BuildStatusAsync));

    public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildConfigurationGetAsync(cancellationToken), nameof(BuildConfigurationGetAsync));

    public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.BuildConfigurationSetAsync(request, cancellationToken), nameof(BuildConfigurationSetAsync));

    public Task<ErrorListResult> ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.ErrorsListAsync(request, cancellationToken), nameof(ErrorsListAsync));

    public Task<TaskListResult> TaskListGetAsync(TaskListRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.TaskListGetAsync(request, cancellationToken), nameof(TaskListGetAsync));

    public Task<TaskListMutationResult> TaskListAddAsync(TaskListAddRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.TaskListAddAsync(request, cancellationToken), nameof(TaskListAddAsync));

    public Task<TaskListMutationResult> TaskListRemoveAsync(TaskListMutationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.TaskListRemoveAsync(request, cancellationToken), nameof(TaskListRemoveAsync));

    public Task<TaskListMutationResult> TaskListSetCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.TaskListSetCheckedAsync(request, cancellationToken), nameof(TaskListSetCheckedAsync));

    public Task<OutputReadResult> OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.OutputReadAsync(request, cancellationToken), nameof(OutputReadAsync));

    public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => build.OutputListPanesAsync(cancellationToken), nameof(OutputListPanesAsync));

    public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.OutputClearAsync(request, cancellationToken), nameof(OutputClearAsync));

    public Task<OutputReadResult> OutputWriteAsync(OutputWriteRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => build.OutputWriteAsync(request, cancellationToken), nameof(OutputWriteAsync));

    public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugStatusAsync(cancellationToken), nameof(DebugStatusAsync));

    public Task<HotReloadApplyResult> DebugHotReloadApplyAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugHotReloadApplyAsync(cancellationToken), nameof(DebugHotReloadApplyAsync));

    public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugGetModeAsync(cancellationToken), nameof(DebugGetModeAsync));

    public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugStartAsync(cancellationToken), nameof(DebugStartAsync));

    public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugStartWithoutDebuggingAsync(cancellationToken), nameof(DebugStartWithoutDebuggingAsync));

    public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugRestartAsync(cancellationToken), nameof(DebugRestartAsync));

    public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugStopAsync(cancellationToken), nameof(DebugStopAsync));

    public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugContinueAsync(cancellationToken), nameof(DebugContinueAsync));

    public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugBreakAsync(cancellationToken), nameof(DebugBreakAsync));

    public Task<DebuggerStateInfo> DebugStepAsync(DebugStepRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugStepAsync(request, cancellationToken), nameof(DebugStepAsync));

    public Task<BreakpointInfo> BreakpointSetAsync(BreakpointSetRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.BreakpointSetAsync(request, cancellationToken), nameof(BreakpointSetAsync));

    public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.BreakpointListAsync(cancellationToken), nameof(BreakpointListAsync));

    public Task<BreakpointRemoveResult> BreakpointRemoveAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.BreakpointRemoveAsync(request, cancellationToken), nameof(BreakpointRemoveAsync));

    public Task<BreakpointEnableResult> BreakpointEnableAsync(BreakpointEnableRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.BreakpointEnableAsync(request, cancellationToken), nameof(BreakpointEnableAsync));

    public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugGetCallstackAsync(cancellationToken), nameof(DebugGetCallstackAsync));

    public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugGetLocalsAsync(cancellationToken), nameof(DebugGetLocalsAsync));

    public Task<EvaluateExpressionResult> DebugEvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugEvaluateAsync(request, cancellationToken), nameof(DebugEvaluateAsync));

    public Task<DebugSetVariableResult> DebugSetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugSetVariableAsync(request, cancellationToken), nameof(DebugSetVariableAsync));

    public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.WatchAddAsync(request, cancellationToken), nameof(WatchAddAsync));

    public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.WatchRemoveAsync(request, cancellationToken), nameof(WatchRemoveAsync));

    public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.WatchListAsync(cancellationToken), nameof(WatchListAsync));

    public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugGetThreadsAsync(cancellationToken), nameof(DebugGetThreadsAsync));

    public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ProcessListDebuggedAsync(cancellationToken), nameof(ProcessListDebuggedAsync));

    public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ProcessListLocalAsync(cancellationToken), nameof(ProcessListLocalAsync));

    public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.DebugAttachAsync(request, cancellationToken), nameof(DebugAttachAsync));

    public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ProcessDetachAsync(request, cancellationToken), nameof(ProcessDetachAsync));

    public Task<ProcessTerminateResult> ProcessTerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ProcessTerminateAsync(request, cancellationToken), nameof(ProcessTerminateAsync));

    public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ThreadSwitchAsync(request, cancellationToken), nameof(ThreadSwitchAsync));

    public Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ThreadSetFrozenAsync(request, cancellationToken), nameof(ThreadSetFrozenAsync));

    public Task<ThreadCallStackResult> ThreadGetCallstackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ThreadGetCallstackAsync(request, cancellationToken), nameof(ThreadGetCallstackAsync));

    public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ModuleListAsync(cancellationToken), nameof(ModuleListAsync));

    public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ImmediateExecuteAsync(request, cancellationToken), nameof(ImmediateExecuteAsync));

    public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ExceptionSettingsGetAsync(request, cancellationToken), nameof(ExceptionSettingsGetAsync));

    public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ExceptionSettingsSetAsync(request, cancellationToken), nameof(ExceptionSettingsSetAsync));

    public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ParallelStacksAsync(cancellationToken), nameof(ParallelStacksAsync));

    public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => debugger.ParallelWatchAsync(cancellationToken), nameof(ParallelWatchAsync));

    public Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.ConsoleReadAsync(request, cancellationToken), nameof(ConsoleReadAsync));

    public Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.DiagnosticsBindingErrorsAsync(request, cancellationToken), nameof(DiagnosticsBindingErrorsAsync));

    public Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.ConsoleSendAsync(request, cancellationToken), nameof(ConsoleSendAsync));

    public Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.ConsoleGetInfoAsync(request, cancellationToken), nameof(ConsoleGetInfoAsync));

    public Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiCaptureWindowAsync(request, cancellationToken), nameof(UiCaptureWindowAsync));

    public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiCaptureRegionAsync(request, cancellationToken), nameof(UiCaptureRegionAsync));

    public Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiSnapshotAsync(request, cancellationToken), nameof(UiSnapshotAsync));

    public Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiGetTreeAsync(request, cancellationToken), nameof(UiGetTreeAsync));

    public Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiFindElementsAsync(request, cancellationToken), nameof(UiFindElementsAsync));

    public Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiGetElementAsync(request, cancellationToken), nameof(UiGetElementAsync));

    public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiClickAsync(request, cancellationToken), nameof(UiClickAsync));

    public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiDoubleClickAsync(request, cancellationToken), nameof(UiDoubleClickAsync));

    public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiRightClickAsync(request, cancellationToken), nameof(UiRightClickAsync));

    public Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiDragAsync(request, cancellationToken), nameof(UiDragAsync));

    public Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiSetValueAsync(request, cancellationToken), nameof(UiSetValueAsync));

    public Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiInvokeAsync(request, cancellationToken), nameof(UiInvokeAsync));

    public Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiSendKeysAsync(request, cancellationToken), nameof(UiSendKeysAsync));

    public Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiWaitForElementAsync(request, cancellationToken), nameof(UiWaitForElementAsync));

    public Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.UiWaitIdleAsync(request, cancellationToken), nameof(UiWaitIdleAsync));

    public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebConnectAsync(request, cancellationToken), nameof(WebConnectAsync));

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebDisconnectAsync(request, cancellationToken), nameof(WebDisconnectAsync));

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebStatusAsync(request, cancellationToken), nameof(WebStatusAsync));

    public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebNavigateAsync(request, cancellationToken), nameof(WebNavigateAsync));

    public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebScreenshotAsync(request, cancellationToken), nameof(WebScreenshotAsync));

    public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebDomGetAsync(request, cancellationToken), nameof(WebDomGetAsync));

    public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebDomQueryAsync(request, cancellationToken), nameof(WebDomQueryAsync));

    public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebConsoleAsync(request, cancellationToken), nameof(WebConsoleAsync));

    public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebJsExecuteAsync(request, cancellationToken), nameof(WebJsExecuteAsync));

    public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebNetworkAsync(request, cancellationToken), nameof(WebNetworkAsync));

    public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebElementClickAsync(request, cancellationToken), nameof(WebElementClickAsync));

    public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => automation.WebElementSetValueAsync(request, cancellationToken), nameof(WebElementSetValueAsync));

    public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.SolutionInfoAsync(cancellationToken), nameof(SolutionInfoAsync));

    public Task<SolutionInfoResult> SolutionOpenAsync(SolutionOpenRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.SolutionOpenAsync(request, cancellationToken), nameof(SolutionOpenAsync));

    public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.SolutionCloseAsync(cancellationToken), nameof(SolutionCloseAsync));

    public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectListAsync(cancellationToken), nameof(ProjectListAsync));

    public Task<ProjectInfo> SolutionAddProjectAsync(SolutionAddProjectRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.SolutionAddProjectAsync(request, cancellationToken), nameof(SolutionAddProjectAsync));

    public Task<ProjectInfo> SolutionRemoveProjectAsync(ProjectInfoRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.SolutionRemoveProjectAsync(request, cancellationToken), nameof(SolutionRemoveProjectAsync));

    public Task<ProjectInfo?> ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectInfoAsync(request, cancellationToken), nameof(ProjectInfoAsync));

    public Task<ProjectInfo> ProjectAddFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectAddFileAsync(request, cancellationToken), nameof(ProjectAddFileAsync));

    public Task<ProjectFileResult> ProjectRemoveFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectRemoveFileAsync(request, cancellationToken), nameof(ProjectRemoveFileAsync));

    public Task<ProjectReferenceResult> ProjectAddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectAddReferenceAsync(request, cancellationToken), nameof(ProjectAddReferenceAsync));

    public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.ProjectRemoveReferenceAsync(request, cancellationToken), nameof(ProjectRemoveReferenceAsync));

    public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.StartupProjectGetAsync(cancellationToken), nameof(StartupProjectGetAsync));

    public Task<StartupProjectResult> StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.StartupProjectSetAsync(request, cancellationToken), nameof(StartupProjectSetAsync));

    public Task<TestOperationResult> TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.TestDiscoverAsync(request, cancellationToken), nameof(TestDiscoverAsync));

    public Task<TestOperationResult> TestRunAsync(TestRunRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.TestRunAsync(request, cancellationToken), nameof(TestRunAsync));

    public Task<TestDebugResult> TestDebugAsync(TestDebugRequest request, CancellationToken cancellationToken) =>
        solution.TestDebugAsync(request, cancellationToken);

    public Task<TestOperationResult> TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.TestResultsAsync(request, cancellationToken), nameof(TestResultsAsync));

    public Task<PackageRestoreResult> PackageRestoreAsync(PackageRestoreRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.PackageRestoreAsync(request, cancellationToken), nameof(PackageRestoreAsync));

    public Task<NugetListResult> NugetListAsync(NugetListRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.NugetListAsync(request, cancellationToken), nameof(NugetListAsync));

    public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.NugetSearchAsync(request, cancellationToken), nameof(NugetSearchAsync));

    public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.NugetInstallAsync(request, cancellationToken), nameof(NugetInstallAsync));

    public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.NugetUpdateAsync(request, cancellationToken), nameof(NugetUpdateAsync));

    public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        InvokeAsync(() => solution.NugetUninstallAsync(request, cancellationToken), nameof(NugetUninstallAsync));

    // Uniform exception-handling boundary: the delegate methods below forward
    // straight into a capability service with no local try/catch. Without this, an unexpected
    // exception (COMException, NullReferenceException from a stale DTE reference, etc.) would
    // propagate out of the target object that StreamJsonRpc invokes and cross the wire as a
    // default-serialized fault - noisy, inconsistent with the structured ToolResponseWire.Fail(...)
    // shape the 3 legacy methods above return, and unlogged on the VSIX side. Routing every
    // delegate through this single helper means a new tool method gets the same protection for
    // free, without every capability author needing to remember to catch locally.
    private static async Task<T> InvokeAsync<T>(Func<Task<T>> operation, string rpcMethodName)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp: RPC method '{rpcMethodName}' threw {ex.GetType().Name}: {ex.Message}");
            throw new LocalRpcException($"NetVsMcp VSIX: '{rpcMethodName}' failed: {ex.Message}")
            {
                ErrorCode = VsCapabilityRpcErrorCodes.UnhandledCapabilityException,
            };
        }
    }

    private static string FormatSymbolLabel(DocumentSymbolInfo symbol)
    {
        var scope = symbol.ContainingType ?? symbol.ContainingNamespace;
        var qualifiedName = string.IsNullOrWhiteSpace(scope)
            ? symbol.Name
            : $"{scope}.{symbol.Name}";

        return $"{qualifiedName} ({symbol.Kind}) {symbol.File}:{symbol.Line}:{symbol.Column}";
    }

}
