using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal sealed class AutomationRequest
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

internal sealed class AutomationResult
{
    public AutomationResult(
        bool supported,
        bool success,
        string? message,
        string? text,
        IReadOnlyDictionary<string, string>? metadata)
    {
        Supported = supported;
        Success = success;
        Message = message;
        Text = text;
        Metadata = metadata;
    }

    public bool Supported { get; }
    public bool Success { get; }
    public string? Message { get; }
    public string? Text { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }
}
