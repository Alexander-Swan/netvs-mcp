namespace NetVsMcp.Contracts;

/// <summary>
/// Generic payload shared by the UI-automation and web-debugging (CDP) tool family; not every field
/// applies to every tool (e.g. <see cref="Url"/> is only meaningful for <c>web_*</c> tools).
/// </summary>
public sealed class AutomationRequest
{
    public string ToolName { get; set; } = string.Empty;
    /// <summary>Window/element target identifier, meaning depends on <see cref="ToolName"/>.</summary>
    public string? Target { get; set; }
    /// <summary>UIA selector or CSS/DOM selector, depending on <see cref="ToolName"/> — see the <c>ui_*</c> selector mini-language.</summary>
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
    /// <summary>False when the underlying automation surface (UIA/CDP) isn't available at all, distinct from a supported-but-failed call.</summary>
    bool Supported,
    bool Success,
    string? Message,
    string? Text = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
