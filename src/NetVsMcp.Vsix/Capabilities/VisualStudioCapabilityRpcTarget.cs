using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class VisualStudioCapabilityRpcTarget
{
    private readonly EditorRpcTarget editor;
    private readonly GeneralIdeRpcTarget generalIde;
    private readonly NavigationRpcTarget navigation;
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
        build = new BuildRpcTarget(capabilities.Build);
        debugger = new DebuggerRpcTarget(capabilities.Debugger);
        automation = capabilities.Automation;
        solution = new SolutionRpcTarget(capabilities.Solution);
    }

    public async Task<ToolResponseWire<VsSessionInfoWire>> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await snapshotProvider.CaptureAsync(cancellationToken);
            return ToolResponseWire<VsSessionInfoWire>.Ok(VsSessionInfoWire.FromSnapshot(snapshot, capabilities));
        }
        catch (Exception ex)
        {
            return ToolResponseWire<VsSessionInfoWire>.Fail(ex.Message);
        }
    }

    public async Task<ToolResponseWire<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var document = await editor.DocumentActiveAsync(cancellationToken);
            return ToolResponseWire<string?>.Ok(document?.Path ?? document?.Name);
        }
        catch (Exception ex)
        {
            return ToolResponseWire<string?>.Fail(ex.Message);
        }
    }

    public async Task<ToolResponseWire<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
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
            return ToolResponseWire<IReadOnlyCollection<string>>.Ok(symbols);
        }
        catch (Exception ex)
        {
            return ToolResponseWire<IReadOnlyCollection<string>>.Fail(ex.Message);
        }
    }

    public Task<EditorDocumentInfo?> DocumentActiveAsync(CancellationToken cancellationToken) =>
        editor.DocumentActiveAsync(cancellationToken);

    public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken) =>
        editor.DocumentListAsync(cancellationToken);

    public Task<DocumentCloseResult> DocumentCloseAsync(DocumentCloseRequest request, CancellationToken cancellationToken) =>
        editor.DocumentCloseAsync(request, cancellationToken);

    public Task<ExecuteCommandResult> ExecuteCommandAsync(ExecuteCommandRequest request, CancellationToken cancellationToken) =>
        generalIde.ExecuteCommandAsync(request, cancellationToken);

    public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken) =>
        generalIde.WindowListAsync(cancellationToken);

    public Task<WindowActivateResult> WindowActivateAsync(WindowActivateRequest request, CancellationToken cancellationToken) =>
        generalIde.WindowActivateAsync(request, cancellationToken);

    public Task<ToolWindowResult> ToolWindowShowAsync(ToolWindowRequest request, CancellationToken cancellationToken) =>
        generalIde.ToolWindowShowAsync(request, cancellationToken);

    public Task<ToolWindowResult> ToolWindowHideAsync(ToolWindowRequest request, CancellationToken cancellationToken) =>
        generalIde.ToolWindowHideAsync(request, cancellationToken);

    public Task<DocumentReadResult> DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken) =>
        editor.DocumentReadAsync(request, cancellationToken);

    public Task<TextSearchResult> EditorFindAsync(EditorFindRequest request, CancellationToken cancellationToken) =>
        editor.EditorFindAsync(request, cancellationToken);

    public Task<TextSearchResult> FindInFilesAsync(FindInFilesRequest request, CancellationToken cancellationToken) =>
        editor.FindInFilesAsync(request, cancellationToken);

    public Task<EditorDocumentInfo> DocumentOpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken) =>
        editor.DocumentOpenAsync(request, cancellationToken);

    public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken) =>
        editor.SelectionGetAsync(cancellationToken);

    public Task<DocumentMutationResult> DocumentWriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken) =>
        editor.DocumentWriteAsync(request, cancellationToken);

    public Task<DocumentMutationResult> DocumentSaveAsync(DocumentSaveRequest request, CancellationToken cancellationToken) =>
        editor.DocumentSaveAsync(request, cancellationToken);

    public Task<DocumentMutationResult> EditorInsertAsync(EditorInsertRequest request, CancellationToken cancellationToken) =>
        editor.EditorInsertAsync(request, cancellationToken);

    public Task<DocumentMutationResult> EditorReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken) =>
        editor.EditorReplaceAsync(request, cancellationToken);

    public Task<EditorDocumentInfo> EditorGotoLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken) =>
        editor.EditorGotoLineAsync(request, cancellationToken);

    public Task<SelectionInfo> SelectionSetAsync(SelectionSetRequest request, CancellationToken cancellationToken) =>
        editor.SelectionSetAsync(request, cancellationToken);

    public Task<DocumentCleanupResult> DocumentCleanupAsync(DocumentCleanupRequest request, CancellationToken cancellationToken) =>
        editor.DocumentCleanupAsync(request, cancellationToken);

    public Task<EditPreviewResult> EditPreviewAsync(EditPreviewRequest request, CancellationToken cancellationToken) =>
        editor.EditPreviewAsync(request, cancellationToken);

    public Task<EditDecisionResult> EditApproveAsync(EditDecisionRequest request, CancellationToken cancellationToken) =>
        editor.EditApproveAsync(request, cancellationToken);

    public Task<EditDecisionResult> EditRejectAsync(EditDecisionRequest request, CancellationToken cancellationToken) =>
        editor.EditRejectAsync(request, cancellationToken);

    public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken) =>
        editor.EditListPendingAsync(cancellationToken);

    public Task<DocumentSymbolsResult> CodeDocumentSymbolsAsync(DocumentSymbolsRequest request, CancellationToken cancellationToken) =>
        navigation.CodeDocumentSymbolsAsync(request, cancellationToken);

    public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        navigation.CodeGoToDefinitionAsync(request, cancellationToken);

    public Task<FindReferencesResult> CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        navigation.CodeFindReferencesAsync(request, cancellationToken);

    public Task<FindImplementationsResult> CodeFindImplementationsAsync(CodePositionRequest request, CancellationToken cancellationToken) =>
        navigation.CodeFindImplementationsAsync(request, cancellationToken);

    public Task<CodeWorkspaceSymbolsResult> CodeWorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken) =>
        navigation.CodeWorkspaceSymbolsAsync(request, cancellationToken);

    public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken) =>
        navigation.CodeRenameSymbolPreviewAsync(request, cancellationToken);

    public Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) =>
        build.BuildSolutionAsync(request, cancellationToken);

    public Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken) =>
        build.BuildProjectAsync(request, cancellationToken);

    public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken) =>
        build.BuildCancelAsync(cancellationToken);

    public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken) =>
        build.CleanSolutionAsync(cancellationToken);

    public Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) =>
        build.RebuildSolutionAsync(request, cancellationToken);

    public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken) =>
        build.BuildStatusAsync(cancellationToken);

    public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken) =>
        build.BuildConfigurationGetAsync(cancellationToken);

    public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken) =>
        build.BuildConfigurationSetAsync(request, cancellationToken);

    public Task<ErrorListResult> ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken) =>
        build.ErrorsListAsync(request, cancellationToken);

    public Task<OutputReadResult> OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken) =>
        build.OutputReadAsync(request, cancellationToken);

    public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken) =>
        build.OutputListPanesAsync(cancellationToken);

    public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken) =>
        build.OutputClearAsync(request, cancellationToken);

    public Task<OutputReadResult> OutputWriteAsync(OutputWriteRequest request, CancellationToken cancellationToken) =>
        build.OutputWriteAsync(request, cancellationToken);

    public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken) =>
        debugger.DebugStatusAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetModeAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken) =>
        debugger.DebugStartAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken) =>
        debugger.DebugStartWithoutDebuggingAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken) =>
        debugger.DebugRestartAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken) =>
        debugger.DebugStopAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken) =>
        debugger.DebugContinueAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken) =>
        debugger.DebugBreakAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugStepAsync(DebugStepRequest request, CancellationToken cancellationToken) =>
        debugger.DebugStepAsync(request, cancellationToken);

    public Task<BreakpointInfo> BreakpointSetAsync(BreakpointSetRequest request, CancellationToken cancellationToken) =>
        debugger.BreakpointSetAsync(request, cancellationToken);

    public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken) =>
        debugger.BreakpointListAsync(cancellationToken);

    public Task<BreakpointRemoveResult> BreakpointRemoveAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken) =>
        debugger.BreakpointRemoveAsync(request, cancellationToken);

    public Task<BreakpointEnableResult> BreakpointEnableAsync(BreakpointEnableRequest request, CancellationToken cancellationToken) =>
        debugger.BreakpointEnableAsync(request, cancellationToken);

    public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetCallstackAsync(cancellationToken);

    public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetLocalsAsync(cancellationToken);

    public Task<EvaluateExpressionResult> DebugEvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken) =>
        debugger.DebugEvaluateAsync(request, cancellationToken);

    public Task<DebugSetVariableResult> DebugSetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken) =>
        debugger.DebugSetVariableAsync(request, cancellationToken);

    public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) =>
        debugger.WatchAddAsync(request, cancellationToken);

    public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) =>
        debugger.WatchRemoveAsync(request, cancellationToken);

    public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken) =>
        debugger.WatchListAsync(cancellationToken);

    public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetThreadsAsync(cancellationToken);

    public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken) =>
        debugger.ProcessListDebuggedAsync(cancellationToken);

    public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken) =>
        debugger.ProcessListLocalAsync(cancellationToken);

    public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken) =>
        debugger.DebugAttachAsync(request, cancellationToken);

    public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken) =>
        debugger.ProcessDetachAsync(request, cancellationToken);

    public Task<ProcessTerminateResult> ProcessTerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken) =>
        debugger.ProcessTerminateAsync(request, cancellationToken);

    public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) =>
        debugger.ThreadSwitchAsync(request, cancellationToken);

    public Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken) =>
        debugger.ThreadSetFrozenAsync(request, cancellationToken);

    public Task<ThreadCallStackResult> ThreadGetCallstackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken) =>
        debugger.ThreadGetCallstackAsync(request, cancellationToken);

    public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken) =>
        debugger.ModuleListAsync(cancellationToken);

    public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) =>
        debugger.ImmediateExecuteAsync(request, cancellationToken);

    public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        debugger.ExceptionSettingsGetAsync(request, cancellationToken);

    public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        debugger.ExceptionSettingsSetAsync(request, cancellationToken);

    public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken) =>
        debugger.ParallelStacksAsync(cancellationToken);

    public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken) =>
        debugger.ParallelWatchAsync(cancellationToken);

    public Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.ConsoleReadAsync(request, cancellationToken);

    public Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.DiagnosticsBindingErrorsAsync(request, cancellationToken);

    public Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.ConsoleSendAsync(request, cancellationToken);

    public Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.ConsoleGetInfoAsync(request, cancellationToken);

    public Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiCaptureWindowAsync(request, cancellationToken);

    public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiCaptureRegionAsync(request, cancellationToken);

    public Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiSnapshotAsync(request, cancellationToken);

    public Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiGetTreeAsync(request, cancellationToken);

    public Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiFindElementsAsync(request, cancellationToken);

    public Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiGetElementAsync(request, cancellationToken);

    public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiDoubleClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiRightClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiDragAsync(request, cancellationToken);

    public Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiSetValueAsync(request, cancellationToken);

    public Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiInvokeAsync(request, cancellationToken);

    public Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiSendKeysAsync(request, cancellationToken);

    public Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiWaitForElementAsync(request, cancellationToken);

    public Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.UiWaitIdleAsync(request, cancellationToken);

    public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebConnectAsync(request, cancellationToken);

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebDisconnectAsync(request, cancellationToken);

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebStatusAsync(request, cancellationToken);

    public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebNavigateAsync(request, cancellationToken);

    public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebScreenshotAsync(request, cancellationToken);

    public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebDomGetAsync(request, cancellationToken);

    public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebDomQueryAsync(request, cancellationToken);

    public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebConsoleAsync(request, cancellationToken);

    public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebJsExecuteAsync(request, cancellationToken);

    public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebNetworkAsync(request, cancellationToken);

    public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebElementClickAsync(request, cancellationToken);

    public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        automation.WebElementSetValueAsync(request, cancellationToken);

    public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken) =>
        solution.SolutionInfoAsync(cancellationToken);

    public Task<SolutionInfoResult> SolutionOpenAsync(SolutionOpenRequest request, CancellationToken cancellationToken) =>
        solution.SolutionOpenAsync(request, cancellationToken);

    public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken) =>
        solution.SolutionCloseAsync(cancellationToken);

    public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken) =>
        solution.ProjectListAsync(cancellationToken);

    public Task<ProjectInfo> SolutionAddProjectAsync(SolutionAddProjectRequest request, CancellationToken cancellationToken) =>
        solution.SolutionAddProjectAsync(request, cancellationToken);

    public Task<ProjectInfo> SolutionRemoveProjectAsync(ProjectInfoRequest request, CancellationToken cancellationToken) =>
        solution.SolutionRemoveProjectAsync(request, cancellationToken);

    public Task<ProjectInfo?> ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken) =>
        solution.ProjectInfoAsync(request, cancellationToken);

    public Task<ProjectInfo> ProjectAddFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) =>
        solution.ProjectAddFileAsync(request, cancellationToken);

    public Task<ProjectFileResult> ProjectRemoveFileAsync(ProjectFileRequest request, CancellationToken cancellationToken) =>
        solution.ProjectRemoveFileAsync(request, cancellationToken);

    public Task<ProjectReferenceResult> ProjectAddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) =>
        solution.ProjectAddReferenceAsync(request, cancellationToken);

    public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken) =>
        solution.ProjectRemoveReferenceAsync(request, cancellationToken);

    public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken) =>
        solution.StartupProjectGetAsync(cancellationToken);

    public Task<StartupProjectResult> StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken) =>
        solution.StartupProjectSetAsync(request, cancellationToken);

    public Task<TestOperationResult> TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken) =>
        solution.TestDiscoverAsync(request, cancellationToken);

    public Task<TestOperationResult> TestRunAsync(TestRunRequest request, CancellationToken cancellationToken) =>
        solution.TestRunAsync(request, cancellationToken);

    public Task<TestOperationResult> TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken) =>
        solution.TestResultsAsync(request, cancellationToken);

    public Task<PackageRestoreResult> PackageRestoreAsync(PackageRestoreRequest request, CancellationToken cancellationToken) =>
        solution.PackageRestoreAsync(request, cancellationToken);

    public Task<NugetListResult> NugetListAsync(NugetListRequest request, CancellationToken cancellationToken) =>
        solution.NugetListAsync(request, cancellationToken);

    public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken) =>
        solution.NugetSearchAsync(request, cancellationToken);

    public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        solution.NugetInstallAsync(request, cancellationToken);

    public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        solution.NugetUpdateAsync(request, cancellationToken);

    public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
        solution.NugetUninstallAsync(request, cancellationToken);

    private static string FormatSymbolLabel(DocumentSymbolInfo symbol)
    {
        var scope = symbol.ContainingType ?? symbol.ContainingNamespace;
        var qualifiedName = string.IsNullOrWhiteSpace(scope)
            ? symbol.Name
            : $"{scope}.{symbol.Name}";

        return $"{qualifiedName} ({symbol.Kind}) {symbol.File}:{symbol.Line}:{symbol.Column}";
    }

}
