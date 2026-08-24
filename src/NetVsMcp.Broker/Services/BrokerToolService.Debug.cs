using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
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
    [McpServerTool(Name = "debug_hot_reload_apply")]
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
    private const string BreakpointActionMetadataWarning =
        "'dependsOnBreakpointName' is stored as informational metadata only. Visual Studio's EnvDTE automation API " +
        "does not expose breakpoint dependencies, so this breakpoint will not actually wait for another breakpoint " +
        "to be hit first. 'actionMessage' + 'continueAfterAction' are real: when this breakpoint is hit, the broker's " +
        "VSIX extension logs the (expression-interpolated, e.g. \"value={x}\") message to the Debug output pane and, " +
        "if requested, resumes execution automatically instead of breaking.";
    private static bool HasUnsupportedBreakpointActionMetadata(BreakpointSetRequest request) =>
        !string.IsNullOrWhiteSpace(request.DependsOnBreakpointName);
    [McpServerTool(Name = "breakpoint_set")]
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
            return ToolResponse<BreakpointInfo>.Fail("Document path is required.");
        }

        if (line < 1)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint line must be greater than zero.");
        }

        if (column < 1)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint column must be greater than zero.");
        }

        if (hitCount is < 0)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint hit count must be zero or greater.");
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
    [McpServerTool(Name = "breakpoint_group_list")]
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
    [McpServerTool(Name = "breakpoint_remove")]
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
    [McpServerTool(Name = "breakpoint_enable")]
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

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var result = await connection.BreakpointEnableAsync(request, ct);

                if (enabled)
                {
                    return result;
                }

                var state = await SettleDebuggerStateAsync(connection, continueExecution, settleTimeoutMilliseconds, ct);
                return result with { State = state };
            },
            cancellationToken);
    }
    [McpServerTool(Name = "breakpoint_group_enable")]
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
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
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
                    state = await SettleDebuggerStateAsync(connection, continueExecution, settleTimeoutMilliseconds, ct);
                }

                return new BreakpointGroupOperationResult(normalizedGroupName, matches.Length, updated, updatedBreakpoints, state);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "breakpoint_group_remove")]
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
    private const int DebugSnapshotPollIntervalMilliseconds = 50;
    private static async Task<DebuggerStateInfo> SettleDebuggerStateAsync(
        IVisualStudioSessionRpc connection,
        bool continueExecution,
        int settleTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var state = await connection.DebugStatusAsync(cancellationToken);

        if (!continueExecution || state.Mode != "dbgBreakMode")
        {
            return state;
        }

        state = await connection.DebugContinueAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
        {
            await Task.Delay(DebugSnapshotPollIntervalMilliseconds, cancellationToken);
            state = await connection.DebugStatusAsync(cancellationToken);
        }

        return state;
    }
    private static readonly string[] DebugSnapshotKnownIncludeKeys =
    [
        "callStack",
        "breakpoints",
        "watch",
        "threads",
        "modules",
        "parallelStacks",
        "parallelWatch"
    ];
    [McpServerTool(Name = "debug_snapshot")]
    [Description("Optionally advances the debugger (stepInto, stepOver, stepOut, continue, or break), waits for it to settle, and returns state plus locals in one call. Use 'include' to also fetch any of callStack, breakpoints, watch, threads, modules, parallelStacks, parallelWatch (defaults to callStack only when omitted; pass an empty array to fetch none of them). Locals are always fetched best-effort while paused. When 'action' is omitted this is a pure, non-mutating inspection of current state.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugSnapshot(
        DebugAdvanceAction? action = null,
        string[]? include = null,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                DebuggerStateInfo state;

                if (action is null)
                {
                    state = await connection.DebugStatusAsync(ct);
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
                        _ => await connection.DebugStatusAsync(ct)
                    };

                    var stopwatch = Stopwatch.StartNew();
                    while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
                    {
                        await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                        state = await connection.DebugStatusAsync(ct);
                    }
                }

                return await CollectDebugSnapshotAsync(connection, state, includeKeys, unrecognizedInclude, ct);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "debug_wait_for_break")]
    [Description("Waits for a routed Visual Studio session's debugger to leave dbgRunMode - typically because a breakpoint or tracepoint fired - then returns state, locals, and the requested include categories in one call, the same shape as debug_snapshot. Does not itself advance the debugger; call debug_continue, debug_snapshot (with an action), or breakpoint_group_enable(..., continueExecution: true) first if the debuggee is not already running. Use 'include' the same way as debug_snapshot.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugWaitForBreak(
        [Description("Maximum time in seconds to wait for the debugger to leave dbgRunMode before giving up and returning the still-running state.")]
        int timeoutSeconds = 30,
        string[]? include = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (timeoutSeconds <= 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("timeoutSeconds must be greater than zero."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);
        var timeoutMilliseconds = timeoutSeconds * 1000L;

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var state = await connection.DebugStatusAsync(ct);

                var stopwatch = Stopwatch.StartNew();
                while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                    state = await connection.DebugStatusAsync(ct);
                }

                var timedOut = state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds >= timeoutMilliseconds;
                return await CollectDebugSnapshotAsync(connection, state, includeKeys, unrecognizedInclude, ct, timedOut);
            },
            cancellationToken);
    }
    private static async Task<DebugSnapshotResult> CollectDebugSnapshotAsync(
        IVisualStudioSessionRpc connection,
        DebuggerStateInfo state,
        HashSet<string> includeKeys,
        IReadOnlyCollection<string>? unrecognizedInclude,
        CancellationToken cancellationToken,
        bool timedOut = false)
    {
        if (state.Mode != "dbgBreakMode")
        {
            return new DebugSnapshotResult(state, null, null, null, UnrecognizedInclude: unrecognizedInclude, TimedOut: timedOut);
        }

        var locals = await connection.DebugGetLocalsAsync(cancellationToken);
        var callStack = includeKeys.Contains("callStack") ? await connection.DebugGetCallstackAsync(cancellationToken) : null;
        var breakpoints = includeKeys.Contains("breakpoints") ? await connection.BreakpointListAsync(cancellationToken) : null;
        var watch = includeKeys.Contains("watch") ? await connection.WatchListAsync(cancellationToken) : null;
        var threads = includeKeys.Contains("threads") ? await connection.DebugGetThreadsAsync(cancellationToken) : null;
        var modules = includeKeys.Contains("modules") ? await connection.ModuleListAsync(cancellationToken) : null;
        var parallelStacks = includeKeys.Contains("parallelStacks") ? await connection.ParallelStacksAsync(cancellationToken) : null;
        var parallelWatch = includeKeys.Contains("parallelWatch") ? await connection.ParallelWatchAsync(cancellationToken) : null;

        return new DebugSnapshotResult(
            state,
            callStack,
            locals,
            breakpoints,
            watch,
            threads,
            modules,
            parallelStacks,
            parallelWatch,
            unrecognizedInclude,
            timedOut);
    }
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
    [McpServerTool(Name = "debug_eval_many")]
    [Description("Evaluates multiple debugger expressions through a routed Visual Studio session.")]
    public Task<ToolResponse<DebugEvalManyResult>> DebugEvalMany(
        string[] expressions,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
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
            cancellationToken);
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
    [McpServerTool(Name = "debug_start_without_debugging")]
    [Description("Starts the current startup project without debugging.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStartWithoutDebugging(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStartWithoutDebuggingAsync(ct),
            cancellationToken);
    [McpServerTool(Name = "debug_restart")]
    [Description("Restarts the active debug session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugRestart(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugRestartAsync(ct),
            cancellationToken);
    [McpServerTool(Name = "debug_attach")]
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
    [McpServerTool(Name = "debug_get_threads")]
    [Description("Lists debugger threads for the current debug program.")]
    public Task<ToolResponse<DebugThreadListResult>> DebugGetThreads(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.DebugGetThreadsAsync(ct), cancellationToken);
    [McpServerTool(Name = "debug_set_variable")]
    [Description("Sets a debugger variable by evaluating an assignment expression.")]
    public Task<ToolResponse<DebugSetVariableResult>> DebugSetVariable(string name, string value, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
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
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.DebugSetVariableAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "watch_add")]
    [Description("Adds a debugger watch expression when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchOperationResult>> WatchAdd(string expression, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchAddRequest { Expression = expression };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.WatchAddAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "watch_remove")]
    [Description("Removes a debugger watch expression when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchOperationResult>> WatchRemove(string expression, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchRemoveRequest { Expression = expression };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.WatchRemoveAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "watch_list")]
    [Description("Lists debugger watch expressions when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchListResult>> WatchList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.WatchListAsync(ct), cancellationToken);
    [McpServerTool(Name = "thread_switch")]
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
    [McpServerTool(Name = "thread_set_frozen")]
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
    [McpServerTool(Name = "thread_get_callstack")]
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
    [McpServerTool(Name = "process_list_debugged")]
    [Description("Lists processes currently being debugged by Visual Studio.")]
    public Task<ToolResponse<DebuggedProcessListResult>> ProcessListDebugged(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListDebuggedAsync(ct),
            cancellationToken);
    [McpServerTool(Name = "process_list_local")]
    [Description("Lists local processes visible to Visual Studio for debugger attach workflows.")]
    public Task<ToolResponse<LocalProcessListResult>> ProcessListLocal(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListLocalAsync(ct),
            cancellationToken);
    [McpServerTool(Name = "process_detach")]
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
    [McpServerTool(Name = "process_terminate")]
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
    [McpServerTool(Name = "immediate_execute")]
    [Description("Executes text in the immediate window when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ImmediateExecuteResult>> ImmediateExecute(string? statement = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return Task.FromResult(FailWithCode<ImmediateExecuteResult>("Statement is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ImmediateExecuteRequest { Statement = statement };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ImmediateExecuteAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "module_list")]
    [Description("Lists debugger modules when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ModuleListResult>> ModuleList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ModuleListAsync(ct), cancellationToken);
    [McpServerTool(Name = "exception_settings_get")]
    [Description("Returns debugger exception settings when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsGet(string? exceptionName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsGetAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "exception_settings_set")]
    [Description("Sets debugger exception settings when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsSet(string exceptionName, bool breakOnThrown, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exceptionName))
        {
            return Task.FromResult(FailWithCode<ExceptionSettingsResult>("Exception name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName, BreakOnThrown = breakOnThrown };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsSetAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "parallel_stacks")]
    [Description("Returns parallel stack information when the active Visual Studio debug engine exposes it.")]
    public Task<ToolResponse<ParallelStacksResult>> ParallelStacks(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelStacksAsync(ct), cancellationToken);
    [McpServerTool(Name = "parallel_watch")]
    [Description("Returns parallel watch expressions when the active Visual Studio debug engine exposes them.")]
    public Task<ToolResponse<ParallelWatchResult>> ParallelWatch(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelWatchAsync(ct), cancellationToken);
    [McpServerTool(Name = "test_debug")]
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
