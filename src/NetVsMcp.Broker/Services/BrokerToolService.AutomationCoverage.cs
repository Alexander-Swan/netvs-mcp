using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "console_read")]
    [Description("Planned: reads debuggee console output.")]
    public Task<ToolResponse<UnsupportedToolResult>> ConsoleRead(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Debuggee Console", "Implement routed debuggee console output capture.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "console_send")]
    [Description("Planned: sends debuggee console input.")]
    public Task<ToolResponse<UnsupportedToolResult>> ConsoleSend(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Debuggee Console", "Implement debug-profile-gated console input.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "console_get_info")]
    [Description("Planned: returns debuggee console metadata.")]
    public Task<ToolResponse<UnsupportedToolResult>> ConsoleGetInfo(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Debuggee Console", "Implement console process/window metadata discovery.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_capture_window")]
    [Description("Planned: captures a debuggee window.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiCaptureWindow(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement debuggee-window-scoped screenshot capture.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_capture_region")]
    [Description("Planned: captures a debuggee screen region.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiCaptureRegion(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement bounded region capture.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_snapshot")]
    [Description("Planned: returns UI automation snapshot data.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiSnapshot(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement UIA tree plus screenshot metadata snapshots.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_get_tree")]
    [Description("Planned: returns debuggee UI automation tree.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiGetTree(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement bounded UIA tree extraction.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_find_elements")]
    [Description("Planned: finds UI automation elements.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiFindElements(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement UIA element search.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_get_element")]
    [Description("Planned: returns one UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiGetElement(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement stable element lookup.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_click")]
    [Description("Planned: clicks a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiClick(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI click.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_double_click")]
    [Description("Planned: double-clicks a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiDoubleClick(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI double-click.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_right_click")]
    [Description("Planned: right-clicks a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiRightClick(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI right-click.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_drag")]
    [Description("Planned: drags a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiDrag(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI drag.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_set_value")]
    [Description("Planned: sets a UI automation value.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiSetValue(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI value setting.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_invoke")]
    [Description("Planned: invokes a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiInvoke(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated UI invoke.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_send_keys")]
    [Description("Planned: sends keys to the debuggee UI.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiSendKeys(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement admin-gated SendKeys scoped to debuggee UI.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_wait_for_element")]
    [Description("Planned: waits for a UI automation element.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiWaitForElement(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement UI wait with explicit timeouts.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "ui_wait_idle")]
    [Description("Planned: waits for debuggee UI idle.")]
    public Task<ToolResponse<UnsupportedToolResult>> UiWaitIdle(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("UI Automation", "Implement debuggee UI idle detection.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_connect")]
    [Description("Planned: connects browser debugging.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebConnect(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement explicit browser debug connection.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_disconnect")]
    [Description("Planned: disconnects browser debugging.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebDisconnect(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement browser debug disconnection.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_status")]
    [Description("Planned: returns browser debugging status.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebStatus(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement browser debug connection status.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_navigate")]
    [Description("Planned: navigates a connected browser.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebNavigate(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement admin-gated browser navigation.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_screenshot")]
    [Description("Planned: captures a browser screenshot.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebScreenshot(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement browser screenshot capture.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_dom_get")]
    [Description("Planned: returns browser DOM data.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebDomGet(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement DOM snapshot extraction.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_dom_query")]
    [Description("Planned: queries browser DOM elements.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebDomQuery(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement DOM selector query.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_console")]
    [Description("Planned: returns browser console entries.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebConsole(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement browser console collection.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_js_execute")]
    [Description("Planned: executes JavaScript in a connected browser.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebJsExecute(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement admin-gated JavaScript execution.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_network")]
    [Description("Planned: returns browser network events.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebNetwork(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement browser network event capture.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_element_click")]
    [Description("Planned: clicks a browser element.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebElementClick(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement admin-gated DOM element click.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "web_element_set_value")]
    [Description("Planned: sets a browser element value.")]
    public Task<ToolResponse<UnsupportedToolResult>> WebElementSetValue(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Web Debugging", "Implement admin-gated DOM value mutation.", sessionId, solutionName, solutionPath, cancellationToken);
}
