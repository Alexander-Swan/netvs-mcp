using System.Diagnostics;
using System.IO;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

/// <summary>
/// Launches a new Visual Studio (devenv.exe) process and waits for it to register with the
/// broker, so `vs_launch_instance` can hand back a usable, routable session.
/// </summary>
public sealed class VisualStudioLauncher
{
    private readonly SessionRegistry _sessions;

    public VisualStudioLauncher(SessionRegistry sessions)
    {
        _sessions = sessions;
    }

    public async Task<VsLaunchInstanceResult> LaunchAsync(
        string? solutionPath,
        bool experimental,
        string? edition,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(solutionPath) && !File.Exists(solutionPath))
        {
            return new VsLaunchInstanceResult(false, $"Solution path '{solutionPath}' does not exist.", null, null);
        }

        string? devenvPath;
        try
        {
            devenvPath = FindDevenvPath(edition);
        }
        catch (Exception ex)
        {
            return new VsLaunchInstanceResult(false, $"Failed to locate a Visual Studio installation: {ex.Message}", null, null);
        }

        if (devenvPath is null)
        {
            return new VsLaunchInstanceResult(
                false,
                "No Visual Studio installation was found (vswhere.exe returned no devenv.exe candidates and no already-running instance could be reused).",
                null,
                null);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = devenvPath,
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            startInfo.ArgumentList.Add(solutionPath);
        }

        if (experimental)
        {
            startInfo.ArgumentList.Add("/rootsuffix");
            startInfo.ArgumentList.Add("Exp");
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Exception ex)
        {
            return new VsLaunchInstanceResult(false, $"Failed to launch Visual Studio: {ex.Message}", null, null);
        }

        var effectiveTimeout = timeoutSeconds <= 0 ? 60 : Math.Min(timeoutSeconds, 300);
        var registered = await WaitForRegistrationAsync(process.Id, TimeSpan.FromSeconds(effectiveTimeout), cancellationToken);
        if (registered is null)
        {
            return new VsLaunchInstanceResult(
                false,
                $"Visual Studio process {process.Id} started but did not register with the broker within {effectiveTimeout}s.",
                process.Id,
                null);
        }

        return new VsLaunchInstanceResult(true, "Visual Studio instance launched and registered.", process.Id, registered);
    }

    private async Task<VsSessionInfo?> WaitForRegistrationAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var match = _sessions.ListSessions().FirstOrDefault(session => session.ProcessId == processId);
            if (match is not null)
            {
                return match;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(500, cancellationToken);
        }
    }

    private static string? FindDevenvPath(string? editionFilter)
    {
        var vswherePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (File.Exists(vswherePath))
        {
            var psi = new ProcessStartInfo
            {
                FileName = vswherePath,
                Arguments = "-products * -property productPath -format value",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var vswhereProcess = Process.Start(psi);
            if (vswhereProcess is not null)
            {
                var output = vswhereProcess.StandardOutput.ReadToEnd();
                vswhereProcess.WaitForExit(10000);

                var candidates = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(path => File.Exists(path) && string.Equals(Path.GetFileName(path), "devenv.exe", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (candidates.Length > 0)
                {
                    if (!string.IsNullOrWhiteSpace(editionFilter))
                    {
                        var filtered = candidates.FirstOrDefault(path => path.Contains(editionFilter, StringComparison.OrdinalIgnoreCase));
                        if (filtered is not null)
                        {
                            return filtered;
                        }
                    }

                    return candidates[0];
                }
            }
        }

        // Fall back to reusing the executable path of an already-running devenv.exe process.
        try
        {
            foreach (var process in Process.GetProcessesByName("devenv"))
            {
                using (process)
                {
                    var path = process.MainModule?.FileName;
                    if (path is not null && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access to MainModule can be denied for elevated devenv processes; ignore and fall through.
        }

        return null;
    }
}
