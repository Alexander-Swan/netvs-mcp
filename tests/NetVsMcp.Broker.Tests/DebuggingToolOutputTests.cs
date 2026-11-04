using ModelContextProtocol.Protocol;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace NetVsMcp.Broker.Tests;

public sealed class DebuggingToolOutputTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Theory]
    [InlineData("thread_switch")]
    [InlineData("thread_set_frozen")]
    [InlineData("watch_remove")]
    [InlineData("process_detach")]
    [InlineData("process_terminate")]
    public void SuccessfulCommandsReturnTrueWithoutState(string tool)
    {
        var (node, result) = Output(tool, new { success = true, message = "Completed", state = new { mode = "dbgBreakMode" } });
        Assert.True(node!.GetValue<bool>());
        Assert.Null(result.StructuredContent);
        Assert.False(result.IsError == true);
    }

    [Fact]
    public void ModalEvidenceNeverBecomesSuccessTrue()
    {
        var (node, _) = Output("process_detach", new { success = true, state = new { mode = "dbgModalBlock", operation = new { operationId = "op-1" } } });
        Assert.Equal("dbgModalBlock", node!["state"]!["mode"]!.GetValue<string>());
    }

    [Fact]
    public void FailedOperationUsesMcpErrorAndPreservesRecoveryEvidence()
    {
        var (node, result) = Output("debug_hot_reload_apply", new { success = false, message = "Build failed", errors = new[] { new { file = "Main.cs", line = 4, column = 2, description = "; expected" } } });
        Assert.True(result.IsError);
        Assert.Equal("Build failed", node!["error"]!.GetValue<string>());
        Assert.Equal("Main.cs:4:2", node["errors"]![0]!["location"]!.GetValue<string>());
    }

    [Fact]
    public void WatchesDistinguishUnavailableFromFailureToAdd()
    {
        var (node, result) = Output("watch_add", new WatchOperationResult(true, true, null, new("missing", "Not in scope", null, false)));
        Assert.Equal("Not in scope", node!["$unavailable"]!.GetValue<string>());
        Assert.False(result.IsError == true);
        var (_, failed) = Output("watch_add", new WatchOperationResult(true, false, "Could not add", null));
        Assert.True(failed.IsError);
    }

    [Fact]
    public void LocalsKeepStringsAndDuplicateNamesWithoutDataLoss()
    {
        var state = new DebuggerStateInfo("dbgBreakMode", StopId: "s", Generation: 1);
        var (node, _) = Output("debug_get_locals", new LocalsResult(state,
            [new("a", "001", "int", true), new("order", "{Shop.Order}", "Shop.Order", true), new("bad", "Out of scope", null, false)]));
        Assert.Equal("001", node!["locals"]!["a"]!.GetValue<string>());
        Assert.Equal("{Shop.Order}", node["locals"]!["order"]!["$preview"]!.GetValue<string>());
        Assert.Equal("Out of scope", node["locals"]!["bad"]!["$error"]!.GetValue<string>());
        var (duplicates, _) = Output("debug_get_locals", new LocalsResult(state, [new("a", "1", null, true), new("a", "2", null, true)]));
        Assert.Equal(2, duplicates!["locals"]!.AsArray().Count);
    }

    [Fact]
    public void ModulesGroupProcessesAndOnlyShortenActuallyAdjacentPdbPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "OutputModules");
        ModuleInfo Module(int pid, string path, string? symbols, bool loaded, string status = "notLoaded") =>
            new(pid, new Guid(pid, 0, 0, new byte[8]), Guid.NewGuid(), Path.GetFileName(path), path, "1.0", 100, 500,
                loaded, status, symbols, "Search diagnostics");
        var (node, _) = Output("module_list", new ModuleListResult(true, null,
            [Module(10, Path.Combine(root, "App.dll"), Path.Combine(root, "App.pdb"), true),
             Module(10, Path.Combine(root, "Lib.dll"), "C:/Symbols/Lib.pdb", true),
             Module(20, Path.Combine(root, "Worker.dll"), null, false)]), Path.Combine(root, "App.slnx"));
        var processes = node!["processes"]!.AsArray();
        Assert.Equal(2, processes.Count);
        Assert.Equal(".pdb", processes[0]!["modules"]![0]!["symbols"]!.GetValue<string>());
        Assert.Equal("App.dll", processes[0]!["modules"]![0]!["path"]!.GetValue<string>());
        Assert.Equal("C:/Symbols/Lib.pdb", processes[0]!["modules"]![1]!["symbols"]!.GetValue<string>());
        Assert.Equal("notLoaded", processes[1]!["modules"]![0]!["symbols"]!.GetValue<string>());
        Assert.Null(processes[0]!["modules"]![0]!["processId"]);
        Assert.Null(processes[0]!["modules"]![0]!["size"]);
        Assert.Equal("Search diagnostics", processes[0]!["modules"]![0]!["symbolLoadInformation"]!.GetValue<string>());
    }

    [Fact]
    public void ExpansionReturnsPlainObjectAndPreservesContinuationWhenIncomplete()
    {
        var root = new DebugValueNode("order", "Order", "{Order}", true, [],
            [new("Id", "int", "10", true, [1], [], null, null), new("Total", "decimal", "100.01", true, [2], [], null, null)], null, null);
        var (node, _) = Output("debug_expand", new DebugExpansionResult(root, 3, 2, false));
        Assert.Equal("10", node!["Id"]!.GetValue<string>());
        Assert.Equal("100.01", node["Total"]!.GetValue<string>());
        Assert.Equal(2, node.AsObject().Count);
        var (partial, _) = Output("debug_expand", new DebugExpansionResult(root with { NextOffset = 2, TruncationReason = "maxChildren" }, 3, 2, false));
        Assert.Equal(2, partial!["$expansion"]!["continuations"]![0]!["nextOffset"]!.GetValue<int>());
        Assert.Null(partial["$expansion"]!["root"]);
        Assert.Equal("10", partial["$value"]!["Id"]!.GetValue<string>());
    }

    [Fact]
    public void BreakpointGroupsPreserveTracepointsAndExactIdentifiers()
    {
        var bp = new BreakpointInfo("C:/App/Main.cs, line 10", "C:/App/Main.cs", 10, 1, "Main", "count > 0", true,
            "Print", "count={count}", true, GroupName: "Trace");
        var (node, _) = Output("breakpoint_group_list", new BreakpointGroupListResult(["Trace"], [bp]));
        var trace = node!["Trace"]![0]!;
        Assert.Equal(bp.Name, trace["name"]!.GetValue<string>());
        Assert.Equal("Print", trace["action"]!.GetValue<string>());
        Assert.True(trace["continueAfterAction"]!.GetValue<bool>());
        Assert.Null(trace["location"]);
        Assert.Null(trace["groupName"]);
    }

    [Fact]
    public void DiagnosticsGroupBySeverityAndKeepDistinctSourcePositions()
    {
        var (node, _) = Output("errors_list", new ErrorListResult(
            [new("Missing ;", "Main.cs", 10, 2, "Error", "App"), new("Possible null", "Other.cs", 3, 8, "Warning", "App")]));
        Assert.Equal("Main.cs:10:2", node!["errors"]![0]!["position"]!.GetValue<string>());
        Assert.Equal("Possible null", node["warnings"]![0]!["description"]!.GetValue<string>());
        Assert.Null(node["errors"]![0]!["level"]);
        Assert.Null(node["messages"]);
    }

    [Fact]
    public void IdeDiagnosticsKeepSeverityInErrorListsAndBuildResults()
    {
        var diagnostics = new ErrorListResult([
            new("Undefined name", "Probe.cs", 3, 1, "vsBuildErrorLevelHigh", "App"),
            new("Probe warning", "Probe.cs", 4, 1, "vsBuildErrorLevelMedium", "App"),
            new("Build message", null, 0, 0, "vsBuildErrorLevelLow", "App")]);
        var (errors, _) = Output("errors_list", diagnostics);
        var (build, _) = Output("build_and_get_errors", new BuildAndGetErrorsResult(
            new(new("Idle", 1), 1, "build-1", "failed"), diagnostics));
        foreach (var node in new[] { errors, build })
        {
            Assert.Equal("Undefined name", node!["errors"]![0]!["description"]!.GetValue<string>());
            Assert.Equal("Probe warning", node["warnings"]![0]!["description"]!.GetValue<string>());
            Assert.Equal("Build message", node["messages"]![0]!["description"]!.GetValue<string>());
        }
        Assert.Equal(1, build!["build"]!["lastBuildInfo"]!.GetValue<int>());
    }

    [Fact]
    public void CompletedBuildKeepsLastBuildInfoAndAutomationKeepsActiveMethod()
    {
        var (build, _) = Output("build_and_get_errors", new BuildAndGetErrorsResult(
            new(new("Idle", 0), 0, "build-1", "completed"), new([])));
        Assert.Equal(0, build!["build"]!["lastBuildInfo"]!.GetValue<int>());
        var (automation, _) = Output("session_automation_control", new SessionAutomationControl("vs-1", false, "EditorReplaceAsync"));
        Assert.False(automation!["paused"]!.GetValue<bool>());
        Assert.Equal("EditorReplaceAsync", automation["activeMethod"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("test_coverage_run")]
    [InlineData("test_coverage_results")]
    [InlineData("test_coverage_file")]
    public void CoveragePathsRemainExactRetainedHistoryKeys(string tool)
    {
        const string path = @"C:\Fixture\RegressionCoverage\CoverageProbe.cs";
        var (node, _) = Output(tool, new { runId = "run-1", path,
            coverage = new { files = new[] { new { path, lines = new[] { new { number = 3, hits = 1 } } } } } }, @"C:\Fixture\App.slnx");
        Assert.Equal(path, node!["path"]!.GetValue<string>());
        Assert.Equal(path, node["coverage"]!["files"]![0]!["path"]!.GetValue<string>());
        Assert.Equal(1, node["coverage"]!["files"]![0]!["lines"]!["3"]!.GetValue<int>());
    }

    [Fact]
    public void EventsInheritBatchIdentityOnlyWhenItMatches()
    {
        var (node, _) = Output("vs_events", new VsEventBatch("vs-1", "epoch", 2, false,
            [new("vs-1", "epoch", 1, "debugger", DateTimeOffset.UtcNow), new("other", "other-epoch", 2, "debugger", DateTimeOffset.UtcNow)]));
        Assert.Null(node!["events"]![0]!["sessionId"]);
        Assert.Null(node["events"]![0]!["epoch"]);
        Assert.Equal("other", node["events"]![1]!["sessionId"]!.GetValue<string>());
        Assert.Null(node["requiresReconciliation"]);
    }

    [Fact]
    public void SnapshotInheritsBrokerRoutingIdentityButNeverAChangedPause()
    {
        var state = new DebuggerStateInfo("dbgBreakMode", SessionId: "vs-1", StopId: "s", Generation: 1,
            EventCursor: new("vs-1", "epoch", 10));
        var child = state with { SessionId = null, EventCursor = null };
        var (node, _) = Output("debug_snapshot", new DebugSnapshotResult(state, new(child, []), new(child with { StopId = "other" }, []), null));
        Assert.Null(node!["callStack"]!["state"]);
        Assert.Equal("other", node["locals"]!["state"]!["stopId"]!.GetValue<string>());
    }

    [Fact]
    public void SameNumericPidOnDifferentTargetsKeepsDistinctProcessIdentity()
    {
        var first = new ModuleInfo(10, Guid.NewGuid(), Guid.NewGuid(), "App.dll", "App.dll", null, 0, 0, false, "notLoaded", null, null);
        var second = first with { ProcessUniqueId = Guid.NewGuid(), Path = "Other.dll" };
        var (node, _) = Output("module_list", new ModuleListResult(true, null, [first, second]));
        var processes = node!["processes"]!.AsArray();
        Assert.Equal(2, processes.Count);
        Assert.NotEqual(processes[0]!["processUniqueId"]!.GetValue<string>(), processes[1]!["processUniqueId"]!.GetValue<string>());
    }

    [Fact]
    public void LaunchIdsAreObservedNotInvented()
    {
        var (single, _) = Output("debug_start_without_debugging", new DebuggerStateInfo("dbgDesignMode", LaunchedProcessIds: [123]));
        Assert.Equal(123, single!.GetValue<int>());
        var (many, _) = Output("debug_start_without_debugging", new DebuggerStateInfo("dbgDesignMode", LaunchedProcessIds: [123, 456]));
        Assert.Equal(2, many!.AsArray().Count);
        var (unknown, _) = Output("debug_start_without_debugging", new DebuggerStateInfo("dbgDesignMode"));
        Assert.True(unknown!["processIdUnavailable"]!.GetValue<bool>());
    }

    [Fact]
    public void EveryProjectedToolDocumentsItsOutputAndDebuggerInputsHaveNoCompactFlag()
    {
        foreach (var method in typeof(BrokerToolService).GetMethods())
        {
            var tool = method.GetCustomAttribute<McpServerToolAttribute>();
            if (tool is null || !DebuggingToolOutput.Handles(tool.Name)) continue;
            var description = method.GetCustomAttribute<DescriptionAttribute>()!.Description;
            Assert.Contains("Output:", description);
            Assert.Contains("isError", description);
            Assert.DoesNotContain(method.GetParameters(), p => p.Name == "compact");
        }
    }

    private static (JsonNode? Node, CallToolResult Result) Output<T>(string tool, T payload, string? solution = null)
    {
        var envelope = ToolResponse<T>.Ok(payload);
        var text = new TextContentBlock { Text = JsonSerializer.Serialize(envelope, Json) };
        var result = new CallToolResult { Content = [text], StructuredContent = JsonSerializer.SerializeToElement(envelope, Json) };
        DebuggingToolOutput.Apply(result, tool, solution);
        var node = JsonNode.Parse(text.Text);
        if (node is JsonObject)
            Assert.True(JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(result.StructuredContent)));
        else Assert.Null(result.StructuredContent);
        return (node, result);
    }
}
