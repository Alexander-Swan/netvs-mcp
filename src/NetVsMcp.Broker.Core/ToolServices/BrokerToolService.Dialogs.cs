using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    private const string DialogSessionDescription = "VS session ID returned by vs_list_sessions or the blocked operation's metadata.sessionId. Use that same session for listing and responding, especially with multiple VS instances. Omit only to use normal active/single-session routing.";
    private const string DialogSolutionNameDescription = "Optional open solution name used to select a VS session when sessionId is omitted. If names are ambiguous, use sessionId or the full solutionPath.";
    private const string DialogSolutionPathDescription = "Optional absolute path to the open .sln or .slnx used to select the VS session when sessionId is omitted. Prefer forward slashes, for example D:/Work/App/App.slnx.";

    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "dialog_list", Title = "List Visual Studio Dialogs", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Inspects visible modal dialogs in the selected Visual Studio process while VS is busy or waiting for input. Returns title, accessible message, dialogId, buttons with buttonId/name/isEnabled, and truncated. An empty list means no supported modal dialog was found. Debug state tools expose dbgModalBlock with dialogs, and other pending IDE tools return dialog_blocked metadata with dialogs and an operationId; use those IDs directly with dialog_respond. Call this tool to inspect independently or refresh rejected IDs. Each listing or automatic probe replaces previous IDs for this process. Debuggee/application dialogs require the ui_* tools on /mcp-wu.")]
    public Task<ToolResponse<IReadOnlyList<DialogInfo>>> DialogList(
        [Description(DialogSessionDescription)] string? sessionId = null,
        [Description(DialogSolutionNameDescription)] string? solutionName = null,
        [Description(DialogSolutionPathDescription)] string? solutionPath = null,
        CancellationToken cancellationToken = default) =>
        DispatchDialog((pid, ct) => _dialogs.ListAsync(pid, ct), sessionId, solutionName, solutionPath, cancellationToken);

    [BrokerToolMetadata(BrokerToolCategory.Admin, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "dialog_respond", Title = "Respond to Visual Studio Dialog", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description("Answers a Visual Studio modal dialog by invoking one enabled button from dialog_list, dialog_blocked metadata.dialogs, or debug_status. Choose the button from its name and the message according to the user's task and existing authorization; proceed directly when that decision is already authorized. Buttons may discard changes or authorize other actions. Pass the exact dialogId/buttonId and the same sessionId; do not guess IDs or send a button label as buttonId. Each dialog ID permits one response attempt. Success means invocation was sent, not that the dialog closed. Call debug_status afterward with the same sessionId and any returned operationId instead of repeating the original command. After a timeout or rejected ID, call dialog_list before another response because the click may already have occurred.")]
    public Task<ToolResponse<bool>> DialogRespond(
        [Description("Exact opaque dialogId returned in a dialog object by the latest dialog_list, dialog_blocked metadata.dialogs, or debug_status for this VS process. Not a window title or handle. IDs are replaced by a new scan and consumed by a response attempt.")] string dialogId,
        [Description("Exact opaque buttonId from that dialog's buttons array. Select a button whose isEnabled is true and whose name has the intended meaning, such as Cancel or Continue. Pass the ID, not the displayed name, index, or keyboard shortcut.")] string buttonId,
        [Description(DialogSessionDescription)] string? sessionId = null,
        [Description(DialogSolutionNameDescription)] string? solutionName = null,
        [Description(DialogSolutionPathDescription)] string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dialogId) || string.IsNullOrWhiteSpace(buttonId))
            return Task.FromResult(FailWithCode<bool>("dialogId and buttonId from dialog_list are required.", ToolErrorCodes.InvalidRequest));
        return DispatchDialog((pid, ct) => _dialogs.RespondAsync(pid, dialogId, buttonId, ct), sessionId, solutionName, solutionPath, cancellationToken);
    }

    private async Task<ToolResponse<T>> DispatchDialog<T>(Func<int, CancellationToken, Task<T>> operation,
        string? sessionId, string? solutionName, string? solutionPath, CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var route = _runtime.Sessions.Resolve(target);
        var watch = Stopwatch.StartNew();
        ToolResponse<T> response;
        if (!route.Success)
            response = new ToolResponse<T>(false, default, route.Message, CreateRouteFailureMetadata(route));
        else
        {
            try
            {
                using var lease = _runtime.Sessions.BeginDispatch(route.Session!.SessionId);
                response = ToolResponse<T>.Ok(await operation(route.Session.ProcessId, cancellationToken));
            }
            catch (TimeoutException)
            {
                response = FailWithCode<T>("Dialog automation timed out. Call dialog_list before attempting another response; a button invocation may already have occurred.", ToolErrorCodes.OperationTimedOut);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                response = FailWithCode<T>($"Dialog automation failed: {ex.Message}", ToolErrorCodes.VisualStudioError);
            }
        }
        AuditToolResult(toolName, target, response.Success, route.Session?.SessionId, response.Message,
            responsePayload: response, duration: watch.Elapsed);
        return response;
    }
}
