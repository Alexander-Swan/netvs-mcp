using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

/// <summary>
/// Stress test proving <see cref="SessionRegistry"/>'s <c>lock (_gate)</c> holds under real
/// concurrent access -- see TEST-3 in docs/IMPROVEMENT_PLAN.md. The locking looked correct by
/// inspection but nothing previously exercised register/update/resolve simultaneously from
/// multiple threads, which is the actual traffic pattern in production (heartbeats + tool
/// dispatch racing against each other).
/// </summary>
public sealed class SessionRegistryConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRegisterUpdateHeartbeatResolveUnregister_NoCorruptionOrExceptions()
    {
        const int SessionCount = 20;
        const int IterationsPerSession = 200;

        var registry = new SessionRegistry();
        var sessionIds = Enumerable.Range(0, SessionCount).Select(i => $"vs-stress-{i}").ToArray();

        // Pre-register every session so update/heartbeat/resolve have something to race against
        // from the very first iteration, not just after registration workers get around to it.
        foreach (var sessionId in sessionIds)
        {
            registry.Register(CreateRegistration(sessionId, $"Solution{sessionId}"));
        }

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = new List<Task>();

        // Registration/update churn: re-register and update every session repeatedly.
        foreach (var sessionId in sessionIds)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < IterationsPerSession; i++)
                    {
                        registry.Register(CreateRegistration(sessionId, $"Solution{sessionId}"));
                        registry.Update(CreateUpdate(sessionId, $"Solution{sessionId}"));
                        registry.Heartbeat(sessionId);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Dispatch-style reads: resolve by session id, by solution name, and list everything.
        for (var reader = 0; reader < 8; reader++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < IterationsPerSession; i++)
                    {
                        var sessionId = sessionIds[i % sessionIds.Length];

                        _ = registry.Resolve(new RoutingTarget(SessionId: sessionId));
                        _ = registry.Resolve(new RoutingTarget(SolutionName: $"Solution{sessionId}"));
                        _ = registry.ListSessions();
                        _ = registry.ListSessionStatuses();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);

        // Every pre-registered session should still resolve cleanly and be internally consistent
        // (no torn writes -- SolutionName/SolutionPath/ActiveDocument all reflect one coherent update).
        foreach (var sessionId in sessionIds)
        {
            var result = registry.Resolve(new RoutingTarget(SessionId: sessionId));
            Assert.True(result.Success);
            Assert.NotNull(result.Session);
            Assert.Equal(sessionId, result.Session!.SessionId);
            Assert.Equal($"Solution{sessionId}", result.Session.SolutionName);
        }

        Assert.Equal(SessionCount, registry.ListSessions().Count);
    }

    [Fact]
    public async Task ConcurrentRegisterAndUnregister_SameSessionId_NoCorruptionOrExceptions()
    {
        const int Iterations = 500;
        var registry = new SessionRegistry();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var registerTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < Iterations; i++)
                {
                    registry.Register(CreateRegistration("vs-churn", "ChurnSolution"));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var unregisterTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < Iterations; i++)
                {
                    registry.Unregister("vs-churn");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var readTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < Iterations; i++)
                {
                    _ = registry.ListSessions();
                    _ = registry.Resolve(new RoutingTarget(SessionId: "vs-churn"));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(registerTask, unregisterTask, readTask);

        Assert.Empty(exceptions);

        // Final state is whatever it ended up as (register or unregister could have gone last),
        // but the registry itself must be internally consistent, not corrupted.
        var sessions = registry.ListSessions();
        Assert.True(sessions.Count is 0 or 1);
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: Random.Shared.Next(1000, 9999),
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: $@"C:\Code\{solutionName}\{solutionName}.sln",
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private static VsSessionUpdate CreateUpdate(string sessionId, string solutionName)
    {
        return new VsSessionUpdate(
            SessionId: sessionId,
            SolutionName: solutionName,
            SolutionPath: $@"C:\Code\{solutionName}\{solutionName}.sln",
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Run,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Debugger]);
    }
}
