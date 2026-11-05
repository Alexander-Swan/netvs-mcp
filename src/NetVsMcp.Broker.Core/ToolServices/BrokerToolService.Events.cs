using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Broker, requiresVisualStudioSession: false)]
    [McpServerTool(Name = "vs_events", Title = "Read or Wait for Session Events", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads or waits for buffered Visual Studio session events, including stops that occurred before the call. On requiresReconciliation, use debug_snapshot with include: [] to refresh state and its event cursor without replaying the mutation. Older VSIX versions without events fail explicitly. Compact output.")]
    public async Task<ToolResponse<VsEventBatch>> VsEvents(
        [Description("Epoch from the same event cursor as afterSequence. A changed epoch requires reconciliation.")] string? epoch = null,
        [Description("Read events after this nonnegative sequence. Start at the cursor's watermark and advance pages using the last returned event's sequence. Omit to get the current cursor.")] long? afterSequence = null,
        [Description("Seconds to wait for events when afterSequence is supplied, 0 to 60. Zero reads immediately.")] int timeoutSeconds = 0,
        [Description("Maximum events per page, 1 to 1000. Defaults to 100.")] int maxItems = 100,
        string? sessionId = null,
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
