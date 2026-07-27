using System.IO;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class SessionRegistry
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();
    private readonly Dictionary<string, VsSessionInfo> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public SessionRegistry(Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public event EventHandler? SessionsChanged;
    public event EventHandler<SessionConnectedEventArgs>? SessionConnected;

    public IReadOnlyCollection<VsSessionInfo> ListSessions()
    {
        lock (_gate)
        {
            return _sessions.Values
                .OrderByDescending(session => session.IsActiveWindow)
                .ThenBy(session => session.SolutionName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyCollection<VsSessionStatus> ListSessionStatuses(DateTimeOffset? now = null)
    {
        var snapshotTime = now ?? _utcNow();

        lock (_gate)
        {
            return _sessions.Values
                .Select(session =>
                {
                    var age = snapshotTime - session.LastSeenUtc;
                    var health = age > StaleAfter ? SessionHealth.Stale : SessionHealth.Connected;
                    return new VsSessionStatus(session, health, age);
                })
                .OrderByDescending(status => status.Session.IsActiveWindow)
                .ThenBy(status => status.Session.SolutionName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public ToolResponse Register(VsSessionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var session = new VsSessionInfo(
            registration.SessionId,
            registration.ProcessId,
            registration.VisualStudioVersion,
            registration.Edition,
            registration.SolutionName,
            SolutionPathNormalizer.Normalize(registration.SolutionPath),
            registration.ActiveDocument,
            registration.DebuggerMode,
            registration.IsActiveWindow,
            _utcNow(),
            registration.Capabilities);

        var isNewSession = false;
        lock (_gate)
        {
            isNewSession = !_sessions.ContainsKey(session.SessionId);
            _sessions[session.SessionId] = session;
        }

        OnSessionsChanged();
        if (isNewSession)
        {
            OnSessionConnected(session);
        }

        return ToolResponse.Ok($"Registered Visual Studio session '{session.SessionId}'.");
    }

    public ToolResponse Update(VsSessionUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_gate)
        {
            if (!_sessions.TryGetValue(update.SessionId, out var existing))
            {
                return ToolResponse.Fail($"Visual Studio session '{update.SessionId}' is not registered.");
            }

            _sessions[update.SessionId] = existing with
            {
                SolutionName = update.SolutionName,
                SolutionPath = SolutionPathNormalizer.Normalize(update.SolutionPath),
                ActiveDocument = update.ActiveDocument,
                DebuggerMode = update.DebuggerMode,
                IsActiveWindow = update.IsActiveWindow,
                LastSeenUtc = _utcNow(),
                Capabilities = update.Capabilities ?? existing.Capabilities
            };
        }

        OnSessionsChanged();
        return ToolResponse.Ok($"Updated Visual Studio session '{update.SessionId}'.");
    }

    public ToolResponse Heartbeat(string sessionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var existing))
            {
                return ToolResponse.Fail($"Visual Studio session '{sessionId}' is not registered.");
            }

            _sessions[sessionId] = existing with { LastSeenUtc = _utcNow() };
        }

        OnSessionsChanged();
        return ToolResponse.Ok();
    }

    public ToolResponse Unregister(string sessionId)
    {
        var removed = false;

        lock (_gate)
        {
            removed = _sessions.Remove(sessionId);
        }

        if (removed)
        {
            OnSessionsChanged();
            return ToolResponse.Ok($"Unregistered Visual Studio session '{sessionId}'.");
        }

        return ToolResponse.Fail($"Visual Studio session '{sessionId}' is not registered.");
    }

    public RouteResult Resolve(RoutingTarget? target)
    {
        var sessions = ListSessions();

        if (sessions.Count == 0)
        {
            return RouteResult.Failed(
                RouteFailureReason.NoRegisteredSessions,
                "No Visual Studio sessions are currently registered.");
        }

        if (!string.IsNullOrWhiteSpace(target?.SessionId))
        {
            var bySession = sessions.SingleOrDefault(
                session => string.Equals(session.SessionId, target.SessionId, StringComparison.OrdinalIgnoreCase));

            return bySession is null
                ? RouteResult.Failed(RouteFailureReason.SessionNotFound, $"No session matched '{target.SessionId}'.", sessions)
                : RouteResult.Found(bySession);
        }

        if (target?.ProcessId is > 0)
        {
            var matches = sessions
                .Where(session => session.ProcessId == target.ProcessId)
                .ToArray();

            return ResolveMatches(
                matches,
                RouteFailureReason.ProcessIdNotFound,
                $"No session has process id '{target.ProcessId}'.",
                sessions);
        }

        if (!string.IsNullOrWhiteSpace(target?.SolutionPath))
        {
            var normalizedTargetPath = SolutionPathNormalizer.Normalize(target.SolutionPath);
            var matches = sessions
                .Where(session => string.Equals(session.SolutionPath, normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return ResolveMatches(
                matches,
                RouteFailureReason.SolutionPathNotFound,
                $"No session has solution path '{normalizedTargetPath}'.",
                sessions);
        }

        var workspacePath = target?.WorkspacePath ?? target?.RootPath;
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            var solutionPath = FindNearestSolutionPath(workspacePath);
            if (solutionPath is null)
            {
                return RouteResult.Failed(
                    RouteFailureReason.WorkspacePathNotFound,
                    $"No .sln or .slnx file was found at or above workspace path '{workspacePath}'.",
                    sessions);
            }

            var normalizedSolutionPath = SolutionPathNormalizer.Normalize(solutionPath);
            var matches = sessions
                .Where(session => string.Equals(session.SolutionPath, normalizedSolutionPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return ResolveMatches(
                matches,
                RouteFailureReason.WorkspacePathNotFound,
                $"No session matched workspace path '{workspacePath}'.",
                sessions);
        }

        if (!string.IsNullOrWhiteSpace(target?.SolutionName))
        {
            var matches = sessions
                .Where(session => string.Equals(session.SolutionName, target.SolutionName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return ResolveMatches(
                matches,
                RouteFailureReason.SolutionNameNotFound,
                $"No session has solution name '{target.SolutionName}'.",
                sessions);
        }

        var activeSessions = sessions.Where(session => session.IsActiveWindow).ToArray();
        if (activeSessions.Length == 1)
        {
            return RouteResult.Found(activeSessions[0]);
        }

        if (sessions.Count == 1)
        {
            return RouteResult.Found(sessions.Single());
        }

        return RouteResult.Failed(
            RouteFailureReason.Ambiguous,
            "Multiple Visual Studio sessions are available. Specify sessionId, processId, solutionPath, workspacePath, or solutionName.",
            sessions);
    }

    public int RemoveStaleSessions(DateTimeOffset? now = null)
    {
        var snapshotTime = now ?? _utcNow();
        var removed = 0;

        lock (_gate)
        {
            foreach (var sessionId in _sessions
                .Where(pair => snapshotTime - pair.Value.LastSeenUtc > StaleAfter)
                .Select(pair => pair.Key)
                .ToArray())
            {
                if (_sessions.Remove(sessionId))
                {
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            OnSessionsChanged();
        }

        return removed;
    }

    private static RouteResult ResolveMatches(
        IReadOnlyCollection<VsSessionInfo> matches,
        RouteFailureReason notFoundReason,
        string notFoundMessage,
        IReadOnlyCollection<VsSessionInfo> allSessions)
    {
        return matches.Count switch
        {
            0 => RouteResult.Failed(notFoundReason, notFoundMessage, allSessions),
            1 => RouteResult.Found(matches.Single()),
            _ => RouteResult.Failed(
                RouteFailureReason.Ambiguous,
                "Multiple Visual Studio sessions matched the target. Specify sessionId.",
                matches)
        };
    }

    private static string? FindNearestSolutionPath(string workspacePath)
    {
        var normalizedPath = SolutionPathNormalizer.Normalize(workspacePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        if (File.Exists(normalizedPath) &&
            (normalizedPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
             normalizedPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            return normalizedPath;
        }

        var directory = File.Exists(normalizedPath)
            ? Path.GetDirectoryName(normalizedPath)
            : normalizedPath;

        while (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            var solutions = Directory
                .EnumerateFiles(directory, "*.sln*")
                .Where(path => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (solutions.Length == 1)
            {
                return solutions[0];
            }

            if (solutions.Length > 1)
            {
                return solutions.FirstOrDefault(path => Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
                    ?? solutions[0];
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private void OnSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);

    private void OnSessionConnected(VsSessionInfo session) => SessionConnected?.Invoke(this, new SessionConnectedEventArgs(session));
}

public sealed class SessionConnectedEventArgs : EventArgs
{
    public SessionConnectedEventArgs(VsSessionInfo session)
    {
        Session = session;
    }

    public VsSessionInfo Session { get; }
}
