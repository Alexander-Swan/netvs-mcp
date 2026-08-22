using System.Windows.Automation;

namespace NetVsMcp.Vsix;

/// <summary>
/// Parses the ui_* tool family's "key=value" selector mini-language (see the
/// automate-visual-studio guide) into UI Automation Conditions. Kept separate from
/// AutomationCapabilityService specifically so the parsing/mapping rules can be unit tested
/// without a live debuggee window - everything here only depends on UIAutomationClient's plain
/// Condition/PropertyCondition/ControlType types, not on any running window or process.
/// </summary>
internal static class SelectorConditions
{
    /// <summary>
    /// A bare selector (no "key=" prefix) matches Name, AutomationId, or ClassName (OR'd
    /// together) - this is the fallback used both for a plain-text selector and for an
    /// unrecognized "key=" prefix.
    /// </summary>
    public static Condition BuildSelectorCondition(string selector)
    {
        var trimmed = (selector ?? string.Empty).Trim();
        var separator = trimmed.IndexOf('=');
        if (separator > 0)
        {
            var key = trimmed.Substring(0, separator).Trim().ToLowerInvariant();
            var value = trimmed.Substring(separator + 1).Trim();
            return key switch
            {
                "id" or "automationid" or "automation-id" => new PropertyCondition(AutomationElement.AutomationIdProperty, value),
                "name" or "text" => new PropertyCondition(AutomationElement.NameProperty, value),
                "class" or "classname" or "class-name" => new PropertyCondition(AutomationElement.ClassNameProperty, value),
                "type" or "controltype" or "control-type" => ControlTypeCondition(value),
                _ => BuildTextCondition(value)
            };
        }

        return BuildTextCondition(trimmed);
    }

    public static Condition BuildTextCondition(string text) =>
        new OrCondition(
            new PropertyCondition(AutomationElement.NameProperty, text),
            new PropertyCondition(AutomationElement.AutomationIdProperty, text),
            new PropertyCondition(AutomationElement.ClassNameProperty, text));

    public static Condition ControlTypeCondition(string value)
    {
        var normalized = value.Replace("ControlType.", string.Empty).Trim().ToLowerInvariant();
        var controlType = normalized switch
        {
            "button" => ControlType.Button,
            "edit" or "textbox" or "text-box" => ControlType.Edit,
            "text" => ControlType.Text,
            "window" => ControlType.Window,
            "pane" => ControlType.Pane,
            "document" => ControlType.Document,
            "hyperlink" or "link" => ControlType.Hyperlink,
            "menuitem" or "menu-item" => ControlType.MenuItem,
            "tabitem" or "tab-item" => ControlType.TabItem,
            "listitem" or "list-item" => ControlType.ListItem,
            _ => ControlType.Custom
        };
        return new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
    }
}
