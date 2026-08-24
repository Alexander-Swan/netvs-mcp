using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.Text.Json;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{

    private static BrokerRuntime CreateRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "NetVsMcp.Broker.Tests", Guid.NewGuid().ToString("N"));
        var options = BrokerOptions.LocalDefault with
        {
            LogsDirectory = Path.Combine(root, "Logs"),
            SessionsDirectory = Path.Combine(root, "Sessions"),
            SettingsFilePath = Path.Combine(root, "settings.json")
        };

        return new BrokerRuntime(options, new SessionRegistry());
    }

    private static JsonElement ReadSingleAuditEntry(BrokerRuntime runtime)
    {
        var auditFile = Directory.GetFiles(runtime.AuditLog.LogsDirectory, "audit-*.jsonl").Single();
        var line = File.ReadLines(auditFile).Single();
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        int processId = 1234)
    {
        return CreateRegistration(
            sessionId,
            solutionName,
            $@"C:\Code\{solutionName}\{solutionName}.slnx",
            isActive: true,
            processId: processId);
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        string solutionPath,
        bool isActive,
        int processId = 1234)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: processId,
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

        public CodePositionRequest? LastCodeGoToDefinitionRequest { get; private set; }

        public CodePositionRequest? LastCodeFindReferencesRequest { get; private set; }

        public CodePositionRequest? LastCodeFindImplementationsRequest { get; private set; }

        public CodeWorkspaceSymbolsRequest? LastCodeWorkspaceSymbolsRequest { get; private set; }

        public RenameSymbolRequest? LastRenameSymbolRequest { get; private set; }

        public CallHierarchyRequest? LastCallHierarchyRequest { get; private set; }

        public ExecuteCommandRequest? LastExecuteCommandRequest { get; private set; }

        public WindowActivateRequest? LastWindowActivateRequest { get; private set; }

        public ToolWindowRequest? LastToolWindowShowRequest { get; private set; }

        public ToolWindowRequest? LastToolWindowHideRequest { get; private set; }

        public SolutionOpenRequest? LastSolutionOpenRequest { get; private set; }

        public SolutionAddProjectRequest? LastSolutionAddProjectRequest { get; private set; }

        public ProjectInfoRequest? LastSolutionRemoveProjectRequest { get; private set; }

        public ProjectFileRequest? LastProjectAddFileRequest { get; private set; }

        public ProjectFileRequest? LastProjectRemoveFileRequest { get; private set; }

        public bool SolutionClosed { get; private set; }

        public DocumentReadRequest? LastDocumentReadRequest { get; private set; }

        public DocumentOpenRequest? LastDocumentOpenRequest { get; private set; }

        public DocumentWriteRequest? LastDocumentWriteRequest { get; private set; }

        public EditorInsertRequest? LastEditorInsertRequest { get; private set; }

        public EditorReplaceRequest? LastEditorReplaceRequest { get; private set; }

        public EditorGotoLineRequest? LastEditorGotoLineRequest { get; private set; }

        public SelectionSetRequest? LastSelectionSetRequest { get; private set; }

        public DocumentCleanupRequest? LastDocumentCleanupRequest { get; private set; }

        public EditPreviewRequest? LastEditPreviewRequest { get; private set; }

        public EditDecisionRequest? LastEditApproveRequest { get; private set; }

        public EditDecisionRequest? LastEditRejectRequest { get; private set; }

        public ProjectInfoRequest? LastProjectInfoRequest { get; private set; }

        public StartupProjectSetRequest? LastStartupProjectSetRequest { get; private set; }

        public TestDiscoverRequest? LastTestDiscoverRequest { get; private set; }

        public TestRunRequest? LastTestRunRequest { get; private set; }

        public TestDebugRequest? LastTestDebugRequest { get; private set; }

        public TestResultsRequest? LastTestResultsRequest { get; private set; }

        public PackageRestoreRequest? LastPackageRestoreRequest { get; private set; }

        public BuildSolutionRequest? LastBuildSolutionRequest { get; private set; }

        public bool ThrowOnBuildStatus { get; init; }

        public ErrorListRequest? LastErrorListRequest { get; private set; }

        public OutputReadRequest? LastOutputReadRequest { get; private set; }

        public OutputWriteRequest? LastOutputWriteRequest { get; private set; }

        public DebugStepRequest? LastDebugStepRequest { get; private set; }

        public BreakpointSetRequest? LastBreakpointSetRequest { get; private set; }

        public BreakpointRemoveRequest? LastBreakpointRemoveRequest { get; private set; }

        public BreakpointEnableRequest? LastBreakpointEnableRequest { get; private set; }

        public EvaluateExpressionRequest? LastEvaluateExpressionRequest { get; private set; }

        public DebugAttachRequest? LastDebugAttachRequest { get; private set; }

        public ProcessDetachRequest? LastProcessDetachRequest { get; private set; }

        public bool DocumentListCalled { get; private set; }

        public EditorFindRequest? LastEditorFindRequest { get; private set; }

        public FindInFilesRequest? LastFindInFilesRequest { get; private set; }

        public DocumentCloseRequest? LastDocumentCloseRequest { get; private set; }

        public ProjectReferenceRequest? LastProjectAddReferenceRequest { get; private set; }

        public ProjectReferenceRequest? LastProjectRemoveReferenceRequest { get; private set; }

        public NugetListRequest? LastNugetListRequest { get; private set; }

        public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken)
        {
            DocumentListCalled = true;
            IReadOnlyCollection<EditorDocumentInfo> documents =
            [
                CreateDocument(_activeDocument)
            ];

            return Task.FromResult(new DocumentListResult(documents, _activeDocument));
        }

        public Task<DocumentCloseResult> DocumentCloseAsync(
            DocumentCloseRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentCloseRequest = request;
            return Task.FromResult(new DocumentCloseResult(true, "Document closed.", CreateDocument(string.IsNullOrWhiteSpace(request.Path) ? _activeDocument : request.Path), request.Policy));
        }

        public Task<TextSearchResult> EditorFindAsync(
            EditorFindRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorFindRequest = request;
            return Task.FromResult(CreateTextSearchResult(request.Query, request.Path));
        }

        public Task<TextSearchResult> FindInFilesAsync(
            FindInFilesRequest request,
            CancellationToken cancellationToken)
        {
            LastFindInFilesRequest = request;
            return Task.FromResult(CreateTextSearchResult(request.Query, @"C:\Code\NetVsMcp\Editor.cs"));
        }

        public Task<ProjectReferenceResult> ProjectAddReferenceAsync(
            ProjectReferenceRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectAddReferenceRequest = request;
            return Task.FromResult(new ProjectReferenceResult(true, "Reference added.", CreateProject(request.ProjectName), request.Reference, request.ReferenceType));
        }

        public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(
            ProjectReferenceRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectRemoveReferenceRequest = request;
            return Task.FromResult(new ProjectReferenceResult(true, "Reference removed.", CreateProject(request.ProjectName), request.Reference, request.ReferenceType));
        }

        public Task<NugetListResult> NugetListAsync(
            NugetListRequest request,
            CancellationToken cancellationToken)
        {
            LastNugetListRequest = request;
            IReadOnlyCollection<NugetPackageInfo> packages =
            [
                new("StreamJsonRpc", "2.25.29", request.ProjectName ?? "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.csproj")
            ];

            return Task.FromResult(new NugetListResult(packages));
        }

        public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<NugetPackageInfo> packages = [new(request.Query, "1.0.0", null, null)];
            return Task.FromResult(new NugetSearchResult(packages));
        }

        public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Installed."));

        public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Updated."));

        public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Uninstalled."));

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<VsSessionInfo>.Ok(new VsSessionInfo(
                SessionId: "vs-fake",
                ProcessId: 1234,
                VisualStudioVersion: "18.0",
                Edition: "Enterprise",
                SolutionName: "NetVsMcp",
                SolutionPath: @"C:\Code\NetVsMcp\NetVsMcp.slnx",
                ActiveDocument: _activeDocument,
                DebuggerMode: DebuggerMode.Break,
                IsActiveWindow: true,
                LastSeenUtc: DateTimeOffset.Parse("2026-07-22T15:00:00Z"),
                Capabilities: [VsCapability.Editor, VsCapability.Navigation])));
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

        public Task<ExecuteCommandResult> ExecuteCommandAsync(
            ExecuteCommandRequest request,
            CancellationToken cancellationToken)
        {
            LastExecuteCommandRequest = request;
            return Task.FromResult(new ExecuteCommandResult(true, request.CommandName, request.Arguments, "Command executed."));
        }

        public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WindowInfo> windows =
            [
                new(_activeDocument, "Document", "Text", true, true)
            ];

            return Task.FromResult(new WindowListResult(windows));
        }

        public Task<WindowActivateResult> WindowActivateAsync(
            WindowActivateRequest request,
            CancellationToken cancellationToken)
        {
            LastWindowActivateRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new WindowActivateResult(
                true,
                "Window activated.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", true, true)));
        }

        public Task<ToolWindowResult> ToolWindowShowAsync(
            ToolWindowRequest request,
            CancellationToken cancellationToken)
        {
            LastToolWindowShowRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new ToolWindowResult(
                true,
                "Tool window shown.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", true, true)));
        }

        public Task<ToolWindowResult> ToolWindowHideAsync(
            ToolWindowRequest request,
            CancellationToken cancellationToken)
        {
            LastToolWindowHideRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new ToolWindowResult(
                true,
                "Tool window hidden.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", false, false)));
        }

        public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeGoToDefinitionRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeLocationInfo> definitions =
            [
                new(request.DocumentPath, request.Line, request.Column, symbol)
            ];

            return Task.FromResult(new GoToDefinitionResult(symbol, definitions, true));
        }

        public Task<FindReferencesResult> CodeFindReferencesAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeFindReferencesRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeReferenceInfo> references =
            [
                new(request.DocumentPath, request.Line + 1, request.Column, false, symbol)
            ];

            return Task.FromResult(new FindReferencesResult(symbol, references));
        }

        public Task<FindImplementationsResult> CodeFindImplementationsAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeFindImplementationsRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeLocationInfo> implementations =
            [
                new(request.DocumentPath, request.Line + 2, request.Column, symbol)
            ];

            return Task.FromResult(new FindImplementationsResult(true, "Found implementations.", request, implementations));
        }

        public Task<CodeWorkspaceSymbolsResult> CodeWorkspaceSymbolsAsync(
            CodeWorkspaceSymbolsRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeWorkspaceSymbolsRequest = request;
            IReadOnlyCollection<DocumentSymbolInfo> symbols =
            [
                CreateSymbol(new CodePositionRequest { DocumentPath = "Program.cs", Line = 10, Column = 5 })
            ];

            return Task.FromResult(new CodeWorkspaceSymbolsResult(request.Query, symbols.Count, false, symbols));
        }

        public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(
            RenameSymbolRequest request,
            CancellationToken cancellationToken)
        {
            LastRenameSymbolRequest = request;
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            IReadOnlyCollection<RenameSymbolChangeInfo> changes =
            [
                new(request.DocumentPath, request.Line, request.Column, request.Line, request.Column + 3, request.NewName)
            ];

            return Task.FromResult(new RenameSymbolPreviewResult(true, "Previewed rename.", position, request.NewName, CreateSymbol(position), changes));
        }

        public Task<RenameSymbolApplyResult> CodeRenameSymbolApplyAsync(
            RenameSymbolRequest request,
            CancellationToken cancellationToken)
        {
            LastRenameSymbolRequest = request;
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            IReadOnlyCollection<RenameSymbolChangeInfo> changes =
            [
                new(request.DocumentPath, request.Line, request.Column, request.Line, request.Column + 3, request.NewName)
            ];

            return Task.FromResult(new RenameSymbolApplyResult(true, "Applied rename.", position, request.NewName, CreateSymbol(position), changes));
        }

        public Task<CallHierarchyResult> CallHierarchyGetAsync(
            CallHierarchyRequest request,
            CancellationToken cancellationToken)
        {
            LastCallHierarchyRequest = request;
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            var symbol = CreateSymbol(position);
            IReadOnlyCollection<CallHierarchyNode> incoming =
                request.Direction is "incoming" or "both"
                    ? [new(symbol, new CodeLocationInfo(request.DocumentPath, request.Line, request.Column, symbol), Array.Empty<CallHierarchyNode>(), false, false)]
                    : Array.Empty<CallHierarchyNode>();
            IReadOnlyCollection<CallHierarchyNode> outgoing =
                request.Direction is "outgoing" or "both"
                    ? [new(symbol, new CodeLocationInfo(request.DocumentPath, request.Line, request.Column, symbol), Array.Empty<CallHierarchyNode>(), false, false)]
                    : Array.Empty<CallHierarchyNode>();

            return Task.FromResult(new CallHierarchyResult(true, "Found call hierarchy node(s).", position, request.Direction, symbol, incoming, outgoing));
        }

        public CodeActionsListRequest? LastCodeActionsListRequest { get; private set; }

        public CodeActionsApplyRequest? LastCodeActionsApplyRequest { get; private set; }

        public Task<CodeActionsListResult> CodeActionsListAsync(
            CodeActionsListRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeActionsListRequest = request;
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            IReadOnlyCollection<CodeActionInfo> actions =
            [
                new(0, "Remove unnecessary usings", "fix", "CS0105", "RemoveUnnecessaryUsings")
            ];

            return Task.FromResult(new CodeActionsListResult(position, actions));
        }

        public Task<CodeActionsApplyResult> CodeActionsApplyAsync(
            CodeActionsApplyRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeActionsApplyRequest = request;
            IReadOnlyCollection<RenameSymbolChangeInfo> changes =
            [
                new(request.DocumentPath, request.Line, request.Column, request.Line, request.Column + 3, string.Empty)
            ];

            return Task.FromResult(new CodeActionsApplyResult(true, "Applied 'Remove unnecessary usings'.", "Remove unnecessary usings", changes));
        }

        public Task<DocumentReadResult> DocumentReadAsync(
            DocumentReadRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentReadRequest = request;
            return Task.FromResult(new DocumentReadResult(CreateDocument(request.Path), "class Program {}", "live", true));
        }

        public Task<EditorDocumentInfo> DocumentOpenAsync(
            DocumentOpenRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentOpenRequest = request;
            return Task.FromResult(CreateDocument(request.Path));
        }

        public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<SelectionInfo?>(CreateSelection());
        }

        public Task<DocumentMutationResult> DocumentWriteAsync(
            DocumentWriteRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentWriteRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterWrite, request.Text.Length));
        }

        public Task<DocumentMutationResult> DocumentSaveAsync(
            DocumentSaveRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateMutation(string.IsNullOrWhiteSpace(request.Path) ? "Program.cs" : request.Path, true, 0));
        }

        public Task<DocumentMutationResult> EditorInsertAsync(
            EditorInsertRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorInsertRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterEdit, request.Text.Length));
        }

        public Task<DocumentMutationResult> EditorReplaceAsync(
            EditorReplaceRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorReplaceRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterEdit, request.Text.Length));
        }

        public Task<EditorDocumentInfo> EditorGotoLineAsync(
            EditorGotoLineRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorGotoLineRequest = request;
            return Task.FromResult(CreateDocument(request.Path));
        }

        public Task<SelectionInfo> SelectionSetAsync(
            SelectionSetRequest request,
            CancellationToken cancellationToken)
        {
            LastSelectionSetRequest = request;
            return Task.FromResult(CreateSelection());
        }

        public Task<DocumentCleanupResult> DocumentCleanupAsync(
            DocumentCleanupRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentCleanupRequest = request;
            return Task.FromResult(new DocumentCleanupResult(
                true,
                true,
                null,
                CreateDocument(request.Path),
                request.SaveAfterCleanup,
                "Edit.FormatDocument"));
        }

        public Task<EditPreviewResult> EditPreviewAsync(
            EditPreviewRequest request,
            CancellationToken cancellationToken)
        {
            LastEditPreviewRequest = request;
            return Task.FromResult(new EditPreviewResult(true, null, CreatePendingEdit(request.Operation, request.Path, request.Text)));
        }

        public Task<EditDecisionResult> EditApproveAsync(
            EditDecisionRequest request,
            CancellationToken cancellationToken)
        {
            LastEditApproveRequest = request;
            var pending = CreatePendingEdit("replace", "Program.cs", "replacement");
            return Task.FromResult(new EditDecisionResult(
                true,
                null,
                request.EditId,
                true,
                pending,
                CreateMutation("Program.cs", request.SaveAfterApply, pending.ProposedLength)));
        }

        public Task<EditDecisionResult> EditRejectAsync(
            EditDecisionRequest request,
            CancellationToken cancellationToken)
        {
            LastEditRejectRequest = request;
            return Task.FromResult(new EditDecisionResult(
                true,
                null,
                request.EditId,
                false,
                CreatePendingEdit("replace", "Program.cs", "replacement"),
                null));
        }

        public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PendingEditInfo> pendingEdits = [CreatePendingEdit("replace", "Program.cs", "replacement")];
            return Task.FromResult(new PendingEditListResult(pendingEdits));
        }

        public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new SolutionInfoResult(
                "NetVsMcp",
                @"C:\Code\NetVsMcp\NetVsMcp.slnx",
                true,
                2,
                @"src\NetVsMcp.Broker\NetVsMcp.Broker.csproj"));
        }

        public Task<SolutionInfoResult> SolutionOpenAsync(
            SolutionOpenRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionOpenRequest = request;
            return Task.FromResult(new SolutionInfoResult(
                Path.GetFileNameWithoutExtension(request.Path),
                request.Path,
                true,
                1,
                null));
        }

        public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken)
        {
            SolutionClosed = true;
            return Task.FromResult(new SolutionInfoResult(null, null, false, 0, null));
        }

        public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ProjectInfo> projects =
            [
                CreateProject("NetVsMcp.Broker"),
                CreateProject("NetVsMcp.Broker.Tests")
            ];

            return Task.FromResult(new ProjectListResult(projects));
        }

        public Task<ProjectInfo> SolutionAddProjectAsync(
            SolutionAddProjectRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionAddProjectRequest = request;
            return Task.FromResult(CreateProject(Path.GetFileNameWithoutExtension(request.ProjectPath)));
        }

        public Task<ProjectInfo> SolutionRemoveProjectAsync(
            ProjectInfoRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionRemoveProjectRequest = request;
            return Task.FromResult(CreateProject(request.ProjectName));
        }

        public Task<ProjectInfo?> ProjectInfoAsync(
            ProjectInfoRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectInfoRequest = request;
            return Task.FromResult<ProjectInfo?>(CreateProject(request.ProjectName));
        }

        public Task<ProjectInfo> ProjectAddFileAsync(
            ProjectFileRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectAddFileRequest = request;
            return Task.FromResult(CreateProject(request.ProjectName));
        }

        public Task<ProjectFileResult> ProjectRemoveFileAsync(
            ProjectFileRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectRemoveFileRequest = request;
            return Task.FromResult(new ProjectFileResult(true, "File removed from project.", CreateProject(request.ProjectName), request.FilePath));
        }

        public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<string> projects = [@"src\NetVsMcp.Broker\NetVsMcp.Broker.csproj"];
            return Task.FromResult(new StartupProjectResult(projects, false));
        }

        public Task<StartupProjectResult> StartupProjectSetAsync(
            StartupProjectSetRequest request,
            CancellationToken cancellationToken)
        {
            LastStartupProjectSetRequest = request;
            IReadOnlyCollection<string> projects = [request.ProjectName];
            return Task.FromResult(new StartupProjectResult(projects, false));
        }

        public Task<TestOperationResult> TestDiscoverAsync(
            TestDiscoverRequest request,
            CancellationToken cancellationToken)
        {
            LastTestDiscoverRequest = request;
            IReadOnlyCollection<TestCaseInfo> tests =
            [
                new("BrokerToolServiceTests.ProjectList", request.ProjectName, "BrokerToolServiceTests.cs")
            ];

            return Task.FromResult(new TestOperationResult(true, "Discovered tests.", tests, []));
        }

        public Task<TestOperationResult> TestRunAsync(
            TestRunRequest request,
            CancellationToken cancellationToken)
        {
            LastTestRunRequest = request;
            return Task.FromResult(new TestOperationResult(true, "Ran tests.", [], CreateTestResults()));
        }

        public Task<TestDebugResult> TestDebugAsync(
            TestDebugRequest request,
            CancellationToken cancellationToken)
        {
            LastTestDebugRequest = request;
            return Task.FromResult(new TestDebugResult(
                true,
                "Attached debugger.",
                request.ProjectName,
                request.Filter,
                1234,
                "testhost",
                5678,
                "dotnet",
                "dotnet test project --filter test",
                @"C:\Code\NetVsMcp",
                @"C:\Code\NetVsMcp\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj",
                request.AttachTimeoutSeconds));
        }

        public Task<TestOperationResult> TestResultsAsync(
            TestResultsRequest request,
            CancellationToken cancellationToken)
        {
            LastTestResultsRequest = request;
            return Task.FromResult(new TestOperationResult(true, "Returned test results.", [], CreateTestResults()));
        }

        public Task<PackageRestoreResult> PackageRestoreAsync(
            PackageRestoreRequest request,
            CancellationToken cancellationToken)
        {
            LastPackageRestoreRequest = request;
            return Task.FromResult(new PackageRestoreResult(true, "Restored packages.", CreateProject(request.ProjectName ?? "NetVsMcp.Broker"), 0));
        }

        public Task<BuildSolutionResult> BuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            LastBuildSolutionRequest = request;
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildSolutionResult> BuildProjectAsync(
            BuildProjectRequest request,
            CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildStatusInfo("Cancelled", 0));
        }

        public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildSolutionResult> RebuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnBuildStatus)
            {
                throw new InvalidOperationException("A build must be performed before this information is available.");
            }

            return Task.FromResult(new BuildStatusInfo("Idle", 0));
        }

        public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildConfigurationInfo("Debug", "Any CPU"));
        }

        public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(
            BuildConfigurationSetRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildConfigurationInfo(request.Configuration, request.Platform));
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

        public Task<TaskListResult> TaskListGetAsync(
            TaskListRequest request,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<TaskListItemInfo> items =
            [
                new(
                    Index: 1,
                    Description: "Investigate flaky test.",
                    File: @"C:\Code\NetVsMcp\Program.cs",
                    Line: 42,
                    Priority: "High",
                    Category: "Comment",
                    IsUserTask: false,
                    Checked: null)
            ];

            return Task.FromResult(new TaskListResult(items));
        }

        public Task<TaskListMutationResult> TaskListAddAsync(
            TaskListAddRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaskListMutationResult(true, "Task item added."));
        }

        public Task<TaskListMutationResult> TaskListRemoveAsync(
            TaskListMutationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaskListMutationResult(true, "Task item removed."));
        }

        public Task<TaskListMutationResult> TaskListSetCheckedAsync(
            TaskListSetCheckedRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaskListMutationResult(true, "Task item updated."));
        }

        public Task<OutputReadResult> OutputReadAsync(
            OutputReadRequest request,
            CancellationToken cancellationToken)
        {
            LastOutputReadRequest = request;
            return Task.FromResult(new OutputReadResult(request.PaneName, "Build output", false));
        }

        public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<OutputPaneInfo> panes = [new("Build"), new("Debug")];
            return Task.FromResult(new OutputPaneListResult(panes));
        }

        public Task<OutputReadResult> OutputClearAsync(
            OutputPaneRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new OutputReadResult(request.PaneName ?? "Build", string.Empty, false));
        }

        public Task<OutputReadResult> OutputWriteAsync(
            OutputWriteRequest request,
            CancellationToken cancellationToken)
        {
            LastOutputWriteRequest = request;
            return Task.FromResult(new OutputReadResult(request.PaneName ?? "NetVsMcp", $"Build output{Environment.NewLine}{request.Text}", false));
        }

        public string DebugStatusMode { get; set; } = "Break";

        public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo(DebugStatusMode));
        }

        public Task<HotReloadApplyResult> DebugHotReloadApplyAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new HotReloadApplyResult(true, "Applied code changes.", Array.Empty<ErrorListItemInfo>()));
        }

        public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Design"));
        }

        public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<DebuggerStateInfo> DebugStepAsync(
            DebugStepRequest request,
            CancellationToken cancellationToken)
        {
            LastDebugStepRequest = request;
            return Task.FromResult(new DebuggerStateInfo(DebugStatusMode));
        }

        public Task<BreakpointInfo> BreakpointSetAsync(
            BreakpointSetRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointSetRequest = request;
            return Task.FromResult(CreateBreakpoint(request.Condition, request));
        }

        public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<BreakpointInfo> breakpoints = [CreateBreakpoint(null)];
            return Task.FromResult(new BreakpointListResult(breakpoints));
        }

        public Task<BreakpointRemoveResult> BreakpointRemoveAsync(
            BreakpointRemoveRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointRemoveRequest = request;
            return Task.FromResult(new BreakpointRemoveResult(1));
        }

        public Task<BreakpointEnableResult> BreakpointEnableAsync(
            BreakpointEnableRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointEnableRequest = request;
            IReadOnlyCollection<BreakpointInfo> breakpoints = [CreateBreakpoint(null) with { Enabled = request.Enabled }];
            return Task.FromResult(new BreakpointEnableResult(1, breakpoints));
        }

        public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CallStackFrameInfo> frames =
            [
                new("Program.Main", @"C:\Code\NetVsMcp\Program.cs", 42, 1)
            ];

            return Task.FromResult(new CallStackResult(new DebuggerStateInfo("Break"), frames));
        }

        public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> locals =
            [
                new("count", "42", "int", true)
            ];

            return Task.FromResult(new LocalsResult(new DebuggerStateInfo("Break"), locals));
        }

        public Task<EvaluateExpressionResult> DebugEvaluateAsync(
            EvaluateExpressionRequest request,
            CancellationToken cancellationToken)
        {
            LastEvaluateExpressionRequest = request;
            var expression = new DebugExpressionInfo(request.Expression, "43", "int", true);
            return Task.FromResult(new EvaluateExpressionResult(new DebuggerStateInfo("Break"), expression));
        }

        public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebuggedProcessInfo> processes = [new(1234, "NetVsMcp.Broker.exe", "Default", "alex")];
            return Task.FromResult(new DebuggedProcessListResult(processes));
        }

        public Task<DebugSetVariableResult> DebugSetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken)
        {
            var evaluation = new EvaluateExpressionResult(
                new DebuggerStateInfo("Break"),
                new DebugExpressionInfo(request.Name, request.Value, "string", true));
            return Task.FromResult(new DebugSetVariableResult(true, null, evaluation));
        }

        public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<LocalProcessInfo> processes = [new(1234, "NetVsMcp.Broker.exe", "Default", "alex", true)];
            return Task.FromResult(new LocalProcessListResult(processes));
        }

        public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken)
        {
            LastDebugAttachRequest = request;
            return Task.FromResult(new DebugAttachResult(true, null, new DebuggedProcessInfo(request.ProcessId ?? 1234, request.ProcessName ?? "NetVsMcp.Broker.exe", "Default", "alex")));
        }

        public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken)
        {
            LastProcessDetachRequest = request;
            return Task.FromResult(new ProcessDetachResult(true, null, new DebuggedProcessInfo(request.ProcessId ?? 1234, request.ProcessName ?? "NetVsMcp.Broker.exe", "Default", "alex"), new DebuggerStateInfo("Break")));
        }

        public Task<ProcessTerminateResult> ProcessTerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessTerminateResult(true, null, new DebuggedProcessInfo(request.ProcessId ?? 1234, request.ProcessName ?? "NetVsMcp.Broker.exe", "Default", "alex"), new DebuggerStateInfo("Break")));

        public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new WatchOperationResult(true, true, "Added.", new DebugExpressionInfo(request.Expression, "1", "int", true)));

        public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new WatchOperationResult(true, true, "Removed.", new DebugExpressionInfo(request.Expression, "1", "int", true)));

        public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> watches = [new("count", "42", "int", true)];
            return Task.FromResult(new WatchListResult(true, null, watches));
        }

        public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugThreadInfo> threads = [new(1, "Main Thread", true)];
            return Task.FromResult(new DebugThreadListResult(true, null, threads));
        }

        public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ThreadSwitchResult(true, true, null, new DebugThreadInfo(request.ThreadId, "Main Thread", true)));

        public Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ThreadSetFrozenResult(true, true, null, new DebugThreadInfo(request.ThreadId, "Main Thread", true), request.Frozen));

        public Task<ThreadCallStackResult> ThreadGetCallstackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CallStackFrameInfo> frames = [new("Program.Main", "Program.cs", 42, 1)];
            return Task.FromResult(new ThreadCallStackResult(true, null, new DebugThreadInfo(request.ThreadId, "Main Thread", true), frames));
        }

        public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugModuleInfo> modules = [new("NetVsMcp.Broker.dll", @"C:\Code\NetVsMcp\NetVsMcp.Broker.dll")];
            return Task.FromResult(new ModuleListResult(true, null, modules));
        }

        public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ImmediateExecuteResult(true, true, null, "ok"));

        public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExceptionSettingsResult(true, true, null));

        public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExceptionSettingsResult(true, true, null));

        public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ParallelStackFrameInfo> frames = [new(1, "Main Thread", "Program.Main", "Program.cs", 42, 1)];
            return Task.FromResult(new ParallelStacksResult(true, null, frames));
        }

        public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> expressions = [new("count", "42", "int", true)];
            return Task.FromResult(new ParallelWatchResult(true, null, expressions));
        }

        public Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);
        public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) => AutomationAsync(request);

        private static Task<AutomationResult> AutomationAsync(AutomationRequest request)
        {
            IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>
            {
                ["toolName"] = request.ToolName
            };
            return Task.FromResult(new AutomationResult(false, false, "Automation backend unavailable.", request.Text, metadata));
        }

        private static BreakpointInfo CreateBreakpoint(string? condition, BreakpointSetRequest? request = null)
        {
            return new BreakpointInfo(
                Name: "bp-1",
                File: @"C:\Code\NetVsMcp\Program.cs",
                Line: 42,
                Column: 3,
                FunctionName: null,
                Condition: condition,
                Enabled: true,
                Action: request?.Action,
                ActionMessage: request?.ActionMessage,
                ContinueAfterAction: request?.ContinueAfterAction ?? false,
                HitCount: request?.HitCount,
                HitCountType: request?.HitCountType,
                DependsOnBreakpointName: request?.DependsOnBreakpointName,
                GroupName: request?.GroupName ?? "critical");
        }

        private static NugetMutationResult CreateNugetMutation(NugetPackageMutationRequest request, string message)
        {
            return new NugetMutationResult(true, message, CreateProject(request.ProjectName), request.PackageId, request.Version, 0);
        }

        private static EditorDocumentInfo CreateDocument(string path)
        {
            return new EditorDocumentInfo(
                Name: Path.GetFileName(path),
                Path: path,
                Language: "CSharp",
                IsOpen: true,
                IsSaved: false);
        }

        private static TextSearchResult CreateTextSearchResult(string query, string path)
        {
            IReadOnlyCollection<TextSearchMatch> matches =
            [
                new(path, 3, 9, "var value = needle;", query)
            ];

            return new TextSearchResult(query, matches.Count, false, matches);
        }

        private static SelectionInfo CreateSelection()
        {
            return new SelectionInfo(
                CreateDocument("Program.cs"),
                "selected",
                1,
                1,
                2,
                1,
                false);
        }

        private static DocumentMutationResult CreateMutation(
            string path,
            bool saved,
            int charactersChanged)
        {
            return new DocumentMutationResult(
                true,
                null,
                CreateDocument(path),
                saved,
                charactersChanged);
        }

        private static PendingEditInfo CreatePendingEdit(
            string operation,
            string path,
            string proposedText)
        {
            return new PendingEditInfo(
                "edit-1",
                operation,
                path,
                "Replace text.",
                "old",
                proposedText,
                1,
                1,
                1,
                5,
                3,
                proposedText.Length,
                DateTimeOffset.Parse("2026-07-22T15:00:00Z"));
        }

        private static ProjectInfo CreateProject(string name)
        {
            return new ProjectInfo(
                Name: name,
                UniqueName: $@"src\{name}\{name}.csproj",
                FullName: $@"C:\Code\NetVsMcp\src\{name}\{name}.csproj",
                Kind: "CSharp",
                IsLoaded: true,
                Language: "CSharp",
                OutputFileName: $"{name}.dll");
        }

        private static IReadOnlyCollection<TestResultInfo> CreateTestResults()
        {
            return
            [
                new("BrokerToolServiceTests.ProjectList", "Passed", "00:00:00.100", null)
            ];
        }

        private static DocumentSymbolInfo CreateSymbol(CodePositionRequest request)
        {
            return new DocumentSymbolInfo(
                "Run",
                "Method",
                request.DocumentPath,
                request.Line,
                request.Column,
                "Program",
                "NetVsMcp");
        }
    }
}
