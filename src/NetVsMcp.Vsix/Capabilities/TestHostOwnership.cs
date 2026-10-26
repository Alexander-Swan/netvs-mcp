using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace NetVsMcp.Vsix;

internal sealed class TestProcessSnapshot(int id, int parentId, DateTime startedUtc, string name, string? commandLine)
{
    internal int Id { get; } = id;
    internal int ParentId { get; } = parentId;
    internal DateTime StartedUtc { get; } = startedUtc;
    internal string Name { get; } = name;
    internal string? CommandLine { get; } = commandLine;
}

/// <summary>Fail-closed attribution to the exact runner; elapsed time alone never establishes ownership.</summary>
internal static class TestHostOwnership
{
    internal static IReadOnlyList<TestProcessSnapshot> Capture()
    {
        var result = new List<TestProcessSnapshot>();
        using var searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, CreationDate, Name, CommandLine FROM Win32_Process");
        using var rows = searcher.Get();
        foreach (ManagementObject row in rows)
        {
            using (row)
            {
                if (row["CreationDate"] is not string created) continue;
                result.Add(new(Convert.ToInt32(row["ProcessId"]), Convert.ToInt32(row["ParentProcessId"]),
                    ManagementDateTimeConverter.ToDateTime(created).ToUniversalTime(), (string)row["Name"], row["CommandLine"] as string));
            }
        }
        return result;
    }

    internal static IReadOnlyList<TestProcessSnapshot> FindOwnedHosts(IReadOnlyList<TestProcessSnapshot> snapshot, int runnerId, DateTime runnerStartedUtc)
    {
        var processes = snapshot.ToDictionary(p => p.Id);
        // WMI CreationDate has microsecond precision; Process.StartTime can contain finer ticks.
        if (!processes.TryGetValue(runnerId, out var root) || !SameStart(root.StartedUtc, runnerStartedUtc))
            return Array.Empty<TestProcessSnapshot>();
        return snapshot.Where(p => IsTestHost(p) && IsDescendant(p, root, processes)).ToArray();
    }

    internal static bool SameStart(DateTime first, DateTime second) => Math.Abs((first - second).Ticks) < 10;

    private static bool IsDescendant(TestProcessSnapshot process, TestProcessSnapshot root, IReadOnlyDictionary<int, TestProcessSnapshot> processes)
    {
        var seen = new HashSet<int>();
        while (seen.Add(process.Id) && process.Id != root.Id)
        {
            if (!processes.TryGetValue(process.ParentId, out var parent) || parent.StartedUtc > process.StartedUtc) return false;
            if (parent.Id == root.Id) return true;
            process = parent;
        }
        return false;
    }

    internal static bool IsTestHost(TestProcessSnapshot process)
    {
        if (new[] { "testhost.exe", "testhost.x86.exe", "testhost.arm64.exe" }.Contains(process.Name, StringComparer.OrdinalIgnoreCase)) return true;
        if (!string.Equals(process.Name, "dotnet.exe", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(process.CommandLine)) return false;
        var argv = SplitCommandLine(process.CommandLine!);
        return HasTestHostEntryPoint(argv);
    }

    internal static bool HasTestHostEntryPoint(IReadOnlyList<string> argv)
    {
        var index = 1; // executable, optional 'exec', known host options, then the managed entry point
        if (index < argv.Count && string.Equals(argv[index], "exec", StringComparison.OrdinalIgnoreCase)) index++;
        var valueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "--runtimeconfig", "--depsfile", "--additionalprobingpath", "--additional-deps", "--fx-version", "--roll-forward", "--roll-forward-on-no-candidate-fx" };
        while (index < argv.Count && argv[index].StartsWith("-", StringComparison.Ordinal))
        {
            if (!valueOptions.Contains(argv[index]) || index + 1 >= argv.Count) return false;
            index += 2;
        }
        return index < argv.Count && string.Equals(Path.GetFileName(argv[index]), "testhost.dll", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        var pointer = CommandLineToArgvW(commandLine, out var count);
        if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var result = new string[count];
            for (var i = 0; i < count; i++) result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size)) ?? string.Empty;
            return result;
        }
        finally { LocalFree(pointer); }
    }

    internal static Process OpenVerified(TestProcessSnapshot expected)
    {
        var process = Process.GetProcessById(expected.Id);
        try
        {
            _ = process.Handle; // Keep the exact process handle through final validation and attach/cleanup.
            if (process.HasExited || !SameStart(process.StartTime.ToUniversalTime(), expected.StartedUtc))
                throw new InvalidOperationException("The owned testhost exited or its process identity changed.");
            return process;
        }
        catch { process.Dispose(); throw; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);
}
