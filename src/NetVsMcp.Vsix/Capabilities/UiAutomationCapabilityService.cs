using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using DiagnosticsProcess = System.Diagnostics.Process;
using VsProcess = EnvDTE.Process;

namespace NetVsMcp.Vsix;

/// <summary>
/// Desktop UI Automation (UIA) backend: window discovery/capture, element tree traversal and
/// find, and simulated mouse/keyboard input. Extracted from the former monolithic
/// AutomationCapabilityService (see ARCH-7 in docs/IMPROVEMENT_PLAN.md).
/// </summary>
internal sealed class UiAutomationCapabilityService
{
    private const int MaxTreeDepth = 4;
    private const int MaxTreeNodes = 200;

    private readonly AsyncPackage package;
    private readonly object elementsLock = new();
    private readonly Dictionary<string, ElementRef> elements = new(StringComparer.OrdinalIgnoreCase);
    private int nextElementId;

    public UiAutomationCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is null)
        {
            return AutomationSupport.Failure(request, "No target window was found to capture.", ("backend", "screen-capture"));
        }

        return CaptureRectangle(request, target.Value.Bounds, ("windowHandle", target.Value.WindowHandle.ToString()));
    }

    public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Width.GetValueOrDefault() <= 0 || request.Height.GetValueOrDefault() <= 0)
        {
            return Task.FromResult(AutomationSupport.Failure(request, "Capture width and height must be greater than zero.", ("backend", "screen-capture")));
        }

        return Task.FromResult(CaptureRectangle(
            request,
            new Rectangle(request.X.GetValueOrDefault(), request.Y.GetValueOrDefault(), request.Width!.Value, request.Height!.Value)));
    }

    public async Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        var text = SerializeWindows(windows);
        return AutomationSupport.Success(request, text, ("backend", "uia"), ("windowCount", windows.Count.ToString()));
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

        return AutomationSupport.Success(request, builder.ToString(), ("backend", "uia"), ("nodeCount", count.ToString()));
    }

    public async Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var matches = await FindElementsAsync(request, firstOnly: false, cancellationToken);
        return AutomationSupport.Success(request, SerializeElements(matches), ("backend", "uia"), ("matchCount", matches.Count.ToString()));
    }

    public async Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = (await FindElementsAsync(request, firstOnly: true, cancellationToken)).FirstOrDefault();
        return match.Element is null
            ? AutomationSupport.Failure(request, "No matching UI element was found.", ("backend", "uia"))
            : AutomationSupport.Success(request, SerializeElement(match), ("backend", "uia"), ("elementId", ElementId(match)));
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
            return AutomationSupport.Failure(request, "No matching UI element was found for drag.", ("backend", "uia-input"));
        }

        var from = Center(match.Element.Current.BoundingRectangle);
        var toX = request.X ?? (int)from.X;
        var toY = request.Y ?? (int)from.Y;
        MoveMouse((int)from.X, (int)from.Y);
        MouseEvent(MouseEventFlags.LeftDown);
        MoveMouse(toX, toY);
        MouseEvent(MouseEventFlags.LeftUp);
        return AutomationSupport.Success(request, null, ("backend", "uia-input"), ("elementId", ElementId(match)));
    }

    public async Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return AutomationSupport.Failure(request, "No matching UI element was found for value mutation.", ("backend", "uia"));
        }

        if (match.Element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern)
        {
            valuePattern.SetValue(request.Text ?? string.Empty);
            return AutomationSupport.Success(request, null, ("backend", "uia-value-pattern"), ("elementId", ElementId(match)));
        }

        if (TrySetForegroundWindow(WindowFromElement(match.Element)))
        {
            SendKeys.SendWait("^a");
            SendKeys.SendWait(request.Text ?? string.Empty);
            return AutomationSupport.Success(request, null, ("backend", "sendkeys-fallback"), ("elementId", ElementId(match)));
        }

        return AutomationSupport.Failure(request, "Element does not expose ValuePattern and could not be activated.", ("backend", "uia"));
    }

    public async Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return AutomationSupport.Failure(request, "No matching UI element was found for invoke.", ("backend", "uia"));
        }

        if (match.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObject) &&
            invokePatternObject is InvokePattern invokePattern)
        {
            invokePattern.Invoke();
            return AutomationSupport.Success(request, null, ("backend", "uia-invoke-pattern"), ("elementId", ElementId(match)));
        }

        return await ClickElementAsync(request, MouseClickKind.Left, 1, cancellationToken);
    }

    public async Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetWindowAsync(request, cancellationToken);
        if (target is not null && !TrySetForegroundWindow(target.Value.WindowHandle))
        {
            return AutomationSupport.Failure(request, "Unable to activate target window for key input.", ("backend", "sendkeys"));
        }

        SendKeys.SendWait(request.Text ?? string.Empty);
        return AutomationSupport.Success(request, null, ("backend", "sendkeys"));
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
                return AutomationSupport.Success(request, SerializeElement(match), ("backend", "uia"), ("elementId", ElementId(match)));
            }

            await Task.Delay(100, cancellationToken);
        }

        return AutomationSupport.Failure(request, "Timed out waiting for UI element.", ("backend", "uia"));
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

        return AutomationSupport.Success(request, null, ("backend", "process-input-idle"), ("windowCount", windows.Count.ToString()));
    }

    /// <summary>
    /// Resolves a single best-match target window. Also used by the console-automation and
    /// web-debug services as a shared window-discovery primitive.
    /// </summary>
    internal async Task<TargetWindow?> ResolveTargetWindowAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ResolveTargetWindowsAsync(request, cancellationToken);
        return windows.Count == 0 ? null : windows.First();
    }

    /// <summary>
    /// Resolves the candidate windows for a request (debugged-process windows plus any
    /// title/process-name matches). Also used by the console-automation service.
    /// </summary>
    internal async Task<IReadOnlyCollection<TargetWindow>> ResolveTargetWindowsAsync(AutomationRequest request, CancellationToken cancellationToken)
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

    /// <summary>Activates the given window. Also used by the console-automation service.</summary>
    internal static bool TrySetForegroundWindow(long hwnd) =>
        hwnd != 0 && SetForegroundWindow(new IntPtr(hwnd));

    private async Task<DTE> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE
            ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
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
            roots.AddRange(desktop.FindAll(TreeScope.Children, SelectorConditions.BuildTextCondition(request.Target ?? string.Empty)).Cast<AutomationElement>());
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
        bool hasCached;
        ElementRef cached;
        lock (elementsLock)
        {
            hasCached = elements.TryGetValue(selector.Trim(), out cached);
        }

        if (hasCached)
        {
            var cachedElement = AutomationElement.FromHandle(new IntPtr(cached.WindowHandle));
            if (cachedElement is not null)
            {
                return [new ElementMatch(selector.Trim(), cachedElement)];
            }
        }

        var roots = await ResolveAutomationRootsAsync(request, cancellationToken);
        var condition = SelectorConditions.BuildSelectorCondition(selector);
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
        lock (elementsLock)
        {
            elements[id] = new ElementRef(hwnd);
        }

        return new ElementMatch(id, element);
    }

    private async Task<AutomationResult> ClickElementAsync(AutomationRequest request, MouseClickKind clickKind, int count, CancellationToken cancellationToken)
    {
        var match = await ResolveElementAsync(request, cancellationToken);
        if (match.Element is null)
        {
            return AutomationSupport.Failure(request, "No matching UI element was found for click.", ("backend", "uia-input"));
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

        return AutomationSupport.Success(request, null, ("backend", "uia-input"), ("elementId", ElementId(match)));
    }

    private AutomationResult CaptureRectangle(AutomationRequest request, Rectangle rectangle, params (string Key, string Value)[] extraMetadata)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return AutomationSupport.Failure(request, "Capture rectangle is empty.", ("backend", "screen-capture"));
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
        return AutomationSupport.Success(request, Convert.ToBase64String(stream.ToArray()), metadata.ToArray());
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

    /// <summary>Formats windows for status/snapshot output. Also used by the console-automation service.</summary>
    internal static string SerializeWindows(IReadOnlyCollection<TargetWindow> windows) =>
        string.Join(Environment.NewLine, windows.Select(window =>
            $"pid={window.ProcessId}; hwnd={window.WindowHandle}; title=\"{window.Title}\"; bounds={window.Bounds.X},{window.Bounds.Y},{window.Bounds.Width},{window.Bounds.Height}"));

    private static string SerializeElements(IReadOnlyCollection<ElementMatch> matches) =>
        string.Join(Environment.NewLine, matches.Select(SerializeElement));

    private static string SerializeElement(ElementMatch match) =>
        $"id={match.Id}; {FormatElement(match.Element!)}";

    private static string ElementId(ElementMatch match) =>
        match.Id ?? throw new InvalidOperationException("Matched UI element did not have a registered element ID.");

    private static string FormatElement(AutomationElement element)
    {
        var current = element.Current;
        var bounds = current.BoundingRectangle;
        return $"type={current.ControlType.ProgrammaticName}; name=\"{current.Name}\"; automationId=\"{current.AutomationId}\"; class=\"{current.ClassName}\"; bounds={bounds.X:0},{bounds.Y:0},{bounds.Width:0},{bounds.Height:0}";
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

    internal readonly struct TargetWindow
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
