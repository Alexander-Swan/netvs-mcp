using System.Text.Json;
using ModelContextProtocol.Protocol;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class CompactDebuggerOutputTests
{
    private static readonly DebuggerStateInfo State = new("dbgBreakMode", BreakReason: "breakpoint", Outcome: "stopped",
        Generation: 4, StopId: "session-epoch:4:12", ExecutionSequence: 9);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    [Fact]
    public void DefaultCompositeRetainsEveryLegacyField()
    {
        var snapshot = new DebugSnapshotResult(State, new(State, [], true), new(State, []), null);
        var response = ToolResponse<DebugSnapshotResult>.Ok(snapshot);
        var shaped = BrokerToolService.ShapeInspectionOutput(response, false);
        Assert.Equal(JsonSerializer.Serialize(response, Json), JsonSerializer.Serialize(shaped, Json));
    }

    [Fact]
    public void SnapshotOutputKeepsStateOnceWithoutMutatingOriginal()
    {
        var snapshot = new DebugSnapshotResult(State, new(State, [], true), new(State, []), null);
        var response = ToolResponse<DebugSnapshotResult>.Ok(snapshot);
        var compact = BrokerToolService.ShapeInspectionOutput(response, false).Value!;
        Assert.Equal(State, compact.State);
        Assert.Equal(State.Mode, compact.CallStack!.State.Mode);
        Assert.Equal(State, compact.CallStack.State);
        Assert.Equal(State, compact.Locals!.State);
        Assert.True(compact.CallStack.HasMore);
        Assert.Equal(State.StopId, snapshot.CallStack!.State.StopId);
        var output = Output(compact);
        Assert.Equal(State.StopId, output.GetProperty("state").GetProperty("stopId").GetString());
        Assert.False(output.GetProperty("callStack").TryGetProperty("state", out _));
        Assert.False(output.GetProperty("locals").TryGetProperty("state", out _));
    }

    [Fact]
    public void CompactBatchNeverHidesAChangedPauseOrModalEvidence()
    {
        var other = State with { StopId = "another-pause" };
        var modal = State with { Mode = "dbgModalBlock", UnderlyingMode = "dbgBreakMode", UnderlyingModeIsStale = true };
        var batch = new DebugEvalManyResult(State, [new(State, new("a", "1", "int", true)),
            new(other, new("b", "2", "int", true)), new(modal, new("c", "blocked", null, false))]);
        var compact = BrokerToolService.ShapeInspectionOutput(ToolResponse<DebugEvalManyResult>.Ok(batch), false).Value!;
        var rows = compact.Results.ToArray();
        Assert.Equal(State, compact.State);
        Assert.Equal(State, rows[0].State);
        Assert.Equal(other, rows[1].State);
        Assert.Equal(modal, rows[2].State);
        Assert.False(rows[2].Expression.IsValidValue);
        var sameModal = BrokerToolService.ShapeInspectionOutput(ToolResponse<DebugEvalManyResult>.Ok(
            new(modal, [new(modal, new("blocked", null, null, false))])), false).Value!;
        Assert.Equal(modal, Assert.Single(sameModal.Results).State);
        var output = Output(compact).GetProperty("results");
        Assert.False(output[0].TryGetProperty("state", out _));
        Assert.Equal(other.StopId, output[1].GetProperty("state").GetProperty("stopId").GetString());
        Assert.Equal("dbgModalBlock", output[2].GetProperty("state").GetProperty("mode").GetString());
        Assert.False(Output(sameModal).GetProperty("results")[0].TryGetProperty("state", out _));
    }

    [Fact]
    public void CompactSnapshotInheritsCursorButPreservesDifferentObservations()
    {
        var observed = State with { EventCursor = new("vs-1", "epoch", 12) };
        var different = State with { EventCursor = new("vs-1", "epoch", 13) };
        var snapshot = new DebugSnapshotResult(observed, new(State, []), new(different, []), null);
        var output = Output(snapshot);
        Assert.Equal(12, output.GetProperty("state").GetProperty("eventCursor").GetProperty("watermark").GetInt64());
        Assert.False(output.GetProperty("callStack").TryGetProperty("state", out _));
        Assert.Equal(13, output.GetProperty("locals").GetProperty("state").GetProperty("eventCursor").GetProperty("watermark").GetInt64());
    }

    [Fact]
    public void CompactFilesAreSolutionRelativeWithoutShorteningExternalOrUnknownPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "CompactPaths");
        var source = Path.Combine(root, "Project", "Program.cs");
        var external = Path.Combine(root + "Sibling", "External.cs");
        var snapshot = new DebugSnapshotResult(State, new(State,
            [new("Main", source, 5, 0, 1, true), new("External", external, 3, 0), new("Native", null, 0, 0)]), null, null);
        var solution = Path.Combine(root, "Example.slnx");
        var frames = snapshot.CallStack!.Frames.ToArray();
        Assert.Equal("Project/Program.cs", BrokerToolService.CompactSourcePath(frames[0].File, solution));
        Assert.Equal(5, frames[0].Line);
        Assert.Equal(1, frames[0].FrameIndex);
        Assert.Equal(external, frames[1].File);
        Assert.Null(frames[2].File);
        Assert.Equal(source, snapshot.CallStack!.Frames.First().File);
    }

    [Fact]
    public void FrameInspectionDeduplicatesAllSections()
    {
        var locals = new LocalsResult(State, [new("count", "5", "int", true)]);
        var frame = new DebugFrameInspectionResult(State, locals,
            [new(State, new("count", "5", "int", true))], null, new(State, []));
        var output = Output(frame);
        Assert.False(output.GetProperty("locals").TryGetProperty("state", out _));
        Assert.False(output.GetProperty("callStack").TryGetProperty("state", out _));
        Assert.False(output.GetProperty("results")[0].TryGetProperty("state", out _));
        Assert.Equal("5", output.GetProperty("locals").GetProperty("locals")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void PlainTextAndStandaloneStateRemainUnchanged()
    {
        var text = new TextContentBlock { Text = "debugger unavailable" };
        var result = new CallToolResult { Content = [text] };
        DebuggerResponseOutput.DeduplicateState(result);
        Assert.Equal("debugger unavailable", text.Text);
        Assert.Equal(State.Mode, Output(new LocalsResult(State, [])).GetProperty("state").GetProperty("mode").GetString());
    }

    private static JsonElement Output<T>(T value)
    {
        var json = JsonSerializer.Serialize(ToolResponse<T>.Ok(value), Json);
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            StructuredContent = JsonSerializer.SerializeToElement(ToolResponse<T>.Ok(value), Json)
        };
        DebuggerResponseOutput.DeduplicateState(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        using var document = JsonDocument.Parse(text);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(text),
            System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(result.StructuredContent))));
        return document.RootElement.GetProperty("value").Clone();
    }
}
