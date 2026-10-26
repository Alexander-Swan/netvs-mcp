using System.Diagnostics;
using System.IO;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

/// <summary>
/// Launches a new Visual Studio (devenv.exe) process and waits for it to register with the
/// broker, so `vs_launch_instance` can hand back a usable, routable session.
/// </summary>
internal sealed class VisualStudioLauncher
{
    private readonly SessionRegistry _sessions;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Pid, long Started)> owned = new();
    private readonly SemaphoreSlim launches = new(1, 1);
    public IReadOnlyList<VsOwnedInstance> OwnedInstances => owned.Select(p => new VsOwnedInstance(p.Key, p.Value.Pid,
        new DateTimeOffset(p.Value.Started, TimeSpan.Zero))).OrderBy(p => p.StartedUtc).ToArray();

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
        await launches.WaitAsync(cancellationToken);
        try
        {
            if (owned.Count >= 256) return new(false, "Launch receipt capacity reached for this broker lifetime.", null, null);
            return await LaunchCoreAsync(solutionPath, experimental, edition, timeoutSeconds, cancellationToken);
        }
        finally { launches.Release(); }
    }

    private async Task<VsLaunchInstanceResult> LaunchCoreAsync(string? solutionPath, bool experimental, string? edition,
        int timeoutSeconds, CancellationToken cancellationToken)
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
        using var launched = process;
        var ownershipToken = Guid.NewGuid().ToString("N");
        owned[ownershipToken] = (process.Id, process.StartTime.ToUniversalTime().Ticks);
        var registered = await WaitForRegistrationAsync(process.Id, TimeSpan.FromSeconds(effectiveTimeout), cancellationToken);
        if (registered is null)
        {
            return new VsLaunchInstanceResult(
                false,
                $"Visual Studio process {process.Id} started but did not register with the broker within {effectiveTimeout}s.",
                process.Id,
                null, ownershipToken);
        }

        return new VsLaunchInstanceResult(true, "Visual Studio instance launched and registered.", process.Id, registered, ownershipToken);
    }

    public async Task<VsCloseInstanceResult> CloseOwnedAsync(string ownershipToken, int timeoutSeconds, CancellationToken cancellationToken,
        bool unsavedStateInspected = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeoutSeconds is < 1 or > 60) throw new ArgumentException("timeoutSeconds must be 1–60.");
        if (!owned.TryGetValue(ownershipToken, out var identity))
            throw new InvalidOperationException("Unknown ownershipToken. Only instances launched by this broker lifetime may be closed.");
        Process process;
        try { process = Process.GetProcessById(identity.Pid); }
        catch (ArgumentException) { return new(identity.Pid, "exited", true); }
        using (process)
        {
            if (process.StartTime.ToUniversalTime().Ticks != identity.Started)
                throw new InvalidOperationException("Process identity changed; the instance will not be closed.");
            if (process.HasExited) return new(identity.Pid, "exited", true);
            if (!unsavedStateInspected) return new(identity.Pid, "inspectionUnavailable", false,
                "The owned instance is not registered with inspectable document state. No close was requested.");
            // WM_CLOSE preserves Visual Studio's normal save/cancel dialogs. Never terminate or discard edits.
            if (!process.CloseMainWindow()) return new(identity.Pid, "closeUnavailable", false, "No main window accepted close.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { return new(identity.Pid, "closePending", false, "Exit was not observed; inspect save dialogs or canceled close before retrying."); }
            return new(identity.Pid, "exited", true);
        }
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
