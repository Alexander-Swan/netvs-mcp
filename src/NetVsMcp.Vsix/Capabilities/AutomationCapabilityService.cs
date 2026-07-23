using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
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
    private int nextElementId;
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
        var text = ReadOutputPane(dte, request.Target, ["Debug", "Tests", "Build"]);
        return Success(request, text, ("backend", "visual-studio-output"), ("source", "output-window"));
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
        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is null)
        {
            return Failure(request, "No target window was found for console input.", ("backend", "sendkeys"));
        }

        if (!TrySetForegroundWindow(target.Value.WindowHandle))
        {
            return Failure(request, "Unable to activate target window for console input.", ("backend", "sendkeys"));
        }

        SendKeys.SendWait(request.Text ?? string.Empty);
        return Success(request, null, ("backend", "sendkeys"), ("windowHandle", target.Value.WindowHandle.ToString()));
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

    public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connectedWebTarget = request.Target;
        connectedWebUrl = request.Url ?? connectedWebUrl;
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            DiagnosticsProcess.Start(request.Url);
        }

        return Task.FromResult(Success(request, null, ("backend", "browser-shell-uia"), ("url", connectedWebUrl ?? string.Empty)));
    }

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connectedWebTarget = null;
        connectedWebUrl = null;
        return Task.FromResult(Success(request, null, ("backend", "browser-shell-uia")));
    }

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = $"connected={connectedWebTarget is not null || connectedWebUrl is not null}; target={connectedWebTarget}; url={connectedWebUrl}";
        return Task.FromResult(Success(request, text, ("backend", "browser-shell-uia")));
    }

    public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return Task.FromResult(Failure(request, "URL is required for browser navigation.", ("backend", "browser-shell")));
        }

        connectedWebUrl = request.Url;
        DiagnosticsProcess.Start(request.Url);
        return Task.FromResult(Success(request, null, ("backend", "browser-shell"), ("url", request.Url ?? string.Empty)));
    }

    public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        UiCaptureWindowAsync(WithWebTarget(request), cancellationToken);

    public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = request.Url ?? connectedWebUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult(Failure(request, "A URL is required before DOM fetch.", ("backend", "http-fetch")));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        return Task.FromResult(Success(request, Truncate(html), ("backend", "http-fetch"), ("url", resolvedUrl)));
    }

    public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = request.Url ?? connectedWebUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult(Failure(request, "A URL is required before DOM query.", ("backend", "http-fetch")));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        var matches = QueryHtml(html, request.Selector).ToArray();
        return Task.FromResult(Success(request, string.Join(Environment.NewLine, matches), ("backend", "http-fetch"), ("matchCount", matches.Length.ToString())));
    }

    public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Browser console capture requires CDP; no console entries are available from the shell backend.")));
    }

    public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Failure(request, "JavaScript execution requires a connected browser debug protocol backend; the current shell/UIA backend cannot execute scripts.", ("backend", "browser-shell-uia")));
    }

    public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Network capture requires CDP; no network events are available from the shell backend.")));
    }

    public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        UiClickAsync(WithWebTarget(request), cancellationToken);

    public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        UiSetValueAsync(WithWebTarget(request), cancellationToken);

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
            var pane = panes.Item(index);
            first ??= pane;
            var currentPaneName = pane.Name;
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
        if (pane.TextDocument is not TextDocument textDocument)
        {
            return string.Empty;
        }

        var editPoint = textDocument.StartPoint.CreateEditPoint();
        return editPoint.GetText(textDocument.EndPoint);
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

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

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

    private sealed class ElementMatch
    {
        public ElementMatch(string id, AutomationElement? element)
        {
            Id = id;
            Element = element;
        }

        public string Id { get; }
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
