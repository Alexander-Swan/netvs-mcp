using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using VsProcess = EnvDTE.Process;

namespace NetVsMcp.Vsix;

/// <summary>
/// Win32-console P/Invoke automation (attach/read/write to a debuggee's console buffer) plus
/// Visual Studio output-window reading. Extracted from the former monolithic
/// AutomationCapabilityService (see ARCH-7 in docs/IMPROVEMENT_PLAN.md). Falls back to
/// SendKeys-based window automation via <see cref="UiAutomationCapabilityService"/> when a
/// process has no attachable console.
/// </summary>
internal sealed class ConsoleAutomationCapabilityService
{
    private readonly AsyncPackage package;
    private readonly UiAutomationCapabilityService ui;
    private readonly object consoleLock = new();

    public ConsoleAutomationCapabilityService(AsyncPackage package, UiAutomationCapabilityService ui)
    {
        this.package = package;
        this.ui = ui;
    }

    public async Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var processId = ResolveConsoleProcessId(dte, request);
        string? consoleMessage = null;
        if (processId is not null && TryReadConsoleBuffer(processId.Value, out var consoleText, out consoleMessage))
        {
            return AutomationSupport.Success(request, consoleText, ("backend", "windows-console"), ("source", "debuggee-console"), ("processId", processId.Value.ToString()));
        }

        var text = ReadOutputPane(dte, request.Target, ["Debug", "Tests", "Build"]);
        return AutomationSupport.Success(request, text, ("backend", "visual-studio-output"), ("source", "output-window"), ("consoleMessage", consoleMessage ?? string.Empty));
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
        return AutomationSupport.Success(request, string.Join(Environment.NewLine, lines), ("backend", "visual-studio-output"), ("matches", lines.Length.ToString()));
    }

    public async Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var processId = ResolveConsoleProcessId(dte, request);
        string? consoleMessage = null;
        if (processId is not null && TryWriteConsoleInput(processId.Value, request.Text ?? string.Empty, out consoleMessage))
        {
            return AutomationSupport.Success(request, null, ("backend", "windows-console"), ("processId", processId.Value.ToString()));
        }

        var target = await ui.ResolveTargetWindowAsync(request, cancellationToken);
        if (target is null)
        {
            return AutomationSupport.Failure(request, "No target window was found for console input.", ("backend", "sendkeys"), ("consoleMessage", consoleMessage ?? string.Empty));
        }

        if (!UiAutomationCapabilityService.TrySetForegroundWindow(target.Value.WindowHandle))
        {
            return AutomationSupport.Failure(request, "Unable to activate target window for console input.", ("backend", "sendkeys"));
        }

        SendKeys.SendWait(request.Text ?? string.Empty);
        return AutomationSupport.Success(request, null, ("backend", "sendkeys"), ("windowHandle", target.Value.WindowHandle.ToString()), ("consoleMessage", consoleMessage ?? string.Empty));
    }

    public async Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var windows = await ui.ResolveTargetWindowsAsync(request, cancellationToken);
        var text = UiAutomationCapabilityService.SerializeWindows(windows);
        return AutomationSupport.Success(request, text, ("backend", "process-window-enumeration"), ("windowCount", windows.Count.ToString()));
    }

    private async Task<DTE> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE
            ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
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
                return AutomationSupport.Truncate(ReadPaneText(pane));
            }
        }

        return first is null ? string.Empty : AutomationSupport.Truncate(ReadPaneText(first));
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
                var length = (uint)Math.Min(width * (info.CursorPosition.Y - startY + 1), AutomationSupport.MaxTextChars);
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

        return AutomationSupport.Truncate(string.Join(Environment.NewLine, lines).TrimEnd());
    }

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
                EventType = ConsoleAutomationCapabilityService.KeyEvent,
                KeyEvent = new ConsoleKeyEventRecord
                {
                    KeyDown = keyDown,
                    RepeatCount = 1,
                    UnicodeChar = character
                }
            };
    }
}
