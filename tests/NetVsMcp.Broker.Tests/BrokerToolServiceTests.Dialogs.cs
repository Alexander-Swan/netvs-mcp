using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DialogTools_RouteWithoutVsixConnection()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "First", processId: 100));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Second", processId: 200));
        var backend = new FakeDialogAutomationService();
        var tools = new BrokerToolService(runtime, backend);

        var list = await tools.DialogList(sessionId: "vs-2");
        Assert.True(list.Success);
        Assert.Equal(200, backend.ProcessId);
        Assert.Equal("Save changes?", Assert.Single(list.Value!).Message);
        var response = await tools.DialogRespond("dialog", "cancel", sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal(100, backend.ProcessId);
        Assert.Equal(("dialog", "cancel"), backend.Response);
    }

    [Fact]
    public async Task DialogTools_RejectMissingSessionAndMissingIds()
    {
        var runtime = CreateRuntime();
        var backend = new FakeDialogAutomationService();
        var tools = new BrokerToolService(runtime, backend);
        var missingSession = await tools.DialogList();
        Assert.False(missingSession.Success);
        Assert.Equal(0, backend.Calls);
        var missingIds = await tools.DialogRespond("", "");
        Assert.False(missingIds.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, missingIds.Metadata!["error_code"]);
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public async Task DialogTools_ReturnBackendErrors()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        var tools = new BrokerToolService(runtime, new FakeDialogAutomationService { Fail = true });
        var response = await tools.DialogRespond("stale", "button");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.VisualStudioError, response.Metadata!["error_code"]);
        Assert.Contains("changed", response.Message);
    }

    private sealed class FakeDialogAutomationService : IDialogAutomationService
    {
        public int ProcessId { get; private set; }
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public bool FailListing { get; init; }
        public Action? OnRespond { get; set; }
        public IReadOnlyList<DialogInfo> CurrentDialogs { get; set; } = [new("dialog", "Visual Studio", "Save changes?", [new("cancel", "Cancel", true)])];
        public (string, string)? Response { get; private set; }
        public IReadOnlyList<DialogInfo> List(int processId, CancellationToken cancellationToken)
        {
            Calls++;
            ProcessId = processId;
            if (FailListing) throw new InvalidOperationException("UIA provider unavailable.");
            return CurrentDialogs;
        }
        public bool Respond(int processId, string dialogId, string buttonId, CancellationToken cancellationToken)
        {
            Calls++;
            ProcessId = processId;
            if (Fail) throw new InvalidOperationException("The dialog changed.");
            Response = (dialogId, buttonId);
            OnRespond?.Invoke();
            return true;
        }
    }

    [Theory]
    [InlineData(nameof(BrokerToolService.DialogList))]
    [InlineData(nameof(BrokerToolService.DialogRespond))]
    [InlineData(nameof(BrokerToolService.DebugStatus))]
    public void DialogTools_DescribeEveryClientParameter(string methodName)
    {
        var method = typeof(BrokerToolService).GetMethod(methodName)!;
        foreach (var parameter in method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)))
            Assert.False(string.IsNullOrWhiteSpace(parameter.GetCustomAttribute<DescriptionAttribute>()?.Description), parameter.Name);
    }

    [Fact]
    public async Task DebugStatus_RejectsUnknownAndEmptyOperationIds()
    {
        var runtime = CreateRuntime();
        Assert.False((await runtime.Tools.DebugStatus(operationId: "unknown")).Success);
        Assert.False((await runtime.Tools.DebugStatus(operationId: "")).Success);
    }

    [Fact]
    public async Task PendingOperation_HandsControlToClient_RespondsAndCollectsOriginalResultOnce()
    {
        var original = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeDialogAutomationService();
        backend.OnRespond = () =>
        {
            backend.CurrentDialogs = [];
            original.TrySetResult("debugging started");
        };
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First", processId: 100));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        using var request = new CancellationTokenSource();
        var calls = 0;
        var blocked = await runtime.Dispatcher.DispatchAsync(new RoutingTarget(SessionId: "vs-1"), (_, ct) =>
        {
            calls++;
            return original.Task.WaitAsync(ct);
        }, request.Token).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(VsSessionDispatchFailureReason.DialogBlocked, blocked.FailureReason);
        Assert.Equal(100, backend.ProcessId);
        Assert.NotNull(blocked.OperationId);
        var dialog = Assert.Single(blocked.Dialogs!);
        // Returning the interrupted MCP request must not cancel its original VS operation.
        request.Cancel();
        Assert.Equal(0, runtime.Sessions.RemoveStaleSessions(DateTimeOffset.UtcNow.AddHours(1)));
        var response = await runtime.Tools.DialogRespond(dialog.DialogId, Assert.Single(dialog.Buttons).ButtonId, sessionId: "vs-1");
        Assert.True(response.Success);
        var completed = await AwaitOperationAsync(runtime.Tools, blocked.OperationId);
        Assert.True(completed.Success);
        Assert.Equal("completed", completed.Value!.Operation!.Status);
        Assert.Equal("debugging started", Assert.IsType<JsonElement>(completed.Value.Operation.Result).GetString());
        Assert.Equal(1, calls);
        var repeated = await runtime.Tools.DebugStatus(operationId: blocked.OperationId);
        Assert.Equal("completed", repeated.Value!.Operation!.Status);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PendingOperation_ReportsNextDialogAndRunningState_WithoutCancelingOriginalOnWaitCancellation()
    {
        var original = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeDialogAutomationService();
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var blocked = await runtime.Dispatcher.DispatchAsync(new RoutingTarget(SessionId: "vs-1"),
            (_, ct) => original.Task.WaitAsync(ct), default).WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            backend.CurrentDialogs = [new("second", "Next dialog", "Continue?", [new("continue", "Continue", true)])];
            var next = await runtime.Tools.DebugStatus(operationId: blocked.OperationId);
            Assert.Equal("dbgModalBlock", next.Value!.Mode);
            Assert.Equal("second", Assert.Single(next.Value.Dialogs!).DialogId);
            backend.CurrentDialogs = [];
            var running = await runtime.Tools.DebugStatus(operationId: blocked.OperationId);
            Assert.Equal("running", running.Value!.Operation!.Status);
            Assert.True(running.Value.UnderlyingModeIsStale);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.Tools.DebugStatus(operationId: blocked.OperationId, cancellationToken: canceled.Token));
        }
        finally { original.TrySetResult("finished"); }
        var completed = await AwaitOperationAsync(runtime.Tools, blocked.OperationId!);
        Assert.Equal("completed", completed.Value!.Operation!.Status);
    }

    [Fact]
    public async Task PendingOperation_ProbeFailureDoesNotFailOriginalCommand()
    {
        var backend = new FakeDialogAutomationService { FailListing = true };
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var result = await runtime.Dispatcher.DispatchAsync(new RoutingTarget(SessionId: "vs-1"), async (_, ct) =>
        {
            await Task.Delay(1500, ct);
            return "finished";
        }, default);
        Assert.True(result.Success);
        Assert.Equal("finished", result.Value);
        Assert.Null(result.OperationId);
        Assert.True(backend.Calls > 0);
    }

    [Fact]
    public async Task DebugStart_ReturnsActionableDialogMetadata_AndResumesWithoutRepeatingCommand()
    {
        var original = new TaskCompletionSource<DebuggerStateInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeDialogAutomationService();
        backend.OnRespond = () =>
        {
            backend.CurrentDialogs = [];
            original.TrySetResult(new("Run"));
        };
        var runtime = CreateRuntime(backend);
        var starts = 0;
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs")
        {
            DebugStartHandler = ct => { starts++; return original.Task.WaitAsync(ct); }
        });
        try
        {
            var start = await runtime.Tools.DebugStart(sessionId: "vs-1").WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(start.Success);
            Assert.Equal("dbgModalBlock", start.Value!.Mode);
            Assert.Equal("running", start.Value.Operation!.Status);
            Assert.Contains("debug_status", start.Message);
            var dialog = Assert.Single(start.Value.Dialogs!);
            Assert.True((await runtime.Tools.DialogRespond(dialog.DialogId, dialog.Buttons.Single().ButtonId, sessionId: start.Value.SessionId)).Success);
            var completed = await AwaitOperationAsync(runtime.Tools, start.Value.Operation.OperationId);
            Assert.Equal("completed", completed.Value!.Operation!.Status);
            Assert.Equal("Run", Assert.IsType<JsonElement>(completed.Value.Operation.Result).GetProperty("mode").GetString());
            Assert.Equal(1, starts);
        }
        finally { original.TrySetResult(new("Run")); }
    }

    [Fact]
    public async Task PendingOperation_PreservesOriginalTimeout()
    {
        var backend = new FakeDialogAutomationService();
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs"));
        var dispatcher = new DialogAwareSessionDispatcher(new VsSessionDispatcher(runtime.Sessions, runtime.Connections),
            runtime.Sessions, runtime.Dialogs, TimeSpan.FromMilliseconds(10));
        var original = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = await dispatcher.DispatchAsync(new RoutingTarget(SessionId: "vs-1"),
            (_, _) => original.Task, default, TimeSpan.FromMilliseconds(200));
        try
        {
            Assert.Equal(VsSessionDispatchFailureReason.DialogBlocked, blocked.FailureReason);
            backend.CurrentDialogs = [];
            ToolResponse<DebuggerStateInfo> result;
            do
            {
                await Task.Delay(25);
                result = await dispatcher.GetDebugStatusAsync(null, blocked.OperationId, default);
            } while (result.Value!.Operation!.Status == "running");
            Assert.Equal("failed", result.Value.Operation.Status);
            Assert.False(result.Value.Operation.Success);
            Assert.Equal(ToolErrorCodes.OperationTimedOut, result.Value.Operation.ErrorCode);
        }
        finally { original.TrySetResult("late completion"); }
    }

    private static async Task<ToolResponse<DebuggerStateInfo>> AwaitOperationAsync(BrokerToolService tools, string operationId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var status = await tools.DebugStatus(operationId: operationId, cancellationToken: timeout.Token);
            if (!status.Success || status.Value!.Operation!.Status != "running") return status;
            await Task.Delay(10, timeout.Token);
        }
    }

    [Fact]
    public async Task DebugStatus_DetectsLateDialogAfterContinue_PreservesLastKnownMode_WithoutCallingVsix()
    {
        var backend = new FakeDialogAutomationService { CurrentDialogs = [] };
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        var statusReads = 0;
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs")
        {
            DebugStatusHandler = _ => { statusReads++; return Task.FromResult(new DebuggerStateInfo("dbgRunMode")); }
        });
        var running = await runtime.Tools.DebugStatus(sessionId: "vs-1");
        Assert.Equal("dbgRunMode", running.Value!.Mode);
        Assert.True((await runtime.Tools.DebugContinue(sessionId: "vs-1")).Success);
        // Continue has returned; the breakpoint warning appears later.
        backend.CurrentDialogs = [new("condition", "Breakpoint condition", "Expression cannot be evaluated", [new("ok", "OK", true)])];
        var blocked = await runtime.Tools.DebugStatus(sessionId: "vs-1");
        Assert.True(blocked.Success);
        Assert.Equal("dbgModalBlock", blocked.Value!.Mode);
        Assert.Equal("Run", blocked.Value.UnderlyingMode); // Last successful Continue result.
        Assert.True(blocked.Value.UnderlyingModeIsStale);
        Assert.Null(blocked.Value.Operation); // No original command remains pending.
        Assert.Equal(1, statusReads);
        var mode = await runtime.Tools.DebugGetMode(sessionId: "vs-1");
        Assert.Equal("dbgModalBlock", mode.Value!.Mode);
        Assert.Equal(1, statusReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DebugSnapshotAndWait_ReturnModalStateWithoutFetchingInspection(bool wait)
    {
        var backend = new FakeDialogAutomationService();
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        var session = new FakeVisualStudioSessionRpc("Program.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("Status RPC must not run behind a known dialog.")
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var snapshot = wait
            ? await runtime.Tools.DebugWaitForBreak(include: ["locals", "callStack", "watch"], sessionId: "vs-1")
            : await runtime.Tools.DebugSnapshot(include: ["locals", "callStack", "watch"], sessionId: "vs-1");
        Assert.True(snapshot.Success);
        Assert.Equal("dbgModalBlock", snapshot.Value!.State.Mode);
        Assert.NotEmpty(snapshot.Value.State.Dialogs!);
        Assert.False(snapshot.Value.TimedOut);
        Assert.Null(snapshot.Value.Locals);
        Assert.Null(snapshot.Value.CallStack);
        Assert.Null(snapshot.Value.Watch);
        Assert.Null(session.LastCallStackRequest);
    }

    [Fact]
    public async Task DebugWaitForBreak_DetectsDialogAppearingDuringPolling()
    {
        var backend = new FakeDialogAutomationService { CurrentDialogs = [] };
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        var polls = 0;
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs")
        {
            DebugStatusHandler = _ =>
            {
                if (++polls == 2)
                    backend.CurrentDialogs = [new("late", "Breakpoint condition", "Cannot evaluate", [new("ok", "OK", true)])];
                return Task.FromResult(new DebuggerStateInfo("dbgRunMode"));
            }
        });
        var snapshot = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 30, include: ["locals"], sessionId: "vs-1")
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("dbgModalBlock", snapshot.Value!.State.Mode);
        Assert.Equal("dbgRunMode", snapshot.Value.State.UnderlyingMode);
        Assert.False(snapshot.Value.TimedOut);
        Assert.Null(snapshot.Value.Locals);
        Assert.True(polls >= 2);
    }

    [Fact]
    public async Task DebugStatus_CannotRouteOperationToAnotherSession()
    {
        var original = new TaskCompletionSource<DebuggerStateInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeDialogAutomationService();
        var runtime = CreateRuntime(backend);
        runtime.Sessions.Register(CreateRegistration("vs-1", "First", processId: 100));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Second", processId: 200));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Program.cs") { DebugStartHandler = ct => original.Task.WaitAsync(ct) });
        try
        {
            var start = await runtime.Tools.DebugStart(sessionId: "vs-1");
            var id = start.Value!.Operation!.OperationId;
            var wrong = await runtime.Tools.DebugStatus(sessionId: "vs-2", operationId: id);
            Assert.False(wrong.Success);
            Assert.Equal(ToolErrorCodes.InvalidRequest, wrong.Metadata!["error_code"]);
            var selected = await runtime.Tools.DebugStatus(operationId: id);
            Assert.Equal("vs-1", selected.Value!.SessionId);
            Assert.Equal(100, backend.ProcessId);
        }
        finally { original.TrySetResult(new("Run")); }
    }

    [Fact]
    public void DialogFlow_DoesNotExposeAnAdditionalOperationTool()
    {
        var tools = typeof(BrokerToolService).GetMethods().Select(m => m.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolAttribute>()?.Name);
        Assert.DoesNotContain("vs_operation_wait", tools);
    }
}
