using System.Collections.Generic;
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

internal sealed class BreakpointSetRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; } = 1;
    public string? Condition { get; set; }
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
        bool enabled)
    {
        Name = name;
        File = file;
        Line = line;
        Column = column;
        FunctionName = functionName;
        Condition = condition;
        Enabled = enabled;
    }

    public string? Name { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public string? FunctionName { get; }
    public string? Condition { get; }
    public bool Enabled { get; }

    public static BreakpointInfo FromBreakpoint(Breakpoint breakpoint)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new BreakpointInfo(
            breakpoint.Name,
            breakpoint.File,
            breakpoint.FileLine,
            breakpoint.FileColumn,
            breakpoint.FunctionName,
            breakpoint.Condition,
            breakpoint.Enabled);
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
