using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    private const string WebAutomationFallbackDescription = "Use web_* tools only when no dedicated browser automation tool is installed or available, such as Playwright, Chrome DevTools, or browser-specific tools, or when you need to automate a browser/debuggee through the routed Visual Studio session. ";

    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_connect", Title = "Web Connect", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Connects browser debugging when a VSIX browser backend is available. Pass a Chrome or Edge remote debugging endpoint as target, such as http://127.0.0.1:9222, for CDP-backed JavaScript, console, and network coverage; passing only a page URL may fall back to the Visual Studio browser shell/UIA backend.")]
    public Task<ToolResponse<AutomationResult>> WebConnect(string? url = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_connect", target, null, url, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebConnectAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_disconnect", Title = "Web Disconnect", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Disconnects browser debugging when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDisconnect(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_disconnect", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDisconnectAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_status", Title = "Web Status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns browser debugging status when a VSIX browser backend is available. Check the reported backend before choosing follow-up tools: CDP supports JavaScript execution, console, and network inspection; browser-shell/UIA backends provide limited navigation, screenshot, DOM fallback, and UI automation.")]
    public Task<ToolResponse<AutomationResult>> WebStatus(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_status", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebStatusAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_navigate", Title = "Web Navigate", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Navigates a connected browser when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebNavigate(string? url = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult(FailWithCode<AutomationResult>("Url is required.", ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("web_navigate", target, null, url, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebNavigateAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_screenshot", Title = "Web Screenshot", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Captures a browser screenshot when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebScreenshot(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_screenshot", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebScreenshotAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_dom_get", Title = "Web Dom Get", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns browser DOM data when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDomGet(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_dom_get", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDomGetAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_dom_query", Title = "Web Dom Query", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Queries browser DOM elements when a VSIX browser backend is available.")]
    public Task<ToolResponse<AutomationResult>> WebDomQuery(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_dom_query", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebDomQueryAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_console", Title = "Web Console", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns browser console entries when a VSIX browser backend is available. Requires a CDP backend from web_connect, typically Chrome or Edge launched with a remote debugging port; shell/UIA backends can return an empty supported result without console entries.")]
    public Task<ToolResponse<AutomationResult>> WebConsole(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_console", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebConsoleAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_js_execute", Title = "Web Js Execute", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Executes JavaScript in a connected browser when a VSIX browser backend is available. Call web_connect first with a Chrome or Edge remote debugging endpoint, such as http://127.0.0.1:9222; this tool requires the connected backend to be CDP and fails on browser-shell/UIA fallback connections.")]
    public Task<ToolResponse<AutomationResult>> WebJsExecute(string? text = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(FailWithCode<AutomationResult>("Text is required.", ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("web_js_execute", target, null, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebJsExecuteAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_inspect", Title = "Web Blazor Inspect", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Inspects the connected browser page for Blazor runtime markers and reports whether the page appears to be Blazor Server, Blazor WebAssembly, or an unknown Blazor hosting model. Call web_connect first with a Chrome or Edge remote debugging endpoint; this tool requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorInspect(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_inspect", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorInspectAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_wait_ready", Title = "Web Blazor Wait Ready", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Waits until the connected browser page exposes observable Blazor readiness markers, such as the Blazor browser global, component markers, or framework resources. Call web_connect first with a Chrome or Edge remote debugging endpoint; this tool requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorWaitReady(int timeoutMilliseconds = 10000, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_wait_ready", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorWaitReadyAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_get_components", Title = "Web Blazor Get Components", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns observable Blazor component markers from the connected page, including Blazor comment descriptors and elements with Blazor-generated marker attributes. This reports browser-visible markers, not private .NET component instances. Requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorGetComponents(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_get_components", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorGetComponentsAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_get_state", Title = "Web Blazor Get State", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns browser-visible Blazor page state for a selector or the document, including form values, status text, component markers, and runtime/resource markers. This does not expose private .NET component fields. Requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorGetState(string? selector = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_get_state", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorGetStateAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_trigger_event", Title = "Web Blazor Trigger Event", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Dispatches a browser event such as click, input, change, keydown, or submit on a selected element in a connected Blazor page. Requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorTriggerEvent(string selector, string eventName, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (ValidateSelector(selector) is { } selectorValidation)
        {
            return Task.FromResult(FailWithCode<AutomationResult>(selectorValidation, ToolErrorCodes.InvalidRequest));
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            return Task.FromResult(FailWithCode<AutomationResult>("Event name is required.", ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("web_blazor_trigger_event", target, selector, null, eventName, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorTriggerEventAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_inspect_circuit", Title = "Web Blazor Inspect Circuit", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Inspects observable Blazor Server circuit indicators on the connected page, including _blazor resources, server component descriptors, and SignalR endpoint evidence. Requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorInspectCircuit(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_inspect_circuit", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorInspectCircuitAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_blazor_inspect_wasm", Title = "Web Blazor Inspect Wasm", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Inspects observable Blazor WebAssembly runtime resources on the connected page, including dotnet JavaScript, .wasm, boot JSON, and loaded framework resources. Requires CDP.")]
    public Task<ToolResponse<AutomationResult>> WebBlazorInspectWasm(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_blazor_inspect_wasm", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebBlazorInspectWasmAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_network", Title = "Web Network", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Returns browser network events when a VSIX browser backend is available. Requires a CDP backend from web_connect, typically Chrome or Edge launched with a remote debugging port; shell/UIA backends can return an empty supported result without captured network events.")]
    public Task<ToolResponse<AutomationResult>> WebNetwork(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_network", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebNetworkAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_element_click", Title = "Web Element Click", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Clicks a browser element when a VSIX browser backend is available. With browser-shell/UIA backends, selector is a UI Automation selector or visible/accessibility text, not a CSS selector; prefer DOM tools or a CDP connection when CSS/JavaScript targeting is required.")]
    public Task<ToolResponse<AutomationResult>> WebElementClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_element_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebElementClickAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "web_element_set_value", Title = "Web Element Set Value", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description(WebAutomationFallbackDescription + "Sets a browser element value when a VSIX browser backend is available. With browser-shell/UIA backends, selector is a UI Automation selector or visible/accessibility text, not a CSS selector; prefer DOM tools or a CDP connection when CSS/JavaScript targeting is required.")]
    public Task<ToolResponse<AutomationResult>> WebElementSetValue(string selector, string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("web_element_set_value", target, selector, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.WebElementSetValueAsync(request, ct), cancellationToken);
}

