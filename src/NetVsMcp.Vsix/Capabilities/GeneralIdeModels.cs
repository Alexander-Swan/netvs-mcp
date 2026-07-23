using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal sealed class ExecuteCommandRequest
{
    public string CommandName { get; set; } = string.Empty;

    public string? Arguments { get; set; }
}

internal sealed class ExecuteCommandResult
{
    public ExecuteCommandResult(bool success, string commandName, string? arguments, string? message)
    {
        Success = success;
        CommandName = commandName;
        Arguments = arguments;
        Message = message;
    }

    public bool Success { get; }
    public string CommandName { get; }
    public string? Arguments { get; }
    public string? Message { get; }
}

internal sealed class WindowInfo
{
    public WindowInfo(string? caption, string? kind, string? objectKind, bool isActive, bool isVisible)
    {
        Caption = caption;
        Kind = kind;
        ObjectKind = objectKind;
        IsActive = isActive;
        IsVisible = isVisible;
    }

    public string? Caption { get; }
    public string? Kind { get; }
    public string? ObjectKind { get; }
    public bool IsActive { get; }
    public bool IsVisible { get; }
}

internal sealed class WindowListResult
{
    public WindowListResult(IReadOnlyCollection<WindowInfo> windows)
    {
        Windows = windows;
    }

    public IReadOnlyCollection<WindowInfo> Windows { get; }
}

internal sealed class WindowActivateRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

internal sealed class WindowActivateResult
{
    public WindowActivateResult(bool success, string? message, WindowInfo? window)
    {
        Success = success;
        Message = message;
        Window = window;
    }

    public bool Success { get; }
    public string? Message { get; }
    public WindowInfo? Window { get; }
}

internal sealed class ToolWindowRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

internal sealed class ToolWindowResult
{
    public ToolWindowResult(bool success, string? message, WindowInfo? window)
    {
        Success = success;
        Message = message;
        Window = window;
    }

    public bool Success { get; }
    public string? Message { get; }
    public WindowInfo? Window { get; }
}
