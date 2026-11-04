using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Broker, requiresVisualStudioSession: false)]
    [McpServerTool(Name = "vs_events", Title = "Read or Wait for Session Events", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads buffered observations pushed by the selected Visual Studio instance. Omit epoch/afterSequence to obtain a watermark before issuing a command. Then pass that epoch and watermark as afterSequence to receive subsequent events, including fast stops that occurred before this call. timeoutSeconds=0 reads immediately; positive values wait. Advance a paged cursor using the last returned event sequence, not watermark until all pages are read. requiresReconciliation means events were lost or the publisher restarted: call debug_snapshot(include: []) for current debugger state plus state.eventCursor; never replay a mutation. Unsupported older VSIX versions fail explicitly. Events are internal VS-to-broker observations; clients do not need MCP notification support. Output: standard reduced MCP JSON without success/value wrappers. Source paths are solution-relative when possible; source locations use file:line:column. Call failures set isError and retain an error message/recovery metadata. Events inherit matching sessionId/epoch from the enclosing batch. requiresReconciliation appears when true; differing identities remain explicit.")]
    public async Task<ToolResponse<VsEventBatch>> VsEvents(string? epoch = null, long? afterSequence = null,
        int timeoutSeconds = 0, int maxItems = 100, string? sessionId = null,
        string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        try
        {
            if (afterSequence is < 0 || timeoutSeconds < 0 || timeoutSeconds > 60 || maxItems < 1 || maxItems > 1000)
                return ToolResponse<VsEventBatch>.Fail("Invalid event cursor, timeout (0–60), or maxItems (1–1000).");
            var route = _runtime.Sessions.Resolve(target);
            if (!route.Success || route.Session is null)
                return new(false, null, route.Message, CreateRouteFailureMetadata(route));
            var stream = _runtime.Sessions.Events.Find(route.Session.SessionId);
            if (stream is null)
                return ToolResponse<VsEventBatch>.Fail("This session has not published events. Update the VSIX or reconcile using ordinary state tools.");
            var current = stream.Read(null, stream.Snapshot().Watermark);
            var batch = afterSequence is null ? current : timeoutSeconds == 0
                ? stream.Read(epoch, afterSequence.Value, maxItems)
                : await stream.WaitAsync(epoch, afterSequence.Value, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken, maxItems);
            lock (_runtime.Sessions.RegistrationGate)
                if (!ReferenceEquals(stream, _runtime.Sessions.Events.Find(route.Session.SessionId)))
                    batch = batch with { RequiresReconciliation = true, Events = Array.Empty<VsSessionEvent>() };
            AuditToolResult(nameof(VsEvents), target, true, route.Session.SessionId, null);
            return ToolResponse<VsEventBatch>.Ok(batch);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return AuditBrokerException<VsEventBatch>(nameof(VsEvents), ex); }
    }
}
