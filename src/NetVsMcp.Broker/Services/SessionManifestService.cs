using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface ISessionManifestService
{
    string SessionsDirectory { get; }
    void Sync(IReadOnlyCollection<VsSessionInfo> sessions);
    int CleanupStale(DateTimeOffset staleBeforeUtc);
}

public sealed record SessionManifest(
    string SessionId,
    int ProcessId,
    string? SolutionName,
    string? SolutionPath,
    string? VisualStudioVersion,
    string? Edition,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    DateTimeOffset LastSeenUtc,
    IReadOnlyCollection<VsCapability> Capabilities);

public sealed class SessionManifestService : ISessionManifestService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly object _gate = new();

    public SessionManifestService(string sessionsDirectory)
    {
        SessionsDirectory = string.IsNullOrWhiteSpace(sessionsDirectory)
            ? BrokerOptions.DefaultSessionsDirectory
            : sessionsDirectory;
    }

    public string SessionsDirectory { get; }

    public void Sync(IReadOnlyCollection<VsSessionInfo> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        Directory.CreateDirectory(SessionsDirectory);
        var liveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            foreach (var session in sessions)
            {
                var path = GetManifestPath(session.SessionId);
                liveFiles.Add(path);
                var manifest = new SessionManifest(
                    session.SessionId,
                    session.ProcessId,
                    session.SolutionName,
                    session.SolutionPath,
                    session.VisualStudioVersion,
                    session.Edition,
                    session.ActiveDocument,
                    session.DebuggerMode,
                    session.IsActiveWindow,
                    session.LastSeenUtc,
                    session.Capabilities);

                File.WriteAllText(path, JsonSerializer.Serialize(manifest, SerializerOptions));
            }

            foreach (var path in Directory.EnumerateFiles(SessionsDirectory, "vs-*.json"))
            {
                if (!liveFiles.Contains(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    public int CleanupStale(DateTimeOffset staleBeforeUtc)
    {
        if (!Directory.Exists(SessionsDirectory))
        {
            return 0;
        }

        var removed = 0;
        lock (_gate)
        {
            foreach (var path in Directory.EnumerateFiles(SessionsDirectory, "vs-*.json"))
            {
                if (TryRead(path, out var manifest) && manifest.LastSeenUtc < staleBeforeUtc)
                {
                    File.Delete(path);
                    removed++;
                }
            }
        }

        return removed;
    }

    private string GetManifestPath(string sessionId)
    {
        var safeSessionId = string.Join("_", sessionId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(SessionsDirectory, $"{safeSessionId}.json");
    }

    private static bool TryRead(string path, out SessionManifest manifest)
    {
        try
        {
            manifest = JsonSerializer.Deserialize<SessionManifest>(
                File.ReadAllText(path),
                SerializerOptions)!;
            return manifest is not null;
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        manifest = null!;
        return false;
    }
}
