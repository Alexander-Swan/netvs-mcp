using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "console_read")]
    [Description("Reads debuggee console output when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleRead(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_read", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleReadAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "console_send")]
    [Description("Sends debuggee console input when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleSend(string text, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_send", target, null, null, text, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleSendAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "console_get_info")]
    [Description("Returns debuggee console metadata when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleGetInfo(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_get_info", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleGetInfoAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_capture_window")]
    [Description("Captures a debuggee window when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiCaptureWindow(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_capture_window", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiCaptureWindowAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_capture_region")]
    [Description("Captures a screen region when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiCaptureRegion(int x, int y, int width, int height, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_capture_region", null, null, null, null, x, y, width, height, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiCaptureRegionAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_snapshot")]
    [Description("Returns a debuggee UI snapshot when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSnapshot(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_snapshot", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSnapshotAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_get_tree")]
    [Description("Returns a debuggee UI automation tree when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiGetTree(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_get_tree", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiGetTreeAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_find_elements")]
    [Description("Finds UI automation elements when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiFindElements(string selector, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_find_elements", target, selector, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiFindElementsAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_get_element")]
    [Description("Returns one UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiGetElement(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_get_element", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiGetElementAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_click")]
    [Description("Clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiClickAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_double_click")]
    [Description("Double-clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiDoubleClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_double_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiDoubleClickAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_right_click")]
    [Description("Right-clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiRightClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_right_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiRightClickAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_drag")]
    [Description("Drags a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiDrag(string selector, int x, int y, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_drag", target, selector, null, null, x, y, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiDragAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_set_value")]
    [Description("Sets a UI automation value when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSetValue(string selector, string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_set_value", target, selector, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSetValueAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_invoke")]
    [Description("Invokes a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiInvoke(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_invoke", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiInvokeAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_send_keys")]
    [Description("Sends keys to the debuggee UI when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSendKeys(string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_send_keys", target, null, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSendKeysAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_wait_for_element")]
    [Description("Waits for a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiWaitForElement(string selector, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_wait_for_element", target, selector, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiWaitForElementAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "ui_wait_idle")]
    [Description("Waits for debuggee UI idle when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiWaitIdle(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_wait_idle", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiWaitIdleAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_connect")]
    [Description("Connects browser debugging when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebConnect(string? url = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_connect", target, null, url, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebConnectAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_disconnect")]
    [Description("Disconnects browser debugging when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDisconnect(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_disconnect", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDisconnectAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_status")]
    [Description("Returns browser debugging status when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebStatus(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_status", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebStatusAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_navigate")]
    [Description("Navigates a connected browser when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebNavigate(string url, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_navigate", target, null, url, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebNavigateAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_screenshot")]
    [Description("Captures a browser screenshot when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebScreenshot(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_screenshot", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebScreenshotAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_dom_get")]
    [Description("Returns browser DOM data when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDomGet(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_dom_get", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDomGetAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_dom_query")]
    [Description("Queries browser DOM elements when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDomQuery(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_dom_query", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDomQueryAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_console")]
    [Description("Returns browser console entries when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebConsole(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_console", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebConsoleAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_js_execute")]
    [Description("Executes JavaScript in a connected browser when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebJsExecute(string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_js_execute", target, null, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebJsExecuteAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_network")]
    [Description("Returns browser network events when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebNetwork(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_network", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebNetworkAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_element_click")]
    [Description("Clicks a browser element when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebElementClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_element_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebElementClickAsync(request, ct), cancellationToken);

    [McpServerTool(Name = "web_element_set_value")]
    [Description("Sets a browser element value when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebElementSetValue(string selector, string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_element_set_value", target, selector, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebElementSetValueAsync(request, ct), cancellationToken);

    private Task<ToolResponse<AutomationResult>> DispatchAutomation(
        string toolName,
        string? target,
        string? selector,
        string? url,
        string? text,
        int? x,
        int? y,
        int? width,
        int? height,
        int timeoutMilliseconds,
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        Func<IVisualStudioSessionRpc, AutomationRequest, CancellationToken, Task<AutomationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (timeoutMilliseconds <= 0)
        {
            return Task.FromResult(FailWithCode<AutomationResult>("Timeout must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new AutomationRequest
        {
            ToolName = toolName,
            Target = target,
            Selector = selector,
            Url = url,
            Text = text,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            TimeoutMilliseconds = timeoutMilliseconds
        };

        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => operation(connection, request, ct), cancellationToken);
    }
}
