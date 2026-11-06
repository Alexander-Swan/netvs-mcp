using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: false)]
    [McpServerTool(Name = "vs_close_instance", Title = "Close Owned Visual Studio", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Closes only an instance launched by this broker lifetime. Pass sessionId from vs_launch_instance. A legacy ownershipToken is also accepted. Registered instances with unsaved buffers return unsavedChanges without closing. Otherwise requests normal close; never kills or discards edits. Inspect outcome/exitObserved: closePending is not completion. Cancellation cannot retract a posted close.")]
    public async Task<ToolResponse<VsCloseInstanceResult>> VsCloseInstance(string? ownershipToken = null, int timeoutSeconds = 10,
        CancellationToken cancellationToken = default, string? sessionId = null)
    {
        try
        {
            if (sessionId is not null)
            {
                var selected = _runtime.Sessions.ListSessions().SingleOrDefault(s => s.SessionId == sessionId);
                var matches = _runtime.Launcher.OwnedInstances.Where(p => p.ProcessId == selected?.ProcessId).ToArray();
                if (matches.Length != 1)
                    return FailWithCode<VsCloseInstanceResult>("The session does not identify a unique owned Visual Studio instance.", ToolErrorCodes.InvalidRequest);
                ownershipToken = matches[0].OwnershipToken;
            }
            if (string.IsNullOrWhiteSpace(ownershipToken))
                return FailWithCode<VsCloseInstanceResult>("Supply the session ID returned by vs_launch_instance.", ToolErrorCodes.InvalidRequest);
            var owned = _runtime.Launcher.OwnedInstances.SingleOrDefault(p => p.OwnershipToken == ownershipToken);
            var session = owned is null ? null : _runtime.Sessions.ListSessions().SingleOrDefault(s => s.ProcessId == owned.ProcessId);
            using var lease = session is null ? null : _runtime.Mutations.Enter(session.SessionId, nameof(VsCloseInstance));
            if (session is not null)
            {
                var documents = await DocumentList(sessionId: session.SessionId, cancellationToken: cancellationToken);
                if (!documents.Success || documents.Value is null)
                    return FailWithCode<VsCloseInstanceResult>("Unsaved document state could not be inspected; the close was not requested.", ToolErrorCodes.InspectionUnavailable);
                if (documents.Value.Documents.Any(d => !d.IsSaved))
                    return ToolResponse<VsCloseInstanceResult>.Ok(new(session.ProcessId, "unsavedChanges", false,
                        "Save or explicitly discard the unsaved documents before closing. No close was requested."));
            }
            var result = await _runtime.Launcher.CloseOwnedAsync(ownershipToken, timeoutSeconds, cancellationToken,
                unsavedStateInspected: session is not null);
            AuditToolResult(nameof(VsCloseInstance), null, result.ExitObserved, null, result.Outcome);
            return ToolResponse<VsCloseInstanceResult>.Ok(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (SessionMutationConflictException ex) { return FailWithCode<VsCloseInstanceResult>(ex.Message, ToolErrorCodes.MutationConflict); }
        catch (Exception ex) { return AuditBrokerException<VsCloseInstanceResult>(nameof(VsCloseInstance), ex); }
    }
}
