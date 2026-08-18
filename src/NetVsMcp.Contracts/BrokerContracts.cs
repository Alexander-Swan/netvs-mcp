namespace NetVsMcp.Contracts;

public enum VsCapability
{
    Editor,
    Navigation,
    Build,
    Debugger,
    Diagnostics,
    Tests,
    ProjectSystem
}

public enum DebuggerMode
{
    Unknown,
    Design,
    Run,
    Break
}

public enum DebugStepKind
{
    Into,
    Over,
    Out
}

public enum DebugAdvanceAction
{
    StepInto,
    StepOver,
    StepOut,
    Continue,
    Break
}

public enum SessionHealth
{
    Unknown,
    Connected,
    Stale,
    Disconnected
}

public enum RouteFailureReason
{
    None,
    NoRegisteredSessions,
    SessionNotFound,
    ProcessIdNotFound,
    SolutionPathNotFound,
    SolutionNameNotFound,
    WorkspacePathNotFound,
    Ambiguous
}

public enum BrokerToolCategory
{
    Broker,
    Read,
    EditPreview,
    EditDirect,
    Build,
    Debug,
    Project,
    Test,
    Admin
}

public static class VsRpcProtocol
{
    public const string CurrentVersion = "1.1";
    public const int CurrentMajorVersion = 1;
}

public static class ToolErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string SessionRoutingFailed = "session_routing_failed";
    public const string SessionNotConnected = "session_not_connected";
    public const string RpcFailure = "rpc_failure";
    public const string ProtocolMismatch = "protocol_mismatch";
    public const string ToolNotImplemented = "tool_not_implemented";
    public const string VisualStudioError = "visual_studio_error";
}

public sealed record RoutingTarget(
    string? SessionId = null,
    string? SolutionName = null,
    string? SolutionPath = null,
    int? ProcessId = null,
    string? WorkspacePath = null,
    string? RootPath = null);

public sealed class ExecuteCommandRequest
{
    public string CommandName { get; set; } = string.Empty;

    public string? Arguments { get; set; }
}

public sealed record ExecuteCommandResult(
    bool Success,
    string CommandName,
    string? Arguments,
    string? Message);

public sealed record WindowInfo(
    string? Caption,
    string? Kind,
    string? ObjectKind,
    bool IsActive,
    bool IsVisible);

public sealed record WindowListResult(
    IReadOnlyCollection<WindowInfo> Windows);

public sealed class WindowActivateRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

public sealed record WindowActivateResult(
    bool Success,
    string? Message,
    WindowInfo? Window);

public sealed class ToolWindowRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

public sealed record ToolWindowResult(
    bool Success,
    string? Message,
    WindowInfo? Window);

public sealed record VsSessionInfo(
    string SessionId,
    int ProcessId,
    string? VisualStudioVersion,
    string? Edition,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    DateTimeOffset LastSeenUtc,
    IReadOnlyCollection<VsCapability> Capabilities);

public sealed record VsLaunchInstanceResult(
    bool Success,
    string? Message,
    int? ProcessId,
    VsSessionInfo? Session);

public sealed record VsSessionRegistration(
    string SessionId,
    int ProcessId,
    string? VisualStudioVersion,
    string? Edition,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    IReadOnlyCollection<VsCapability> Capabilities,
    string? ProtocolVersion = VsRpcProtocol.CurrentVersion);

public sealed record VsSessionUpdate(
    string SessionId,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    IReadOnlyCollection<VsCapability>? Capabilities = null,
    string? ProtocolVersion = VsRpcProtocol.CurrentVersion);

public sealed record VsSessionStatus(
    VsSessionInfo Session,
    SessionHealth Health,
    TimeSpan Age);

public sealed record EditorDocumentInfo(
    string? Name,
    string? Path,
    string? Language,
    bool IsOpen,
    bool IsSaved);

public sealed record DocumentListResult(
    IReadOnlyCollection<EditorDocumentInfo> Documents,
    string? ActiveDocument);

public enum DocumentClosePolicy
{
    NoSave,
    Save,
    Discard
}

public sealed class DocumentCloseRequest
{
    public string Path { get; set; } = string.Empty;
    public DocumentClosePolicy Policy { get; set; } = DocumentClosePolicy.NoSave;
    public bool AllowDirtyDiscard { get; set; }
}

public sealed record DocumentCloseResult(
    bool Success,
    string? Message,
    EditorDocumentInfo? Document,
    DocumentClosePolicy Policy);

public sealed class EditorFindRequest
{
    public string Path { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
}

public sealed class FindInFilesRequest
{
    public string Query { get; set; } = string.Empty;
    public string? RootPath { get; set; }
    public string? FilePattern { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
}

public sealed record TextSearchMatch(
    string Path,
    int Line,
    int Column,
    string LineText,
    string MatchText);

public sealed record TextSearchResult(
    string Query,
    int MatchCount,
    bool Truncated,
    IReadOnlyCollection<TextSearchMatch> Matches);

public sealed class DocumentReadRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record DocumentReadResult(
    EditorDocumentInfo Document,
    string Text,
    string Source,
    bool UsedLiveBuffer);

public sealed class DocumentOpenRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record SelectionInfo(
    EditorDocumentInfo Document,
    string Text,
    int AnchorLine,
    int AnchorColumn,
    int ActiveLine,
    int ActiveColumn,
    bool IsEmpty);

public sealed class DocumentWriteRequest
{
    public string Path { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool CreateIfMissing { get; set; }

    public bool SaveAfterWrite { get; set; }
}

public sealed record DocumentMutationResult(
    bool Success,
    string? Message,
    EditorDocumentInfo? Document,
    bool Saved,
    int CharactersChanged);

public sealed class DocumentSaveRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed class EditorInsertRequest
{
    public string Path { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool SaveAfterEdit { get; set; }
}

public sealed class EditorReplaceRequest
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool SaveAfterEdit { get; set; }
}

public sealed class EditorGotoLineRequest
{
    public string Path { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; } = 1;
}

public sealed class SelectionSetRequest
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }
}

public sealed class DocumentCleanupRequest
{
    public string Path { get; set; } = string.Empty;

    public bool SaveAfterCleanup { get; set; }
}

public sealed record DocumentCleanupResult(
    bool Success,
    bool Supported,
    string? Message,
    EditorDocumentInfo? Document,
    bool Saved,
    string? Command);

public sealed class EditPreviewRequest
{
    public string Operation { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool CreateIfMissing { get; set; }

    public bool SaveAfterEdit { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }
}

public sealed class EditDecisionRequest
{
    public string EditId { get; set; } = string.Empty;

    public bool SaveAfterApply { get; set; }
}

public sealed record PendingEditInfo(
    string EditId,
    string Operation,
    string Path,
    string Summary,
    string? OriginalText,
    string ProposedText,
    int? StartLine,
    int? StartColumn,
    int? EndLine,
    int? EndColumn,
    int OriginalLength,
    int ProposedLength,
    DateTimeOffset CreatedUtc);

public sealed record EditPreviewResult(
    bool Success,
    string? Message,
    PendingEditInfo? PendingEdit);

public sealed record EditDecisionResult(
    bool Success,
    string? Message,
    string EditId,
    bool Applied,
    PendingEditInfo? PendingEdit,
    DocumentMutationResult? Mutation);

public sealed record PendingEditListResult(
    IReadOnlyCollection<PendingEditInfo> PendingEdits);

public sealed record SolutionInfoResult(
    string? Name,
    string? Path,
    bool IsOpen,
    int ProjectCount,
    string? StartupProject);

public sealed class SolutionOpenRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record ProjectListResult(
    IReadOnlyCollection<ProjectInfo> Projects);

public sealed class ProjectInfoRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class SolutionAddProjectRequest
{
    public string ProjectPath { get; set; } = string.Empty;
}

public sealed class ProjectFileRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}

public sealed record ProjectFileResult(
    bool Success,
    string? Message,
    ProjectInfo? Project,
    string FilePath);

public sealed record ProjectInfo(
    string? Name,
    string? UniqueName,
    string? FullName,
    string? Kind,
    bool IsLoaded,
    string? Language,
    string? OutputFileName);

public sealed class ProjectReferenceRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = "assembly";
    public string? HintPath { get; set; }
}

public sealed record ProjectReferenceResult(
    bool Success,
    string? Message,
    ProjectInfo? Project,
    string Reference,
    string ReferenceType);

public sealed class NugetListRequest
{
    public string? ProjectName { get; set; }
}

public sealed class NugetSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
    public bool IncludePrerelease { get; set; }
}

public sealed class NugetPackageMutationRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string? Version { get; set; }
}

public sealed record NugetPackageInfo(
    string Id,
    string? Version,
    string? ProjectName,
    string? ProjectPath);

public sealed record NugetListResult(
    IReadOnlyCollection<NugetPackageInfo> Packages);

public sealed record NugetSearchResult(
    IReadOnlyCollection<NugetPackageInfo> Packages);

public sealed record NugetMutationResult(
    bool Success,
    string Message,
    ProjectInfo? Project,
    string PackageId,
    string? Version,
    int ExitCode);

public sealed record StartupProjectResult(
    IReadOnlyCollection<string> Projects,
    bool IsMultiStartup);

public sealed class StartupProjectSetRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class TestDiscoverRequest
{
    public string? ProjectName { get; set; }
}

public sealed class TestRunRequest
{
    public string? ProjectName { get; set; }

    public string? Filter { get; set; }
}

public sealed class TestResultsRequest
{
    public string? RunId { get; set; }
}

public sealed record TestOperationResult(
    bool Supported,
    string Message,
    IReadOnlyCollection<TestCaseInfo> Tests,
    IReadOnlyCollection<TestResultInfo> Results);

public sealed record TestCaseInfo(
    string Name,
    string? ProjectName,
    string? Source);

public sealed record TestResultInfo(
    string Name,
    string Outcome,
    string? Duration,
    string? Message);

public sealed class CodePositionRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; }
}

public sealed class CodeWorkspaceSymbolsRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 100;
}

public sealed record DocumentSymbolInfo(
    string Name,
    string Kind,
    string? File,
    int Line,
    int Column,
    string? ContainingType,
    string? ContainingNamespace);

public sealed record CodeLocationInfo(
    string? File,
    int Line,
    int Column,
    DocumentSymbolInfo Symbol);

public sealed record CodeReferenceInfo(
    string? File,
    int Line,
    int Column,
    bool IsImplicit,
    DocumentSymbolInfo Symbol);

public sealed record GoToDefinitionResult(
    DocumentSymbolInfo? Symbol,
    IReadOnlyCollection<CodeLocationInfo> Definitions,
    bool Navigated);

public sealed record FindReferencesResult(
    DocumentSymbolInfo? Symbol,
    IReadOnlyCollection<CodeReferenceInfo> References);

public sealed record CodeWorkspaceSymbolsResult(
    string Query,
    int MatchCount,
    bool Truncated,
    IReadOnlyCollection<DocumentSymbolInfo> Symbols);

public sealed class RenameSymbolRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; }

    public string NewName { get; set; } = string.Empty;
}

public sealed record RenameSymbolChangeInfo(
    string? File,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string NewText);

public sealed class PackageRestoreRequest
{
    public string? ProjectName { get; set; }
}

public sealed class BuildSolutionRequest
{
    public bool WaitForBuildToFinish { get; set; }
}

public sealed record BuildSolutionResult(
    BuildStatusInfo Status,
    int LastBuildInfo);

public sealed record BuildStatusInfo(
    string State,
    int LastBuildInfo);

public sealed record BuildConfigurationInfo(
    string? Configuration,
    string? Platform);

public sealed class BuildProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public bool WaitForBuildToFinish { get; set; } = true;
}

public sealed class BuildConfigurationSetRequest
{
    public string Configuration { get; set; } = string.Empty;
    public string? Platform { get; set; }
}

public sealed class ErrorListRequest
{
    public bool IncludeWarnings { get; set; } = true;

    public int MaxItems { get; set; } = 200;
}

public sealed record ErrorListResult(
    IReadOnlyCollection<ErrorListItemInfo> Items);

public sealed record ErrorListItemInfo(
    string? Description,
    string? File,
    int Line,
    int Column,
    string Level,
    string? Project);

public sealed class OutputReadRequest
{
    public string? PaneName { get; set; }

    public int MaxChars { get; set; } = 20000;
}

public sealed record OutputReadResult(
    string? PaneName,
    string Text,
    bool Truncated);

public sealed record OutputPaneInfo(string Name);

public sealed record OutputPaneListResult(
    IReadOnlyCollection<OutputPaneInfo> Panes);

public sealed class OutputPaneRequest
{
    public string? PaneName { get; set; }
}

public sealed class OutputWriteRequest
{
    public string? PaneName { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Activate { get; set; }
}

public sealed record DebuggerStateInfo(string Mode);

public sealed record DebuggedProcessInfo(
    int ProcessId,
    string? Name,
    string? Transport,
    string? UserName);

public sealed record LocalProcessInfo(
    int ProcessId,
    string? Name,
    string? Transport,
    string? UserName,
    bool IsBeingDebugged);

public sealed record DebuggedProcessListResult(
    IReadOnlyCollection<DebuggedProcessInfo> Processes);

public sealed record LocalProcessListResult(
    IReadOnlyCollection<LocalProcessInfo> Processes);

public sealed class DebugAttachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

public sealed record DebugAttachResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process);

public sealed class ProcessDetachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

public sealed record ProcessDetachResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process,
    DebuggerStateInfo State);

public sealed class ProcessTerminateRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

public sealed record ProcessTerminateResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process,
    DebuggerStateInfo State);

public sealed class WatchAddRequest
{
    public string Expression { get; set; } = string.Empty;
}

public sealed class WatchRemoveRequest
{
    public string Expression { get; set; } = string.Empty;
}

public sealed record WatchOperationResult(
    bool Supported,
    bool Success,
    string? Message,
    DebugExpressionInfo? Watch);

public sealed record WatchListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugExpressionInfo> Watches);

public sealed record DebugThreadInfo(
    int Id,
    string? Name,
    bool IsCurrent);

public sealed record DebugThreadListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugThreadInfo> Threads);

public sealed class ThreadSwitchRequest
{
    public int ThreadId { get; set; }
}

public sealed class DebugSetVariableRequest
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 5000;
}

public sealed record DebugSetVariableResult(
    bool Success,
    string? Message,
    EvaluateExpressionResult? Evaluation);

public sealed class ThreadSetFrozenRequest
{
    public int ThreadId { get; set; }
    public bool Frozen { get; set; }
}

public sealed record ThreadSwitchResult(
    bool Supported,
    bool Success,
    string? Message,
    DebugThreadInfo? Thread);

public sealed record ThreadSetFrozenResult(
    bool Supported,
    bool Success,
    string? Message,
    DebugThreadInfo? Thread,
    bool Frozen);

public sealed class ThreadCallStackRequest
{
    public int ThreadId { get; set; }
}

public sealed record ThreadCallStackResult(
    bool Supported,
    string? Message,
    DebugThreadInfo? Thread,
    IReadOnlyCollection<CallStackFrameInfo> Frames);

public sealed record DebugModuleInfo(
    string? Name,
    string? Path);

public sealed record ModuleListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugModuleInfo> Modules);

public sealed class ImmediateExecuteRequest
{
    public string Statement { get; set; } = string.Empty;
}

public sealed record ImmediateExecuteResult(
    bool Supported,
    bool Success,
    string? Message,
    string? Output);

public sealed class ExceptionSettingsRequest
{
    public string? ExceptionName { get; set; }
    public bool? BreakOnThrown { get; set; }
}

public sealed record ExceptionSettingInfo(
    string? GroupName,
    string? Name,
    bool BreakWhenThrown,
    bool BreakWhenUserUnhandled,
    bool UserDefined);

public sealed record ExceptionSettingsResult(
    bool Supported,
    bool Success,
    string? Message,
    IReadOnlyCollection<ExceptionSettingInfo>? Settings = null);

public sealed record ParallelStackFrameInfo(
    int ThreadId,
    string? ThreadName,
    string? FunctionName,
    string? File,
    int Line,
    int Column);

public sealed record ParallelStacksResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<ParallelStackFrameInfo> Frames);

public sealed record ParallelWatchResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugExpressionInfo> Expressions);

public sealed class DebugStepRequest
{
    public DebugStepKind StepKind { get; set; } = DebugStepKind.Over;
}

public sealed class BreakpointSetRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; } = 1;

    public string? Condition { get; set; }

    public string? Action { get; set; }

    public string? ActionMessage { get; set; }

    public bool ContinueAfterAction { get; set; }

    public int? HitCount { get; set; }

    public string? HitCountType { get; set; }

    public string? DependsOnBreakpointName { get; set; }

    public string? GroupName { get; set; }
}

public sealed class BreakpointRemoveRequest
{
    public string? Name { get; set; }

    public string? DocumentPath { get; set; }

    public int Line { get; set; }
}

public sealed record BreakpointRemoveResult(int Removed);

public sealed class BreakpointEnableRequest
{
    public string? Name { get; set; }

    public string? DocumentPath { get; set; }

    public int Line { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed record BreakpointEnableResult(
    int Updated,
    IReadOnlyCollection<BreakpointInfo> Breakpoints,
    DebuggerStateInfo? State = null);

public sealed record BreakpointListResult(
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointInfo(
    string? Name,
    string? File,
    int Line,
    int Column,
    string? FunctionName,
    string? Condition,
    bool Enabled,
    string? Action = null,
    string? ActionMessage = null,
    bool ContinueAfterAction = false,
    int? HitCount = null,
    string? HitCountType = null,
    string? DependsOnBreakpointName = null,
    string? GroupName = null);

public sealed record BreakpointGroupListResult(
    IReadOnlyCollection<string> Groups,
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointGroupOperationResult(
    string GroupName,
    int Matched,
    int Updated,
    IReadOnlyCollection<BreakpointInfo> Breakpoints,
    DebuggerStateInfo? State = null);

public sealed record CallStackResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<CallStackFrameInfo> Frames);

public sealed record CallStackFrameInfo(
    string? FunctionName,
    string? File,
    int Line,
    int Column);

public sealed record LocalsResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<DebugExpressionInfo> Locals);

public sealed class EvaluateExpressionRequest
{
    public string Expression { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; } = 5000;
}

public sealed record EvaluateExpressionResult(
    DebuggerStateInfo State,
    DebugExpressionInfo Expression);

public sealed record DebugExpressionInfo(
    string? Name,
    string? Value,
    string? Type,
    bool IsValidValue);

public sealed record VsContextSnapshotResult(
    VsSessionInfo? Session,
    SolutionInfoResult? Solution,
    string? ActiveDocument,
    SelectionInfo? Selection,
    DebuggerStateInfo? Debugger,
    BuildStatusInfo? Build,
    ErrorListResult? Errors,
    PendingEditListResult? PendingEdits);

public sealed record SolutionOverviewResult(
    SolutionInfoResult Solution,
    ProjectListResult Projects,
    StartupProjectResult StartupProject,
    IReadOnlyCollection<ProjectInfo> TestProjects);

public sealed record ProjectDependenciesResult(
    ProjectInfo? Project,
    IReadOnlyCollection<string> TargetFrameworks,
    IReadOnlyCollection<ProjectDependencyInfo> ProjectReferences,
    IReadOnlyCollection<ProjectDependencyInfo> PackageReferences);

public sealed record ProjectDependencyInfo(
    string Name,
    string? Version,
    string? Path);

public sealed record BuildAndGetErrorsResult(
    BuildSolutionResult Build,
    ErrorListResult Errors);

public sealed record TestRunAndGetResultsResult(
    TestOperationResult Run,
    TestOperationResult Results);

public sealed record SymbolContextResult(
    DocumentReadResult Document,
    GoToDefinitionResult Definition,
    FindReferencesResult References,
    string Snippet);

public sealed record OpenRelevantFilesResult(
    IReadOnlyCollection<EditorDocumentInfo> Documents);

public sealed record PrepareSafeEditResult(
    DocumentReadResult Original,
    EditPreviewResult Preview);

public sealed record ApplySafeEditAndBuildResult(
    EditDecisionResult Edit,
    BuildSolutionResult Build,
    ErrorListResult Errors);

public sealed record DebugSnapshotResult(
    DebuggerStateInfo State,
    CallStackResult? CallStack,
    LocalsResult? Locals,
    BreakpointListResult? Breakpoints,
    WatchListResult? Watch = null,
    DebugThreadListResult? Threads = null,
    ModuleListResult? Modules = null,
    ParallelStacksResult? ParallelStacks = null,
    ParallelWatchResult? ParallelWatch = null,
    IReadOnlyCollection<string>? UnrecognizedInclude = null,
    bool TimedOut = false);

public sealed record DebugEvalManyResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<EvaluateExpressionResult> Results);

public sealed record WorkspaceSearchResult(
    string RootPath,
    IReadOnlyCollection<WorkspaceSearchMatch> Matches,
    bool Truncated);

public sealed record WorkspaceSearchMatch(
    string Path,
    int? Line,
    string? Preview);

public sealed record DocumentOutlineResult(
    string DocumentPath,
    IReadOnlyCollection<string> Symbols);

public sealed record RenameSymbolPreviewResult(
    bool Supported,
    string Message,
    CodePositionRequest Position,
    string NewName,
    DocumentSymbolInfo? Symbol = null,
    IReadOnlyCollection<RenameSymbolChangeInfo>? Changes = null);

public sealed record FindImplementationsResult(
    bool Supported,
    string Message,
    CodePositionRequest Position,
    IReadOnlyCollection<CodeLocationInfo> Implementations);

public sealed record FormatAndOrganizeResult(
    DocumentCleanupResult Cleanup,
    string Message);

public sealed record DiagnosticsForDocumentResult(
    string DocumentPath,
    IReadOnlyCollection<ErrorListItemInfo> Items);

public sealed record PackageRestoreResult(
    bool Supported,
    string Message,
    ProjectInfo? Project,
    int ExitCode = 0);

public sealed record GitContextResult(
    bool Supported,
    string Message,
    string? RootPath,
    IReadOnlyCollection<string> ChangedFiles);

public sealed record BrokerStatus(
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    DateTimeOffset StartedUtc,
    string Version,
    IReadOnlyCollection<VsSessionStatus> Sessions);

public sealed record BrokerToolDescriptor(
    string Name,
    string Description,
    bool RequiresVisualStudioSession,
    BrokerToolCategory Category = BrokerToolCategory.Read);

public sealed record BrokerCapabilities(
    string McpEndpoint,
    IReadOnlyCollection<BrokerToolDescriptor> Tools,
    IReadOnlyCollection<VsCapability> VisualStudioCapabilities);

public sealed record RouteResult(
    bool Success,
    VsSessionInfo? Session,
    RouteFailureReason FailureReason,
    string? Message,
    IReadOnlyCollection<VsSessionInfo> Candidates)
{
    public static RouteResult Found(VsSessionInfo session) =>
        new(true, session, RouteFailureReason.None, null, Array.Empty<VsSessionInfo>());

    public static RouteResult Failed(
        RouteFailureReason reason,
        string message,
        IReadOnlyCollection<VsSessionInfo>? candidates = null) =>
        new(false, null, reason, message, candidates ?? Array.Empty<VsSessionInfo>());
}

public sealed record ToolResponse(
    bool Success,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse Ok(string? message = null) => new(true, message);

    public static ToolResponse Fail(string message) => new(false, message);
}

public sealed record ToolResponse<T>(
    bool Success,
    T? Value,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse<T> Ok(T value, string? message = null) => new(true, value, message);

    public static ToolResponse<T> Fail(string message) => new(false, default, message);
}

public sealed class AutomationRequest
{
    public string ToolName { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Selector { get; set; }
    public string? Url { get; set; }
    public string? Text { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int TimeoutMilliseconds { get; set; } = 5000;
}

public sealed record AutomationResult(
    bool Supported,
    bool Success,
    string? Message,
    string? Text = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record BrokerLogEntry(
    string Path,
    string Name,
    DateTimeOffset LastWriteUtc,
    long Length,
    string Text,
    bool Truncated);

public sealed record BrokerLogResult(
    string LogsDirectory,
    IReadOnlyCollection<BrokerLogEntry> Files);

public interface IBrokerRegistrationRpc
{
    Task<ToolResponse> RegisterAsync(VsSessionRegistration registration, CancellationToken cancellationToken);

    Task<ToolResponse> UpdateAsync(VsSessionUpdate update, CancellationToken cancellationToken);

    Task<ToolResponse> HeartbeatAsync(string sessionId, CancellationToken cancellationToken);

    Task<ToolResponse> UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}

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
