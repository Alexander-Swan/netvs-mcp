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

        lock (_gate)
        {
            _sessions[session.SessionId] = session;
        }

        OnSessionsChanged();
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
            "Multiple Visual Studio sessions are available. Specify sessionId, solutionPath, or solutionName.",
            sessions);
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

    private void OnSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);
}
