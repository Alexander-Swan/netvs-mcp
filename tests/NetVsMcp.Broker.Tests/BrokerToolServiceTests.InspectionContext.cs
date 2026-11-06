using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DefaultBatchUsesOneFrameRpcAndPreservesSharedIdentity()
    {
        var runtime = CreateRuntime();
        var context = new DebugInspectionContext("exact", 2, 10, 20, 3, 2, "Caller");
        var calls = 0;
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            FrameInspectionHandler = (request, _) =>
            {
                calls++;
                var state = new DebuggerStateInfo("dbgBreakMode", StopId: "exact", Generation: 2);
                return Task.FromResult(new DebugFrameInspectionResult(state, null,
                    request.Expressions.Select(text => new EvaluateExpressionResult(state, new(text, "42", "int", true), context)).ToArray(), null, Context: context));
            }
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugEvalMany(["one", "two"], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal(1, calls);
        Assert.Null(response.Value!.Context);
        Assert.All(response.Value.Results, result => Assert.Null(result.Context));
        Assert.Null(session.LastEvaluateExpressionRequest);
    }

    [Fact]
    public async Task ExplicitBatchIdentityDoesNotFallBackOnOlderExtensions()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod() };
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugEvalMany(["one", "two"], sessionId: "vs-1",
            expectedStopId: "exact");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        Assert.Null(session.LastEvaluateExpressionRequest);
    }

    [Fact]
    public async Task PlainBatchPreservesLegacyExtensionBehavior()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode",
            FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod() };
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugEvalMany(["one", "two"], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal(2, response.Value!.Results.Count);
        Assert.Equal("two", session.LastEvaluateExpressionRequest!.Expression);
        Assert.Null(response.Value.Context);
    }

    [Fact]
    public async Task DefaultBatchJsonHasNoAddedIdentityTokens()
    {
        var runtime = CreateRuntime();
        var context = new DebugInspectionContext("exact", 2, 10, 20, 3, 2, "Caller");
        var state = new DebuggerStateInfo("dbgBreakMode", StopId: "exact", Generation: 2);
        var results = new[] { new EvaluateExpressionResult(state, new("one", "42", "int", true), context) };
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        { FrameInspectionHandler = (_, _) => Task.FromResult(new DebugFrameInspectionResult(state, null, results, null, Context: context)) });
        var compact = await runtime.Tools.DebugEvalMany(["one"], sessionId: "vs-1");
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        var legacy = new DebugEvalManyResult(state, results.Select(r => r with { Context = null }).ToArray());
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(legacy, options), System.Text.Json.JsonSerializer.Serialize(compact.Value, options));
    }

    [Theory]
    [InlineData("stack")]
    [InlineData("watch")]
    [InlineData("watchAdd")]
    public async Task StackAndWatchGuardsUseStoppedFrameRpcAndNeverBypassOlderExtensions(string tool)
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod() };
        runtime.Sessions.Register(CreateRegistration("vs-1", "First"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = tool switch
        {
            "stack" => BoxFrameResult(await runtime.Tools.DebugGetCallstack(sessionId: "vs-1", expectedStopId: "exact", expectedGeneration: 2)),
            "watch" => BoxFrameResult(await runtime.Tools.WatchList(sessionId: "vs-1", expectedStopId: "exact", expectedGeneration: 2)),
            _ => BoxFrameResult(await runtime.Tools.WatchAdd("one", sessionId: "vs-1", expectedStopId: "exact", expectedGeneration: 2))
        };
        Assert.False(response.Success);
        Assert.True(session.UsedStoppedInspectionRpc);
        Assert.Equal("exact", session.LastFrameInspectionRequest!.ExpectedStopId);
        Assert.Equal(2, session.LastFrameInspectionRequest.ExpectedGeneration);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        Assert.Contains("guard was not bypassed", response.Message);
    }
}
