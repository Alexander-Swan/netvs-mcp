using System;
using System.Collections.Generic;

namespace NetVsMcp.Vsix;

/// <summary>
/// Shared helpers for building <see cref="AutomationResult"/> instances and truncating large
/// text payloads. Used by the desktop UIA, console, and web-debug automation services that were
/// previously combined in a single <c>AutomationCapabilityService.cs</c> file (see ARCH-7 in
/// docs/IMPROVEMENT_PLAN.md).
/// </summary>
internal static class AutomationSupport
{
    public const int MaxTextChars = 20000;

    public static AutomationResult Success(AutomationRequest request, string? text, params (string Key, string Value)[] metadata) =>
        new(true, true, null, text, Metadata(request, metadata));

    public static AutomationResult Failure(AutomationRequest request, string message, params (string Key, string Value)[] metadata) =>
        new(true, false, message, null, Metadata(request, metadata));

    public static IReadOnlyDictionary<string, string> Metadata(AutomationRequest request, params (string Key, string Value)[] metadata)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["toolName"] = string.IsNullOrWhiteSpace(request.ToolName) ? "automation" : request.ToolName,
            ["implementation"] = "vsix-routed",
            ["backend"] = "windows"
        };

        foreach (var (key, value) in metadata)
        {
            values[key] = value;
        }

        return values;
    }

    public static string Truncate(string text) =>
        text.Length <= MaxTextChars ? text : text.Substring(text.Length - MaxTextChars, MaxTextChars);
}
