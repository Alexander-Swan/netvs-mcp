using System.Diagnostics;
using Microsoft.Win32;

namespace NetVsMcp.Broker.Services;

public interface IAutostartService
{
    bool IsSupported { get; }
    string StatusText { get; }
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class AutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NetVsMcp.Broker";

    public bool IsSupported => OperatingSystem.IsWindows();

    public string StatusText => IsSupported
        ? "Per-user Windows startup entry"
        : "Autostart is only supported on Windows.";

    public bool IsEnabled()
    {
        if (!IsSupported)
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value
            && string.Equals(value, BuildCommandLine(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
        {
            throw new NotSupportedException("Autostart is only supported on Windows.");
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, BuildCommandLine());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string BuildCommandLine()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Could not resolve broker executable path.");
        }

        return $"\"{processPath}\"";
    }
}
