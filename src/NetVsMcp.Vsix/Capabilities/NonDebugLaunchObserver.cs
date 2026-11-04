using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Process = System.Diagnostics.Process;

namespace NetVsMcp.Vsix;

// Observe only newly created processes with an exact startup executable path.
// Unsupported launch profiles (remote, browser, dotnet-hosted DLLs) remain explicitly unavailable.
internal static class NonDebugLaunchObserver
{
    internal static IReadOnlyList<string> StartupExecutables(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var paths = new List<string>();
        try
        {
            var startup = dte.Solution.SolutionBuild.StartupProjects;
            var names = startup is Array array ? array.Cast<object>().Select(v => v?.ToString()) : new[] { startup?.ToString() };
            foreach (var name in names.Where(n => !string.IsNullOrEmpty(n)))
            {
                try
                {
                    var project = dte.Solution.Projects.Item(name);
                    var configuration = project.ConfigurationManager.ActiveConfiguration;
                    string? Property(string key)
                    {
                        try { return configuration.Properties.Item(key)?.Value?.ToString(); }
                        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException) { return null; }
                    }
                    var program = Property("StartProgram");
                    var action = Property("StartAction");
                    if (action == "1" && !string.IsNullOrEmpty(program)) { paths.Add(Path.GetFullPath(program)); continue; }
                    var output = project.Properties.Item("OutputFileName").Value?.ToString();
                    var directory = configuration.Properties.Item("OutputPath").Value?.ToString();
                    if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(directory)) continue;
                    var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullName)!, directory, output));
                    if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) path = Path.ChangeExtension(path, ".exe");
                    paths.Add(path);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static HashSet<int> ProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcesses()) using (process) ids.Add(process.Id);
        return ids;
    }

    internal static async Task<IReadOnlyList<int>> ObserveAsync(IReadOnlyList<string> targets, HashSet<int> before,
        DateTime started, CancellationToken cancellationToken)
    {
        if (targets.Count == 0) return Array.Empty<int>();
        var found = new HashSet<int>();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() =>
            {
                foreach (var process in Process.GetProcesses()) using (process)
                {
                    try
                    {
                        if (!before.Contains(process.Id) && process.StartTime.ToUniversalTime() >= started &&
                            targets.Contains(process.MainModule?.FileName ?? "", StringComparer.OrdinalIgnoreCase)) found.Add(process.Id);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                }
            }, cancellationToken);
            if (found.Count >= targets.Count) break;
            await Task.Delay(100, cancellationToken);
        }
        return found.OrderBy(id => id).ToArray();
    }
}
