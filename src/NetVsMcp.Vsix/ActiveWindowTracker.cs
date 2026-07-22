using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetVsMcp.Vsix;

internal static class ActiveWindowTracker
{
    public static bool IsCurrentProcessForegroundWindow()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        return processId == Process.GetCurrentProcess().Id;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);
}
