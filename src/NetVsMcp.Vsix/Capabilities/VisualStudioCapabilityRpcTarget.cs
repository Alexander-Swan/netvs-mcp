using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class VisualStudioCapabilityRpcTarget
{
    private readonly EditorRpcTarget editor;
    private readonly NavigationRpcTarget navigation;
    private readonly BuildRpcTarget build;
    private readonly DebuggerRpcTarget debugger;
    private readonly SolutionRpcTarget solution;
    private readonly IVisualStudioSessionSnapshotProvider snapshotProvider;
    private readonly IVisualStudioCapabilityCatalog capabilities;

    public VisualStudioCapabilityRpcTarget(
        IVisualStudioCapabilityCatalog capabilities,
        IVisualStudioSessionSnapshotProvider snapshotProvider)
    {
        this.capabilities = capabilities;
        this.snapshotProvider = snapshotProvider;
        editor = new EditorRpcTarget(capabilities.Editor);
        navigation = new NavigationRpcTarget(capabilities.Navigation);
        build = new BuildRpcTarget(capabilities.Build);
        debugger = new DebuggerRpcTarget(capabilities.Debugger);
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

    public Task<DocumentReadResult> DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken) =>
        editor.DocumentReadAsync(request, cancellationToken);

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

    public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken) =>
        navigation.CodeRenameSymbolPreviewAsync(request, cancellationToken);

    public Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken) =>
        build.BuildSolutionAsync(request, cancellationToken);

    public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken) =>
        build.BuildStatusAsync(cancellationToken);

    public Task<ErrorListResult> ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken) =>
        build.ErrorsListAsync(request, cancellationToken);

    public Task<OutputReadResult> OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken) =>
        build.OutputReadAsync(request, cancellationToken);

    public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken) =>
        debugger.DebugStatusAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetModeAsync(cancellationToken);

    public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken) =>
        debugger.DebugStartAsync(cancellationToken);

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

    public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) =>
        debugger.WatchAddAsync(request, cancellationToken);

    public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) =>
        debugger.WatchRemoveAsync(request, cancellationToken);

    public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken) =>
        debugger.WatchListAsync(cancellationToken);

    public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken) =>
        debugger.DebugGetThreadsAsync(cancellationToken);

    public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) =>
        debugger.ThreadSwitchAsync(request, cancellationToken);

    public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken) =>
        debugger.ModuleListAsync(cancellationToken);

    public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) =>
        debugger.ImmediateExecuteAsync(request, cancellationToken);

    public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        debugger.ExceptionSettingsGetAsync(request, cancellationToken);

    public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
        debugger.ExceptionSettingsSetAsync(request, cancellationToken);

    public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken) =>
        solution.SolutionInfoAsync(cancellationToken);

    public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken) =>
        solution.ProjectListAsync(cancellationToken);

    public Task<ProjectInfo?> ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken) =>
        solution.ProjectInfoAsync(request, cancellationToken);

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

    private static string FormatSymbolLabel(DocumentSymbolInfo symbol)
    {
        var scope = symbol.ContainingType ?? symbol.ContainingNamespace;
        var qualifiedName = string.IsNullOrWhiteSpace(scope)
            ? symbol.Name
            : $"{scope}.{symbol.Name}";

        return $"{qualifiedName} ({symbol.Kind}) {symbol.File}:{symbol.Line}:{symbol.Column}";
    }
}
