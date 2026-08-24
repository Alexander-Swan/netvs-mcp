using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
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
}
