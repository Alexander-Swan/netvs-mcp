using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerToolServiceTests
{
    [Fact]
    public void VsListSessions_ReturnsRegisteredSessions()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsListSessions();

        Assert.True(response.Success);
        Assert.Single(response.Value!);
        Assert.Equal("NetVsMcp", response.Value!.Single().SolutionName);
    }

    [Fact]
    public void VsGetCapabilities_ReturnsInitialBrokerTools()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.VsGetCapabilities();

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Tools, tool => tool.Name == "vs_list_sessions");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_status");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_capabilities");
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_get_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_select_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_ping", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_active", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_document_symbols", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_solution", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_status", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "errors_list", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "output_read", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_status", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_step", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_set", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_evaluate", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_read", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_write", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_preview", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_list_pending", RequiresVisualStudioSession: true });
        Assert.All(
            response.Value.Tools.Where(tool => tool.Name.StartsWith("vs_", StringComparison.Ordinal)),
            tool => Assert.False(tool.RequiresVisualStudioSession));
    }

    [Fact]
    public void VsGetSession_SelectsBySessionId()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other"));

        var response = runtime.Tools.VsGetSession(sessionId: "vs-2");

        Assert.True(response.Success);
        Assert.Equal("vs-2", response.Value!.Session.SessionId);
        Assert.Equal(SessionHealth.Connected, response.Value.Health);
    }

    [Fact]
    public void VsGetSession_SelectsByNormalizedSolutionPath()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.slnx", isActive: false));

        var response = runtime.Tools.VsGetSession(solutionPath: @"c:/code/NetVsMcp/../NetVsMcp/NetVsMcp.slnx");

        Assert.True(response.Success);
        Assert.Equal("vs-1", response.Value!.Session.SessionId);
    }

    [Fact]
    public void VsSelectSession_SelectsBySolutionName()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other"));

        var response = runtime.Tools.VsSelectSession(solutionName: "Other");

        Assert.True(response.Success);
        Assert.Equal("vs-2", response.Value!.SessionId);
        Assert.Equal("Other", response.Value.SolutionName);
    }

    [Fact]
    public void VsSelectSession_ReturnsAmbiguousFailureWithCandidates()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.slnx", isActive: false));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.slnx", isActive: false));

        var response = runtime.Tools.VsSelectSession(solutionName: "Shared");

        Assert.False(response.Success);
        Assert.Equal("Ambiguous", response.Metadata!["failureReason"]);
        Assert.Equal("2", response.Metadata["candidateCount"]);
        Assert.Equal("vs-1,vs-2", response.Metadata["candidateSessionIds"]);
    }

    [Fact]
    public void VsGetSession_ReturnsNoSessionsFailure()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.VsGetSession();

        Assert.False(response.Success);
        Assert.Equal("NoRegisteredSessions", response.Metadata!["failureReason"]);
    }

    [Fact]
    public void VsPing_ReturnsBrokerHealthWithoutTarget()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsPing();

        Assert.True(response.Success);
        Assert.Equal(BrokerOptions.LocalDefault.McpEndpoint, response.Value!.McpEndpoint);
        Assert.Equal(1, response.Value.RegisteredSessionCount);
        Assert.Null(response.Value.TargetSession);
    }

    [Fact]
    public void VsPing_ReturnsTargetStatus_WhenTargetIsSupplied()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.VsPing(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("vs-1", response.Value!.TargetSession!.Session.SessionId);
    }

    [Fact]
    public async Task DocumentActive_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DocumentActive(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Editor.cs", response.Value);
    }

    [Fact]
    public async Task CodeDocumentSymbols_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeDocumentSymbols(
            documentPath: @"C:\Code\NetVsMcp\Editor.cs",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(@"C:\Code\NetVsMcp\Editor.cs", session.LastSymbolsDocumentPath);
        Assert.Equal(["Editor", "Editor.Run"], response.Value);
    }

    [Fact]
    public async Task DocumentActive_ReturnsAmbiguousFailureWithCandidates()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.slnx", isActive: false));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.slnx", isActive: false));

        var response = await runtime.Tools.DocumentActive(solutionName: "Shared");

        Assert.False(response.Success);
        Assert.Equal("AmbiguousTarget", response.Metadata!["failureReason"]);
        Assert.Equal("2", response.Metadata["candidateCount"]);
        Assert.Equal("vs-1,vs-2", response.Metadata["candidateSessionIds"]);
    }

    [Fact]
    public async Task DocumentActive_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.DocumentActive(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

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
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(42, session.LastBreakpointSetRequest!.Line);
        Assert.Equal("count > 0", session.LastBreakpointSetRequest.Condition);
        Assert.Equal("bp-1", response.Value!.Name);
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
    public async Task CodeDocumentSymbols_RequiresDocumentPath()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.CodeDocumentSymbols("");

        Assert.False(response.Success);
        Assert.Equal("Document path is required.", response.Message);
    }

    [Fact]
    public async Task DocumentRead_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentRead("Program.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastDocumentReadRequest!.Path);
        Assert.Equal("class Program {}", response.Value!.Text);
    }

    [Fact]
    public async Task DocumentOpen_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentOpen("Program.cs", solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastDocumentOpenRequest!.Path);
        Assert.Equal("Program.cs", response.Value!.Name);
    }

    [Fact]
    public async Task SelectionGet_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SelectionGet(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("selected", response.Value!.Text);
    }

    [Fact]
    public async Task DocumentWrite_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentWrite(
            path: "Program.cs",
            text: "new text",
            createIfMissing: true,
            saveAfterWrite: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("new text", session.LastDocumentWriteRequest!.Text);
        Assert.True(session.LastDocumentWriteRequest.CreateIfMissing);
        Assert.True(response.Value!.Saved);
    }

    [Fact]
    public async Task EditorInsert_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditorInsert("Program.cs", 3, 4, "hello", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(3, session.LastEditorInsertRequest!.Line);
        Assert.Equal("hello", session.LastEditorInsertRequest.Text);
    }

    [Fact]
    public async Task EditorReplace_ValidatesRange()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.EditorReplace("Program.cs", 5, 1, 4, 1, "x");

        Assert.False(response.Success);
        Assert.Equal("End position must be greater than or equal to start position.", response.Message);
    }

    [Fact]
    public async Task EditorReplace_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditorReplace("Program.cs", 1, 1, 1, 5, "class", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(5, session.LastEditorReplaceRequest!.EndColumn);
        Assert.Equal("class", session.LastEditorReplaceRequest.Text);
    }

    [Fact]
    public async Task EditorGotoLine_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditorGotoLine("Program.cs", 12, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(12, session.LastEditorGotoLineRequest!.Line);
    }

    [Fact]
    public async Task SelectionSet_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SelectionSet("Program.cs", 1, 1, 2, 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(2, session.LastSelectionSetRequest!.EndLine);
        Assert.False(response.Value!.IsEmpty);
    }

    [Fact]
    public async Task DocumentCleanup_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentCleanup("Program.cs", saveAfterCleanup: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastDocumentCleanupRequest!.SaveAfterCleanup);
        Assert.Equal("Edit.FormatDocument", response.Value!.Command);
    }

    [Fact]
    public async Task EditPreview_ValidatesOperation()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.EditPreview("delete", "Program.cs", "x");

        Assert.False(response.Success);
        Assert.Equal("Edit operation must be one of: write, insert, replace.", response.Message);
    }

    [Fact]
    public async Task EditPreview_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditPreview(
            operation: "replace",
            path: "Program.cs",
            text: "replacement",
            startLine: 1,
            startColumn: 1,
            endLine: 1,
            endColumn: 5,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("replace", session.LastEditPreviewRequest!.Operation);
        Assert.Equal("edit-1", response.Value!.PendingEdit!.EditId);
    }

    [Fact]
    public async Task EditApprove_RequiresEditId()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.EditApprove("");

        Assert.False(response.Success);
        Assert.Equal("Edit id is required.", response.Message);
    }

    [Fact]
    public async Task EditApprove_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditApprove("edit-1", saveAfterApply: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastEditApproveRequest!.SaveAfterApply);
        Assert.True(response.Value!.Applied);
    }

    [Fact]
    public async Task EditReject_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditReject("edit-1", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("edit-1", session.LastEditRejectRequest!.EditId);
        Assert.False(response.Value!.Applied);
    }

    [Fact]
    public async Task EditListPending_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.EditListPending(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("edit-1", Assert.Single(response.Value!.PendingEdits).EditId);
    }

    [Fact]
    public async Task DocumentRead_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.DocumentRead("Program.cs", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
    }

    [Fact]
    public async Task BuildSolution_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildSolution(
            waitForBuildToFinish: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.Equal("Done", response.Value!.Status.State);
        Assert.Equal(0, response.Value.LastBuildInfo);
    }

    [Fact]
    public async Task BuildStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BuildStatus(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Idle", response.Value!.State);
    }

    [Fact]
    public async Task ErrorsList_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ErrorsList(
            includeWarnings: false,
            maxItems: 25,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.False(session.LastErrorListRequest!.IncludeWarnings);
        Assert.Equal(25, session.LastErrorListRequest.MaxItems);
        var item = Assert.Single(response.Value!.Items);
        Assert.Equal("Build failed.", item.Description);
        Assert.Equal("Error", item.Level);
    }

    [Fact]
    public async Task OutputRead_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OutputRead(
            paneName: "Build",
            maxChars: 100,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Build", session.LastOutputReadRequest!.PaneName);
        Assert.Equal(100, session.LastOutputReadRequest.MaxChars);
        Assert.Equal("Build output", response.Value!.Text);
    }

    [Fact]
    public async Task BuildStatus_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.BuildStatus(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

    private static BrokerRuntime CreateRuntime()
    {
        return new BrokerRuntime(BrokerOptions.LocalDefault, new SessionRegistry());
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return CreateRegistration(
            sessionId,
            solutionName,
            $@"C:\Code\{solutionName}\{solutionName}.slnx",
            isActive: true);
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        string solutionPath,
        bool isActive)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: 1234,
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: solutionPath,
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: isActive,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private sealed class FakeVisualStudioSessionRpc : IVisualStudioSessionRpc
    {
        private readonly string _activeDocument;

        public FakeVisualStudioSessionRpc(string activeDocument)
        {
            _activeDocument = activeDocument;
        }

        public string? LastSymbolsDocumentPath { get; private set; }

        public DocumentReadRequest? LastDocumentReadRequest { get; private set; }

        public DocumentOpenRequest? LastDocumentOpenRequest { get; private set; }

        public DocumentWriteRequest? LastDocumentWriteRequest { get; private set; }

        public EditorInsertRequest? LastEditorInsertRequest { get; private set; }

        public EditorReplaceRequest? LastEditorReplaceRequest { get; private set; }

        public EditorGotoLineRequest? LastEditorGotoLineRequest { get; private set; }

        public SelectionSetRequest? LastSelectionSetRequest { get; private set; }

        public DocumentCleanupRequest? LastDocumentCleanupRequest { get; private set; }

        public EditPreviewRequest? LastEditPreviewRequest { get; private set; }

        public EditDecisionRequest? LastEditApproveRequest { get; private set; }

        public EditDecisionRequest? LastEditRejectRequest { get; private set; }

        public BuildSolutionRequest? LastBuildSolutionRequest { get; private set; }

        public ErrorListRequest? LastErrorListRequest { get; private set; }

        public OutputReadRequest? LastOutputReadRequest { get; private set; }

        public DebugStepRequest? LastDebugStepRequest { get; private set; }

        public BreakpointSetRequest? LastBreakpointSetRequest { get; private set; }

        public BreakpointRemoveRequest? LastBreakpointRemoveRequest { get; private set; }

        public BreakpointEnableRequest? LastBreakpointEnableRequest { get; private set; }

        public EvaluateExpressionRequest? LastEvaluateExpressionRequest { get; private set; }

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<string?>.Ok(_activeDocument));
        }

        public Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
            string documentPath,
            CancellationToken cancellationToken)
        {
            LastSymbolsDocumentPath = documentPath;
            IReadOnlyCollection<string> symbols = ["Editor", "Editor.Run"];
            return Task.FromResult(ToolResponse<IReadOnlyCollection<string>>.Ok(symbols));
        }

        public Task<DocumentReadResult> DocumentReadAsync(
            DocumentReadRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentReadRequest = request;
            return Task.FromResult(new DocumentReadResult(CreateDocument(request.Path), "class Program {}", "live", true));
        }

        public Task<EditorDocumentInfo> DocumentOpenAsync(
            DocumentOpenRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentOpenRequest = request;
            return Task.FromResult(CreateDocument(request.Path));
        }

        public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<SelectionInfo?>(CreateSelection());
        }

        public Task<DocumentMutationResult> DocumentWriteAsync(
            DocumentWriteRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentWriteRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterWrite, request.Text.Length));
        }

        public Task<DocumentMutationResult> DocumentSaveAsync(
            DocumentSaveRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateMutation(string.IsNullOrWhiteSpace(request.Path) ? "Program.cs" : request.Path, true, 0));
        }

        public Task<DocumentMutationResult> EditorInsertAsync(
            EditorInsertRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorInsertRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterEdit, request.Text.Length));
        }

        public Task<DocumentMutationResult> EditorReplaceAsync(
            EditorReplaceRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorReplaceRequest = request;
            return Task.FromResult(CreateMutation(request.Path, request.SaveAfterEdit, request.Text.Length));
        }

        public Task<EditorDocumentInfo> EditorGotoLineAsync(
            EditorGotoLineRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorGotoLineRequest = request;
            return Task.FromResult(CreateDocument(request.Path));
        }

        public Task<SelectionInfo> SelectionSetAsync(
            SelectionSetRequest request,
            CancellationToken cancellationToken)
        {
            LastSelectionSetRequest = request;
            return Task.FromResult(CreateSelection());
        }

        public Task<DocumentCleanupResult> DocumentCleanupAsync(
            DocumentCleanupRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentCleanupRequest = request;
            return Task.FromResult(new DocumentCleanupResult(
                true,
                true,
                null,
                CreateDocument(request.Path),
                request.SaveAfterCleanup,
                "Edit.FormatDocument"));
        }

        public Task<EditPreviewResult> EditPreviewAsync(
            EditPreviewRequest request,
            CancellationToken cancellationToken)
        {
            LastEditPreviewRequest = request;
            return Task.FromResult(new EditPreviewResult(true, null, CreatePendingEdit(request.Operation, request.Path, request.Text)));
        }

        public Task<EditDecisionResult> EditApproveAsync(
            EditDecisionRequest request,
            CancellationToken cancellationToken)
        {
            LastEditApproveRequest = request;
            var pending = CreatePendingEdit("replace", "Program.cs", "replacement");
            return Task.FromResult(new EditDecisionResult(
                true,
                null,
                request.EditId,
                true,
                pending,
                CreateMutation("Program.cs", request.SaveAfterApply, pending.ProposedLength)));
        }

        public Task<EditDecisionResult> EditRejectAsync(
            EditDecisionRequest request,
            CancellationToken cancellationToken)
        {
            LastEditRejectRequest = request;
            return Task.FromResult(new EditDecisionResult(
                true,
                null,
                request.EditId,
                false,
                CreatePendingEdit("replace", "Program.cs", "replacement"),
                null));
        }

        public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PendingEditInfo> pendingEdits = [CreatePendingEdit("replace", "Program.cs", "replacement")];
            return Task.FromResult(new PendingEditListResult(pendingEdits));
        }

        public Task<BuildSolutionResult> BuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            LastBuildSolutionRequest = request;
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildStatusInfo("Idle", 0));
        }

        public Task<ErrorListResult> ErrorsListAsync(
            ErrorListRequest request,
            CancellationToken cancellationToken)
        {
            LastErrorListRequest = request;
            IReadOnlyCollection<ErrorListItemInfo> items =
            [
                new(
                    Description: "Build failed.",
                    File: @"C:\Code\NetVsMcp\Program.cs",
                    Line: 12,
                    Column: 8,
                    Level: "Error",
                    Project: "NetVsMcp")
            ];

            return Task.FromResult(new ErrorListResult(items));
        }

        public Task<OutputReadResult> OutputReadAsync(
            OutputReadRequest request,
            CancellationToken cancellationToken)
        {
            LastOutputReadRequest = request;
            return Task.FromResult(new OutputReadResult(request.PaneName, "Build output", false));
        }

        public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Design"));
        }

        public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<DebuggerStateInfo> DebugStepAsync(
            DebugStepRequest request,
            CancellationToken cancellationToken)
        {
            LastDebugStepRequest = request;
            return Task.FromResult(new DebuggerStateInfo("Break"));
        }

        public Task<BreakpointInfo> BreakpointSetAsync(
            BreakpointSetRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointSetRequest = request;
            return Task.FromResult(CreateBreakpoint(request.Condition));
        }

        public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<BreakpointInfo> breakpoints = [CreateBreakpoint(null)];
            return Task.FromResult(new BreakpointListResult(breakpoints));
        }

        public Task<BreakpointRemoveResult> BreakpointRemoveAsync(
            BreakpointRemoveRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointRemoveRequest = request;
            return Task.FromResult(new BreakpointRemoveResult(1));
        }

        public Task<BreakpointEnableResult> BreakpointEnableAsync(
            BreakpointEnableRequest request,
            CancellationToken cancellationToken)
        {
            LastBreakpointEnableRequest = request;
            IReadOnlyCollection<BreakpointInfo> breakpoints = [CreateBreakpoint(null) with { Enabled = request.Enabled }];
            return Task.FromResult(new BreakpointEnableResult(1, breakpoints));
        }

        public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CallStackFrameInfo> frames =
            [
                new("Program.Main", @"C:\Code\NetVsMcp\Program.cs", 42, 1)
            ];

            return Task.FromResult(new CallStackResult(new DebuggerStateInfo("Break"), frames));
        }

        public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> locals =
            [
                new("count", "42", "int", true)
            ];

            return Task.FromResult(new LocalsResult(new DebuggerStateInfo("Break"), locals));
        }

        public Task<EvaluateExpressionResult> DebugEvaluateAsync(
            EvaluateExpressionRequest request,
            CancellationToken cancellationToken)
        {
            LastEvaluateExpressionRequest = request;
            var expression = new DebugExpressionInfo(request.Expression, "43", "int", true);
            return Task.FromResult(new EvaluateExpressionResult(new DebuggerStateInfo("Break"), expression));
        }

        private static BreakpointInfo CreateBreakpoint(string? condition)
        {
            return new BreakpointInfo(
                Name: "bp-1",
                File: @"C:\Code\NetVsMcp\Program.cs",
                Line: 42,
                Column: 3,
                FunctionName: null,
                Condition: condition,
                Enabled: true);
        }

        private static EditorDocumentInfo CreateDocument(string path)
        {
            return new EditorDocumentInfo(
                Name: Path.GetFileName(path),
                Path: path,
                Language: "CSharp",
                IsOpen: true,
                IsSaved: false);
        }

        private static SelectionInfo CreateSelection()
        {
            return new SelectionInfo(
                CreateDocument("Program.cs"),
                "selected",
                1,
                1,
                2,
                1,
                false);
        }

        private static DocumentMutationResult CreateMutation(
            string path,
            bool saved,
            int charactersChanged)
        {
            return new DocumentMutationResult(
                true,
                null,
                CreateDocument(path),
                saved,
                charactersChanged);
        }

        private static PendingEditInfo CreatePendingEdit(
            string operation,
            string path,
            string proposedText)
        {
            return new PendingEditInfo(
                "edit-1",
                operation,
                path,
                "Replace text.",
                "old",
                proposedText,
                1,
                1,
                1,
                5,
                3,
                proposedText.Length,
                DateTimeOffset.Parse("2026-07-22T15:00:00Z"));
        }
    }
}
