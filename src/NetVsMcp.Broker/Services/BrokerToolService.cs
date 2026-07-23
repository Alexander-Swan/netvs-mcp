using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NetVsMcp.Broker.Services;

[McpServerToolType]
public sealed class BrokerToolService
{
    private static readonly BrokerToolDescriptor[] ToolDescriptors =
    [
        new("vs_list_sessions", "Lists Visual Studio instances registered with the local broker.", false),
        new("vs_get_status", "Returns local broker endpoint, uptime, and registered session status.", false),
        new("vs_get_capabilities", "Lists broker tools and Visual Studio capability categories.", false),
        new("vs_get_session", "Resolves a Visual Studio session and returns its current broker status.", false),
        new("vs_select_session", "Resolves a Visual Studio session using broker routing rules without persisting selection.", false),
        new("vs_ping", "Returns lightweight broker health and optional routed Visual Studio session status.", false),
        new("document_active", "Returns the active document for a routed Visual Studio session.", true),
        new("code_document_symbols", "Lists document symbols through a routed Visual Studio session.", true),
        new("code_go_to_definition", "Finds and navigates to a symbol definition through a routed Visual Studio session.", true),
        new("code_find_references", "Finds symbol references through a routed Visual Studio session.", true),
        new("build_solution", "Starts a solution build in a routed Visual Studio session.", true),
        new("build_status", "Returns build status from a routed Visual Studio session.", true),
        new("errors_list", "Lists errors and warnings from a routed Visual Studio session.", true),
        new("output_read", "Reads an output pane from a routed Visual Studio session.", true),
        new("debug_status", "Returns debugger status from a routed Visual Studio session.", true),
        new("debug_get_mode", "Returns debugger mode from a routed Visual Studio session.", true),
        new("debug_start", "Starts debugging in a routed Visual Studio session.", true),
        new("debug_stop", "Stops debugging in a routed Visual Studio session.", true),
        new("debug_continue", "Continues debugging in a routed Visual Studio session.", true),
        new("debug_break", "Breaks into debugging in a routed Visual Studio session.", true),
        new("debug_step", "Steps the debugger in a routed Visual Studio session.", true),
        new("breakpoint_set", "Sets a breakpoint in a routed Visual Studio session.", true),
        new("breakpoint_list", "Lists breakpoints from a routed Visual Studio session.", true),
        new("breakpoint_remove", "Removes breakpoints in a routed Visual Studio session.", true),
        new("breakpoint_enable", "Enables or disables breakpoints in a routed Visual Studio session.", true),
        new("debug_get_callstack", "Returns the current call stack from a routed Visual Studio session.", true),
        new("debug_get_locals", "Returns locals from a routed Visual Studio session.", true),
        new("debug_evaluate", "Evaluates an expression in a routed Visual Studio session.", true),
        new("document_read", "Reads a document through a routed Visual Studio session.", true),
        new("document_open", "Opens a document through a routed Visual Studio session.", true),
        new("selection_get", "Returns the current editor selection from a routed Visual Studio session.", true),
        new("document_write", "Replaces a document buffer through a routed Visual Studio session.", true),
        new("document_save", "Saves a document through a routed Visual Studio session.", true),
        new("editor_insert", "Inserts text through a routed Visual Studio session.", true),
        new("editor_replace", "Replaces a text range through a routed Visual Studio session.", true),
        new("editor_goto_line", "Moves the caret through a routed Visual Studio session.", true),
        new("selection_set", "Sets the editor selection through a routed Visual Studio session.", true),
        new("document_cleanup", "Formats/cleans up a document through a routed Visual Studio session.", true),
        new("edit_preview", "Creates a pending safe-edit preview through a routed Visual Studio session.", true),
        new("edit_approve", "Approves a pending safe edit through a routed Visual Studio session.", true),
        new("edit_reject", "Rejects a pending safe edit through a routed Visual Studio session.", true),
        new("edit_list_pending", "Lists pending safe edits through a routed Visual Studio session.", true),
        new("solution_info", "Returns solution metadata from a routed Visual Studio session.", true),
        new("project_list", "Lists projects from a routed Visual Studio session.", true),
        new("project_info", "Returns project metadata from a routed Visual Studio session.", true),
        new("startup_project_get", "Returns startup project metadata from a routed Visual Studio session.", true),
        new("startup_project_set", "Sets the startup project in a routed Visual Studio session.", true),
        new("test_discover", "Discovers tests through a routed Visual Studio session.", true),
        new("test_run", "Runs tests through a routed Visual Studio session.", true),
        new("test_results", "Returns test results through a routed Visual Studio session.", true)
    ];

    private static readonly VsCapability[] VisualStudioCapabilities =
    [
        VsCapability.Editor,
        VsCapability.Navigation,
        VsCapability.Build,
        VsCapability.Debugger,
        VsCapability.Diagnostics,
        VsCapability.Tests,
        VsCapability.ProjectSystem
    ];

    private readonly BrokerRuntime _runtime;

    public BrokerToolService(BrokerRuntime runtime)
    {
        _runtime = runtime;
    }

    [McpServerTool(Name = "vs_list_sessions")]
    [Description("Lists Visual Studio instances registered with the local NetVsMcp broker.")]
    public ToolResponse<IReadOnlyCollection<VsSessionInfo>> VsListSessions()
    {
        var response = ToolResponse<IReadOnlyCollection<VsSessionInfo>>.Ok(_runtime.Sessions.ListSessions());
        AuditToolResult(nameof(VsListSessions), null, response.Success, null, response.Message);
        return response;
    }

    [McpServerTool(Name = "vs_get_status")]
    [Description("Returns local broker endpoint, uptime, registration pipe, and registered Visual Studio session status.")]
    public ToolResponse<BrokerStatus> VsGetStatus()
    {
        var response = ToolResponse<BrokerStatus>.Ok(_runtime.GetStatus());
        AuditToolResult(nameof(VsGetStatus), null, response.Success, null, response.Message);
        return response;
    }

    [McpServerTool(Name = "vs_get_capabilities")]
    [Description("Lists NetVsMcp broker tools and Visual Studio capability categories.")]
    public ToolResponse<BrokerCapabilities> VsGetCapabilities()
    {
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            _runtime.Options.CapabilityProfile,
            ToolDescriptors.Select(WithAccessMetadata).ToArray(),
            VisualStudioCapabilities);

        var response = ToolResponse<BrokerCapabilities>.Ok(capabilities);
        AuditToolResult(nameof(VsGetCapabilities), null, response.Success, null, response.Message);
        return response;
    }

    [McpServerTool(Name = "vs_get_session")]
    [Description("Resolves a Visual Studio session using sessionId, solutionName, or solutionPath and returns its current broker status.")]
    public ToolResponse<VsSessionStatus> VsGetSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<VsSessionStatus>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsGetSession), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var status = GetSessionStatus(route.Session);
        var response = status is null
            ? ToolResponse<VsSessionStatus>.Fail($"Visual Studio session '{route.Session.SessionId}' is no longer registered.")
            : ToolResponse<VsSessionStatus>.Ok(status);
        AuditToolResult(nameof(VsGetSession), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }

    [McpServerTool(Name = "vs_select_session")]
    [Description("Resolves and returns a Visual Studio session using broker routing rules without storing global selection state.")]
    public ToolResponse<VsSessionInfo> VsSelectSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<VsSessionInfo>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsSelectSession), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var response = ToolResponse<VsSessionInfo>.Ok(route.Session);
        AuditToolResult(nameof(VsSelectSession), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }

    [McpServerTool(Name = "vs_ping")]
    [Description("Returns lightweight broker health and optional routed Visual Studio session status.")]
    public ToolResponse<BrokerPing> VsPing(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        if (!HasRoutingFields(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath))
        {
            var brokerOnlyResponse = ToolResponse<BrokerPing>.Ok(CreatePing(null));
            AuditToolResult(nameof(VsPing), null, brokerOnlyResponse.Success, null, brokerOnlyResponse.Message);
            return brokerOnlyResponse;
        }

        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<BrokerPing>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsPing), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var status = GetSessionStatus(route.Session);
        var response = ToolResponse<BrokerPing>.Ok(CreatePing(status));
        AuditToolResult(nameof(VsPing), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }

    [McpServerTool(Name = "document_active")]
    [Description("Returns the active document for a routed Visual Studio session.")]
    public async Task<ToolResponse<string?>> DocumentActive(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.GetActiveDocumentAsync(ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(DocumentActive), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "code_document_symbols")]
    [Description("Lists document symbols for a document in a routed Visual Studio session.")]
    public async Task<ToolResponse<IReadOnlyCollection<string>>> CodeDocumentSymbols(
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<IReadOnlyCollection<string>>.Fail("Document path is required.");
        }

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ListDocumentSymbolsAsync(documentPath, ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(CodeDocumentSymbols), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "code_go_to_definition")]
    [Description("Finds and navigates to a symbol definition through a routed Visual Studio session.")]
    public Task<ToolResponse<GoToDefinitionResult>> CodeGoToDefinition(
        string documentPath,
        int line,
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<GoToDefinitionResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeGoToDefinitionAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "code_find_references")]
    [Description("Finds symbol references through a routed Visual Studio session.")]
    public Task<ToolResponse<FindReferencesResult>> CodeFindReferences(
        string documentPath,
        int line,
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<FindReferencesResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindReferencesAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_read")]
    [Description("Reads a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentReadResult>> DocumentRead(
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<DocumentReadResult>.Fail(validation));
        }

        var request = new DocumentReadRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentReadAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_open")]
    [Description("Opens a document through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> DocumentOpen(
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(validation));
        }

        var request = new DocumentOpenRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentOpenAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "selection_get")]
    [Description("Returns the current editor selection from a routed Visual Studio session.")]
    public Task<ToolResponse<SelectionInfo?>> SelectionGet(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.SelectionGetAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_write")]
    [Description("Replaces a document buffer through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentWrite(
        string path,
        string text,
        bool createIfMissing = false,
        bool saveAfterWrite = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new DocumentWriteRequest
        {
            Path = path.Trim(),
            Text = text,
            CreateIfMissing = createIfMissing,
            SaveAfterWrite = saveAfterWrite
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentWriteAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_save")]
    [Description("Saves a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentSave(
        string? path = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DocumentSaveRequest { Path = NormalizeOptional(path) ?? string.Empty };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentSaveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_insert")]
    [Description("Inserts text through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorInsert(
        string path,
        int line,
        int column,
        string text,
        bool saveAfterEdit = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (ValidatePosition(line, column) is { } positionValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(positionValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new EditorInsertRequest
        {
            Path = path.Trim(),
            Line = line,
            Column = column,
            Text = text,
            SaveAfterEdit = saveAfterEdit
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorInsertAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_replace")]
    [Description("Replaces a text range through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorReplace(
        string path,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string text,
        bool saveAfterEdit = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (ValidateRange(startLine, startColumn, endLine, endColumn) is { } rangeValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(rangeValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new EditorReplaceRequest
        {
            Path = path.Trim(),
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn,
            Text = text,
            SaveAfterEdit = saveAfterEdit
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorReplaceAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_goto_line")]
    [Description("Moves the caret through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> EditorGotoLine(
        string path,
        int line,
        int column = 1,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(pathValidation));
        }

        if (ValidatePosition(line, column) is { } positionValidation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(positionValidation));
        }

        var request = new EditorGotoLineRequest
        {
            Path = path.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorGotoLineAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "selection_set")]
    [Description("Sets the editor selection through a routed Visual Studio session.")]
    public Task<ToolResponse<SelectionInfo>> SelectionSet(
        string path,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(pathValidation));
        }

        if (ValidateRange(startLine, startColumn, endLine, endColumn) is { } rangeValidation)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(rangeValidation));
        }

        var request = new SelectionSetRequest
        {
            Path = path.Trim(),
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SelectionSetAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_cleanup")]
    [Description("Formats/cleans up a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentCleanupResult>> DocumentCleanup(
        string path,
        bool saveAfterCleanup = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentCleanupResult>.Fail(pathValidation));
        }

        var request = new DocumentCleanupRequest
        {
            Path = path.Trim(),
            SaveAfterCleanup = saveAfterCleanup
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentCleanupAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "edit_preview")]
    [Description("Creates a pending safe-edit preview through a routed Visual Studio session.")]
    public Task<ToolResponse<EditPreviewResult>> EditPreview(
        string operation,
        string path,
        string text,
        bool createIfMissing = false,
        bool saveAfterEdit = false,
        int line = 0,
        int column = 0,
        int startLine = 0,
        int startColumn = 0,
        int endLine = 0,
        int endColumn = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditPreview(operation, path, text, line, column, startLine, startColumn, endLine, endColumn) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditPreviewResult>.Fail(validation));
        }

        var normalizedOperation = operation.Trim().ToLowerInvariant();
        var request = new EditPreviewRequest
        {
            Operation = normalizedOperation,
            Path = path.Trim(),
            Text = text,
            CreateIfMissing = createIfMissing,
            SaveAfterEdit = saveAfterEdit,
            Line = line,
            Column = column,
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditPreviewAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "edit_approve")]
    [Description("Approves a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditApprove(
        string editId,
        bool saveAfterApply = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditDecisionResult>.Fail(validation));
        }

        var request = new EditDecisionRequest
        {
            EditId = editId.Trim(),
            SaveAfterApply = saveAfterApply
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditApproveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "edit_reject")]
    [Description("Rejects a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditReject(
        string editId,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditDecisionResult>.Fail(validation));
        }

        var request = new EditDecisionRequest { EditId = editId.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditRejectAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "edit_list_pending")]
    [Description("Lists pending safe edits through a routed Visual Studio session.")]
    public Task<ToolResponse<PendingEditListResult>> EditListPending(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.EditListPendingAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "solution_info")]
    [Description("Returns solution metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<SolutionInfoResult>> SolutionInfo(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.SolutionInfoAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "project_list")]
    [Description("Lists projects from a routed Visual Studio session.")]
    public Task<ToolResponse<ProjectListResult>> ProjectList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProjectListAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "project_info")]
    [Description("Returns project metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<ProjectInfo?>> ProjectInfo(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo?>.Fail(validation));
        }

        var request = new ProjectInfoRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ProjectInfoAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "startup_project_get")]
    [Description("Returns startup project metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<StartupProjectResult>> StartupProjectGet(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.StartupProjectGetAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "startup_project_set")]
    [Description("Sets the startup project in a routed Visual Studio session.")]
    public Task<ToolResponse<StartupProjectResult>> StartupProjectSet(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<StartupProjectResult>.Fail(validation));
        }

        var request = new StartupProjectSetRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.StartupProjectSetAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "test_discover")]
    [Description("Discovers tests through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestDiscover(
        string? projectName = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestDiscoverRequest { ProjectName = NormalizeOptional(projectName) };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestDiscoverAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "test_run")]
    [Description("Runs tests through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestRun(
        string? projectName = null,
        string? filter = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestRunRequest
        {
            ProjectName = NormalizeOptional(projectName),
            Filter = NormalizeOptional(filter)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestRunAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "test_results")]
    [Description("Returns test results through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestResults(
        string? runId = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestResultsRequest { RunId = NormalizeOptional(runId) };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestResultsAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "build_solution")]
    [Description("Starts a solution build in a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildSolutionResult>> BuildSolution(
        bool waitForBuildToFinish = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        if (ValidateToolAccess(nameof(BuildSolution), target) is { } denied)
        {
            return denied.As<BuildSolutionResult>();
        }

        var request = new BuildSolutionRequest
        {
            WaitForBuildToFinish = waitForBuildToFinish
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            (connection, ct) => connection.BuildSolutionAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(BuildSolution), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "build_status")]
    [Description("Returns build status from a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildStatusInfo>> BuildStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.BuildStatusAsync(ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(BuildStatus), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "errors_list")]
    [Description("Lists errors and warnings from a routed Visual Studio session.")]
    public async Task<ToolResponse<ErrorListResult>> ErrorsList(
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return ToolResponse<ErrorListResult>.Fail("Max items must be greater than zero.");
        }

        var request = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ErrorsListAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(ErrorsList), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "output_read")]
    [Description("Reads an output pane from a routed Visual Studio session.")]
    public async Task<ToolResponse<OutputReadResult>> OutputRead(
        string? paneName = null,
        int maxChars = 20000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxChars < 1)
        {
            return ToolResponse<OutputReadResult>.Fail("Max chars must be greater than zero.");
        }

        var request = new OutputReadRequest
        {
            PaneName = NormalizeOptional(paneName),
            MaxChars = maxChars
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.OutputReadAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(OutputRead), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "debug_status")]
    [Description("Returns debugger status from a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStatusAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_mode")]
    [Description("Returns debugger mode from a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugGetMode(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetModeAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_start")]
    [Description("Starts debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStart(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStartAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_stop")]
    [Description("Stops debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStop(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStopAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_continue")]
    [Description("Continues debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugContinue(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugContinueAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_break")]
    [Description("Breaks into debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugBreak(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugBreakAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_step")]
    [Description("Steps the debugger in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStep(
        DebugStepKind stepKind = DebugStepKind.Over,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(stepKind))
        {
            return Task.FromResult(ToolResponse<DebuggerStateInfo>.Fail("Debug step kind is invalid."));
        }

        var request = new DebugStepRequest
        {
            StepKind = stepKind
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DebugStepAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_set")]
    [Description("Sets a breakpoint in a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointInfo>> BreakpointSet(
        string documentPath,
        int line,
        int column = 1,
        string? condition = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return Task.FromResult(ToolResponse<BreakpointInfo>.Fail("Document path is required."));
        }

        if (line < 1)
        {
            return Task.FromResult(ToolResponse<BreakpointInfo>.Fail("Breakpoint line must be greater than zero."));
        }

        if (column < 1)
        {
            return Task.FromResult(ToolResponse<BreakpointInfo>.Fail("Breakpoint column must be greater than zero."));
        }

        var request = new BreakpointSetRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            Condition = NormalizeOptional(condition)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointSetAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_list")]
    [Description("Lists breakpoints from a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointListResult>> BreakpointList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.BreakpointListAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_remove")]
    [Description("Removes breakpoints in a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointRemoveResult>> BreakpointRemove(
        string? name = null,
        string? documentPath = null,
        int line = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBreakpointLookup(name, documentPath, line);
        if (validation is not null)
        {
            return Task.FromResult(ToolResponse<BreakpointRemoveResult>.Fail(validation));
        }

        var request = new BreakpointRemoveRequest
        {
            Name = NormalizeOptional(name),
            DocumentPath = NormalizeOptional(documentPath),
            Line = line
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointRemoveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_enable")]
    [Description("Enables or disables breakpoints in a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointEnableResult>> BreakpointEnable(
        bool enabled,
        string? name = null,
        string? documentPath = null,
        int line = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBreakpointLookup(name, documentPath, line);
        if (validation is not null)
        {
            return Task.FromResult(ToolResponse<BreakpointEnableResult>.Fail(validation));
        }

        var request = new BreakpointEnableRequest
        {
            Name = NormalizeOptional(name),
            DocumentPath = NormalizeOptional(documentPath),
            Line = line,
            Enabled = enabled
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointEnableAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_callstack")]
    [Description("Returns the current call stack from a routed Visual Studio session.")]
    public Task<ToolResponse<CallStackResult>> DebugGetCallstack(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetCallstackAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_locals")]
    [Description("Returns locals from a routed Visual Studio session.")]
    public Task<ToolResponse<LocalsResult>> DebugGetLocals(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetLocalsAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_evaluate")]
    [Description("Evaluates an expression in a routed Visual Studio session.")]
    public Task<ToolResponse<EvaluateExpressionResult>> DebugEvaluate(
        string expression,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("Expression is required."));
        }

        if (timeoutMilliseconds < 1)
        {
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("Timeout milliseconds must be greater than zero."));
        }

        var request = new EvaluateExpressionRequest
        {
            Expression = expression.Trim(),
            TimeoutMilliseconds = timeoutMilliseconds
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DebugEvaluateAsync(request, ct),
            cancellationToken);
    }

    private static RoutingTarget? CreateTarget(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId) &&
            string.IsNullOrWhiteSpace(solutionName) &&
            string.IsNullOrWhiteSpace(solutionPath) &&
            processId is null &&
            string.IsNullOrWhiteSpace(workspacePath) &&
            string.IsNullOrWhiteSpace(rootPath))
        {
            return null;
        }

        return new RoutingTarget(
            NormalizeOptional(sessionId),
            NormalizeOptional(solutionName),
            NormalizeOptional(solutionPath),
            processId,
            NormalizeOptional(workspacePath),
            NormalizeOptional(rootPath));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private VsSessionStatus? GetSessionStatus(VsSessionInfo session)
    {
        return _runtime.Sessions.ListSessionStatuses()
            .SingleOrDefault(status => string.Equals(
                status.Session.SessionId,
                session.SessionId,
                StringComparison.OrdinalIgnoreCase));
    }

    private BrokerPing CreatePing(VsSessionStatus? targetSession)
    {
        return new BrokerPing(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            IsRunning: _runtime.IsHttpEndpointRunning,
            McpEndpoint: _runtime.Options.McpEndpoint,
            PipeName: _runtime.Options.PipeName,
            Uptime: DateTimeOffset.UtcNow - _runtime.StartedUtc,
            RegisteredSessionCount: _runtime.Sessions.ListSessions().Count,
            TargetSession: targetSession);
    }

    private static bool HasRoutingFields(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        return !string.IsNullOrWhiteSpace(sessionId) ||
            !string.IsNullOrWhiteSpace(solutionName) ||
            !string.IsNullOrWhiteSpace(solutionPath) ||
            processId is not null ||
            !string.IsNullOrWhiteSpace(workspacePath) ||
            !string.IsNullOrWhiteSpace(rootPath);
    }

    private static string? ValidateBreakpointLookup(
        string? name,
        string? documentPath,
        int line)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Breakpoint name or document path is required.";
        }

        return line < 1
            ? "Breakpoint line must be greater than zero."
            : null;
    }

    private static string? ValidateRequiredPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? "Path is required."
            : null;
    }

    private static string? ValidatePosition(int line, int column)
    {
        if (line < 1)
        {
            return "Line must be greater than zero.";
        }

        return column < 1
            ? "Column must be greater than zero."
            : null;
    }

    private static string? ValidateRange(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        if (ValidatePosition(startLine, startColumn) is { } startValidation)
        {
            return startValidation;
        }

        if (ValidatePosition(endLine, endColumn) is { } endValidation)
        {
            return endValidation;
        }

        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            return "End position must be greater than or equal to start position.";
        }

        return null;
    }

    private static string? ValidateEditPreview(
        string? operation,
        string? path,
        string? text,
        int line,
        int column,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return "Edit operation is required.";
        }

        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return pathValidation;
        }

        if (text is null)
        {
            return "Text is required.";
        }

        return operation.Trim().ToLowerInvariant() switch
        {
            "write" => null,
            "insert" => ValidatePosition(line, column),
            "replace" => ValidateRange(startLine, startColumn, endLine, endColumn),
            _ => "Edit operation must be one of: write, insert, replace."
        };
    }

    private static string? ValidateEditId(string? editId)
    {
        return string.IsNullOrWhiteSpace(editId)
            ? "Edit id is required."
            : null;
    }

    private static string? ValidateProjectName(string? projectName)
    {
        return string.IsNullOrWhiteSpace(projectName)
            ? "Project name is required."
            : null;
    }

    private static string? ValidateCodePosition(
        string? documentPath,
        int line,
        int column)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Document path is required.";
        }

        return ValidatePosition(line, column);
    }

    private static ToolResponse<T> ToToolResponse<T>(
        VsSessionDispatchResult<ToolResponse<T>> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        return dispatch.Value ?? ToolResponse<T>.Fail("Visual Studio session returned no response.");
    }

    private static ToolResponse<T> ToValueToolResponse<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        if (dispatch.Value is null)
        {
            return ToolResponse<T>.Fail("Visual Studio session returned no response.");
        }

        return ToolResponse<T>.Ok(dispatch.Value);
    }

    private async Task<ToolResponse<T>> DispatchValueAsync<T>(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        if (ValidateToolAccess(toolName, target) is { } denied)
        {
            return denied.As<T>();
        }

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            operation,
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(toolName, target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    private ToolAccessDenied? ValidateToolAccess(string toolName, RoutingTarget? target)
    {
        var mcpToolName = ToMcpToolName(toolName);
        var category = CategorizeTool(mcpToolName);
        if (BrokerToolAccessPolicy.IsAllowed(_runtime.Options.CapabilityProfile, category))
        {
            return null;
        }

        var message = $"Tool '{mcpToolName}' requires profile '{BrokerToolAccessPolicy.MinimumProfile(category)}' or higher; active profile is '{_runtime.Options.CapabilityProfile}'.";
        AuditToolResult(toolName, target, false, null, message, "CapabilityProfileDenied");
        return new ToolAccessDenied(message, new Dictionary<string, string>
        {
            ["failureReason"] = "CapabilityProfileDenied",
            ["activeProfile"] = _runtime.Options.CapabilityProfile.ToString(),
            ["requiredProfile"] = BrokerToolAccessPolicy.MinimumProfile(category).ToString(),
            ["category"] = category.ToString()
        });
    }

    private void AuditToolResult(
        string toolName,
        RoutingTarget? target,
        bool success,
        string? selectedSessionId,
        string? message,
        string? failureReason = null)
    {
        try
        {
            _runtime.AuditLog.RecordToolCall(new AuditToolCall(
                TimestampUtc: DateTimeOffset.UtcNow,
                ToolName: ToMcpToolName(toolName),
                Success: success,
                SessionId: selectedSessionId ?? target?.SessionId,
                SolutionName: target?.SolutionName,
                SolutionPath: target?.SolutionPath,
                FailureReason: success ? null : NormalizeFailureReason(failureReason),
                Message: TruncateAuditMessage(message)));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp audit logging failed: {ex}");
        }
    }

    private static string? NormalizeFailureReason(string? failureReason)
    {
        return string.IsNullOrWhiteSpace(failureReason) || failureReason == "None"
            ? null
            : failureReason;
    }

    private static string? TruncateAuditMessage(string? message)
    {
        const int maxLength = 500;
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message[..maxLength];
    }

    private static string ToMcpToolName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return "unknown";
        }

        var chars = new List<char>(methodName.Length + 8);
        for (var index = 0; index < methodName.Length; index++)
        {
            var character = methodName[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string([.. chars]);
    }

    private static BrokerToolDescriptor WithAccessMetadata(BrokerToolDescriptor descriptor)
    {
        var category = CategorizeTool(descriptor.Name);
        return descriptor with
        {
            Category = category,
            MinimumProfile = BrokerToolAccessPolicy.MinimumProfile(category)
        };
    }

    private static BrokerToolCategory CategorizeTool(string toolName)
    {
        if (toolName.StartsWith("vs_", StringComparison.Ordinal))
        {
            return BrokerToolCategory.Broker;
        }

        return toolName switch
        {
            "document_active" or
            "document_read" or
            "document_open" or
            "selection_get" or
            "code_document_symbols" or
            "code_go_to_definition" or
            "code_find_references" or
            "solution_info" or
            "project_list" or
            "project_info" or
            "startup_project_get" or
            "build_status" or
            "errors_list" or
            "output_read" or
            "debug_status" or
            "debug_get_mode" or
            "debug_get_callstack" or
            "debug_get_locals" or
            "breakpoint_list" or
            "edit_list_pending" => BrokerToolCategory.Read,

            "edit_preview" or
            "edit_reject" => BrokerToolCategory.EditPreview,

            "document_write" or
            "document_save" or
            "editor_insert" or
            "editor_replace" or
            "editor_goto_line" or
            "selection_set" or
            "document_cleanup" or
            "edit_approve" => BrokerToolCategory.EditDirect,

            "build_solution" => BrokerToolCategory.Build,

            "debug_start" or
            "debug_stop" or
            "debug_continue" or
            "debug_break" or
            "debug_step" or
            "debug_evaluate" or
            "breakpoint_set" or
            "breakpoint_remove" or
            "breakpoint_enable" => BrokerToolCategory.Debug,

            "startup_project_set" => BrokerToolCategory.Admin,

            "test_discover" or
            "test_run" or
            "test_results" => BrokerToolCategory.Test,

            _ => BrokerToolCategory.Admin
        };
    }

    private static IReadOnlyDictionary<string, string> CreateRouteFailureMetadata(RouteResult route)
    {
        var metadata = new Dictionary<string, string>
        {
            ["failureReason"] = route.FailureReason.ToString()
        };

        AddCandidateMetadata(metadata, route.Candidates);
        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateFailureMetadata<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        var metadata = new Dictionary<string, string>
        {
            ["failureReason"] = dispatch.FailureReason.ToString()
        };

        if (dispatch.Session is not null)
        {
            metadata["sessionId"] = dispatch.Session.SessionId;
        }

        if (dispatch.Candidates.Count > 0)
        {
            AddCandidateMetadata(metadata, dispatch.Candidates);
        }

        return metadata;
    }

    private static void AddCandidateMetadata(
        IDictionary<string, string> metadata,
        IReadOnlyCollection<VsSessionInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        metadata["candidateCount"] = candidates.Count.ToString();
        metadata["candidateSessionIds"] = string.Join(",", candidates.Select(candidate => candidate.SessionId));
        metadata["candidateProcessIds"] = string.Join(",", candidates.Select(candidate => candidate.ProcessId));
        metadata["candidateSolutionNames"] = string.Join(",", candidates.Select(candidate => candidate.SolutionName ?? string.Empty));
        metadata["candidateSolutionPaths"] = string.Join("|", candidates.Select(candidate => candidate.SolutionPath ?? string.Empty));
        metadata["candidateActiveWindow"] = string.Join(",", candidates.Select(candidate => candidate.IsActiveWindow.ToString()));
        metadata["candidateLastSeenUtc"] = string.Join(",", candidates.Select(candidate => candidate.LastSeenUtc.ToString("O")));
    }
}

internal sealed record ToolAccessDenied(
    string Message,
    IReadOnlyDictionary<string, string> Metadata)
{
    public ToolResponse<T> As<T>() => new(false, default, Message, Metadata);
}

public sealed record BrokerPing(
    DateTimeOffset ServerTimeUtc,
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    TimeSpan Uptime,
    int RegisteredSessionCount,
    VsSessionStatus? TargetSession);
