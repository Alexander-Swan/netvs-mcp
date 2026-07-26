using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using DiagnosticsProcess = System.Diagnostics.Process;
using VsProcess = EnvDTE.Process;

namespace NetVsMcp.Vsix;

internal interface IAutomationCapabilityService
{
    Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken);
}

internal sealed class AutomationCapabilityService : IAutomationCapabilityService
{
    private const int MaxTreeDepth = 4;
    private const int MaxTreeNodes = 200;
    private const int MaxTextChars = 20000;

    private readonly AsyncPackage package;
    private readonly Dictionary<string, ElementRef> elements = new(StringComparer.OrdinalIgnoreCase);
    private readonly object consoleLock = new();
    private int nextElementId;
    private CdpClient? cdp;
    private string? connectedWebTarget;
    private string? connectedWebUrl;

    public AutomationCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var processId = ResolveConsoleProcessId(dte, request);
        string? consoleMessage = null;
        if (processId is not null && TryReadConsoleBuffer(processId.Value, out var consoleText, out consoleMessage))
        {
            return Success(request, consoleText, ("backend", "windows-console"), ("source", "debuggee-console"), ("processId", processId.Value.ToString()));
        }

        var text = ReadOutputPane(dte, request.Target, ["Debug", "Tests", "Build"]);
        return Success(request, text, ("backend", "visual-studio-output"), ("source", "output-window"), ("consoleMessage", consoleMessage ?? string.Empty));
    }

    public async Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var text = ReadOutputPane(dte, request.Target, ["Debug", "XAML Binding Failures", "XAML Diagnostics", "Output"]);
        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => line.IndexOf("binding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           line.IndexOf("System.Windows.Data Error", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();
        return Success(request, string.Join(Environment.NewLine, lines), ("backend", "visual-studio-output"), ("matches", lines.Length.ToString()));
    }

    public async Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var processId = ResolveConsoleProcessId(dte, request);
        string? consoleMessage = null;
        if (processId is not null && TryWriteConsoleInput(processId.Value, request.Text ?? string.Empty, out consoleMessage))
        {
            return Success(request, null, ("backend", "windows-console"), ("processId", processId.Value.ToString()));
        }

        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is null)
        {
            return Failure(request, "No target window was found for console input.", ("backend", "sendkeys"), ("consoleMessage", consoleMessage ?? string.Empty));
        }

        if (!TrySetForegroundWindow(target.Value.WindowHandle))
        {
            return Failure(request, "Unable to activate target window for console input.", ("backend", "sendkeys"));
        }

        SendKeys.SendWait(request.Text ?? string.Empty);
        return Success(request, null, ("backend", "sendkeys"), ("windowHandle", target.Value.WindowHandle.ToString()), ("consoleMessage", consoleMessage ?? string.Empty));
    }

    public async Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        var text = SerializeWindows(windows);
        return Success(request, text, ("backend", "process-window-enumeration"), ("windowCount", windows.Count.ToString()));
    }

    public async Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is null)
        {
            return Failure(request, "No target window was found to capture.", ("backend", "screen-capture"));
        }

        return CaptureRectangle(request, target.Value.Bounds, ("windowHandle", target.Value.WindowHandle.ToString()));
    }

    public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Width.GetValueOrDefault() <= 0 || request.Height.GetValueOrDefault() <= 0)
        {
            return Task.FromResult(Failure(request, "Capture width and height must be greater than zero.", ("backend", "screen-capture")));
        }

        return Task.FromResult(CaptureRectangle(
            request,
            new Rectangle(request.X.GetValueOrDefault(), request.Y.GetValueOrDefault(), request.Width!.Value, request.Height!.Value)));
    }

    public async Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        var text = SerializeWindows(windows);
        return Success(request, text, ("backend", "uia"), ("windowCount", windows.Count.ToString()));
    }

    public async Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var roots = await ResolveAutomationRootsAsync(request, cancellationToken);
        var builder = new StringBuilder();
        var count = 0;
        foreach (var root in roots)
        {
            AppendElementTree(root, 0, builder, ref count, cancellationToken);
            if (count >= MaxTreeNodes)
            {
                break;
            }
        }

        return Success(request, builder.ToString(), ("backend", "uia"), ("nodeCount", count.ToString()));
    }

    public async Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var matches = await FindElementsAsync(request, firstOnly: false, cancellationToken);
        return Success(request, SerializeElements(matches), ("backend", "uia"), ("matchCount", matches.Count.ToString()));
    }

    public async Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = (await FindElementsAsync(request, firstOnly: true, cancellationToken)).FirstOrDefault();
        return match.Element is null
            ? Failure(request, "No matching UI element was found.", ("backend", "uia"))
            : Success(request, SerializeElement(match), ("backend", "uia"), ("elementId", match.Id));
    }

    public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ClickElementAsync(request, MouseClickKind.Left, 1, cancellationToken);

    public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ClickElementAsync(request, MouseClickKind.Left, 2, cancellationToken);

    public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ClickElementAsync(request, MouseClickKind.Right, 1, cancellationToken);

    public async Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return Failure(request, "No matching UI element was found for drag.", ("backend", "uia-input"));
        }

        var from = Center(match.Element.Current.BoundingRectangle);
        var toX = request.X ?? (int)from.X;
        var toY = request.Y ?? (int)from.Y;
        MoveMouse((int)from.X, (int)from.Y);
        MouseEvent(MouseEventFlags.LeftDown);
        MoveMouse(toX, toY);
        MouseEvent(MouseEventFlags.LeftUp);
        return Success(request, null, ("backend", "uia-input"), ("elementId", match.Id));
    }

    public async Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return Failure(request, "No matching UI element was found for value mutation.", ("backend", "uia"));
        }

        if (match.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern)
        {
            valuePattern.SetValue(request.Text ?? string.Empty);
            return Success(request, null, ("backend", "uia-value-pattern"), ("elementId", match.Id));
        }

        if (TrySetForegroundWindow(WindowFromElement(match.Element)))
        {
            SendKeys.SendWait("^a");
            SendKeys.SendWait(request.Text ?? string.Empty);
            return Success(request, null, ("backend", "sendkeys-fallback"), ("elementId", match.Id));
        }

        return Failure(request, "Element does not expose ValuePattern and could not be activated.", ("backend", "uia"));
    }

    public async Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return Failure(request, "No matching UI element was found for invoke.", ("backend", "uia"));
        }

        if (match.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObject) &&
            invokePatternObject is InvokePattern invokePattern)
        {
            invokePattern.Invoke();
            return Success(request, null, ("backend", "uia-invoke-pattern"), ("elementId", match.Id));
        }

        return await ClickElementAsync(request, MouseClickKind.Left, 1, cancellationToken);
    }

    public async Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is not null && !TrySetForegroundWindow(target.Value.WindowHandle))
        {
            return Failure(request, "Unable to activate target window for key input.", ("backend", "sendkeys"));
        }

        SendKeys.SendWait(request.Text ?? string.Empty);
        return Success(request, null, ("backend", "sendkeys"));
    }

    public async Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, request.TimeoutMilliseconds));
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = (await FindElementsAsync(request, firstOnly: true, cancellationToken)).FirstOrDefault();
            if (match.Element is not null)
            {
                return Success(request, SerializeElement(match), ("backend", "uia"), ("elementId", match.Id));
            }

            await Task.Delay(100, cancellationToken);
        }

        return Failure(request, "Timed out waiting for UI element.", ("backend", "uia"));
    }

    public async Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        foreach (var window in windows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = DiagnosticsProcess.GetProcessById(window.ProcessId);
            process.WaitForInputIdle(Math.Max(1, request.TimeoutMilliseconds));
        }

        return Success(request, null, ("backend", "process-input-idle"), ("windowCount", windows.Count.ToString()));
    }

    public async Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeCdp();
        var endpoint = ResolveCdpEndpoint(request.Target);
        if (endpoint is not null)
        {
            try
            {
                cdp = await CdpClient.ConnectAsync(endpoint, request.Url, cancellationToken);
                connectedWebTarget = endpoint.ToString();
                connectedWebUrl = cdp.TargetUrl;
                return Success(request, null, ("backend", "cdp"), ("endpoint", endpoint.ToString()), ("url", connectedWebUrl ?? string.Empty));
            }
            catch (Exception ex) when (ex is WebException or WebSocketException or JsonException or InvalidOperationException)
            {
                connectedWebTarget = request.Target;
                connectedWebUrl = request.Url ?? connectedWebUrl;
                return Success(request, null, ("backend", "browser-shell-uia"), ("cdpMessage", ex.Message), ("url", connectedWebUrl ?? string.Empty));
            }
        }

        connectedWebTarget = request.Target;
        connectedWebUrl = request.Url ?? connectedWebUrl;
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            DiagnosticsProcess.Start(request.Url);
        }

        return Success(request, null, ("backend", "browser-shell-uia"), ("url", connectedWebUrl ?? string.Empty));
    }

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeCdp();
        connectedWebTarget = null;
        connectedWebUrl = null;
        return Task.FromResult(Success(request, null, ("backend", "cdp")));
    }

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpClient = cdp;
        var text = cdpClient is not null
            ? $"connected=true; backend=cdp; target={connectedWebTarget}; url={connectedWebUrl}; websocket={cdpClient.WebSocketUri}"
            : $"connected={connectedWebTarget is not null || connectedWebUrl is not null}; backend=browser-shell-uia; target={connectedWebTarget}; url={connectedWebUrl}";
        return Task.FromResult(Success(request, text, ("backend", cdpClient is null ? "browser-shell-uia" : "cdp")));
    }

    public async Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return Failure(request, "URL is required for browser navigation.", ("backend", "browser-shell"));
        }

        connectedWebUrl = request.Url;
        if (cdp is not null)
        {
            await cdp.NavigateAsync(request.Url!, cancellationToken);
            return Success(request, null, ("backend", "cdp"), ("url", request.Url ?? string.Empty));
        }

        DiagnosticsProcess.Start(request.Url);
        return Success(request, null, ("backend", "browser-shell"), ("url", request.Url ?? string.Empty));
    }

    public async Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (cdp is not null)
        {
            var image = await cdp.CaptureScreenshotAsync(cancellationToken);
            return Success(request, image, ("backend", "cdp"), ("encoding", "base64"), ("format", "png"));
        }

        return await UiCaptureWindowAsync(WithWebTarget(request), cancellationToken);
    }

    public async Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cdp is not null)
        {
            var liveHtml = await cdp.EvaluateStringAsync("document.documentElement ? document.documentElement.outerHTML : ''", cancellationToken);
            return Success(request, Truncate(liveHtml), ("backend", "cdp"), ("url", connectedWebUrl ?? string.Empty));
        }

        var url = request.Url ?? connectedWebUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Failure(request, "A URL is required before DOM fetch.", ("backend", "http-fetch"));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        return Success(request, Truncate(html), ("backend", "http-fetch"), ("url", resolvedUrl));
    }

    public async Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cdp is not null)
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var expression = $"Array.from(document.querySelectorAll({selectorJson})).map(e => e.outerHTML).join('\\n')";
            var result = await cdp.EvaluateStringAsync(expression, cancellationToken);
            var count = string.IsNullOrEmpty(result) ? 0 : result.Split('\n').Length;
            return Success(request, Truncate(result), ("backend", "cdp"), ("matchCount", count.ToString()));
        }

        var url = request.Url ?? connectedWebUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Failure(request, "A URL is required before DOM query.", ("backend", "http-fetch"));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        var matches = QueryHtml(html, request.Selector).ToArray();
        return Success(request, string.Join(Environment.NewLine, matches), ("backend", "http-fetch"), ("matchCount", matches.Length.ToString()));
    }

    public async Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cdp is not null)
        {
            await cdp.FlushEventsAsync(cancellationToken);
            var entries = cdp.GetConsoleEntries();
            return Success(request, string.Join(Environment.NewLine, entries), ("backend", "cdp"), ("entryCount", entries.Count.ToString()));
        }

        return Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Browser console capture requires CDP; no console entries are available from the shell backend."));
    }

    public async Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cdp is null)
        {
            return Failure(request, "JavaScript execution requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
        }

        var result = await cdp.EvaluateAsync(request.Text ?? string.Empty, cancellationToken);
        return Success(request, result, ("backend", "cdp"));
    }

    public async Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cdp is not null)
        {
            await cdp.FlushEventsAsync(cancellationToken);
            var entries = cdp.GetNetworkEntries();
            return Success(request, string.Join(Environment.NewLine, entries), ("backend", "cdp"), ("entryCount", entries.Count.ToString()));
        }

        return Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Network capture requires CDP; no network events are available from the shell backend."));
    }

    public async Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (cdp is not null)
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var result = await cdp.EvaluateAsync($"(() => {{ const e = document.querySelector({selectorJson}); if (!e) return false; e.click(); return true; }})()", cancellationToken);
            return Success(request, result, ("backend", "cdp"));
        }

        return await UiClickAsync(WithWebTarget(request), cancellationToken);
    }

    public async Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (cdp is not null)
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var valueJson = JsonSerializer.Serialize(request.Text ?? string.Empty);
            var expression = $"(() => {{ const e = document.querySelector({selectorJson}); if (!e) return false; e.value = {valueJson}; e.dispatchEvent(new Event('input', {{ bubbles: true }})); e.dispatchEvent(new Event('change', {{ bubbles: true }})); return true; }})()";
            var result = await cdp.EvaluateAsync(expression, cancellationToken);
            return Success(request, result, ("backend", "cdp"));
        }

        return await UiSetValueAsync(WithWebTarget(request), cancellationToken);
    }

    private static Uri? ResolveCdpEndpoint(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var value = target!.Trim();
        if (int.TryParse(value, out var port) && port > 0)
        {
            return new Uri($"http://127.0.0.1:{port}");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absolute!;
        }

        return value.Contains(":")
            ? new Uri($"http://{value}")
            : null;
    }

    private void DisposeCdp()
    {
        cdp?.Dispose();
        cdp = null;
    }

    private static int? ResolveConsoleProcessId(DTE dte, AutomationRequest request)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (int.TryParse(request.Target, out var targetProcessId) && targetProcessId > 0)
        {
            return targetProcessId;
        }

        foreach (VsProcess process in dte.Debugger.DebuggedProcesses)
        {
            if (string.IsNullOrWhiteSpace(request.Target) ||
                process.Name.IndexOf(request.Target ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return process.ProcessID;
            }
        }

        return null;
    }

    private async Task<DTE> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE
            ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
    }

    private async Task<IReadOnlyCollection<TargetWindow>> ResolveTargetWindowsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var candidates = new List<TargetWindow>();
        var debuggedProcessIds = new HashSet<int>();

        foreach (VsProcess process in dte.Debugger.DebuggedProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            debuggedProcessIds.Add(process.ProcessID);
            AddProcessWindows(process.ProcessID, candidates);
        }

        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            AddTargetWindows(request.Target ?? string.Empty, candidates);
        }

        if (candidates.Count == 0)
        {
            foreach (var processId in debuggedProcessIds)
            {
                AddProcessWindows(processId, candidates, includeInvisible: true);
            }
        }

        return candidates
            .GroupBy(window => window.WindowHandle)
            .Select(group => group.First())
            .ToArray();
    }

    private async Task<TargetWindow?> ResolveTargetWindowAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        return windows.Count == 0 ? null : windows.First();
    }

    private async Task<IReadOnlyCollection<AutomationElement>> ResolveAutomationRootsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        var roots = new List<AutomationElement>();
        foreach (var window in windows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = AutomationElement.FromHandle(new IntPtr(window.WindowHandle));
            if (element is not null)
            {
                roots.Add(element);
            }
        }

        if (roots.Count == 0 && !string.IsNullOrWhiteSpace(request.Target))
        {
            var desktop = AutomationElement.RootElement;
            roots.AddRange(desktop.FindAll(TreeScope.Children, BuildTextCondition(request.Target ?? string.Empty)).Cast<AutomationElement>());
        }

        return roots;
    }

    private async Task<IReadOnlyCollection<ElementMatch>> FindElementsAsync(AutomationRequest request, bool firstOnly, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Selector))
        {
            return Array.Empty<ElementMatch>();
        }

        var selector = request.Selector ?? string.Empty;
        if (elements.TryGetValue(selector.Trim(), out var cached))
        {
            var cachedElement = AutomationElement.FromHandle(new IntPtr(cached.WindowHandle));
            if (cachedElement is not null)
            {
                return [new ElementMatch(selector.Trim(), cachedElement)];
            }
        }

        var roots = await ResolveAutomationRootsAsync(request, cancellationToken);
        var condition = BuildSelectorCondition(selector);
        var matches = new List<ElementMatch>();
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = TreeScope.Element | TreeScope.Descendants;
            if (firstOnly)
            {
                var element = root.FindFirst(scope, condition);
                if (element is not null)
                {
                    matches.Add(RegisterElement(element));
                    break;
                }
            }
            else
            {
                foreach (AutomationElement element in root.FindAll(scope, condition))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    matches.Add(RegisterElement(element));
                    if (matches.Count >= MaxTreeNodes)
                    {
                        return matches;
                    }
                }
            }
        }

        return matches;
    }

    private async Task<ElementMatch> ResolveElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        (await FindElementsAsync(request, firstOnly: true, cancellationToken)).FirstOrDefault();

    private ElementMatch RegisterElement(AutomationElement element)
    {
        var id = "ui-" + Interlocked.Increment(ref nextElementId).ToString("D6");
        var hwnd = WindowFromElement(element);
        elements[id] = new ElementRef(hwnd);
        return new ElementMatch(id, element);
    }

    private async Task<AutomationResult> ClickElementAsync(AutomationRequest request, MouseClickKind clickKind, int count, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return Failure(request, "No matching UI element was found for click.", ("backend", "uia-input"));
        }

        var point = Center(match.Element.Current.BoundingRectangle);
        MoveMouse((int)point.X, (int)point.Y);
        for (var index = 0; index < count; index++)
        {
            if (clickKind == MouseClickKind.Right)
            {
                MouseEvent(MouseEventFlags.RightDown);
                MouseEvent(MouseEventFlags.RightUp);
            }
            else
            {
                MouseEvent(MouseEventFlags.LeftDown);
                MouseEvent(MouseEventFlags.LeftUp);
            }
        }

        return Success(request, null, ("backend", "uia-input"), ("elementId", match.Id));
    }

    private AutomationResult CaptureRectangle(AutomationRequest request, Rectangle rectangle, params (string Key, string Value)[] extraMetadata)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return Failure(request, "Capture rectangle is empty.", ("backend", "screen-capture"));
        }

        using var bitmap = new Bitmap(rectangle.Width, rectangle.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, rectangle.Size);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var metadata = new List<(string Key, string Value)>
        {
            ("backend", "screen-capture"),
            ("mimeType", "image/png"),
            ("encoding", "base64"),
            ("x", rectangle.X.ToString()),
            ("y", rectangle.Y.ToString()),
            ("width", rectangle.Width.ToString()),
            ("height", rectangle.Height.ToString())
        };
        metadata.AddRange(extraMetadata);
        return Success(request, Convert.ToBase64String(stream.ToArray()), metadata.ToArray());
    }

    private static AutomationRequest WithWebTarget(AutomationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            return request;
        }

        request.Target = "chrome";
        return request;
    }

    private static string ReadOutputPane(DTE dte, string? paneName, IReadOnlyCollection<string> fallbackPaneNames)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var panes = (dte as DTE2)?.ToolWindows?.OutputWindow?.OutputWindowPanes;
        if (panes is null)
        {
            return string.Empty;
        }

        OutputWindowPane? first = null;
        for (var index = 1; index <= panes.Count; index++)
        {
            OutputWindowPane pane;
            string currentPaneName;
            try
            {
                pane = panes.Item(index);
                currentPaneName = pane.Name;
            }
            catch (COMException)
            {
                // Some panes (e.g. ones never activated) can throw when queried; skip them.
                continue;
            }

            first ??= pane;
            var matchesRequestedPane = !string.IsNullOrWhiteSpace(paneName) &&
                currentPaneName.IndexOf(paneName, StringComparison.OrdinalIgnoreCase) >= 0;
            var matchesFallbackPane = false;
            foreach (var fallbackPaneName in fallbackPaneNames)
            {
                if (string.Equals(fallbackPaneName, currentPaneName, StringComparison.OrdinalIgnoreCase))
                {
                    matchesFallbackPane = true;
                    break;
                }
            }

            if (matchesRequestedPane || matchesFallbackPane)
            {
                return Truncate(ReadPaneText(pane));
            }
        }

        return first is null ? string.Empty : Truncate(ReadPaneText(first));
    }

    private static string ReadPaneText(OutputWindowPane pane)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (pane.TextDocument is not TextDocument textDocument)
            {
                return string.Empty;
            }

            var editPoint = textDocument.StartPoint.CreateEditPoint();
            return editPoint.GetText(textDocument.EndPoint);
        }
        catch (COMException)
        {
            // The pane's text document may not be available (e.g. pane never activated).
            return string.Empty;
        }
    }

    private static void AddTargetWindows(string target, ICollection<TargetWindow> windows)
    {
        if (int.TryParse(target, out var processId))
        {
            AddProcessWindows(processId, windows);
            return;
        }

        foreach (var process in DiagnosticsProcess.GetProcesses())
        {
            try
            {
                if (process.ProcessName.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    process.MainWindowTitle.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddProcessWindows(process.Id, windows);
                }
            }
            catch
            {
                // Ignore processes that disappear or deny inspection.
            }
        }
    }

    private static void AddProcessWindows(int processId, ICollection<TargetWindow> windows, bool includeInvisible = false)
    {
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var ownerProcessId);
            if (ownerProcessId != processId)
            {
                return true;
            }

            if (!includeInvisible && !IsWindowVisible(hwnd))
            {
                return true;
            }

            var title = GetWindowText(hwnd);
            var bounds = GetWindowRectangle(hwnd);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                windows.Add(new TargetWindow(hwnd.ToInt64(), processId, title, bounds));
            }

            return true;
        }, IntPtr.Zero);
    }

    private static Condition BuildSelectorCondition(string selector)
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

    private static Condition BuildTextCondition(string text) =>
        new OrCondition(
            new PropertyCondition(AutomationElement.NameProperty, text),
            new PropertyCondition(AutomationElement.AutomationIdProperty, text),
            new PropertyCondition(AutomationElement.ClassNameProperty, text));

    private static Condition ControlTypeCondition(string value)
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

    private static void AppendElementTree(AutomationElement element, int depth, StringBuilder builder, ref int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count >= MaxTreeNodes || depth > MaxTreeDepth)
        {
            return;
        }

        count++;
        builder.Append(' ', depth * 2);
        builder.AppendLine(FormatElement(element));
        if (depth == MaxTreeDepth)
        {
            return;
        }

        var children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement child in children)
        {
            AppendElementTree(child, depth + 1, builder, ref count, cancellationToken);
            if (count >= MaxTreeNodes)
            {
                break;
            }
        }
    }

    private static string SerializeWindows(IReadOnlyCollection<TargetWindow> windows) =>
        string.Join(Environment.NewLine, windows.Select(window =>
            $"pid={window.ProcessId}; hwnd={window.WindowHandle}; title=\"{window.Title}\"; bounds={window.Bounds.X},{window.Bounds.Y},{window.Bounds.Width},{window.Bounds.Height}"));

    private static string SerializeElements(IReadOnlyCollection<ElementMatch> matches) =>
        string.Join(Environment.NewLine, matches.Select(SerializeElement));

    private static string SerializeElement(ElementMatch match) =>
        $"id={match.Id}; {FormatElement(match.Element!)}";

    private static string FormatElement(AutomationElement element)
    {
        var current = element.Current;
        var bounds = current.BoundingRectangle;
        return $"type={current.ControlType.ProgrammaticName}; name=\"{current.Name}\"; automationId=\"{current.AutomationId}\"; class=\"{current.ClassName}\"; bounds={bounds.X:0},{bounds.Y:0},{bounds.Width:0},{bounds.Height:0}";
    }

    private static IEnumerable<string> QueryHtml(string html, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            yield break;
        }

        var trimmed = (selector ?? string.Empty).Trim();
        var pattern = trimmed.StartsWith("#", StringComparison.Ordinal)
            ? $@"<[^>]+id\s*=\s*[""']{Regex.Escape(trimmed.Substring(1))}[""'][^>]*>"
            : trimmed.StartsWith(".", StringComparison.Ordinal)
                ? $@"<[^>]+class\s*=\s*[""'][^""']*{Regex.Escape(trimmed.Substring(1))}[^""']*[""'][^>]*>"
                : $@"<{Regex.Escape(trimmed)}(\s[^>]*)?>";

        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
        {
            yield return match.Value;
        }
    }

    private static string DownloadText(string url)
    {
        using var client = new WebClient();
        client.Encoding = Encoding.UTF8;
        return client.DownloadString(url);
    }

    private bool TryReadConsoleBuffer(int processId, out string text, out string? message)
    {
        lock (consoleLock)
        {
            text = string.Empty;
            message = null;
            FreeConsole();
            if (!AttachConsole(processId))
            {
                message = $"AttachConsole failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            try
            {
                var output = GetStdHandle(StdOutputHandle);
                if (output == IntPtr.Zero || output == InvalidHandleValue)
                {
                    message = "The attached process does not expose a console output handle.";
                    return false;
                }

                if (!GetConsoleScreenBufferInfo(output, out var info))
                {
                    message = $"GetConsoleScreenBufferInfo failed: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                var width = Math.Max(1, (int)info.Size.X);
                var height = Math.Max(1, (int)info.Size.Y);
                var startY = Math.Max(0, info.CursorPosition.Y - Math.Min(height, 200) + 1);
                var length = (uint)Math.Min(width * (info.CursorPosition.Y - startY + 1), MaxTextChars);
                var builder = new StringBuilder((int)length);
                if (!ReadConsoleOutputCharacter(output, builder, length, new ConsoleCoord(0, (short)startY), out var read))
                {
                    message = $"ReadConsoleOutputCharacter failed: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                text = FormatConsoleBuffer(builder.ToString(0, (int)read), width);
                return true;
            }
            finally
            {
                FreeConsole();
            }
        }
    }

    private bool TryWriteConsoleInput(int processId, string text, out string? message)
    {
        lock (consoleLock)
        {
            message = null;
            FreeConsole();
            if (!AttachConsole(processId))
            {
                message = $"AttachConsole failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            try
            {
                var input = GetStdHandle(StdInputHandle);
                if (input == IntPtr.Zero || input == InvalidHandleValue)
                {
                    message = "The attached process does not expose a console input handle.";
                    return false;
                }

                var records = BuildConsoleInputRecords(text);
                if (records.Length == 0)
                {
                    return true;
                }

                if (!WriteConsoleInput(input, records, (uint)records.Length, out var written) || written != records.Length)
                {
                    message = $"WriteConsoleInput failed: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                return true;
            }
            finally
            {
                FreeConsole();
            }
        }
    }

    private static ConsoleInputRecord[] BuildConsoleInputRecords(string text)
    {
        var records = new List<ConsoleInputRecord>(text.Length * 2);
        foreach (var ch in text)
        {
            records.Add(ConsoleInputRecord.Key(ch, keyDown: true));
            records.Add(ConsoleInputRecord.Key(ch, keyDown: false));
        }

        return records.ToArray();
    }

    private static string FormatConsoleBuffer(string raw, int width)
    {
        var lines = new List<string>();
        for (var index = 0; index < raw.Length; index += width)
        {
            var length = Math.Min(width, raw.Length - index);
            lines.Add(raw.Substring(index, length).TrimEnd());
        }

        return Truncate(string.Join(Environment.NewLine, lines).TrimEnd());
    }

    private static string Truncate(string text) =>
        text.Length <= MaxTextChars ? text : text.Substring(text.Length - MaxTextChars, MaxTextChars);

    private static AutomationResult Success(AutomationRequest request, string? text, params (string Key, string Value)[] metadata) =>
        new(true, true, null, text, Metadata(request, metadata));

    private static AutomationResult Failure(AutomationRequest request, string message, params (string Key, string Value)[] metadata) =>
        new(true, false, message, null, Metadata(request, metadata));

    private static IReadOnlyDictionary<string, string> Metadata(AutomationRequest request, params (string Key, string Value)[] metadata)
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

    private static System.Windows.Point Center(System.Windows.Rect rectangle) =>
        new(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);

    private static long WindowFromElement(AutomationElement element)
    {
        try
        {
            return new IntPtr(element.Current.NativeWindowHandle).ToInt64();
        }
        catch
        {
            return 0;
        }
    }

    private static void MoveMouse(int x, int y) => SetCursorPos(x, y);

    private static void MouseEvent(MouseEventFlags flags) => mouse_event((uint)flags, 0, 0, 0, UIntPtr.Zero);

    private static bool TrySetForegroundWindow(long hwnd) =>
        hwnd != 0 && SetForegroundWindow(new IntPtr(hwnd));

    private static string GetWindowText(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static Rectangle GetWindowRectangle(IntPtr hwnd)
    {
        return GetWindowRect(hwnd, out var rect)
            ? Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : Rectangle.Empty;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(IntPtr consoleOutput, out ConsoleScreenBufferInfo consoleScreenBufferInfo);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ReadConsoleOutputCharacter(
        IntPtr consoleOutput,
        StringBuilder character,
        uint length,
        ConsoleCoord readCoordinate,
        out uint numberOfCharsRead);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WriteConsoleInput(
        IntPtr consoleInput,
        ConsoleInputRecord[] buffer,
        uint length,
        out uint numberOfEventsWritten);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const short KeyEvent = 0x0001;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleCoord
    {
        public ConsoleCoord(short x, short y)
        {
            X = x;
            Y = y;
        }

        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleSmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleScreenBufferInfo
    {
        public ConsoleCoord Size;
        public ConsoleCoord CursorPosition;
        public short Attributes;
        public ConsoleSmallRect Window;
        public ConsoleCoord MaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConsoleKeyEventRecord
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public char UnicodeChar;
        public uint ControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConsoleInputRecord
    {
        public short EventType;
        public ConsoleKeyEventRecord KeyEvent;

        public static ConsoleInputRecord Key(char character, bool keyDown) =>
            new()
            {
                EventType = AutomationCapabilityService.KeyEvent,
                KeyEvent = new ConsoleKeyEventRecord
                {
                    KeyDown = keyDown,
                    RepeatCount = 1,
                    UnicodeChar = character
                }
            };
    }

    private sealed class CdpClient : IDisposable
    {
        private readonly ClientWebSocket socket;
        private readonly SemaphoreSlim commandLock = new(1, 1);
        private readonly List<string> consoleEntries = new();
        private readonly List<string> networkEntries = new();
        private int nextId;

        private CdpClient(Uri endpoint, Uri webSocketUri, string? targetUrl, ClientWebSocket socket)
        {
            Endpoint = endpoint;
            WebSocketUri = webSocketUri;
            TargetUrl = targetUrl;
            this.socket = socket;
        }

        public Uri Endpoint { get; }
        public Uri WebSocketUri { get; }
        public string? TargetUrl { get; private set; }

        public static async Task<CdpClient> ConnectAsync(Uri endpoint, string? requestedUrl, CancellationToken cancellationToken)
        {
            var target = await ResolveTargetAsync(endpoint, requestedUrl, cancellationToken);
            var websocket = new ClientWebSocket();
            await websocket.ConnectAsync(target.WebSocketUri, cancellationToken);
            var client = new CdpClient(endpoint, target.WebSocketUri, target.Url, websocket);
            await client.SendCommandAsync("Runtime.enable", null, cancellationToken);
            await client.SendCommandAsync("Network.enable", null, cancellationToken);
            await client.SendCommandAsync("Page.enable", null, cancellationToken);
            return client;
        }

        public async Task NavigateAsync(string url, CancellationToken cancellationToken)
        {
            await SendCommandAsync("Page.navigate", "{\"url\":" + JsonSerializer.Serialize(url) + "}", cancellationToken);
            TargetUrl = url;
        }

        public async Task<string> CaptureScreenshotAsync(CancellationToken cancellationToken)
        {
            using var document = await SendCommandAsync("Page.captureScreenshot", "{\"format\":\"png\",\"fromSurface\":true}", cancellationToken);
            return document.RootElement.GetProperty("result").GetProperty("data").GetString() ?? string.Empty;
        }

        public async Task<string> EvaluateStringAsync(string expression, CancellationToken cancellationToken)
        {
            var result = await EvaluateAsync(expression, cancellationToken);
            return result;
        }

        public async Task<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            var parameters = "{\"expression\":" + JsonSerializer.Serialize(expression) + ",\"returnByValue\":true,\"awaitPromise\":true}";
            using var document = await SendCommandAsync("Runtime.evaluate", parameters, cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("result", out var commandResult) &&
                commandResult.TryGetProperty("exceptionDetails", out var exception))
            {
                return JsonSerializer.Serialize(exception);
            }

            if (!root.TryGetProperty("result", out commandResult) ||
                !commandResult.TryGetProperty("result", out var evaluation))
            {
                return string.Empty;
            }

            if (evaluation.TryGetProperty("value", out var value))
            {
                return value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : value.GetRawText();
            }

            if (evaluation.TryGetProperty("description", out var description))
            {
                return description.GetString() ?? string.Empty;
            }

            return evaluation.GetRawText();
        }

        public async Task FlushEventsAsync(CancellationToken cancellationToken)
        {
            await EvaluateAsync("undefined", cancellationToken);
        }

        public IReadOnlyCollection<string> GetConsoleEntries() => consoleEntries.ToArray();

        public IReadOnlyCollection<string> GetNetworkEntries() => networkEntries.ToArray();

        public void Dispose()
        {
            commandLock.Dispose();
            socket.Dispose();
        }

        private async Task<JsonDocument> SendCommandAsync(string method, string? parametersJson, CancellationToken cancellationToken)
        {
            await commandLock.WaitAsync(cancellationToken);
            try
            {
                var id = Interlocked.Increment(ref nextId);
                var payload = parametersJson is null
                    ? "{\"id\":" + id + ",\"method\":" + JsonSerializer.Serialize(method) + "}"
                    : "{\"id\":" + id + ",\"method\":" + JsonSerializer.Serialize(method) + ",\"params\":" + parametersJson + "}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

                while (true)
                {
                    var document = await ReceiveDocumentAsync(cancellationToken);
                    var root = document.RootElement;
                    if (root.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                    {
                        return document;
                    }

                    ProcessEvent(root);
                    document.Dispose();
                }
            }
            finally
            {
                commandLock.Release();
            }
        }

        private async Task<JsonDocument> ReceiveDocumentAsync(CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            var buffer = new byte[8192];
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("The browser debug protocol connection closed.");
                }

                await stream.WriteAsync(buffer, 0, result.Count, cancellationToken);
            }
            while (!result.EndOfMessage);

            return JsonDocument.Parse(stream.ToArray());
        }

        private void ProcessEvent(JsonElement root)
        {
            if (!root.TryGetProperty("method", out var methodProperty))
            {
                return;
            }

            var method = methodProperty.GetString() ?? string.Empty;
            if (method.Equals("Runtime.consoleAPICalled", StringComparison.OrdinalIgnoreCase))
            {
                consoleEntries.Add(FormatConsoleEvent(root));
            }
            else if (method.Equals("Runtime.exceptionThrown", StringComparison.OrdinalIgnoreCase))
            {
                consoleEntries.Add(FormatExceptionEvent(root));
            }
            else if (method.Equals("Network.requestWillBeSent", StringComparison.OrdinalIgnoreCase) ||
                     method.Equals("Network.responseReceived", StringComparison.OrdinalIgnoreCase))
            {
                networkEntries.Add(FormatNetworkEvent(root));
            }
        }

        private static async Task<CdpTarget> ResolveTargetAsync(Uri endpoint, string? requestedUrl, CancellationToken cancellationToken)
        {
            using var client = new WebClient { Encoding = Encoding.UTF8 };
            using var registration = cancellationToken.Register(client.CancelAsync);
            var listUri = new Uri(endpoint, "/json/list");
            var text = await client.DownloadStringTaskAsync(listUri);
            using var document = JsonDocument.Parse(text);
            var targets = document.RootElement.EnumerateArray()
                .Select(element => new CdpTarget(
                    GetJsonString(element, "url"),
                    GetJsonString(element, "title"),
                    GetJsonString(element, "type"),
                    new Uri(GetJsonString(element, "webSocketDebuggerUrl") ?? throw new InvalidOperationException("CDP target is missing webSocketDebuggerUrl."))))
                .ToArray();

            var selected = SelectTarget(targets, requestedUrl);
            if (selected is null)
            {
                throw new InvalidOperationException("No page target was available from the browser debug protocol endpoint.");
            }

            return selected;
        }

        private static CdpTarget? SelectTarget(IReadOnlyCollection<CdpTarget> targets, string? requestedUrl)
        {
            if (!string.IsNullOrWhiteSpace(requestedUrl))
            {
                var expected = requestedUrl!;
                var matching = targets.FirstOrDefault(target =>
                    ContainsOrdinalIgnoreCase(target.Url, expected) ||
                    ContainsOrdinalIgnoreCase(target.Title, expected));
                if (matching is not null)
                {
                    return matching;
                }
            }

            return targets.FirstOrDefault(target => string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
                ?? targets.FirstOrDefault();
        }

        private static string FormatConsoleEvent(JsonElement root)
        {
            var parameters = root.GetProperty("params");
            var kind = GetJsonString(parameters, "type") ?? "log";
            var args = parameters.TryGetProperty("args", out var argsElement)
                ? argsElement.EnumerateArray().Select(FormatRemoteObject)
                : Enumerable.Empty<string>();
            return $"{kind}: {string.Join(" ", args)}";
        }

        private static string FormatExceptionEvent(JsonElement root)
        {
            var parameters = root.GetProperty("params");
            if (parameters.TryGetProperty("exceptionDetails", out var details))
            {
                return "exception: " + (GetJsonString(details, "text") ?? details.GetRawText());
            }

            return "exception";
        }

        private static string FormatNetworkEvent(JsonElement root)
        {
            var method = GetJsonString(root, "method") ?? "Network";
            var parameters = root.GetProperty("params");
            if (parameters.TryGetProperty("request", out var request))
            {
                return $"{method}: {GetJsonString(request, "method")} {GetJsonString(request, "url")}";
            }

            if (parameters.TryGetProperty("response", out var response))
            {
                return $"{method}: {GetJsonString(response, "status")} {GetJsonString(response, "url")}";
            }

            return method;
        }

        private static string FormatRemoteObject(JsonElement element)
        {
            if (element.TryGetProperty("value", out var value))
            {
                return value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : value.GetRawText();
            }

            return GetJsonString(element, "description") ?? element.GetRawText();
        }

        private static bool ContainsOrdinalIgnoreCase(string? value, string expected) =>
            value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string? GetJsonString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property) ? property.ToString() : null;
    }

    private sealed class CdpTarget
    {
        public CdpTarget(string? url, string? title, string? type, Uri webSocketUri)
        {
            Url = url;
            Title = title;
            Type = type;
            WebSocketUri = webSocketUri;
        }

        public string? Url { get; }
        public string? Title { get; }
        public string? Type { get; }
        public Uri WebSocketUri { get; }
    }

    private readonly struct TargetWindow
    {
        public TargetWindow(long windowHandle, int processId, string title, Rectangle bounds)
        {
            WindowHandle = windowHandle;
            ProcessId = processId;
            Title = title;
            Bounds = bounds;
        }

        public long WindowHandle { get; }
        public int ProcessId { get; }
        public string Title { get; }
        public Rectangle Bounds { get; }
    }

    private readonly struct ElementRef
    {
        public ElementRef(long windowHandle)
        {
            WindowHandle = windowHandle;
        }

        public long WindowHandle { get; }
    }

    private readonly struct ElementMatch
    {
        public ElementMatch(string id, AutomationElement? element)
        {
            Id = id;
            Element = element;
        }

        public string? Id { get; }
        public AutomationElement? Element { get; }
    }

    private enum MouseClickKind
    {
        Left,
        Right
    }

    [Flags]
    private enum MouseEventFlags : uint
    {
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
