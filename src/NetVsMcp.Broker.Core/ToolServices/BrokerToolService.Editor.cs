using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "selection_get", Title = "Selection Get", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "document_write", Title = "Document Write", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Replaces a document buffer through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentWrite(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        string? text = null,
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
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(MissingRequiredParameter("text")));
        }

        var request = new DocumentWriteRequest
        {
            Path = path!.Trim(),
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
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "document_save", Title = "Document Save", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Saves a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentSave(
        [Description(OptionalDocumentPathParameterDescription)]
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
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "editor_insert", Title = "Editor Insert", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Inserts text through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorInsert(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        [Description(LineParameterDescription)]
        int? line = null,
        [Description(ColumnParameterDescription)]
        int? column = null,
        string? text = null,
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
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(MissingRequiredParameter("text")));
        }

        var request = new EditorInsertRequest
        {
            Path = path!.Trim(),
            Line = line!.Value,
            Column = column!.Value,
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
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "editor_replace", Title = "Editor Replace", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Replaces a text range through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorReplace(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        [Description(LineParameterDescription)]
        int? startLine = null,
        [Description(ColumnParameterDescription)]
        int? startColumn = null,
        [Description(LineParameterDescription)]
        int? endLine = null,
        [Description(ColumnParameterDescription)]
        int? endColumn = null,
        string? text = null,
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

        var rangeValidation = startLine is null
            ? MissingRequiredParameter("startLine")
            : startColumn is null
                ? MissingRequiredParameter("startColumn")
                : endLine is null
                    ? MissingRequiredParameter("endLine")
                    : endColumn is null
                        ? MissingRequiredParameter("endColumn")
                        : ValidateRange(startLine.Value, startColumn.Value, endLine.Value, endColumn.Value);
        if (rangeValidation is not null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(rangeValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(MissingRequiredParameter("text")));
        }

        var request = new EditorReplaceRequest
        {
            Path = path!.Trim(),
            StartLine = startLine!.Value,
            StartColumn = startColumn!.Value,
            EndLine = endLine!.Value,
            EndColumn = endColumn!.Value,
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
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "editor_goto_line", Title = "Editor Goto Line", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Moves the caret through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> EditorGotoLine(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        [Description(LineParameterDescription)]
        int? line = null,
        [Description(ColumnParameterDescription)]
        int? column = 1,
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
            Path = path!.Trim(),
            Line = line!.Value,
            Column = column!.Value
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorGotoLineAsync(request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "selection_set", Title = "Selection Set", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Sets the editor selection through a routed Visual Studio session.")]
    public Task<ToolResponse<SelectionInfo>> SelectionSet(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        [Description(LineParameterDescription)]
        int? startLine = null,
        [Description(ColumnParameterDescription)]
        int? startColumn = null,
        [Description(LineParameterDescription)]
        int? endLine = null,
        [Description(ColumnParameterDescription)]
        int? endColumn = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(pathValidation));
        }

        var rangeValidation = startLine is null
            ? MissingRequiredParameter("startLine")
            : startColumn is null
                ? MissingRequiredParameter("startColumn")
                : endLine is null
                    ? MissingRequiredParameter("endLine")
                    : endColumn is null
                        ? MissingRequiredParameter("endColumn")
                        : ValidateRange(startLine.Value, startColumn.Value, endLine.Value, endColumn.Value);
        if (rangeValidation is not null)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(rangeValidation));
        }

        var request = new SelectionSetRequest
        {
            Path = path!.Trim(),
            StartLine = startLine!.Value,
            StartColumn = startColumn!.Value,
            EndLine = endLine!.Value,
            EndColumn = endColumn!.Value
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SelectionSetAsync(request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "document_cleanup", Title = "Document Cleanup", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Formats/cleans up a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentCleanupResult>> DocumentCleanup(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
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
            Path = path!.Trim(),
            SaveAfterCleanup = saveAfterCleanup
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentCleanupAsync(request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "format_and_organize", Title = "Format And Organize", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Formats/cleans up a document and reports organize-import status.")]
    public Task<ToolResponse<FormatAndOrganizeResult>> FormatAndOrganize(
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        bool saveAfterCleanup = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<FormatAndOrganizeResult>.Fail(pathValidation));
        }

        var request = new DocumentCleanupRequest
        {
            Path = path!.Trim(),
            SaveAfterCleanup = saveAfterCleanup
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var cleanup = await connection.DocumentCleanupAsync(request, ct);
                var message = cleanup.Command is null
                    ? "Document cleanup completed; organize imports command was not reported by the VSIX."
                    : $"Document cleanup completed with command '{cleanup.Command}'.";
                return new FormatAndOrganizeResult(cleanup, message);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditPreview, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "edit_preview", Title = "Edit Preview", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates a pending safe-edit preview through a routed Visual Studio session.")]
    public Task<ToolResponse<EditPreviewResult>> EditPreview(
        string? operation = null,
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        string? text = null,
        bool createIfMissing = false,
        bool saveAfterEdit = false,
        [Description("1-based line number; required when operation is 'insert'.")]
        int line = 0,
        [Description("1-based column number; required when operation is 'insert'.")]
        int column = 0,
        [Description("1-based start line number; required when operation is 'replace'.")]
        int startLine = 0,
        [Description("1-based start column number; required when operation is 'replace'.")]
        int startColumn = 0,
        [Description("1-based end line number; required when operation is 'replace'.")]
        int endLine = 0,
        [Description("1-based end column number; required when operation is 'replace'.")]
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

        var normalizedOperation = operation!.Trim().ToLowerInvariant();
        var normalizedPath = path!.Trim();
        var request = new EditPreviewRequest
        {
            Operation = normalizedOperation,
            Path = normalizedPath,
            Text = text!,
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
    [BrokerToolMetadata(BrokerToolCategory.EditPreview, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "prepare_safe_edit", Title = "Prepare Safe Edit", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Reads a document and creates a safe-edit preview through a routed Visual Studio session.")]
    public Task<ToolResponse<PrepareSafeEditResult>> PrepareSafeEdit(
        string? operation = null,
        [Description(DocumentPathParameterDescription)]
        string? path = null,
        string? text = null,
        bool createIfMissing = false,
        bool saveAfterEdit = false,
        [Description("1-based line number; required when operation is 'insert'.")]
        int line = 0,
        [Description("1-based column number; required when operation is 'insert'.")]
        int column = 0,
        [Description("1-based start line number; required when operation is 'replace'.")]
        int startLine = 0,
        [Description("1-based start column number; required when operation is 'replace'.")]
        int startColumn = 0,
        [Description("1-based end line number; required when operation is 'replace'.")]
        int endLine = 0,
        [Description("1-based end column number; required when operation is 'replace'.")]
        int endColumn = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditPreview(operation, path, text, line, column, startLine, startColumn, endLine, endColumn) is { } validation)
        {
            return Task.FromResult(ToolResponse<PrepareSafeEditResult>.Fail(validation));
        }

        var normalizedOperation = operation!.Trim().ToLowerInvariant();
        var normalizedPath = path!.Trim();
        var request = new EditPreviewRequest
        {
            Operation = normalizedOperation,
            Path = normalizedPath,
            Text = text!,
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
            async (connection, ct) =>
            {
                var original = await connection.DocumentReadAsync(new DocumentReadRequest { Path = normalizedPath }, ct);
                var preview = await connection.EditPreviewAsync(request, ct);
                return new PrepareSafeEditResult(original, preview);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditDirect, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "edit_approve", Title = "Edit Approve", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Approves a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditApprove(
        string? editId = null,
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
            EditId = editId!.Trim(),
            SaveAfterApply = saveAfterApply
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditApproveAsync(request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Build, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "apply_safe_edit_and_build", Title = "Apply Safe Edit And Build", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Approves a pending safe edit, builds the routed solution, and returns diagnostics.")]
    public Task<ToolResponse<ApplySafeEditAndBuildResult>> ApplySafeEditAndBuild(
        string? editId = null,
        bool saveAfterApply = true,
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<ApplySafeEditAndBuildResult>.Fail(validation));
        }

        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<ApplySafeEditAndBuildResult>.Fail("Max items must be greater than zero."));
        }

        var editRequest = new EditDecisionRequest
        {
            EditId = editId!.Trim(),
            SaveAfterApply = saveAfterApply
        };

        var errorsRequest = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var edit = await connection.EditApproveAsync(editRequest, ct);
                var build = await connection.BuildSolutionAsync(new BuildSolutionRequest { WaitForBuildToFinish = true }, ct);
                var errors = await connection.ErrorsListAsync(errorsRequest, ct);
                return new ApplySafeEditAndBuildResult(edit, build, errors);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.EditPreview, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "edit_reject", Title = "Edit Reject", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Rejects a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditReject(
        string? editId = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditDecisionResult>.Fail(validation));
        }

        var request = new EditDecisionRequest { EditId = editId!.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditRejectAsync(request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "edit_list_pending", Title = "Edit List Pending", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
        if (operation is null)
        {
            return MissingRequiredParameter("operation");
        }

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
            return MissingRequiredParameter("text");
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
        if (editId is null)
        {
            return MissingRequiredParameter("editId");
        }

        return string.IsNullOrWhiteSpace(editId)
            ? "Edit id is required."
            : null;
    }
}

