using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData("locals")]
    [InlineData("evaluate")]
    [InlineData("many")]
    [InlineData("snapshot")]
    [InlineData("wait")]
    [InlineData("watch")]
    [InlineData("watchAdd")]
    [InlineData("assignment")]
    [InlineData("immediate")]
    public async Task FrameInspection_OmittedFrameUsesExistingRpcEvenWithAnOlderVsix(string tool)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusMode = "dbgBreakMode",
            FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod()
        };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await InvokeFrameInspectionTool(runtime, tool, null);

        Assert.True(response.Success, response.Message);
        Assert.Null(session.LastFrameInspectionRequest);
        Assert.Null(session.LastDebugSelectFrameRequest);
    }

    [Theory]
    [InlineData("locals")]
    [InlineData("evaluate")]
    [InlineData("many")]
    [InlineData("snapshot")]
    [InlineData("wait")]
    [InlineData("watch")]
    [InlineData("watchAdd")]
    [InlineData("assignment")]
    [InlineData("immediate")]
    public async Task FrameInspection_ExplicitFrameUsesOneScopedRpc(string tool)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await InvokeFrameInspectionTool(runtime, tool, 2);

        Assert.True(response.Success, response.Message);
        Assert.Equal(2, session.LastFrameInspectionRequest!.StackFrame);
        Assert.Null(session.LastDebugSelectFrameRequest);
        if (tool == "watchAdd")
        {
            Assert.Equal("count", session.LastFrameInspectionRequest.AddWatchExpression);
            Assert.Equal("count", Assert.IsType<WatchOperationResult>(response.Value).Watch!.Name);
        }
        if (tool == "assignment")
        {
            Assert.Equal("count = 7", Assert.Single(session.LastFrameInspectionRequest.Expressions));
            Assert.True(Assert.IsType<DebugSetVariableResult>(response.Value).Success);
        }
        if (tool == "immediate")
            Assert.Equal("42 (int)", Assert.IsType<ImmediateExecuteResult>(response.Value).Output);
        if (tool == "watch")
            Assert.NotNull(session.LastFrameInspectionRequest.Watch);
        if (tool is "snapshot" or "wait")
        {
            Assert.True(session.LastFrameInspectionRequest.IncludeLocals);
            Assert.NotNull(session.LastFrameInspectionRequest.Watch);
            var snapshot = Assert.IsType<DebugSnapshotResult>(response.Value);
            Assert.Equal("callerLocal", Assert.Single(snapshot.Locals!.Locals).Name);
            Assert.Equal("callerWatch", Assert.Single(snapshot.Watch!.Watches).Name);
        }
    }

    [Theory]
    [InlineData("locals")]
    [InlineData("evaluate")]
    [InlineData("many")]
    [InlineData("snapshot")]
    [InlineData("wait")]
    [InlineData("watch")]
    [InlineData("watchAdd")]
    [InlineData("assignment")]
    [InlineData("immediate")]
    public async Task FrameInspection_OlderVsixGivesGuidanceWithoutInspectingTheWrongFrame(string tool)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusMode = "dbgBreakMode",
            FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod()
        });

        var response = await InvokeFrameInspectionTool(runtime, tool, 2);

        Assert.False(response.Success);
        Assert.Null(response.Value);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        Assert.Contains("Omit stackFrame", response.Message);
        Assert.Equal(response.Message, response.Metadata["nextActions"]);
        Assert.DoesNotContain("RPC", response.Message);
    }

    [Theory]
    [InlineData("locals")]
    [InlineData("evaluate")]
    [InlineData("many")]
    [InlineData("snapshot")]
    [InlineData("wait")]
    [InlineData("watch")]
    [InlineData("watchAdd")]
    [InlineData("assignment")]
    [InlineData("immediate")]
    public async Task FrameInspection_InvalidFrameIsRejectedBeforeRouting(string tool)
    {
        foreach (var index in new[] { 0, -1 })
        {
            var response = await InvokeFrameInspectionTool(CreateRuntime(), tool, index);
            Assert.False(response.Success);
            Assert.Contains("stackFrame must be 1 or greater", response.Message);
        }
    }

    [Fact]
    public async Task FrameInspection_EvalManyForwardsTheWholeBatchAndTimeout()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugEvalMany([" one ", "two", "one"], timeoutMilliseconds: 750, sessionId: "vs-1", stackFrame: 3);

        Assert.True(response.Success);
        Assert.Equal(new[] { "one", "two" }, session.LastFrameInspectionRequest!.Expressions);
        Assert.Equal(750, session.LastFrameInspectionRequest.TimeoutMilliseconds);
        Assert.Equal(2, response.Value!.Results.Count);
    }

    [Fact]
    public async Task FrameInspection_DoesNotReclassifyOtherFailuresAsMissingSupport()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            FrameInspectionHandler = (_, _) => throw new InvalidOperationException("The debugger must be paused.")
        });
        var response = await runtime.Tools.DebugGetLocals(sessionId: "vs-1", stackFrame: 2);
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.RpcFailure, response.Metadata!["error_code"]);
        Assert.Contains("must be paused", response.Message);
    }

    [Fact]
    public async Task FrameSelection_OlderVsixGetsFriendlyGuidance()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugSelectFrameHandler = (_, _) => throw MissingFrameInspectionMethod()
        });
        var response = await runtime.Tools.DebugSelectFrame(2, sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        Assert.Contains("Continue inspecting the current frame", response.Message);
    }

    private static RemoteMethodNotFoundException MissingFrameInspectionMethod() =>
        (RemoteMethodNotFoundException)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RemoteMethodNotFoundException));

    private static async Task<ToolResponse<object>> InvokeFrameInspectionTool(BrokerRuntime runtime, string tool, int? stackFrame)
    {
        return tool switch
        {
            "locals" => BoxFrameResult(await runtime.Tools.DebugGetLocals(sessionId: "vs-1", stackFrame: stackFrame)),
            "evaluate" => BoxFrameResult(await runtime.Tools.DebugEvaluate("count", sessionId: "vs-1", stackFrame: stackFrame)),
            "many" => BoxFrameResult(await runtime.Tools.DebugEvalMany(["count", "count + 1"], sessionId: "vs-1", stackFrame: stackFrame)),
            "snapshot" => BoxFrameResult(await runtime.Tools.DebugSnapshot(include: ["locals", "watch"], sessionId: "vs-1", stackFrame: stackFrame)),
            "wait" => BoxFrameResult(await runtime.Tools.DebugWaitForBreak(include: ["locals", "watch"], sessionId: "vs-1", stackFrame: stackFrame)),
            "watch" => BoxFrameResult(await runtime.Tools.WatchList(sessionId: "vs-1", stackFrame: stackFrame)),
            "watchAdd" => BoxFrameResult(await runtime.Tools.WatchAdd("count", sessionId: "vs-1", stackFrame: stackFrame)),
            "assignment" => BoxFrameResult(await runtime.Tools.DebugSetVariable("count", "7", sessionId: "vs-1", stackFrame: stackFrame)),
            "immediate" => BoxFrameResult(await runtime.Tools.ImmediateExecute("count", sessionId: "vs-1", stackFrame: stackFrame)),
            _ => throw new ArgumentException(tool)
        };
    }

    private static ToolResponse<object> BoxFrameResult<T>(ToolResponse<T> response) =>
        new(response.Success, response.Value, response.Message, response.Metadata);
}
