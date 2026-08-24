using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DebugStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugStatus(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Break", response.Value!.Mode);
    }

    [Fact]
    public async Task DebugStep_RoutesStepKindToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugStep(DebugStepKind.Into, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(DebugStepKind.Into, session.LastDebugStepRequest!.StepKind);
        Assert.Equal("Break", response.Value!.Mode);
    }

    [Fact]
    public async Task BreakpointSet_ValidatesLine()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.BreakpointSet("Program.cs", 0);

        Assert.False(response.Success);
        Assert.Equal("Breakpoint line must be greater than zero.", response.Message);
    }

    [Fact]
    public async Task BreakpointSet_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointSet(
            documentPath: @"C:\Code\NetVsMcp\Program.cs",
            line: 42,
            column: 3,
            condition: "count > 0",
            action: "log",
            actionMessage: "count is {count}",
            continueAfterAction: true,
            hitCount: 5,
            hitCountType: "equals",
            dependsOnBreakpointName: "bp-prereq",
            groupName: "critical",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(42, session.LastBreakpointSetRequest!.Line);
        Assert.Equal("count > 0", session.LastBreakpointSetRequest.Condition);
        Assert.Equal("log", session.LastBreakpointSetRequest.Action);
        Assert.Equal("count is {count}", session.LastBreakpointSetRequest.ActionMessage);
        Assert.True(session.LastBreakpointSetRequest.ContinueAfterAction);
        Assert.Equal(5, session.LastBreakpointSetRequest.HitCount);
        Assert.Equal("equals", session.LastBreakpointSetRequest.HitCountType);
        Assert.Equal("bp-prereq", session.LastBreakpointSetRequest.DependsOnBreakpointName);
        Assert.Equal("critical", session.LastBreakpointSetRequest.GroupName);
        Assert.Equal("bp-1", response.Value!.Name);
        Assert.Equal("critical", response.Value.GroupName);
    }

    [Fact]
    public async Task BreakpointList_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BreakpointList(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("bp-1", Assert.Single(response.Value!.Breakpoints).Name);
    }

    [Fact]
    public async Task BreakpointGroupList_ReturnsGroupsFromBreakpoints()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BreakpointGroupList(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("critical", Assert.Single(response.Value!.Groups));
        Assert.Equal("critical", Assert.Single(response.Value.Breakpoints).GroupName);
    }

    [Fact]
    public async Task BreakpointEnable_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointEnable(
            enabled: false,
            name: "bp-1",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.False(session.LastBreakpointEnableRequest!.Enabled);
        Assert.Equal(1, response.Value!.Updated);
    }

    [Fact]
    public async Task BreakpointGroupEnable_EnablesMatchingGroup()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointGroupEnable("critical", enabled: false, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("bp-1", session.LastBreakpointEnableRequest!.Name);
        Assert.False(session.LastBreakpointEnableRequest.Enabled);
        Assert.Equal(1, response.Value!.Matched);
        Assert.Equal(1, response.Value.Updated);
    }

    [Fact]
    public async Task BreakpointEnable_WhenEnabling_DoesNotReturnState()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointEnable(
            enabled: true,
            name: "bp-1",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Null(response.Value!.State);
    }

    [Fact]
    public async Task BreakpointEnable_WhenDisabling_ReturnsCurrentState()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointEnable(
            enabled: false,
            name: "bp-1",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("dbgBreakMode", response.Value!.State!.Mode);
    }

    [Fact]
    public async Task BreakpointEnable_WhenDisablingWithContinueExecution_ResumesDebugger()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointEnable(
            enabled: false,
            name: "bp-1",
            continueExecution: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Run", response.Value!.State!.Mode);
    }

    [Fact]
    public async Task BreakpointEnable_RejectsNegativeSettleTimeout()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BreakpointEnable(
            enabled: false,
            name: "bp-1",
            settleTimeoutMilliseconds: -1,
            sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("settleTimeoutMilliseconds must be zero or greater.", response.Message);
    }

    [Fact]
    public async Task BreakpointGroupEnable_WhenDisabling_ReturnsCurrentState()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointGroupEnable("critical", enabled: false, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("dbgBreakMode", response.Value!.State!.Mode);
    }

    [Fact]
    public async Task BreakpointGroupEnable_WhenDisablingWithContinueExecution_ResumesDebugger()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointGroupEnable(
            "critical",
            enabled: false,
            continueExecution: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Run", response.Value!.State!.Mode);
    }

    [Fact]
    public async Task BreakpointGroupEnable_WhenEnabling_DoesNotReturnState()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointGroupEnable("critical", enabled: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Null(response.Value!.State);
    }

    [Fact]
    public async Task BreakpointRemove_RequiresNameOrDocumentPath()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.BreakpointRemove();

        Assert.False(response.Success);
        Assert.Equal("Breakpoint name or document path is required.", response.Message);
    }

    [Fact]
    public async Task BreakpointRemove_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointRemove(name: "bp-1", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("bp-1", session.LastBreakpointRemoveRequest!.Name);
        Assert.Equal(1, response.Value!.Removed);
    }

    [Fact]
    public async Task BreakpointGroupRemove_RemovesMatchingGroup()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BreakpointGroupRemove("critical", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("bp-1", session.LastBreakpointRemoveRequest!.Name);
        Assert.Equal(1, response.Value!.Matched);
        Assert.Equal(1, response.Value.Updated);
    }

    [Fact]
    public async Task DebugGetCallstack_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugGetCallstack(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Break", response.Value!.State.Mode);
        Assert.Equal("Program.Main", Assert.Single(response.Value.Frames).FunctionName);
    }

    [Fact]
    public async Task DebugGetLocals_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugGetLocals(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("count", Assert.Single(response.Value!.Locals).Name);
    }

    [Fact]
    public async Task DebugEvaluate_RequiresExpression()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.DebugEvaluate("");

        Assert.False(response.Success);
        Assert.Equal("Expression is required.", response.Message);
    }

    [Fact]
    public async Task DebugEvaluate_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugEvaluate(
            expression: "count + 1",
            timeoutMilliseconds: 1000,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("count + 1", session.LastEvaluateExpressionRequest!.Expression);
        Assert.Equal("43", response.Value!.Expression.Value);
    }

    [Fact]
    public async Task DebugSnapshot_ReturnsCompositeDebuggerState()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugSnapshot(
            include: ["callStack", "breakpoints"],
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("dbgBreakMode", response.Value!.State.Mode);
        Assert.False(response.Value.TimedOut);
        Assert.Single(response.Value.CallStack!.Frames);
        Assert.Single(response.Value.Locals!.Locals);
        Assert.Single(response.Value.Breakpoints!.Breakpoints);
        Assert.Null(response.Value.UnrecognizedInclude);
    }

    [Fact]
    public async Task DebugSnapshot_StepsAdvancesAndSettlesBeforeInspecting()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugSnapshot(
            action: DebugAdvanceAction.StepOver,
            include: ["callStack"],
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(DebugStepKind.Over, session.LastDebugStepRequest!.StepKind);
        Assert.Equal("dbgBreakMode", response.Value!.State.Mode);
        Assert.Single(response.Value.CallStack!.Frames);
        Assert.Single(response.Value.Locals!.Locals);
        Assert.Null(response.Value.Breakpoints);
    }

    [Fact]
    public async Task DebugSnapshot_RejectsNegativeSettleTimeout()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugSnapshot(settleTimeoutMilliseconds: -1, sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("settleTimeoutMilliseconds must be zero or greater.", response.Message);
    }

    [Fact]
    public async Task DebugSnapshot_ReportsUnrecognizedIncludeKeys()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugSnapshot(
            include: ["callStack", "bogus"],
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("bogus", Assert.Single(response.Value!.UnrecognizedInclude!));
    }

    [Fact]
    public async Task DebugWaitForBreak_ReturnsImmediatelyWhenAlreadyPaused()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugWaitForBreak(
            include: ["callStack", "breakpoints"],
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("dbgBreakMode", response.Value!.State.Mode);
        Assert.Single(response.Value.CallStack!.Frames);
        Assert.Single(response.Value.Locals!.Locals);
        Assert.Single(response.Value.Breakpoints!.Breakpoints);
    }

    [Fact]
    public async Task DebugWaitForBreak_ReturnsRunningStateAfterTimeoutElapses()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgRunMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("dbgRunMode", response.Value!.State.Mode);
        Assert.True(response.Value.TimedOut);
        Assert.Null(response.Value.Locals);
        Assert.Null(response.Value.CallStack);
    }

    [Fact]
    public async Task DebugWaitForBreak_RejectsNonPositiveTimeout()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 0, sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("timeoutSeconds must be greater than zero.", response.Message);
    }

    [Fact]
    public async Task DebugEvalMany_EvaluatesExpressions()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugEvalMany(["count", "count"], sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Single(response.Value!.Results);
        Assert.Equal("count", session.LastEvaluateExpressionRequest!.Expression);
    }

    [Fact]
    public async Task DebugStatus_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.DebugStatus(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

    [Fact]
    public async Task DebugAttach_RoutesSelectorToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugAttach(processId: 1234, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.Equal(1234, session.LastDebugAttachRequest!.ProcessId);
        Assert.Equal(1234, response.Value.Process!.ProcessId);
    }

    [Fact]
    public async Task DebugAttach_RoutesRemoteTransportSelectorToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugAttach(
            processId: 1234,
            transport: "SSH",
            transportQualifier: "dev-box:22",
            engine: "Managed",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("SSH", session.LastDebugAttachRequest!.Transport);
        Assert.Equal("dev-box:22", session.LastDebugAttachRequest.TransportQualifier);
        Assert.Equal("Managed", session.LastDebugAttachRequest.Engine);
    }

    [Fact]
    public async Task DebugAttach_RequiresProcessSelector()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.DebugAttach();

        Assert.False(response.Success);
        Assert.Equal("Process id or process name is required.", response.Message);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }

    [Fact]
    public async Task ProcessListLocal_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.ProcessListLocal(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker.exe", Assert.Single(response.Value!.Processes).Name);
    }

    [Fact]
    public async Task ProcessDetach_RoutesSelectorToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ProcessDetach(processName: "NetVsMcp.Broker.exe", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.Equal("NetVsMcp.Broker.exe", session.LastProcessDetachRequest!.ProcessName);
        Assert.Equal("Break", response.Value.State.Mode);
    }

    [Fact]
    public async Task ParallelTools_RouteToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var stacks = await runtime.Tools.ParallelStacks(sessionId: "vs-1");
        var watch = await runtime.Tools.ParallelWatch(sessionId: "vs-1");

        Assert.True(stacks.Success);
        Assert.Single(stacks.Value!.Frames);
        Assert.True(watch.Success);
        Assert.Single(watch.Value!.Expressions);
    }

    [Fact]
    public async Task FormerPlannedDebuggerTools_RouteToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var variable = await runtime.Tools.DebugSetVariable("count", "43", sessionId: "vs-1");
        var frozen = await runtime.Tools.ThreadSetFrozen(1, true, sessionId: "vs-1");
        var callstack = await runtime.Tools.ThreadGetCallstack(1, sessionId: "vs-1");
        var terminated = await runtime.Tools.ProcessTerminate(processId: 1234, sessionId: "vs-1");

        Assert.True(variable.Success);
        Assert.True(variable.Value!.Success);
        Assert.True(frozen.Success);
        Assert.True(frozen.Value!.Frozen);
        Assert.True(callstack.Success);
        Assert.Single(callstack.Value!.Frames);
        Assert.True(terminated.Success);
        Assert.True(terminated.Value!.Success);
    }

    [Fact]
    public async Task TestDebug_RoutesFilterToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.TestDebug(
            projectName: "NetVsMcp.Broker.Tests",
            filter: "FullyQualifiedName~BrokerToolServiceTests",
            attachTimeoutSeconds: 12,
            noBuild: true,
            configuration: "Debug",
            framework: "net10.0",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal("FullyQualifiedName~BrokerToolServiceTests", session.LastTestDebugRequest!.Filter);
        Assert.Equal(12, session.LastTestDebugRequest.AttachTimeoutSeconds);
        Assert.True(session.LastTestDebugRequest.NoBuild);
        Assert.Equal("Debug", session.LastTestDebugRequest.Configuration);
        Assert.Equal("net10.0", session.LastTestDebugRequest.Framework);
        Assert.Equal(1234, response.Value.TestHostProcessId);
        Assert.Equal(5678, response.Value.TestRunnerProcessId);
        Assert.Equal("dotnet test project --filter test", response.Value.CommandLine);
    }

    [Fact]
    public async Task TestDebug_RequiresFilter()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.TestDebug(filter: " ");

        Assert.False(response.Success);
        Assert.Equal("Filter is required so test_debug does not start every test under the debugger.", response.Message);
    }
}
