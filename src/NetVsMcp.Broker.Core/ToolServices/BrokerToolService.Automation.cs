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
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "console_read", Title = "Console Read", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads debuggee console output when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleRead(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_read", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleReadAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "console_send", Title = "Console Send", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Sends debuggee console input when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleSend(string text, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_send", target, null, null, text, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleSendAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "console_get_info", Title = "Console Get Info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns debuggee console metadata when a VSIX console backend is available.")]
    public Task<ToolResponse<AutomationResult>> ConsoleGetInfo(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("console_get_info", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.ConsoleGetInfoAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_capture_window", Title = "Ui Capture Window", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Captures a debuggee window when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiCaptureWindow(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_capture_window", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiCaptureWindowAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_capture_region", Title = "Ui Capture Region", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Captures a screen region when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiCaptureRegion(int x, int y, int width, int height, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_capture_region", null, null, null, null, x, y, width, height, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiCaptureRegionAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_snapshot", Title = "Ui Snapshot", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Returns a debuggee UI snapshot when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSnapshot(string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_snapshot", target, null, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSnapshotAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_get_tree", Title = "Ui Get Tree", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Returns a debuggee UI automation tree when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiGetTree(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_get_tree", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiGetTreeAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_find_elements", Title = "Ui Find Elements", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Finds UI automation elements when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiFindElements(string? selector = null, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (ValidateSelector(selector) is { } validation)
        {
            return Task.FromResult(FailWithCode<AutomationResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("ui_find_elements", target, selector, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiFindElementsAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_get_element", Title = "Ui Get Element", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Returns one UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiGetElement(string? selector = null, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (ValidateSelector(selector) is { } validation)
        {
            return Task.FromResult(FailWithCode<AutomationResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("ui_get_element", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiGetElementAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_click", Title = "Ui Click", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiClickAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_double_click", Title = "Ui Double Click", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Double-clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiDoubleClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_double_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiDoubleClickAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_right_click", Title = "Ui Right Click", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Right-clicks a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiRightClick(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_right_click", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiRightClickAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_drag", Title = "Ui Drag", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Drags a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiDrag(string selector, int x, int y, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_drag", target, selector, null, null, x, y, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiDragAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_set_value", Title = "Ui Set Value", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Sets a UI automation value when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSetValue(string selector, string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_set_value", target, selector, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSetValueAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_invoke", Title = "Ui Invoke", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Invokes a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiInvoke(string selector, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_invoke", target, selector, null, null, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiInvokeAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_send_keys", Title = "Ui Send Keys", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Sends keys to the debuggee UI when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiSendKeys(string text, string? target = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_send_keys", target, null, null, text, null, null, null, null, 5000, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiSendKeysAsync(request, ct), cancellationToken);
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_wait_for_element", Title = "Ui Wait For Element", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Waits for a UI automation element when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiWaitForElement(string? selector = null, string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (ValidateSelector(selector) is { } validation)
        {
            return Task.FromResult(FailWithCode<AutomationResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        return DispatchAutomation("ui_wait_for_element", target, selector, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiWaitForElementAsync(request, ct), cancellationToken);
    }
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "ui_wait_idle", Title = "Ui Wait Idle", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Waits for debuggee UI idle when a VSIX UI automation backend is available.")]
    public Task<ToolResponse<AutomationResult>> UiWaitIdle(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchAutomation("ui_wait_idle", target, null, null, null, null, null, null, null, timeoutMilliseconds, sessionId, solutionName, solutionPath, static (connection, request, ct) => connection.UiWaitIdleAsync(request, ct), cancellationToken);
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

    private static string? ValidateSelector(string? selector)
    {
        return string.IsNullOrWhiteSpace(selector)
            ? "Selector is required."
            : null;
    }
}

