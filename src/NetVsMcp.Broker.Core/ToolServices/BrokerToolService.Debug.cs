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
    private const int DefaultCallStackFrameLimit = 2;
    // TODO: Negotiate per-session frame-inspection capabilities during registration instead
    // of discovering older VSIX versions through a missing RPC method at call time.
    private const string ThreadInspectionUnsupportedMessage = "This Visual Studio extension does not support thread-specific inspection yet. Omit threadId to inspect the current thread, or update the extension.";
    private static Task<DebugFrameInspectionResult> InspectDebugFrameAsync(IVisualStudioSessionRpc connection,
        DebugFrameInspectionRequest request, CancellationToken cancellationToken) =>
        request.ThreadId.HasValue ? connection.DebugInspectThreadFrameAsync(request, cancellationToken)
            : connection.DebugInspectFrameAsync(request, cancellationToken);

    private const string StackFrameUnsupportedMessage = "This Visual Studio extension does not support frame-specific operations yet. Omit stackFrame to use the current frame, or update the extension.";

    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_status", Title = "Debug Status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the current debugger state with breakReason when paused (breakpoint, stepCompleted, exception, manualPause, unknown, or other), including broker-defined dbgModalBlock with dialogs when VS is waiting on a modal window. Modal detection runs outside the VS UI thread, including when a dialog appears after debug_continue returned. underlyingMode preserves the last known native mode and underlyingModeIsStale indicates a cached reading. Respond to an appropriate enabled button with dialog_respond, then call debug_status again. If an earlier command returned an operationId, pass it to retrieve that original command's running/completed/failed outcome in operation without repeating the command. Without operationId, includes the latest retained operation for the selected session. Completed outcomes are retained for 10 minutes. Canceling a status check does not cancel a retained command.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStatus(
        [Description(DialogSessionDescription)] string? sessionId = null,
        [Description(DialogSolutionNameDescription)] string? solutionName = null,
        [Description(DialogSolutionPathDescription)] string? solutionPath = null,
        [Description("Optional exact operationId from an earlier modal-blocked command's state.operation.operationId or metadata.operationId. Retrieves that original command's outcome without executing it again. If routing is omitted, the ID selects its original VS session; explicit routing must match. Omit to inspect current state and the latest retained operation for the selected session.")] string? operationId = null,
        CancellationToken cancellationToken = default) =>
        ReadDebuggerStatus(sessionId, solutionName, solutionPath, operationId, cancellationToken);

    private async Task<ToolResponse<DebuggerStateInfo>> ReadDebuggerStatus(string? sessionId, string? solutionName,
        string? solutionPath, string? operationId, CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        if (operationId is not null && string.IsNullOrWhiteSpace(operationId))
            return FailWithCode<DebuggerStateInfo>("operationId must be a non-empty ID when supplied.", ToolErrorCodes.InvalidRequest);
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var watch = Stopwatch.StartNew();
        var response = await _runtime.DialogOperations.GetDebugStatusAsync(target, operationId, cancellationToken);
        AuditToolResult(toolName, target, response.Success, response.Value?.SessionId, response.Message,
            requestPayload: new { target, operationId }, responsePayload: response, duration: watch.Elapsed);
        return response;
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_hot_reload_apply", Title = "Debug Hot Reload Apply", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Applies pending code changes via Hot Reload (Debug.ApplyCodeChanges) to the running debuggee in a routed Visual Studio session. Requires an active debug session.")]
    public Task<ToolResponse<HotReloadApplyResult>> DebugHotReloadApply(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugHotReloadApplyAsync(ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_get_mode", Title = "Debug Get Mode", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the same effective debugger state as debug_status, including dbgModalBlock and dialogs when VS has a modal window. Respond with dialog_respond and check debug_status afterward. Use debug_status(operationId: ...) to retrieve the outcome of a specific modal-blocked command.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugGetMode(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return ReadDebuggerStatus(sessionId, solutionName, solutionPath, null, cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_start", Title = "Debug Start", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_stop", Title = "Debug Stop", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Stops an active debug session in a routed Visual Studio session. Call debug_status first; this tool is expected to fail when the debugger is already in design mode. After stopping, call debug_status again because Visual Studio can report the pre-stop mode in the immediate debug_stop response.")]
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_continue", Title = "Debug Continue", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_break", Title = "Debug Break", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_step", Title = "Debug Step", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
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
    private const string BreakpointActionMetadataWarning =
        "'dependsOnBreakpointName' is stored as informational metadata only. Visual Studio's EnvDTE automation API " +
        "does not expose breakpoint dependencies, so this breakpoint will not actually wait for another breakpoint " +
        "to be hit first. 'actionMessage' + 'continueAfterAction' are real: when this breakpoint is hit, the broker's " +
        "VSIX extension logs the (expression-interpolated, e.g. \"value={x}\") message to the Debug output pane and, " +
        "if requested, resumes execution automatically instead of breaking.";
    private static bool HasUnsupportedBreakpointActionMetadata(BreakpointSetRequest request) =>
        !string.IsNullOrWhiteSpace(request.DependsOnBreakpointName);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_set", Title = "Breakpoint Set", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Sets a breakpoint in a routed Visual Studio session.")]
    public async Task<ToolResponse<BreakpointInfo>> BreakpointSet(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column = 1,
        string? condition = null,
        string? action = null,
        string? actionMessage = null,
        bool continueAfterAction = false,
        int? hitCount = null,
        string? hitCountType = null,
        string? dependsOnBreakpointName = null,
        string? groupName = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return AuditLocalFailure<BreakpointInfo>(
                nameof(BreakpointSet),
                sessionId,
                solutionName,
                solutionPath,
                "Document path is required.");
        }

        if (line < 1)
        {
            return AuditLocalFailure<BreakpointInfo>(
                nameof(BreakpointSet),
                sessionId,
                solutionName,
                solutionPath,
                "Breakpoint line must be greater than zero.");
        }

        if (column < 1)
        {
            return AuditLocalFailure<BreakpointInfo>(
                nameof(BreakpointSet),
                sessionId,
                solutionName,
                solutionPath,
                "Breakpoint column must be greater than zero.");
        }

        if (hitCount is < 0)
        {
            return AuditLocalFailure<BreakpointInfo>(
                nameof(BreakpointSet),
                sessionId,
                solutionName,
                solutionPath,
                "Breakpoint hit count must be zero or greater.");
        }

        var request = new BreakpointSetRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            Condition = NormalizeOptional(condition),
            Action = NormalizeOptional(action),
            ActionMessage = NormalizeOptional(actionMessage),
            ContinueAfterAction = continueAfterAction,
            HitCount = hitCount,
            HitCountType = NormalizeOptional(hitCountType),
            DependsOnBreakpointName = NormalizeOptional(dependsOnBreakpointName),
            GroupName = NormalizeOptional(groupName)
        };

        var response = await DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointSetAsync(request, ct),
            cancellationToken);

        if (response.Success && HasUnsupportedBreakpointActionMetadata(request))
        {
            return response with { Message = BreakpointActionMetadataWarning };
        }

        return response;
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_list", Title = "Breakpoint List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_group_list", Title = "Breakpoint Group List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists breakpoint groups from a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointGroupListResult>> BreakpointGroupList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var breakpoints = await connection.BreakpointListAsync(ct);
                var groups = breakpoints.Breakpoints
                    .Select(breakpoint => NormalizeOptional(breakpoint.GroupName))
                    .Where(group => group is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(group => group!)
                    .ToArray();
                return new BreakpointGroupListResult(groups, breakpoints.Breakpoints);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_remove", Title = "Breakpoint Remove", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Removes breakpoints in a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointRemoveResult>> BreakpointRemove(
        string? name = null,
        [Description(OptionalDocumentPathParameterDescription)]
        string? documentPath = null,
        [Description("1-based line number; used with documentPath to identify the breakpoint to remove.")]
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_enable", Title = "Breakpoint Enable", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Enables or disables breakpoints in a routed Visual Studio session. When disabling, the response also includes the current debugger state (similar to debug_snapshot); pass continueExecution to resume the debugger afterward.")]
    public Task<ToolResponse<BreakpointEnableResult>> BreakpointEnable(
        bool enabled,
        string? name = null,
        [Description(OptionalDocumentPathParameterDescription)]
        string? documentPath = null,
        [Description("1-based line number; used with documentPath to identify the breakpoint to enable or disable.")]
        int line = 0,
        [Description("When disabling, continue debugger execution afterward if it is paused.")]
        bool continueExecution = false,
        int settleTimeoutMilliseconds = 300,
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

        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<BreakpointEnableResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var request = new BreakpointEnableRequest
        {
            Name = NormalizeOptional(name),
            DocumentPath = NormalizeOptional(documentPath),
            Line = line,
            Enabled = enabled
        };

        return DispatchDebugValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, session, ct) =>
            {
                var result = await connection.BreakpointEnableAsync(request, ct);

                if (enabled)
                {
                    return result;
                }

                var state = await SettleDebuggerStateAsync(connection, session, continueExecution, settleTimeoutMilliseconds, ct);
                return result with { State = state };
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_group_enable", Title = "Breakpoint Group Enable", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Enables or disables all breakpoints in a group through a routed Visual Studio session. When disabling, the response also includes the current debugger state (similar to debug_snapshot); pass continueExecution to resume the debugger afterward.")]
    public Task<ToolResponse<BreakpointGroupOperationResult>> BreakpointGroupEnable(
        string groupName,
        bool enabled,
        [Description("When disabling, continue debugger execution afterward if it is paused.")]
        bool continueExecution = false,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("Breakpoint group name is required."));
        }

        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var normalizedGroupName = groupName.Trim();
        return DispatchDebugValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, session, ct) =>
            {
                var list = await connection.BreakpointListAsync(ct);
                var matches = BreakpointsInGroup(list.Breakpoints, normalizedGroupName).ToArray();
                var updated = 0;
                var updatedBreakpoints = new List<BreakpointInfo>();
                foreach (var breakpoint in matches)
                {
                    var request = new BreakpointEnableRequest
                    {
                        Name = breakpoint.Name,
                        DocumentPath = breakpoint.File,
                        Line = breakpoint.Line,
                        Enabled = enabled
                    };
                    var result = await connection.BreakpointEnableAsync(request, ct);
                    updated += result.Updated;
                    updatedBreakpoints.AddRange(result.Breakpoints);
                }

                DebuggerStateInfo? state = null;
                if (!enabled)
                {
                    state = await SettleDebuggerStateAsync(connection, session, continueExecution, settleTimeoutMilliseconds, ct);
                }

                return new BreakpointGroupOperationResult(normalizedGroupName, matches.Length, updated, updatedBreakpoints, state);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_group_remove", Title = "Breakpoint Group Remove", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Removes all breakpoints in a group through a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointGroupOperationResult>> BreakpointGroupRemove(
        string groupName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("Breakpoint group name is required."));
        }

        var normalizedGroupName = groupName.Trim();
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var list = await connection.BreakpointListAsync(ct);
                var matches = BreakpointsInGroup(list.Breakpoints, normalizedGroupName).ToArray();
                var removed = 0;
                foreach (var breakpoint in matches)
                {
                    var request = new BreakpointRemoveRequest
                    {
                        Name = breakpoint.Name,
                        DocumentPath = breakpoint.File,
                        Line = breakpoint.Line
                    };
                    removed += (await connection.BreakpointRemoveAsync(request, ct)).Removed;
                }

                return new BreakpointGroupOperationResult(normalizedGroupName, matches.Length, removed, []);
            },
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_get_callstack", Title = "Debug Get Callstack", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns current call-stack frames from a routed Visual Studio session. maxFrames defaults to 2. hasMore is true when additional frames were omitted. Updated VSIX versions return 1-based frameIndex and isCurrent for each frame; use debug_select_frame to inspect a caller's locals while paused.")]
    public Task<ToolResponse<CallStackResult>> DebugGetCallstack(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        [Description("Maximum number of call-stack frames to return. Defaults to 2.")]
        int maxFrames = DefaultCallStackFrameLimit,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidMaxFrames(maxFrames))
        {
            return Task.FromResult(ToolResponse<CallStackResult>.Fail("maxFrames must be greater than zero when specified."));
        }

        var request = new CallStackRequest { MaxFrames = maxFrames };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => GetCallStackAsync(connection, request, ct),
            cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_select_frame", Title = "Select Debugger Stack Frame", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Selects a 1-based frameIndex from the current thread's debug_get_callstack result. Requires a paused debugger. Changes the inspection context for subsequent debug_get_locals, debug_evaluate, and watch queries without resuming execution or moving the instruction pointer. Refresh the call stack after stepping, continuing, or switching threads because frame indices may change. Requires an updated VSIX.")]
    public Task<ToolResponse<DebugSelectFrameResult>> DebugSelectFrame(
        [Description("1-based frameIndex from the current thread's latest call stack.")] int frameIndex,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (frameIndex < 1)
            return Task.FromResult(ToolResponse<DebugSelectFrameResult>.Fail("frameIndex must be 1 or greater."));
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DebugSelectFrameAsync(new DebugSelectFrameRequest { FrameIndex = frameIndex }, ct), cancellationToken,
            unsupportedByVsixMessage: "Stack-frame selection is not available in this Visual Studio extension yet. Continue inspecting the current frame, or update the extension to select another frame.");
    }

    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_get_locals", Title = "Debug Get Locals", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns locals from a routed Visual Studio session. Omit both threadId and stackFrame to use the currently selected frame. Optional stackFrame is a 1-based index from the current thread's call stack; inspection uses that frame without changing the original selection. Requires a paused debugger when stackFrame is specified. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<LocalsResult>> DebugGetLocals(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        [Description("Optional 1-based call-stack frame index. Omit to inspect the current frame.")]
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
        CancellationToken cancellationToken = default)
    {
        if (stackFrame is <= 0)
            return Task.FromResult(ToolResponse<LocalsResult>.Fail("stackFrame must be 1 or greater."));
        if (threadId is <= 0)
            return Task.FromResult(ToolResponse<LocalsResult>.Fail("threadId must be 1 or greater."));
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) => stackFrame is null && threadId is null
                ? await connection.DebugGetLocalsAsync(ct)
                : (await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest { ThreadId = threadId, StackFrame = stackFrame ?? 1, IncludeLocals = true }, ct)).Locals
                    ?? throw new InvalidOperationException("Frame inspection did not return locals."),
            cancellationToken,
            unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_evaluate", Title = "Debug Evaluate", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Evaluates an expression in a routed Visual Studio session. Omit both threadId and stackFrame for the current frame. Optional stackFrame is a 1-based index from the current thread's call stack; the original frame selection remains unchanged. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<EvaluateExpressionResult>> DebugEvaluate(
        string expression,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
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

        if (stackFrame is <= 0)
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("stackFrame must be 1 or greater."));
        if (threadId is <= 0)
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("threadId must be 1 or greater."));
        var request = new EvaluateExpressionRequest
        {
            Expression = expression.Trim(),
            TimeoutMilliseconds = timeoutMilliseconds
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) => stackFrame is null && threadId is null
                ? await connection.DebugEvaluateAsync(request, ct)
                : (await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
                {
                    ThreadId = threadId, StackFrame = stackFrame ?? 1, Expressions = [request.Expression], TimeoutMilliseconds = timeoutMilliseconds
                }, ct)).Results.Single(),
            cancellationToken,
            unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    private const int DebugSnapshotPollIntervalMilliseconds = 50;
    private Task<ToolResponse<T>> DispatchDebugValueAsync<T>(string? sessionId, string? solutionName, string? solutionPath,
        Func<IVisualStudioSessionRpc, VsSessionInfo, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken, [CallerMemberName] string toolName = "", string? unsupportedByVsixMessage = null)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var response = new ToolResponse<T>(false, default, route.Message, CreateRouteFailureMetadata(route));
            AuditToolResult(toolName, target, false, null, response.Message, responsePayload: response);
            return Task.FromResult(response);
        }
        var session = route.Session;
        return DispatchValueAsync(session.SessionId, null, null,
            (connection, ct) => operation(connection, session, ct), cancellationToken, toolName: toolName, unsupportedByVsixMessage: unsupportedByVsixMessage);
    }

    private async Task<DebuggerStateInfo> SettleDebuggerStateAsync(
        IVisualStudioSessionRpc connection,
        VsSessionInfo session,
        bool continueExecution,
        int settleTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, cancellationToken);

        if (!continueExecution || state.Mode != "dbgBreakMode")
        {
            return state;
        }

        state = await connection.DebugContinueAsync(cancellationToken);
        _runtime.DialogOperations.RememberState(session, state);

        var stopwatch = Stopwatch.StartNew();
        while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
        {
            await Task.Delay(DebugSnapshotPollIntervalMilliseconds, cancellationToken);
            state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, cancellationToken);
        }

        return state;
    }
    private static readonly string[] DebugSnapshotKnownIncludeKeys =
    [
        "callStack",
        "locals",
        "breakpoints",
        "watch",
        "threads",
        "parallelStacks",
        "parallelWatch",
        "modules"
    ];
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_snapshot", Title = "Debug Snapshot", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Optionally advances the debugger (stepInto, stepOver, stepOut, continue, or break), waits for it to settle, and returns a debugger state snapshot with state.breakReason when paused (breakpoint, stepCompleted, exception, manualPause, unknown, or other). Use 'include' to fetch any of callStack, locals, breakpoints, watch, threads, parallelStacks, parallelWatch, modules (omitting include fetches up to two callStack frames; pass an empty array to fetch none of them). maxFrames defaults to 2 and applies when callStack is included or include is omitted. callStack.hasMore is true when additional frames were omitted. Locals are omitted unless you pass include: [\"locals\"]. For specific variables or expressions, use include: [\"watch\"] with watchExpressions. When 'action' is omitted this is a pure, non-mutating inspection of current state. Returns state.mode=dbgModalBlock with dialogs promptly when VS has a modal window; inspection sections are omitted. Respond with dialog_respond, then check debug_status with any returned state.operation.operationId instead of repeating an action. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1. Include modules to read loaded binaries and symbol status once paused; moduleOptions controls process/name filters and optional symbol-search diagnostics. Modules use session/process scope independently of threadId and stackFrame.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugSnapshot(
        DebugAdvanceAction? action = null,
        string[]? include = null,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        [Description("Optional one-shot expressions to evaluate when include contains 'watch'. When omitted, 'watch' evaluates the session's configured watch list.")]
        string[]? watchExpressions = null,
        [Description("Maximum number of call-stack frames when include contains 'callStack' or is omitted. Defaults to 2. An empty include array does not fetch the call stack.")]
        int maxFrames = DefaultCallStackFrameLimit,
        [Description("Optional 1-based frame for locals and watches. Omit for the current frame; selection remains unchanged after inspection.")]
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
        [Description("Options for include: ['modules']: optional processId, case-insensitive name/path substring, and includeSymbolLoadInformation. Omit to list all Concord modules without search diagnostics. Ignored unless modules is included; requires an updated VSIX.")]
        ModuleListRequest? moduleOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (stackFrame is <= 0)
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("stackFrame must be 1 or greater."));
        if (threadId is <= 0)
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("threadId must be 1 or greater."));
        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        if (!IsValidMaxFrames(maxFrames))
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("maxFrames must be greater than zero when specified."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);
        if (includeKeys.Contains("modules") && moduleOptions?.ProcessId is <= 0)
            return Task.FromResult(FailWithCode<DebugSnapshotResult>("moduleOptions.processId must be positive.", ToolErrorCodes.InvalidRequest));
        var watchRequest = CreateWatchListRequest(watchExpressions);
        var callStackRequest = new CallStackRequest { MaxFrames = maxFrames };

        return DispatchDebugValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, session, ct) =>
            {
                DebuggerStateInfo state;

                if (action is null)
                {
                    state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, ct);
                }
                else
                {
                    state = action.Value switch
                    {
                        DebugAdvanceAction.StepInto => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Into }, ct),
                        DebugAdvanceAction.StepOver => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Over }, ct),
                        DebugAdvanceAction.StepOut => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Out }, ct),
                        DebugAdvanceAction.Continue => await connection.DebugContinueAsync(ct),
                        DebugAdvanceAction.Break => await connection.DebugBreakAsync(ct),
                        _ => await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, ct)
                    };

                    _runtime.DialogOperations.RememberState(session, state);
                    var stopwatch = Stopwatch.StartNew();
                    while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
                    {
                        await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                        state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, ct);
                    }
                }

                return await CollectDebugSnapshotAsync(connection, state, includeKeys, callStackRequest, watchRequest, unrecognizedInclude, ct, stackFrame: stackFrame, threadId: threadId, moduleOptions: moduleOptions);
            },
            cancellationToken,
            unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_wait_for_break", Title = "Debug Wait For Break", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Waits for a routed Visual Studio session's debugger to leave dbgRunMode - typically because a breakpoint or tracepoint fired - then returns the requested snapshot categories in the same shape as debug_snapshot. maxFrames defaults to 2 and applies when callStack is included or include is omitted; an empty include array does not fetch the call stack. callStack.hasMore is true when additional frames were omitted. Also returns promptly with state.mode=dbgModalBlock and dialogs when VS is waiting for a modal response, even if Continue returned earlier; snapshot inspection is skipped. Respond with dialog_respond, then check debug_status. Does not itself advance the debugger; call debug_continue, debug_snapshot (with an action), or breakpoint_group_enable(..., continueExecution: true) first if the debuggee is not already running. Locals are omitted unless you pass include: [\"locals\"]. For specific variables or expressions, use include: [\"watch\"] with watchExpressions. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1. Include modules to read loaded binaries and symbol status once paused; moduleOptions controls process/name filters and optional symbol-search diagnostics. Modules use session/process scope independently of threadId and stackFrame.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugWaitForBreak(
        [Description("Maximum time in seconds to wait for the debugger to leave dbgRunMode before giving up and returning the still-running state.")]
        int timeoutSeconds = 30,
        string[]? include = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        [Description("Optional one-shot expressions to evaluate when include contains 'watch'. When omitted, 'watch' evaluates the session's configured watch list.")]
        string[]? watchExpressions = null,
        [Description("Maximum number of call-stack frames when include contains 'callStack' or is omitted. Defaults to 2. An empty include array does not fetch the call stack.")]
        int maxFrames = DefaultCallStackFrameLimit,
        [Description("Optional 1-based frame for locals and watches once paused. Omit for the current frame.")]
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
        [Description("Options for include: ['modules']: optional processId, case-insensitive name/path substring, and includeSymbolLoadInformation. Omit to list all Concord modules without search diagnostics. Ignored unless modules is included; requires an updated VSIX.")]
        ModuleListRequest? moduleOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (stackFrame is <= 0)
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("stackFrame must be 1 or greater."));
        if (threadId is <= 0)
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("threadId must be 1 or greater."));
        if (timeoutSeconds <= 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("timeoutSeconds must be greater than zero."));
        }

        if (!IsValidMaxFrames(maxFrames))
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("maxFrames must be greater than zero when specified."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);
        if (includeKeys.Contains("modules") && moduleOptions?.ProcessId is <= 0)
            return Task.FromResult(FailWithCode<DebugSnapshotResult>("moduleOptions.processId must be positive.", ToolErrorCodes.InvalidRequest));
        var watchRequest = CreateWatchListRequest(watchExpressions);
        var callStackRequest = new CallStackRequest { MaxFrames = maxFrames };
        var timeoutMilliseconds = timeoutSeconds * 1000L;

        return DispatchDebugValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, session, ct) =>
            {
                var state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, ct);

                var stopwatch = Stopwatch.StartNew();
                while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                    state = await _runtime.DialogOperations.ReadDebugStateAsync(session, connection, ct);
                }

                var timedOut = state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds >= timeoutMilliseconds;
                return await CollectDebugSnapshotAsync(connection, state, includeKeys, callStackRequest, watchRequest, unrecognizedInclude, ct, timedOut, stackFrame, threadId, moduleOptions);
            },
            cancellationToken,
            unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    private static async Task<DebugSnapshotResult> CollectDebugSnapshotAsync(
        IVisualStudioSessionRpc connection,
        DebuggerStateInfo state,
        HashSet<string> includeKeys,
        CallStackRequest callStackRequest,
        WatchListRequest watchRequest,
        IReadOnlyCollection<string>? unrecognizedInclude,
        CancellationToken cancellationToken,
        bool timedOut = false,
        int? stackFrame = null,
        int? threadId = null,
        ModuleListRequest? moduleOptions = null)
    {
        if (state.Mode != "dbgBreakMode")
        {
            return new DebugSnapshotResult(state, null, null, null, UnrecognizedInclude: unrecognizedInclude, TimedOut: timedOut);
        }

        DebugFrameInspectionResult? frameInspection = null;
        if ((stackFrame.HasValue || threadId.HasValue) && (includeKeys.Contains("locals") || includeKeys.Contains("watch") || (threadId.HasValue && includeKeys.Contains("callStack"))))
            frameInspection = await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
            {
                ThreadId = threadId, StackFrame = stackFrame ?? 1,
                CallStack = threadId.HasValue && includeKeys.Contains("callStack") ? callStackRequest : null,
                IncludeLocals = includeKeys.Contains("locals"),
                Watch = includeKeys.Contains("watch") ? watchRequest : null
            }, cancellationToken);
        var locals = includeKeys.Contains("locals") ? frameInspection is not null ? frameInspection.Locals : await connection.DebugGetLocalsAsync(cancellationToken) : null;
        CallStackResult? callStack = null;
        if (includeKeys.Contains("callStack"))
            callStack = threadId.HasValue
                ? frameInspection?.CallStack ?? throw new InvalidOperationException("Thread inspection did not return a call stack.")
                : await GetCallStackAsync(connection, callStackRequest, cancellationToken);
        var breakpoints = includeKeys.Contains("breakpoints") ? await connection.BreakpointListAsync(cancellationToken) : null;
        var watch = includeKeys.Contains("watch") ? frameInspection is not null ? frameInspection.Watch : await connection.WatchListAsync(watchRequest, cancellationToken) : null;
        var threads = includeKeys.Contains("threads") ? await connection.DebugGetThreadsAsync(cancellationToken) : null;
        var parallelStacks = includeKeys.Contains("parallelStacks") ? await connection.ParallelStacksAsync(cancellationToken) : null;
        var parallelWatch = includeKeys.Contains("parallelWatch") ? await connection.ParallelWatchAsync(cancellationToken) : null;

        ModuleListResult? modules = null;
        if (includeKeys.Contains("modules"))
        {
            try
            {
                modules = await connection.ModuleListAsync(moduleOptions ?? new ModuleListRequest(), cancellationToken);
            }
            catch (StreamJsonRpc.RemoteMethodNotFoundException)
            {
                modules = new ModuleListResult(false,
                    "This Visual Studio extension does not support module listing. Update the extension or omit modules from include.", []);
            }
        }

        return new DebugSnapshotResult(
            state,
            callStack,
            locals,
            breakpoints,
            watch,
            threads,
            parallelStacks,
            parallelWatch,
            unrecognizedInclude,
            timedOut,
            Modules: modules);
    }

    private static WatchListRequest CreateWatchListRequest(string[]? watchExpressions)
    {
        var expressions = watchExpressions?
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Select(expression => expression.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new WatchListRequest
        {
            Expressions = expressions is { Length: > 0 } ? expressions : null
        };
    }

    private static Task<CallStackResult> GetCallStackAsync(
        IVisualStudioSessionRpc connection,
        CallStackRequest request,
        CancellationToken cancellationToken)
    {
        return request.MaxFrames == DefaultCallStackFrameLimit
            ? connection.DebugGetCallstackAsync(cancellationToken)
            : connection.DebugGetCallstackWithOptionsAsync(request, cancellationToken);
    }

    private static bool IsValidMaxFrames(int maxFrames) => maxFrames > 0;

    private static (HashSet<string> Keys, IReadOnlyCollection<string>? Unrecognized) ParseDebugSnapshotInclude(string[]? include)
    {
        if (include is null)
        {
            return (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "callStack" }, null);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string>? unrecognized = null;

        foreach (var entry in include)
        {
            if (entry is null)
            {
                continue;
            }

            var matched = Array.Find(
                DebugSnapshotKnownIncludeKeys,
                known => string.Equals(known, entry, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                keys.Add(matched);
            }
            else
            {
                (unrecognized ??= []).Add(entry);
            }
        }

        return (keys, unrecognized is { Count: > 0 } ? unrecognized : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_eval_many", Title = "Debug Eval Many", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Evaluates multiple debugger expressions through a routed Visual Studio session. Omit both threadId and stackFrame for the current frame. Optional stackFrame is a 1-based index; all expressions use that frame in one request, the original selection remains unchanged. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<DebugEvalManyResult>> DebugEvalMany(
        string[] expressions,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
        CancellationToken cancellationToken = default)
    {
        if (stackFrame is <= 0)
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("stackFrame must be 1 or greater."));
        if (threadId is <= 0)
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("threadId must be 1 or greater."));
        if (expressions is null || expressions.Length == 0)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("At least one expression is required."));
        }

        if (timeoutMilliseconds < 1)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("Timeout milliseconds must be greater than zero."));
        }

        var normalizedExpressions = expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Select(expression => expression.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedExpressions.Length == 0)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("At least one expression is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                if ((stackFrame.HasValue || threadId.HasValue))
                {
                    var inspection = await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
                    {
                        ThreadId = threadId, StackFrame = stackFrame ?? 1, Expressions = normalizedExpressions, TimeoutMilliseconds = timeoutMilliseconds
                    }, ct);
                    return new DebugEvalManyResult(inspection.State, inspection.Results);
                }
                var state = await connection.DebugStatusAsync(ct);
                var results = new List<EvaluateExpressionResult>();
                foreach (var expression in normalizedExpressions)
                {
                    results.Add(await connection.DebugEvaluateAsync(new EvaluateExpressionRequest
                    {
                        Expression = expression,
                        TimeoutMilliseconds = timeoutMilliseconds
                    }, ct));
                }

                return new DebugEvalManyResult(state, results);
            },
            cancellationToken,
            unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    private static IEnumerable<BreakpointInfo> BreakpointsInGroup(
        IEnumerable<BreakpointInfo> breakpoints,
        string groupName)
    {
        return breakpoints.Where(breakpoint =>
            string.Equals(breakpoint.GroupName, groupName, StringComparison.OrdinalIgnoreCase));
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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_start_without_debugging", Title = "Debug Start Without Debugging", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Starts the current startup project without debugging.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStartWithoutDebugging(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStartWithoutDebuggingAsync(ct),
            cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_restart", Title = "Debug Restart", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Restarts the active debug session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugRestart(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugRestartAsync(ct),
            cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_attach", Title = "Debug Attach", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Attaches the Visual Studio debugger to a local process by id or name, or to a process on a remote debugger transport (SSH/WSL/Docker/etc.) when transport is set.")]
    public Task<ToolResponse<DebugAttachResult>> DebugAttach(
        int? processId = null,
        string? processName = null,
        string? transport = null,
        string? transportQualifier = null,
        string? engine = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<DebugAttachResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new DebugAttachRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName),
            Transport = NormalizeOptional(transport),
            TransportQualifier = NormalizeOptional(transportQualifier),
            Engine = NormalizeOptional(engine)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.DebugAttachAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_get_threads", Title = "Debug Get Threads", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists debugger threads for the current debug program.")]
    public Task<ToolResponse<DebugThreadListResult>> DebugGetThreads(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.DebugGetThreadsAsync(ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_set_variable", Title = "Debug Set Variable", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Sets a debugger variable by evaluating an assignment expression. Optional stackFrame is a 1-based frame index; omit for the current frame. The requested frame's variable is changed while the original inspection frame remains unchanged. Assignment side effects persist. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<DebugSetVariableResult>> DebugSetVariable(string name, string value, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, int? stackFrame = null, int? threadId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Variable name is required.", ToolErrorCodes.InvalidRequest));
        }

        if (value is null)
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Value is required.", ToolErrorCodes.InvalidRequest));
        }

        if (timeoutMilliseconds <= 0)
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Timeout must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new DebugSetVariableRequest { Name = name, Value = value, TimeoutMilliseconds = timeoutMilliseconds };
        if (stackFrame is <= 0)
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("stackFrame must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        if (threadId is <= 0)
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("threadId must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath, async (connection, ct) =>
        {
            if (stackFrame is null && threadId is null)
                return await connection.DebugSetVariableAsync(request, ct);
            var inspection = await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
            {
                ThreadId = threadId, StackFrame = stackFrame ?? 1, Expressions = [$"{name} = {value}"], TimeoutMilliseconds = timeoutMilliseconds
            }, ct);
            var evaluation = inspection.Results.Single();
            return new DebugSetVariableResult(evaluation.Expression.IsValidValue,
                evaluation.Expression.IsValidValue ? null : "Debugger rejected the assignment expression.", evaluation);
        }, cancellationToken, unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "watch_add", Title = "Watch Add", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Adds a debugger watch expression when supported by the VSIX debugger service. Optional stackFrame evaluates it in a 1-based frame for this call without changing the original selection; omit for the current frame. The saved expression is not bound to a frame. Later watch_list calls evaluate in their own requested or current frame. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<WatchOperationResult>> WatchAdd(string expression, string? sessionId = null, string? solutionName = null, string? solutionPath = null, int? stackFrame = null, int? threadId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchAddRequest { Expression = expression };
        if (stackFrame is <= 0)
            return Task.FromResult(FailWithCode<WatchOperationResult>("stackFrame must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        if (threadId is <= 0)
            return Task.FromResult(FailWithCode<WatchOperationResult>("threadId must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath, async (connection, ct) =>
        {
            if (stackFrame is null && threadId is null)
                return await connection.WatchAddAsync(request, ct);
            var inspection = await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
            {
                ThreadId = threadId, StackFrame = stackFrame ?? 1, AddWatchExpression = expression
            }, ct);
            return new WatchOperationResult(true, true, null, inspection.Results.Single().Expression);
        }, cancellationToken, unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "watch_remove", Title = "Watch Remove", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Removes a debugger watch by expression text when supported by the VSIX debugger service. Pass expression, not an id.")]
    public Task<ToolResponse<WatchOperationResult>> WatchRemove(
        [Description("The watch expression text to remove; watch_remove does not accept a watch id.")]
        string expression,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchRemoveRequest { Expression = expression };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.WatchRemoveAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "watch_list", Title = "Watch List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists and evaluates debugger watch expressions. Optional stackFrame is a 1-based frame index; omit for the current frame. The original inspection frame remains unchanged; saved watch expressions are unchanged. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<WatchListResult>> WatchList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, int? stackFrame = null, int? threadId = null, CancellationToken cancellationToken = default)
    {
        if (stackFrame is <= 0)
            return Task.FromResult(FailWithCode<WatchListResult>("stackFrame must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        if (threadId is <= 0)
            return Task.FromResult(FailWithCode<WatchListResult>("threadId must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath, async (connection, ct) => stackFrame is null && threadId is null
            ? await connection.WatchListAsync(new WatchListRequest(), ct)
            : (await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest { ThreadId = threadId, StackFrame = stackFrame ?? 1, Watch = new WatchListRequest() }, ct)).Watch
                ?? throw new InvalidOperationException("Frame inspection did not return watches."),
            cancellationToken, unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "thread_switch", Title = "Thread Switch", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Switches the active debugger thread.")]
    public Task<ToolResponse<ThreadSwitchResult>> ThreadSwitch(int threadId, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadSwitchResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadSwitchRequest { ThreadId = threadId };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadSwitchAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "thread_set_frozen", Title = "Thread Set Frozen", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Freezes or thaws a debugger thread when supported by the active debug engine.")]
    public Task<ToolResponse<ThreadSetFrozenResult>> ThreadSetFrozen(int threadId, bool frozen, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadSetFrozenResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadSetFrozenRequest { ThreadId = threadId, Frozen = frozen };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadSetFrozenAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "thread_get_callstack", Title = "Thread Get Callstack", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the call stack for a debugger thread when supported by the active debug engine.")]
    public Task<ToolResponse<ThreadCallStackResult>> ThreadGetCallstack(int threadId, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadCallStackResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadCallStackRequest { ThreadId = threadId };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadGetCallstackAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "process_list_debugged", Title = "Process List Debugged", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists processes currently being debugged by Visual Studio.")]
    public Task<ToolResponse<DebuggedProcessListResult>> ProcessListDebugged(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListDebuggedAsync(ct),
            cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "process_list_local", Title = "Process List Local", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists local processes visible to Visual Studio for debugger attach workflows.")]
    public Task<ToolResponse<LocalProcessListResult>> ProcessListLocal(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListLocalAsync(ct),
            cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "process_detach", Title = "Process Detach", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Detaches the Visual Studio debugger from a debugged process by id or name.")]
    public Task<ToolResponse<ProcessDetachResult>> ProcessDetach(int? processId = null, string? processName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<ProcessDetachResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ProcessDetachRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ProcessDetachAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "process_terminate", Title = "Process Terminate", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Terminates a debugged process by id or name when supported by the active debug engine.")]
    public Task<ToolResponse<ProcessTerminateResult>> ProcessTerminate(int? processId = null, string? processName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<ProcessTerminateResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ProcessTerminateRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ProcessTerminateAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "immediate_execute", Title = "Immediate Execute", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Executes text in the immediate window when supported by the VSIX debugger service. Pass statement, not expression. Optional stackFrame is a 1-based frame index; omit for the current frame. The original inspection frame remains unchanged; any statement side effects persist. Optional threadId targets another debugger thread without changing thread or frame selection; requires a paused debugger and an updated VSIX. With threadId, stackFrame indexes that thread and defaults to 1.")]
    public Task<ToolResponse<ImmediateExecuteResult>> ImmediateExecute(
        [Description("Immediate-window text to execute. Use the argument name statement, not expression.")]
        string? statement = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? stackFrame = null,
        [Description("Optional debugger thread ID from debug_get_threads. Requires break mode. Defaults stackFrame to 1 on this thread; leaves the selected thread and frame unchanged.")]
        int? threadId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return Task.FromResult(FailWithCode<ImmediateExecuteResult>("Statement is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ImmediateExecuteRequest { Statement = statement };
        if (stackFrame is <= 0)
            return Task.FromResult(FailWithCode<ImmediateExecuteResult>("stackFrame must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        if (threadId is <= 0)
            return Task.FromResult(FailWithCode<ImmediateExecuteResult>("threadId must be 1 or greater.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath, async (connection, ct) =>
        {
            if (stackFrame is null && threadId is null)
                return await connection.ImmediateExecuteAsync(request, ct);
            var inspection = await InspectDebugFrameAsync(connection, new DebugFrameInspectionRequest
            {
                ThreadId = threadId, StackFrame = stackFrame ?? 1, Expressions = [statement]
            }, ct);
            var expression = inspection.Results.Single().Expression;
            var output = expression.IsValidValue && !string.IsNullOrEmpty(expression.Type)
                ? $"{expression.Value} ({expression.Type})" : expression.Value;
            return new ImmediateExecuteResult(true, expression.IsValidValue, null, output);
        }, cancellationToken, unsupportedByVsixMessage: threadId.HasValue ? ThreadInspectionUnsupportedMessage : stackFrame.HasValue ? StackFrameUnsupportedMessage : null);
    }
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "exception_settings_get", Title = "Exception Settings Get", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns debugger exception settings when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsGet(string? exceptionName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsGetAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "exception_settings_set", Title = "Exception Settings Set", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Sets debugger exception settings when supported by the VSIX debugger service. Pass breakOnThrown for thrown-exception behavior.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsSet(
        string exceptionName,
        [Description("Whether to break when this exception is thrown. Use breakOnThrown.")]
        bool breakOnThrown,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exceptionName))
        {
            return Task.FromResult(FailWithCode<ExceptionSettingsResult>("Exception name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName, BreakOnThrown = breakOnThrown };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsSetAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "parallel_stacks", Title = "Parallel Stacks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns parallel stack information when the active Visual Studio debug engine exposes it.")]
    public Task<ToolResponse<ParallelStacksResult>> ParallelStacks(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelStacksAsync(ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "parallel_watch", Title = "Parallel Watch", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns parallel watch expressions when the active Visual Studio debug engine exposes them.")]
    public Task<ToolResponse<ParallelWatchResult>> ParallelWatch(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelWatchAsync(ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_debug", Title = "Test Debug", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Runs one filtered test under the Visual Studio debugger and attaches to the test host.")]
    public Task<ToolResponse<TestDebugResult>> TestDebug(
        string? projectName = null,
        string? filter = null,
        int attachTimeoutSeconds = 30,
        bool noBuild = false,
        string? configuration = null,
        string? framework = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return Task.FromResult(ToolResponse<TestDebugResult>.Fail("Filter is required so test_debug does not start every test under the debugger."));
        }

        if (attachTimeoutSeconds is < 1 or > 120)
        {
            return Task.FromResult(ToolResponse<TestDebugResult>.Fail("Attach timeout seconds must be between 1 and 120."));
        }

        var request = new TestDebugRequest
        {
            ProjectName = NormalizeOptional(projectName),
            Filter = filter.Trim(),
            AttachTimeoutSeconds = attachTimeoutSeconds,
            NoBuild = noBuild,
            Configuration = NormalizeOptional(configuration),
            Framework = NormalizeOptional(framework)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestDebugAsync(request, ct),
            cancellationToken);
    }
}

