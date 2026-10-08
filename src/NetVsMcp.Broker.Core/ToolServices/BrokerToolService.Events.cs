using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    private const int DefaultEventsMaxEvents = 20;
    private const int MaxEventsMaxEvents = 100;
    private const int MaxEventsWaitTimeoutSeconds = 300;

    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "events_list", Title = "Events List", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists recent broker-managed Visual Studio events for a routed session. Use after async operations to retrieve queued events such as build_completed, debugger_break, debugger_stopped, test_run_completed, and breakpoint_changed.")]
    public ToolResponse<BrokerEventsResult> EventsList(
        long sinceSequence = 0,
        string[]? eventTypes = null,
        int maxEvents = DefaultEventsMaxEvents,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ValidateEventsRequest(sinceSequence, maxEvents) is { } validation)
        {
            return FailWithCode<BrokerEventsResult>(validation, ToolErrorCodes.InvalidRequest);
        }

        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var response = new ToolResponse<BrokerEventsResult>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(EventsList), target, response.Success, null, response.Message, route.FailureReason.ToString());
            return response;
        }

        var result = _runtime.Events.List(
            route.Session.SessionId,
            sinceSequence,
            NormalizeEventTypes(eventTypes),
            maxEvents);
        var success = ToolResponse<BrokerEventsResult>.Ok(result);
        AuditToolResult(nameof(EventsList), target, success.Success, route.Session.SessionId, success.Message);
        return success;
    }

    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "events_wait", Title = "Events Wait", ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Long-polls for broker-managed Visual Studio events for a routed session. Prefer this over polling after async operations, for example wait for build_completed after build_solution(waitForBuildToFinish: false), debugger_break after continuing the debugger, or test_run_completed after starting tests.")]
    public async Task<ToolResponse<BrokerEventsResult>> EventsWait(
        long sinceSequence = 0,
        string[]? eventTypes = null,
        int timeoutSeconds = 120,
        int maxEvents = DefaultEventsMaxEvents,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEventsRequest(sinceSequence, maxEvents) is { } validation)
        {
            return FailWithCode<BrokerEventsResult>(validation, ToolErrorCodes.InvalidRequest);
        }

        if (timeoutSeconds < 0 || timeoutSeconds > MaxEventsWaitTimeoutSeconds)
        {
            return FailWithCode<BrokerEventsResult>($"timeoutSeconds must be between 0 and {MaxEventsWaitTimeoutSeconds}.", ToolErrorCodes.InvalidRequest);
        }

        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var response = new ToolResponse<BrokerEventsResult>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(EventsWait), target, response.Success, null, response.Message, route.FailureReason.ToString());
            return response;
        }

        var result = await _runtime.Events.WaitAsync(
            route.Session.SessionId,
            sinceSequence,
            NormalizeEventTypes(eventTypes),
            maxEvents,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);
        var success = ToolResponse<BrokerEventsResult>.Ok(result);
        AuditToolResult(nameof(EventsWait), target, success.Success, route.Session.SessionId, success.Message);
        return success;
    }

    private static string? ValidateEventsRequest(long sinceSequence, int maxEvents)
    {
        if (sinceSequence < 0)
        {
            return "sinceSequence must be zero or greater.";
        }

        if (maxEvents < 1 || maxEvents > MaxEventsMaxEvents)
        {
            return $"maxEvents must be between 1 and {MaxEventsMaxEvents}.";
        }

        return null;
    }

    private static IReadOnlyCollection<string>? NormalizeEventTypes(string[]? eventTypes)
    {
        if (eventTypes is null || eventTypes.Length == 0)
        {
            return null;
        }

        var normalized = eventTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }
}
