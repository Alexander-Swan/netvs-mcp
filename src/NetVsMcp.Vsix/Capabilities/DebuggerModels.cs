using System;
using System.Collections.Generic;
using System.Text.Json;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed class DebuggerStateInfo
{
    public DebuggerStateInfo(string mode)
    {
        Mode = mode;
    }

    public string Mode { get; }
}

internal sealed class DebugStepRequest
{
    public DebugStepKind StepKind { get; set; } = DebugStepKind.Over;
}

internal sealed class DebuggedProcessInfo
{
    public DebuggedProcessInfo(int processId, string name, string transport, string userName)
    {
        ProcessId = processId;
        Name = name;
        Transport = transport;
        UserName = userName;
    }

    public int ProcessId { get; }
    public string Name { get; }
    public string Transport { get; }
    public string UserName { get; }

    public static DebuggedProcessInfo FromProcess(Process process)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return new DebuggedProcessInfo(
            process.ProcessID,
            process.Name ?? string.Empty,
            string.Empty,
            string.Empty);
    }
}

internal sealed class DebuggedProcessListResult
{
    public DebuggedProcessListResult(IReadOnlyCollection<DebuggedProcessInfo> processes)
    {
        Processes = processes;
    }

    public IReadOnlyCollection<DebuggedProcessInfo> Processes { get; }
}

internal sealed class LocalProcessInfo
{
    public LocalProcessInfo(int processId, string name, string transport, string userName, bool isBeingDebugged)
    {
        ProcessId = processId;
        Name = name;
        Transport = transport;
        UserName = userName;
        IsBeingDebugged = isBeingDebugged;
    }

    public int ProcessId { get; }
    public string Name { get; }
    public string Transport { get; }
    public string UserName { get; }
    public bool IsBeingDebugged { get; }

    public static LocalProcessInfo FromProcess(Process process, bool isBeingDebugged)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return new LocalProcessInfo(
            process.ProcessID,
            process.Name ?? string.Empty,
            string.Empty,
            string.Empty,
            isBeingDebugged);
    }
}

internal sealed class LocalProcessListResult
{
    public LocalProcessListResult(IReadOnlyCollection<LocalProcessInfo> processes)
    {
        Processes = processes;
    }

    public IReadOnlyCollection<LocalProcessInfo> Processes { get; }
}

internal sealed class DebugAttachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

internal sealed class DebugAttachResult
{
    public DebugAttachResult(bool success, string? message, DebuggedProcessInfo? process)
    {
        Success = success;
        Message = message;
        Process = process;
    }

    public bool Success { get; }
    public string? Message { get; }
    public DebuggedProcessInfo? Process { get; }
}

internal sealed class ProcessDetachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

internal sealed class ProcessDetachResult
{
    public ProcessDetachResult(bool success, string? message, DebuggedProcessInfo? process, DebuggerStateInfo state)
    {
        Success = success;
        Message = message;
        Process = process;
        State = state;
    }

    public bool Success { get; }
    public string? Message { get; }
    public DebuggedProcessInfo? Process { get; }
    public DebuggerStateInfo State { get; }
}

internal sealed class ProcessTerminateRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

internal sealed class ProcessTerminateResult
{
    public ProcessTerminateResult(bool success, string? message, DebuggedProcessInfo? process, DebuggerStateInfo state)
    {
        Success = success;
        Message = message;
        Process = process;
        State = state;
    }

    public bool Success { get; }
    public string? Message { get; }
    public DebuggedProcessInfo? Process { get; }
    public DebuggerStateInfo State { get; }
}

internal sealed class BreakpointSetRequest
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

internal sealed class BreakpointRemoveRequest
{
    public string? Name { get; set; }
    public string? DocumentPath { get; set; }
    public int Line { get; set; }
}

internal sealed class BreakpointRemoveResult
{
    public BreakpointRemoveResult(int removed)
    {
        Removed = removed;
    }

    public int Removed { get; }
}

internal sealed class BreakpointEnableRequest
{
    public string? Name { get; set; }
    public string? DocumentPath { get; set; }
    public int Line { get; set; }
    public bool Enabled { get; set; } = true;
}

internal sealed class BreakpointEnableResult
{
    public BreakpointEnableResult(int updated, IReadOnlyCollection<BreakpointInfo> breakpoints)
    {
        Updated = updated;
        Breakpoints = breakpoints;
    }

    public int Updated { get; }
    public IReadOnlyCollection<BreakpointInfo> Breakpoints { get; }
}

internal sealed class BreakpointListResult
{
    public BreakpointListResult(IReadOnlyCollection<BreakpointInfo> breakpoints)
    {
        Breakpoints = breakpoints;
    }

    public IReadOnlyCollection<BreakpointInfo> Breakpoints { get; }
}

internal sealed class BreakpointInfo
{
    public BreakpointInfo(
        string? name,
        string? file,
        int line,
        int column,
        string? functionName,
        string? condition,
        bool enabled,
        string? action = null,
        string? actionMessage = null,
        bool continueAfterAction = false,
        int? hitCount = null,
        string? hitCountType = null,
        string? dependsOnBreakpointName = null,
        string? groupName = null)
    {
        Name = name;
        File = file;
        Line = line;
        Column = column;
        FunctionName = functionName;
        Condition = condition;
        Enabled = enabled;
        Action = action;
        ActionMessage = actionMessage;
        ContinueAfterAction = continueAfterAction;
        HitCount = hitCount;
        HitCountType = hitCountType;
        DependsOnBreakpointName = dependsOnBreakpointName;
        GroupName = groupName;
    }

    public string? Name { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public string? FunctionName { get; }
    public string? Condition { get; }
    public bool Enabled { get; }
    public string? Action { get; }
    public string? ActionMessage { get; }
    public bool ContinueAfterAction { get; }
    public int? HitCount { get; }
    public string? HitCountType { get; }
    public string? DependsOnBreakpointName { get; }
    public string? GroupName { get; }

    public static BreakpointInfo FromBreakpoint(Breakpoint breakpoint)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var metadata = BreakpointMetadata.FromBreakpoint(breakpoint);
        return new BreakpointInfo(
            breakpoint.Name,
            breakpoint.File,
            breakpoint.FileLine,
            breakpoint.FileColumn,
            breakpoint.FunctionName,
            breakpoint.Condition,
            breakpoint.Enabled,
            metadata.Action,
            metadata.ActionMessage,
            metadata.ContinueAfterAction,
            metadata.HitCount,
            metadata.HitCountType,
            metadata.DependsOnBreakpointName,
            metadata.GroupName);
    }
}

internal sealed class BreakpointMetadata
{
    private const string TagPrefix = "NetVsMcp:";

    public string? Action { get; set; }
    public string? ActionMessage { get; set; }
    public bool ContinueAfterAction { get; set; }
    public int? HitCount { get; set; }
    public string? HitCountType { get; set; }
    public string? DependsOnBreakpointName { get; set; }
    public string? GroupName { get; set; }

    public static BreakpointMetadata FromRequest(BreakpointSetRequest request) =>
        new()
        {
            Action = EmptyToNull(request.Action),
            ActionMessage = EmptyToNull(request.ActionMessage),
            ContinueAfterAction = request.ContinueAfterAction,
            HitCount = request.HitCount,
            HitCountType = EmptyToNull(request.HitCountType),
            DependsOnBreakpointName = EmptyToNull(request.DependsOnBreakpointName),
            GroupName = EmptyToNull(request.GroupName)
        };

    public static BreakpointMetadata FromBreakpoint(Breakpoint breakpoint)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var metadata = TryReadTag(breakpoint);
        metadata.HitCount ??= TryGetIntProperty(breakpoint, "HitCountTarget") ?? TryGetIntProperty(breakpoint, "HitCount");
        metadata.HitCountType ??= TryGetProperty(breakpoint, "HitCountType")?.ToString();
        metadata.ActionMessage ??= TryGetProperty(breakpoint, "Message")?.ToString();
        return metadata;
    }

    public void ApplyTo(Breakpoint breakpoint)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // EnvDTE80.Breakpoint2.Message + BreakWhenHit map directly to VS's native
        // "Print a Message" tracepoint (Message text, BreakWhenHit=false means "and continue").
        if (!string.IsNullOrWhiteSpace(ActionMessage) && breakpoint is EnvDTE80.Breakpoint2 breakpoint2)
        {
            breakpoint2.Message = ActionMessage;
            breakpoint2.BreakWhenHit = !ContinueAfterAction;
        }

        TrySetProperty(breakpoint, "Tag", TagPrefix + JsonSerializer.Serialize(this));
    }

    private static BreakpointMetadata TryReadTag(Breakpoint breakpoint)
    {
        var tag = TryGetProperty(breakpoint, "Tag")?.ToString();
        if (tag is not { Length: > 0 } nonEmptyTag ||
            !nonEmptyTag.StartsWith(TagPrefix, System.StringComparison.Ordinal))
        {
            return new BreakpointMetadata();
        }

        try
        {
            return JsonSerializer.Deserialize<BreakpointMetadata>(nonEmptyTag.Substring(TagPrefix.Length))
                ?? new BreakpointMetadata();
        }
        catch
        {
            return new BreakpointMetadata();
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? TryGetIntProperty(Breakpoint breakpoint, string propertyName)
    {
        var value = TryGetProperty(breakpoint, propertyName);
        return value is int integer ? integer : null;
    }

    private static object? TryGetProperty(Breakpoint breakpoint, string propertyName)
    {
        try
        {
            return breakpoint.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                breakpoint,
                null);
        }
        catch
        {
            return null;
        }
    }

    private static void TrySetProperty(Breakpoint breakpoint, string propertyName, object? value)
    {
        try
        {
            breakpoint.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.SetProperty,
                null,
                breakpoint,
                [value]);
        }
        catch
        {
        }
    }
}

internal sealed class CallStackResult
{
    public CallStackResult(DebuggerStateInfo state, IReadOnlyCollection<CallStackFrameInfo> frames)
    {
        State = state;
        Frames = frames;
    }

    public DebuggerStateInfo State { get; }
    public IReadOnlyCollection<CallStackFrameInfo> Frames { get; }
}

internal sealed class CallStackFrameInfo
{
    public CallStackFrameInfo(string? functionName, string? file, int line, int column)
    {
        FunctionName = functionName;
        File = file;
        Line = line;
        Column = column;
    }

    public string? FunctionName { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }

    public static CallStackFrameInfo FromStackFrame(StackFrame frame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new CallStackFrameInfo(
            frame.FunctionName,
            null,
            0,
            0);
    }
}

internal sealed class LocalsResult
{
    public LocalsResult(DebuggerStateInfo state, IReadOnlyCollection<DebugExpressionInfo> locals)
    {
        State = state;
        Locals = locals;
    }

    public DebuggerStateInfo State { get; }
    public IReadOnlyCollection<DebugExpressionInfo> Locals { get; }
}

internal sealed class EvaluateExpressionRequest
{
    public string Expression { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 5000;
}

internal sealed class EvaluateExpressionResult
{
    public EvaluateExpressionResult(DebuggerStateInfo state, DebugExpressionInfo expression)
    {
        State = state;
        Expression = expression;
    }

    public DebuggerStateInfo State { get; }
    public DebugExpressionInfo Expression { get; }
}

internal sealed class DebugExpressionInfo
{
    public DebugExpressionInfo(string? name, string? value, string? type, bool isValidValue)
    {
        Name = name;
        Value = value;
        Type = type;
        IsValidValue = isValidValue;
    }

    public string? Name { get; }
    public string? Value { get; }
    public string? Type { get; }
    public bool IsValidValue { get; }

    public static DebugExpressionInfo FromExpression(Expression expression)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new DebugExpressionInfo(
            expression.Name,
            expression.Value,
            expression.Type,
            expression.IsValidValue);
    }
}

internal sealed class WatchAddRequest
{
    public string Expression { get; set; } = string.Empty;
}

internal sealed class WatchRemoveRequest
{
    public string Expression { get; set; } = string.Empty;
}

internal sealed class WatchOperationResult
{
    public WatchOperationResult(bool supported, bool success, string? message, DebugExpressionInfo? watch)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Watch = watch;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public DebugExpressionInfo? Watch { get; }
}

internal sealed class WatchListResult
{
    public WatchListResult(bool supported, string? message, IReadOnlyCollection<DebugExpressionInfo> watches)
    {
        Supported = supported;
        Message = message;
        Watches = watches;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<DebugExpressionInfo> Watches { get; }
}

internal sealed class DebugThreadInfo
{
    public DebugThreadInfo(int id, string? name, bool isCurrent)
    {
        Id = id;
        Name = name;
        IsCurrent = isCurrent;
    }

    public int Id { get; }
    public string? Name { get; }
    public bool IsCurrent { get; }

    public static DebugThreadInfo FromThread(EnvDTE.Thread thread, EnvDTE.Thread? currentThread)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new DebugThreadInfo(
            thread.ID,
            thread.Name,
            currentThread is not null && thread.ID == currentThread.ID);
    }
}

internal sealed class DebugThreadListResult
{
    public DebugThreadListResult(bool supported, string? message, IReadOnlyCollection<DebugThreadInfo> threads)
    {
        Supported = supported;
        Message = message;
        Threads = threads;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<DebugThreadInfo> Threads { get; }
}

internal sealed class ThreadSwitchRequest
{
    public int ThreadId { get; set; }
}

internal sealed class DebugSetVariableRequest
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 5000;
}

internal sealed class DebugSetVariableResult
{
    public DebugSetVariableResult(bool success, string? message, EvaluateExpressionResult? evaluation)
    {
        Success = success;
        Message = message;
        Evaluation = evaluation;
    }

    public bool Success { get; }
    public string? Message { get; }
    public EvaluateExpressionResult? Evaluation { get; }
}

internal sealed class ThreadSetFrozenRequest
{
    public int ThreadId { get; set; }
    public bool Frozen { get; set; }
}

internal sealed class ThreadSwitchResult
{
    public ThreadSwitchResult(bool supported, bool success, string? message, DebugThreadInfo? thread)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Thread = thread;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public DebugThreadInfo? Thread { get; }
}

internal sealed class ThreadSetFrozenResult
{
    public ThreadSetFrozenResult(bool supported, bool success, string? message, DebugThreadInfo? thread, bool frozen)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Thread = thread;
        Frozen = frozen;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public DebugThreadInfo? Thread { get; }
    public bool Frozen { get; }
}

internal sealed class ThreadCallStackRequest
{
    public int ThreadId { get; set; }
}

internal sealed class ThreadCallStackResult
{
    public ThreadCallStackResult(bool supported, string? message, DebugThreadInfo? thread, IReadOnlyCollection<CallStackFrameInfo> frames)
    {
        Supported = supported;
        Message = message;
        Thread = thread;
        Frames = frames;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public DebugThreadInfo? Thread { get; }
    public IReadOnlyCollection<CallStackFrameInfo> Frames { get; }
}

internal sealed class DebugModuleInfo
{
    public DebugModuleInfo(string? name, string? path)
    {
        Name = name;
        Path = path;
    }

    public string? Name { get; }
    public string? Path { get; }

}

internal sealed class ModuleListResult
{
    public ModuleListResult(bool supported, string? message, IReadOnlyCollection<DebugModuleInfo> modules)
    {
        Supported = supported;
        Message = message;
        Modules = modules;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<DebugModuleInfo> Modules { get; }
}

internal sealed class ImmediateExecuteRequest
{
    public string Statement { get; set; } = string.Empty;
}

internal sealed class ImmediateExecuteResult
{
    public ImmediateExecuteResult(bool supported, bool success, string? message, string? output)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Output = output;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public string? Output { get; }
}

internal sealed class ExceptionSettingsRequest
{
    public string? ExceptionName { get; set; }
    public bool? BreakOnThrown { get; set; }
}

internal sealed class ExceptionSettingInfo
{
    public ExceptionSettingInfo(string? groupName, string? name, bool breakWhenThrown, bool breakWhenUserUnhandled, bool userDefined)
    {
        GroupName = groupName;
        Name = name;
        BreakWhenThrown = breakWhenThrown;
        BreakWhenUserUnhandled = breakWhenUserUnhandled;
        UserDefined = userDefined;
    }

    public string? GroupName { get; }
    public string? Name { get; }
    public bool BreakWhenThrown { get; }
    public bool BreakWhenUserUnhandled { get; }
    public bool UserDefined { get; }
}

internal sealed class ExceptionSettingsResult
{
    public ExceptionSettingsResult(bool supported, bool success, string? message, IReadOnlyCollection<ExceptionSettingInfo>? settings = null)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Settings = settings ?? Array.Empty<ExceptionSettingInfo>();
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public IReadOnlyCollection<ExceptionSettingInfo> Settings { get; }
}

internal sealed class MemoryReadRequest
{
    public string AddressExpression { get; set; } = string.Empty;
    public int ByteCount { get; set; } = 64;
}

internal sealed class MemoryReadResult
{
    public MemoryReadResult(bool supported, bool success, string? message, string addressExpression, int byteCount, string? hex)
    {
        Supported = supported;
        Success = success;
        Message = message;
        AddressExpression = addressExpression;
        ByteCount = byteCount;
        Hex = hex;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public string AddressExpression { get; }
    public int ByteCount { get; }
    public string? Hex { get; }
}

internal sealed class RegisterInfo
{
    public RegisterInfo(string name, string? value, string? type)
    {
        Name = name;
        Value = value;
        Type = type;
    }

    public string Name { get; }
    public string? Value { get; }
    public string? Type { get; }
}

internal sealed class RegisterGetRequest
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class RegisterListResult
{
    public RegisterListResult(bool supported, string? message, IReadOnlyCollection<RegisterInfo> registers)
    {
        Supported = supported;
        Message = message;
        Registers = registers;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<RegisterInfo> Registers { get; }
}

internal sealed class RegisterGetResult
{
    public RegisterGetResult(bool supported, bool success, string? message, RegisterInfo? register)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Register = register;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public RegisterInfo? Register { get; }
}

internal sealed class ParallelStackFrameInfo
{
    public ParallelStackFrameInfo(int threadId, string? threadName, string? functionName, string? file, int line, int column)
    {
        ThreadId = threadId;
        ThreadName = threadName;
        FunctionName = functionName;
        File = file;
        Line = line;
        Column = column;
    }

    public int ThreadId { get; }
    public string? ThreadName { get; }
    public string? FunctionName { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
}

internal sealed class ParallelStacksResult
{
    public ParallelStacksResult(bool supported, string? message, IReadOnlyCollection<ParallelStackFrameInfo> frames)
    {
        Supported = supported;
        Message = message;
        Frames = frames;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<ParallelStackFrameInfo> Frames { get; }
}

internal sealed class ParallelWatchResult
{
    public ParallelWatchResult(bool supported, string? message, IReadOnlyCollection<DebugExpressionInfo> expressions)
    {
        Supported = supported;
        Message = message;
        Expressions = expressions;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<DebugExpressionInfo> Expressions { get; }
}

internal sealed class ParallelTaskInfo
{
    public ParallelTaskInfo(string? id, string? status, string? location, int? threadId)
    {
        Id = id;
        Status = status;
        Location = location;
        ThreadId = threadId;
    }

    public string? Id { get; }
    public string? Status { get; }
    public string? Location { get; }
    public int? ThreadId { get; }
}

internal sealed class ParallelTasksResult
{
    public ParallelTasksResult(bool supported, string? message, IReadOnlyCollection<ParallelTaskInfo> tasks)
    {
        Supported = supported;
        Message = message;
        Tasks = tasks;
    }

    public bool Supported { get; }
    public string? Message { get; }
    public IReadOnlyCollection<ParallelTaskInfo> Tasks { get; }
}
