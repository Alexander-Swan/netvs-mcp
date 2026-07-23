using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.Text.Json;

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
        Assert.Equal(BrokerCapabilityProfile.Admin, response.Value!.ActiveProfile);
        Assert.Contains(response.Value!.Tools, tool => tool.Name == "vs_list_sessions");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_status");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_capabilities");
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_get_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_select_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_ping", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_context_snapshot", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "execute_command", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "get_status", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "get_help", RequiresVisualStudioSession: false, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "window_list", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "window_activate", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "toolwindow_show", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "toolwindow_hide", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_active", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_document_symbols", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_go_to_definition", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_find_references", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "symbol_context", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "workspace_search", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "open_relevant_files", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_solution", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_and_get_errors", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_status", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "errors_list", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "output_read", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_status", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_snapshot", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_eval_many", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_step", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_set", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_list", Category: BrokerToolCategory.Read, MinimumProfile: BrokerCapabilityProfile.ReadOnly });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_enable", Category: BrokerToolCategory.Debug, MinimumProfile: BrokerCapabilityProfile.Debug });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_remove", Category: BrokerToolCategory.Debug, MinimumProfile: BrokerCapabilityProfile.Debug });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_evaluate", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_read", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_write", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "prepare_safe_edit", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_preview", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_list_pending", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "apply_safe_edit_and_build", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_open", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_close", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_info", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_add_project", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_remove_project", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_overview", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_list", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_info", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_add_file", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin, MinimumProfile: BrokerCapabilityProfile.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_dependencies", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "startup_project_set", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "test_run", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "test_run_and_get_results", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_write", Category: BrokerToolCategory.EditDirect, MinimumProfile: BrokerCapabilityProfile.EditDirect });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "prepare_safe_edit", Category: BrokerToolCategory.EditPreview, MinimumProfile: BrokerCapabilityProfile.EditPreview });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_and_get_errors", Category: BrokerToolCategory.Build, MinimumProfile: BrokerCapabilityProfile.Debug });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_start", Category: BrokerToolCategory.Debug, MinimumProfile: BrokerCapabilityProfile.Debug });
        Assert.All(
            response.Value.Tools.Where(tool => tool.Name.StartsWith("vs_", StringComparison.Ordinal) && tool.Name != "vs_context_snapshot"),
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
    public async Task DocumentList_RoutesThroughVsixSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentList(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.DocumentListCalled);
        Assert.Single(response.Value!.Documents);
        Assert.Equal("Editor.cs", response.Value.ActiveDocument);
    }

    [Fact]
    public async Task EditorFind_RoutesQueryThroughVsixSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.EditorFind("needle", path: "Editor.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("needle", session.LastEditorFindRequest!.Query);
        Assert.Equal("Editor.cs", session.LastEditorFindRequest.Path);
        Assert.Single(response.Value!.Matches);
    }

    [Fact]
    public async Task FindInFiles_RoutesQueryThroughVsixSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.FindInFiles("needle", rootPath: @"C:\Code\NetVsMcp", filePattern: "*.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("needle", session.LastFindInFilesRequest!.Query);
        Assert.Equal(@"C:\Code\NetVsMcp", session.LastFindInFilesRequest.RootPath);
        Assert.Equal("*.cs", session.LastFindInFilesRequest.FilePattern);
        Assert.Single(response.Value!.Matches);
    }

    [Fact]
    public void GetHelp_FiltersBySessionRequirement()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.GetHelp(requiresVisualStudioSession: false);

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Tools, tool => tool.Name == "get_help");
        Assert.Contains(response.Value.Tools, tool => tool.Name == "vs_get_capabilities");
        Assert.DoesNotContain(response.Value.Tools, tool => tool.RequiresVisualStudioSession);
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
    public void VsGetSession_SelectsByProcessId()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp", processId: 1001));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other", processId: 1002));

        var response = runtime.Tools.VsGetSession(processId: 1002);

        Assert.True(response.Success);
        Assert.Equal("vs-2", response.Value!.Session.SessionId);
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
    public void VsPing_WritesAuditEntry()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.VsPing();

        Assert.True(response.Success);
        var audit = ReadSingleAuditEntry(runtime);
        Assert.Equal("vs_ping", audit.GetProperty("toolName").GetString());
        Assert.True(audit.GetProperty("success").GetBoolean());
        Assert.False(audit.TryGetProperty("failureReason", out _));
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
    public async Task VsContextSnapshot_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.VsContextSnapshot(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("vs-fake", response.Value!.Session!.SessionId);
        Assert.Equal("Editor.cs", response.Value.ActiveDocument);
        Assert.Equal("NetVsMcp", response.Value.Solution!.Name);
        Assert.Equal("Break", response.Value.Debugger!.Mode);
        Assert.Single(response.Value.Errors!.Items);
        Assert.Single(response.Value.PendingEdits!.PendingEdits);
    }

    [Fact]
    public async Task ExecuteCommand_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);

        var response = await runtime.Tools.ExecuteCommand("View.ErrorList", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.Equal("View.ErrorList", response.Value.CommandName);
        Assert.Equal("View.ErrorList", rpc.LastExecuteCommandRequest!.CommandName);
    }

    [Fact]
    public async Task ExecuteCommand_RequiresCommandName()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.ExecuteCommand(" ");

        Assert.False(response.Success);
        Assert.Equal("Command name is required.", response.Message);
    }

    [Fact]
    public async Task ExecuteCommand_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.ExecuteCommand("View.ErrorList", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task GetStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.GetStatus(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("vs-fake", response.Value!.SessionId);
        Assert.Equal("Editor.cs", response.Value.ActiveDocument);
    }

    [Fact]
    public async Task WindowList_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.WindowList(sessionId: "vs-1");

        Assert.True(response.Success);
        var window = Assert.Single(response.Value!.Windows);
        Assert.Equal("Editor.cs", window.Caption);
        Assert.True(window.IsActive);
    }

    [Fact]
    public async Task WindowActivate_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);

        var response = await runtime.Tools.WindowActivate(caption: "Error List", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.Equal("Error List", response.Value.Window!.Caption);
        Assert.Equal("Error List", rpc.LastWindowActivateRequest!.Caption);
    }

    [Fact]
    public async Task WindowActivate_RequiresCaptionOrObjectKind()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.WindowActivate();

        Assert.False(response.Success);
        Assert.Equal("Window caption or object kind is required.", response.Message);
    }

    [Fact]
    public async Task ToolWindowShow_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);

        var response = await runtime.Tools.ToolwindowShow(caption: "Error List", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.True(response.Value.Window!.IsVisible);
        Assert.Equal("Error List", rpc.LastToolWindowShowRequest!.Caption);
    }

    [Fact]
    public async Task ToolWindowHide_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);

        var response = await runtime.Tools.ToolwindowHide(objectKind: "{ErrorList}", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.False(response.Value.Window!.IsVisible);
        Assert.Equal("{ErrorList}", rpc.LastToolWindowHideRequest!.ObjectKind);
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
    public async Task DocumentRead_WritesRoutedAuditEntry()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DocumentRead("Editor.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        var audit = ReadSingleAuditEntry(runtime);
        Assert.Equal("document_read", audit.GetProperty("toolName").GetString());
        Assert.True(audit.GetProperty("success").GetBoolean());
        Assert.Equal("vs-1", audit.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task DocumentWrite_IsDeniedInReadOnlyProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.ReadOnly);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DocumentWrite("Editor.cs", "updated", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("ReadOnly", response.Metadata["activeProfile"]);
        Assert.Equal("EditDirect", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task EditPreview_IsAllowedInEditPreviewProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.EditPreview);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.EditPreview("write", "Editor.cs", "updated", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("edit-1", response.Value!.PendingEdit!.EditId);
    }

    [Fact]
    public async Task BuildSolution_IsDeniedInEditDirectProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.EditDirect);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BuildSolution(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Debug", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public void Runtime_WritesAndRemovesSessionManifests()
    {
        var runtime = CreateRuntime();

        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var manifest = Directory.GetFiles(runtime.SessionManifests.SessionsDirectory, "vs-1.json").Single();
        using (var document = JsonDocument.Parse(File.ReadAllText(manifest)))
        {
            Assert.Equal("vs-1", document.RootElement.GetProperty("sessionId").GetString());
            Assert.Equal("NetVsMcp", document.RootElement.GetProperty("solutionName").GetString());
        }

        runtime.Sessions.Unregister("vs-1");

        Assert.Empty(Directory.GetFiles(runtime.SessionManifests.SessionsDirectory, "vs-*.json"));
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
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DebugSnapshot(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Break", response.Value!.State.Mode);
        Assert.Single(response.Value.CallStack!.Frames);
        Assert.Single(response.Value.Locals!.Locals);
        Assert.Single(response.Value.Breakpoints!.Breakpoints);
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
    public async Task MemoryRead_RoutesBoundedRequestToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.MemoryRead("&count", byteCount: 16, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("&count", session.LastMemoryReadRequest!.AddressExpression);
        Assert.Equal(16, response.Value!.ByteCount);
    }

    [Fact]
    public async Task RegisterAndParallelTools_RouteToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var registers = await runtime.Tools.RegisterList(sessionId: "vs-1");
        var register = await runtime.Tools.RegisterGet("rip", sessionId: "vs-1");
        var stacks = await runtime.Tools.ParallelStacks(sessionId: "vs-1");
        var watch = await runtime.Tools.ParallelWatch(sessionId: "vs-1");
        var tasks = await runtime.Tools.ParallelTasksList(sessionId: "vs-1");

        Assert.True(registers.Success);
        Assert.Equal("rip", Assert.Single(registers.Value!.Registers).Name);
        Assert.True(register.Success);
        Assert.Equal("rip", register.Value!.Register!.Name);
        Assert.True(stacks.Success);
        Assert.Single(stacks.Value!.Frames);
        Assert.True(watch.Success);
        Assert.Single(watch.Value!.Expressions);
        Assert.True(tasks.Success);
        Assert.Single(tasks.Value!.Tasks);
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
    public async Task CodeGoToDefinition_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeGoToDefinition(
            documentPath: "Program.cs",
            line: 10,
            column: 5,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastCodeGoToDefinitionRequest!.DocumentPath);
        Assert.Equal(10, session.LastCodeGoToDefinitionRequest.Line);
        Assert.True(response.Value!.Navigated);
        Assert.Equal("Run", response.Value.Symbol!.Name);
        Assert.Equal("Run", Assert.Single(response.Value.Definitions).Symbol.Name);
    }

    [Fact]
    public async Task CodeFindReferences_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeFindReferences(
            documentPath: "Program.cs",
            line: 10,
            column: 5,
            solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal(5, session.LastCodeFindReferencesRequest!.Column);
        var reference = Assert.Single(response.Value!.References);
        Assert.False(reference.IsImplicit);
        Assert.Equal("Run", reference.Symbol.Name);
    }

    [Fact]
    public async Task SymbolContext_RoutesDocumentAndNavigationCalls()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SymbolContext("Program.cs", 1, 5, contextLines: 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastDocumentReadRequest!.Path);
        Assert.Equal("Program.cs", session.LastCodeGoToDefinitionRequest!.DocumentPath);
        Assert.Equal("Program.cs", session.LastCodeFindReferencesRequest!.DocumentPath);
        Assert.Contains("1: class Program {}", response.Value!.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentOutline_RoutesToDocumentSymbols()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentOutline("Program.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastSymbolsDocumentPath);
        Assert.Contains("Editor.Run", response.Value!.Symbols);
    }

    [Fact]
    public async Task OpenRelevantFiles_DeduplicatesAndRoutesFiles()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OpenRelevantFiles(["Program.cs", "Program.cs", "Other.cs"], sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(2, response.Value!.Documents.Count);
        Assert.Equal("Other.cs", session.LastDocumentOpenRequest!.Path);
    }

    [Fact]
    public async Task FindImplementations_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.FindImplementations("Program.cs", 1, 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal("Program.cs", session.LastCodeFindImplementationsRequest!.DocumentPath);
        Assert.Single(response.Value.Implementations);
    }

    [Fact]
    public async Task RenameSymbolPreview_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.RenameSymbolPreview("Program.cs", 1, 1, "NewName", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal("Program.cs", session.LastRenameSymbolRequest!.DocumentPath);
        Assert.Equal("NewName", response.Value.NewName);
        Assert.Single(response.Value.Changes!);
    }

    [Fact]
    public async Task CodeGoToDefinition_ValidatesPosition()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.CodeGoToDefinition("Program.cs", 0, 5);

        Assert.False(response.Success);
        Assert.Equal("Line must be greater than zero.", response.Message);
    }

    [Fact]
    public async Task CodeFindReferences_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.CodeFindReferences("Program.cs", 10, 5, sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
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
    public async Task PrepareSafeEdit_ReadsOriginalAndPreviewsEdit()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.PrepareSafeEdit(
            operation: "write",
            path: "Program.cs",
            text: "replacement",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Program.cs", session.LastDocumentReadRequest!.Path);
        Assert.Equal("write", session.LastEditPreviewRequest!.Operation);
        Assert.Equal("class Program {}", response.Value!.Original.Text);
        Assert.Equal("edit-1", response.Value.Preview.PendingEdit!.EditId);
    }

    [Fact]
    public async Task FormatAndOrganize_RoutesCleanup()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.FormatAndOrganize("Program.cs", saveAfterCleanup: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastDocumentCleanupRequest!.SaveAfterCleanup);
        Assert.Equal("Edit.FormatDocument", response.Value!.Cleanup.Command);
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
    public async Task ApplySafeEditAndBuild_ApprovesBuildsAndReturnsErrors()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ApplySafeEditAndBuild("edit-1", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("edit-1", session.LastEditApproveRequest!.EditId);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.Single(response.Value!.Errors.Items);
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
    public async Task SolutionInfo_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionInfo(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp", response.Value!.Name);
        Assert.Equal(2, response.Value.ProjectCount);
    }

    [Fact]
    public async Task SolutionOpen_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Admin);
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SolutionOpen(@"C:\Code\Other\Other.slnx", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(@"C:\Code\Other\Other.slnx", session.LastSolutionOpenRequest!.Path);
        Assert.Equal("Other", response.Value!.Name);
    }

    [Fact]
    public async Task SolutionOpen_RequiresPath()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.SolutionOpen(" ");

        Assert.False(response.Success);
        Assert.Equal("Path is required.", response.Message);
    }

    [Fact]
    public async Task SolutionOpen_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionOpen(@"C:\Code\Other\Other.slnx", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task SolutionClose_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Admin);
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SolutionClose(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.SolutionClosed);
        Assert.False(response.Value!.IsOpen);
    }

    [Fact]
    public async Task SolutionClose_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionClose(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task ProjectList_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.ProjectList(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Projects, project => project.Name == "NetVsMcp.Broker");
    }

    [Fact]
    public async Task SolutionAddProject_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Admin);
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SolutionAddProject(@"C:\Code\NetVsMcp\src\NewProject\NewProject.csproj", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(@"C:\Code\NetVsMcp\src\NewProject\NewProject.csproj", session.LastSolutionAddProjectRequest!.ProjectPath);
        Assert.Equal("NewProject", response.Value!.Name);
    }

    [Fact]
    public async Task SolutionAddProject_RequiresPath()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.SolutionAddProject(" ");

        Assert.False(response.Success);
        Assert.Equal("Path is required.", response.Message);
    }

    [Fact]
    public async Task SolutionAddProject_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionAddProject(@"C:\Code\NetVsMcp\src\NewProject\NewProject.csproj", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task SolutionRemoveProject_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Admin);
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SolutionRemoveProject("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastSolutionRemoveProjectRequest!.ProjectName);
        Assert.Equal("NetVsMcp.Broker", response.Value!.Name);
    }

    [Fact]
    public async Task SolutionRemoveProject_RequiresProjectName()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.SolutionRemoveProject(" ");

        Assert.False(response.Success);
        Assert.Equal("Project name is required.", response.Message);
    }

    [Fact]
    public async Task SolutionRemoveProject_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionRemoveProject("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task ProjectInfo_RequiresProjectName()
    {
        var runtime = CreateRuntime();

        var response = await runtime.Tools.ProjectInfo("");

        Assert.False(response.Success);
        Assert.Equal("Project name is required.", response.Message);
    }

    [Fact]
    public async Task ProjectInfo_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ProjectInfo("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastProjectInfoRequest!.ProjectName);
        Assert.Equal("NetVsMcp.Broker", response.Value!.Name);
    }

    [Fact]
    public async Task ProjectAddFile_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Admin);
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ProjectAddFile("NetVsMcp.Broker", @"C:\Code\NetVsMcp\NewFile.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastProjectAddFileRequest!.ProjectName);
        Assert.Equal(@"C:\Code\NetVsMcp\NewFile.cs", session.LastProjectAddFileRequest.FilePath);
        Assert.Equal("NetVsMcp.Broker", response.Value!.Name);
    }

    [Fact]
    public async Task ProjectAddFile_RequiresInputs()
    {
        var runtime = CreateRuntime();

        var missingProject = await runtime.Tools.ProjectAddFile(" ", "File.cs");
        var missingPath = await runtime.Tools.ProjectAddFile("NetVsMcp.Broker", " ");

        Assert.False(missingProject.Success);
        Assert.Equal("Project name is required.", missingProject.Message);
        Assert.False(missingPath.Success);
        Assert.Equal("Path is required.", missingPath.Message);
    }

    [Fact]
    public async Task ProjectAddFile_IsDeniedOutsideAdminProfile()
    {
        var runtime = CreateRuntime(BrokerCapabilityProfile.Debug);
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.ProjectAddFile("NetVsMcp.Broker", "File.cs", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("CapabilityProfileDenied", response.Metadata!["failureReason"]);
        Assert.Equal("Admin", response.Metadata["requiredProfile"]);
    }

    [Fact]
    public async Task SolutionOverview_ReturnsTestProjects()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.SolutionOverview(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp", response.Value!.Solution.Name);
        Assert.Contains(response.Value.TestProjects, project => project.Name == "NetVsMcp.Broker.Tests");
    }

    [Fact]
    public async Task ProjectDependencies_RoutesProjectLookup()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ProjectDependencies("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastProjectInfoRequest!.ProjectName);
        Assert.Equal("NetVsMcp.Broker", response.Value!.Project!.Name);
        Assert.Empty(response.Value.PackageReferences);
    }

    [Fact]
    public async Task PackageRestore_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.PackageRestore("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal("NetVsMcp.Broker", session.LastPackageRestoreRequest!.ProjectName);
        Assert.Equal("NetVsMcp.Broker", response.Value.Project!.Name);
        Assert.Equal(0, response.Value.ExitCode);
    }

    [Fact]
    public async Task StartupProjectGet_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.StartupProjectGet(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("src\\NetVsMcp.Broker\\NetVsMcp.Broker.csproj", Assert.Single(response.Value!.Projects));
    }

    [Fact]
    public async Task StartupProjectSet_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.StartupProjectSet("NetVsMcp.Broker", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastStartupProjectSetRequest!.ProjectName);
        Assert.False(response.Value!.IsMultiStartup);
    }

    [Fact]
    public async Task TestDiscover_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.TestDiscover(projectName: "NetVsMcp.Broker.Tests", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp.Broker.Tests", session.LastTestDiscoverRequest!.ProjectName);
        Assert.True(response.Value!.Supported);
        Assert.Equal("BrokerToolServiceTests.ProjectList", Assert.Single(response.Value.Tests).Name);
    }

    [Fact]
    public async Task TestRun_RoutesFilterToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.TestRun(
            projectName: "NetVsMcp.Broker.Tests",
            filter: "FullyQualifiedName~BrokerToolServiceTests",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("FullyQualifiedName~BrokerToolServiceTests", session.LastTestRunRequest!.Filter);
        Assert.Equal("Passed", Assert.Single(response.Value!.Results).Outcome);
    }

    [Fact]
    public async Task TestResults_RoutesRunIdToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.TestResults(runId: "run-1", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("run-1", session.LastTestResultsRequest!.RunId);
        Assert.Equal("Passed", Assert.Single(response.Value!.Results).Outcome);
    }

    [Fact]
    public async Task TestRunAndGetResults_RoutesRunAndResults()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.TestRunAndGetResults(
            projectName: "NetVsMcp.Broker.Tests",
            filter: "Name~ProjectList",
            runId: "run-1",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Name~ProjectList", session.LastTestRunRequest!.Filter);
        Assert.Equal("run-1", session.LastTestResultsRequest!.RunId);
        Assert.Equal("Passed", Assert.Single(response.Value!.Results.Results).Outcome);
    }

    [Fact]
    public async Task ProjectList_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.ProjectList(sessionId: "vs-1");

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
    public async Task BuildAndGetErrors_BuildsAndReturnsDiagnostics()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildAndGetErrors(includeWarnings: false, maxItems: 10, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.False(session.LastErrorListRequest!.IncludeWarnings);
        Assert.Equal(10, session.LastErrorListRequest.MaxItems);
        Assert.Single(response.Value!.Errors.Items);
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
    public async Task DiagnosticsForDocument_FiltersErrorsByDocument()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DiagnosticsForDocument(@"C:\Code\NetVsMcp\Program.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Single(response.Value!.Items);
    }

    [Fact]
    public async Task WorkspaceSearch_SearchesExplicitRoot()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "NetVsMcp.Broker.Tests", Guid.NewGuid().ToString("N"))).FullName;
        var file = Path.Combine(root, "Program.cs");
        await File.WriteAllTextAsync(file, "class Program { void Run() {} }");

        var response = await runtime.Tools.WorkspaceSearch("Run", "*.cs", root, sessionId: "vs-1");

        Assert.True(response.Success);
        var match = Assert.Single(response.Value!.Matches);
        Assert.Equal(file, match.Path);
        Assert.Equal(1, match.Line);
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

    private static BrokerRuntime CreateRuntime(
        BrokerCapabilityProfile capabilityProfile = BrokerCapabilityProfile.Admin)
    {
        var root = Path.Combine(Path.GetTempPath(), "NetVsMcp.Broker.Tests", Guid.NewGuid().ToString("N"));
        var options = BrokerOptions.LocalDefault with
        {
            LogsDirectory = Path.Combine(root, "Logs"),
            SessionsDirectory = Path.Combine(root, "Sessions"),
            CapabilityProfile = capabilityProfile
        };

        return new BrokerRuntime(options, new SessionRegistry());
    }

    private static JsonElement ReadSingleAuditEntry(BrokerRuntime runtime)
    {
        var auditFile = Directory.GetFiles(runtime.AuditLog.LogsDirectory, "audit-*.jsonl").Single();
        var line = File.ReadLines(auditFile).Single();
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        int processId = 1234)
    {
        return CreateRegistration(
            sessionId,
            solutionName,
            $@"C:\Code\{solutionName}\{solutionName}.slnx",
            isActive: true,
            processId: processId);
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        string solutionPath,
        bool isActive,
        int processId = 1234)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: processId,
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

        public CodePositionRequest? LastCodeGoToDefinitionRequest { get; private set; }

        public CodePositionRequest? LastCodeFindReferencesRequest { get; private set; }

        public CodePositionRequest? LastCodeFindImplementationsRequest { get; private set; }

        public RenameSymbolRequest? LastRenameSymbolRequest { get; private set; }

        public ExecuteCommandRequest? LastExecuteCommandRequest { get; private set; }

        public WindowActivateRequest? LastWindowActivateRequest { get; private set; }

        public ToolWindowRequest? LastToolWindowShowRequest { get; private set; }

        public ToolWindowRequest? LastToolWindowHideRequest { get; private set; }

        public SolutionOpenRequest? LastSolutionOpenRequest { get; private set; }

        public SolutionAddProjectRequest? LastSolutionAddProjectRequest { get; private set; }

        public ProjectInfoRequest? LastSolutionRemoveProjectRequest { get; private set; }

        public ProjectFileRequest? LastProjectAddFileRequest { get; private set; }

        public bool SolutionClosed { get; private set; }

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

        public ProjectInfoRequest? LastProjectInfoRequest { get; private set; }

        public StartupProjectSetRequest? LastStartupProjectSetRequest { get; private set; }

        public TestDiscoverRequest? LastTestDiscoverRequest { get; private set; }

        public TestRunRequest? LastTestRunRequest { get; private set; }

        public TestResultsRequest? LastTestResultsRequest { get; private set; }

        public PackageRestoreRequest? LastPackageRestoreRequest { get; private set; }

        public BuildSolutionRequest? LastBuildSolutionRequest { get; private set; }

        public ErrorListRequest? LastErrorListRequest { get; private set; }

        public OutputReadRequest? LastOutputReadRequest { get; private set; }

        public DebugStepRequest? LastDebugStepRequest { get; private set; }

        public BreakpointSetRequest? LastBreakpointSetRequest { get; private set; }

        public BreakpointRemoveRequest? LastBreakpointRemoveRequest { get; private set; }

        public BreakpointEnableRequest? LastBreakpointEnableRequest { get; private set; }

        public EvaluateExpressionRequest? LastEvaluateExpressionRequest { get; private set; }

        public DebugAttachRequest? LastDebugAttachRequest { get; private set; }

        public ProcessDetachRequest? LastProcessDetachRequest { get; private set; }

        public MemoryReadRequest? LastMemoryReadRequest { get; private set; }

        public PlannedToolRequest? LastPlannedToolRequest { get; private set; }

        public bool DocumentListCalled { get; private set; }

        public EditorFindRequest? LastEditorFindRequest { get; private set; }

        public FindInFilesRequest? LastFindInFilesRequest { get; private set; }

        public DocumentCloseRequest? LastDocumentCloseRequest { get; private set; }

        public ProjectReferenceRequest? LastProjectAddReferenceRequest { get; private set; }

        public ProjectReferenceRequest? LastProjectRemoveReferenceRequest { get; private set; }

        public NugetListRequest? LastNugetListRequest { get; private set; }

        public Task<UnsupportedToolResult> PlannedToolAsync(
            PlannedToolRequest request,
            CancellationToken cancellationToken)
        {
            LastPlannedToolRequest = request;
            return Task.FromResult(new UnsupportedToolResult(
                request.ToolName,
                request.Category,
                $"Tool '{request.ToolName}' reached fake VSIX.",
                request.ImplementationHint));
        }

        public Task<DocumentListResult> DocumentListAsync(CancellationToken cancellationToken)
        {
            DocumentListCalled = true;
            IReadOnlyCollection<EditorDocumentInfo> documents =
            [
                CreateDocument(_activeDocument)
            ];

            return Task.FromResult(new DocumentListResult(documents, _activeDocument));
        }

        public Task<DocumentCloseResult> DocumentCloseAsync(
            DocumentCloseRequest request,
            CancellationToken cancellationToken)
        {
            LastDocumentCloseRequest = request;
            return Task.FromResult(new DocumentCloseResult(true, "Document closed.", CreateDocument(string.IsNullOrWhiteSpace(request.Path) ? _activeDocument : request.Path), request.Policy));
        }

        public Task<TextSearchResult> EditorFindAsync(
            EditorFindRequest request,
            CancellationToken cancellationToken)
        {
            LastEditorFindRequest = request;
            return Task.FromResult(CreateTextSearchResult(request.Query, request.Path));
        }

        public Task<TextSearchResult> FindInFilesAsync(
            FindInFilesRequest request,
            CancellationToken cancellationToken)
        {
            LastFindInFilesRequest = request;
            return Task.FromResult(CreateTextSearchResult(request.Query, @"C:\Code\NetVsMcp\Editor.cs"));
        }

        public Task<ProjectReferenceResult> ProjectAddReferenceAsync(
            ProjectReferenceRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectAddReferenceRequest = request;
            return Task.FromResult(new ProjectReferenceResult(true, "Reference added.", CreateProject(request.ProjectName), request.Reference, request.ReferenceType));
        }

        public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(
            ProjectReferenceRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectRemoveReferenceRequest = request;
            return Task.FromResult(new ProjectReferenceResult(true, "Reference removed.", CreateProject(request.ProjectName), request.Reference, request.ReferenceType));
        }

        public Task<NugetListResult> NugetListAsync(
            NugetListRequest request,
            CancellationToken cancellationToken)
        {
            LastNugetListRequest = request;
            IReadOnlyCollection<NugetPackageInfo> packages =
            [
                new("StreamJsonRpc", "2.25.29", request.ProjectName ?? "NetVsMcp", @"C:\Code\NetVsMcp\NetVsMcp.csproj")
            ];

            return Task.FromResult(new NugetListResult(packages));
        }

        public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<NugetPackageInfo> packages = [new(request.Query, "1.0.0", null, null)];
            return Task.FromResult(new NugetSearchResult(packages));
        }

        public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Installed."));

        public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Updated."));

        public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateNugetMutation(request, "Uninstalled."));

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<VsSessionInfo>.Ok(new VsSessionInfo(
                SessionId: "vs-fake",
                ProcessId: 1234,
                VisualStudioVersion: "18.0",
                Edition: "Enterprise",
                SolutionName: "NetVsMcp",
                SolutionPath: @"C:\Code\NetVsMcp\NetVsMcp.slnx",
                ActiveDocument: _activeDocument,
                DebuggerMode: DebuggerMode.Break,
                IsActiveWindow: true,
                LastSeenUtc: DateTimeOffset.Parse("2026-07-22T15:00:00Z"),
                Capabilities: [VsCapability.Editor, VsCapability.Navigation])));
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

        public Task<ExecuteCommandResult> ExecuteCommandAsync(
            ExecuteCommandRequest request,
            CancellationToken cancellationToken)
        {
            LastExecuteCommandRequest = request;
            return Task.FromResult(new ExecuteCommandResult(true, request.CommandName, request.Arguments, "Command executed."));
        }

        public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WindowInfo> windows =
            [
                new(_activeDocument, "Document", "Text", true, true)
            ];

            return Task.FromResult(new WindowListResult(windows));
        }

        public Task<WindowActivateResult> WindowActivateAsync(
            WindowActivateRequest request,
            CancellationToken cancellationToken)
        {
            LastWindowActivateRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new WindowActivateResult(
                true,
                "Window activated.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", true, true)));
        }

        public Task<ToolWindowResult> ToolWindowShowAsync(
            ToolWindowRequest request,
            CancellationToken cancellationToken)
        {
            LastToolWindowShowRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new ToolWindowResult(
                true,
                "Tool window shown.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", true, true)));
        }

        public Task<ToolWindowResult> ToolWindowHideAsync(
            ToolWindowRequest request,
            CancellationToken cancellationToken)
        {
            LastToolWindowHideRequest = request;
            var caption = request.Caption ?? "Error List";
            return Task.FromResult(new ToolWindowResult(
                true,
                "Tool window hidden.",
                new WindowInfo(caption, "Tool", request.ObjectKind ?? "{ErrorList}", false, false)));
        }

        public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeGoToDefinitionRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeLocationInfo> definitions =
            [
                new(request.DocumentPath, request.Line, request.Column, symbol)
            ];

            return Task.FromResult(new GoToDefinitionResult(symbol, definitions, true));
        }

        public Task<FindReferencesResult> CodeFindReferencesAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeFindReferencesRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeReferenceInfo> references =
            [
                new(request.DocumentPath, request.Line + 1, request.Column, false, symbol)
            ];

            return Task.FromResult(new FindReferencesResult(symbol, references));
        }

        public Task<FindImplementationsResult> CodeFindImplementationsAsync(
            CodePositionRequest request,
            CancellationToken cancellationToken)
        {
            LastCodeFindImplementationsRequest = request;
            var symbol = CreateSymbol(request);
            IReadOnlyCollection<CodeLocationInfo> implementations =
            [
                new(request.DocumentPath, request.Line + 2, request.Column, symbol)
            ];

            return Task.FromResult(new FindImplementationsResult(true, "Found implementations.", request, implementations));
        }

        public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(
            RenameSymbolRequest request,
            CancellationToken cancellationToken)
        {
            LastRenameSymbolRequest = request;
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            IReadOnlyCollection<RenameSymbolChangeInfo> changes =
            [
                new(request.DocumentPath, request.Line, request.Column, request.Line, request.Column + 3, request.NewName)
            ];

            return Task.FromResult(new RenameSymbolPreviewResult(true, "Previewed rename.", position, request.NewName, CreateSymbol(position), changes));
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

        public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new SolutionInfoResult(
                "NetVsMcp",
                @"C:\Code\NetVsMcp\NetVsMcp.slnx",
                true,
                2,
                @"src\NetVsMcp.Broker\NetVsMcp.Broker.csproj"));
        }

        public Task<SolutionInfoResult> SolutionOpenAsync(
            SolutionOpenRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionOpenRequest = request;
            return Task.FromResult(new SolutionInfoResult(
                Path.GetFileNameWithoutExtension(request.Path),
                request.Path,
                true,
                1,
                null));
        }

        public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken)
        {
            SolutionClosed = true;
            return Task.FromResult(new SolutionInfoResult(null, null, false, 0, null));
        }

        public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ProjectInfo> projects =
            [
                CreateProject("NetVsMcp.Broker"),
                CreateProject("NetVsMcp.Broker.Tests")
            ];

            return Task.FromResult(new ProjectListResult(projects));
        }

        public Task<ProjectInfo> SolutionAddProjectAsync(
            SolutionAddProjectRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionAddProjectRequest = request;
            return Task.FromResult(CreateProject(Path.GetFileNameWithoutExtension(request.ProjectPath)));
        }

        public Task<ProjectInfo> SolutionRemoveProjectAsync(
            ProjectInfoRequest request,
            CancellationToken cancellationToken)
        {
            LastSolutionRemoveProjectRequest = request;
            return Task.FromResult(CreateProject(request.ProjectName));
        }

        public Task<ProjectInfo?> ProjectInfoAsync(
            ProjectInfoRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectInfoRequest = request;
            return Task.FromResult<ProjectInfo?>(CreateProject(request.ProjectName));
        }

        public Task<ProjectInfo> ProjectAddFileAsync(
            ProjectFileRequest request,
            CancellationToken cancellationToken)
        {
            LastProjectAddFileRequest = request;
            return Task.FromResult(CreateProject(request.ProjectName));
        }

        public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<string> projects = [@"src\NetVsMcp.Broker\NetVsMcp.Broker.csproj"];
            return Task.FromResult(new StartupProjectResult(projects, false));
        }

        public Task<StartupProjectResult> StartupProjectSetAsync(
            StartupProjectSetRequest request,
            CancellationToken cancellationToken)
        {
            LastStartupProjectSetRequest = request;
            IReadOnlyCollection<string> projects = [request.ProjectName];
            return Task.FromResult(new StartupProjectResult(projects, false));
        }

        public Task<TestOperationResult> TestDiscoverAsync(
            TestDiscoverRequest request,
            CancellationToken cancellationToken)
        {
            LastTestDiscoverRequest = request;
            IReadOnlyCollection<TestCaseInfo> tests =
            [
                new("BrokerToolServiceTests.ProjectList", request.ProjectName, "BrokerToolServiceTests.cs")
            ];

            return Task.FromResult(new TestOperationResult(true, "Discovered tests.", tests, []));
        }

        public Task<TestOperationResult> TestRunAsync(
            TestRunRequest request,
            CancellationToken cancellationToken)
        {
            LastTestRunRequest = request;
            return Task.FromResult(new TestOperationResult(true, "Ran tests.", [], CreateTestResults()));
        }

        public Task<TestOperationResult> TestResultsAsync(
            TestResultsRequest request,
            CancellationToken cancellationToken)
        {
            LastTestResultsRequest = request;
            return Task.FromResult(new TestOperationResult(true, "Returned test results.", [], CreateTestResults()));
        }

        public Task<PackageRestoreResult> PackageRestoreAsync(
            PackageRestoreRequest request,
            CancellationToken cancellationToken)
        {
            LastPackageRestoreRequest = request;
            return Task.FromResult(new PackageRestoreResult(true, "Restored packages.", CreateProject(request.ProjectName ?? "NetVsMcp.Broker"), 0));
        }

        public Task<BuildSolutionResult> BuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            LastBuildSolutionRequest = request;
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildSolutionResult> BuildProjectAsync(
            BuildProjectRequest request,
            CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildStatusInfo("Cancelled", 0));
        }

        public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildSolutionResult> RebuildSolutionAsync(
            BuildSolutionRequest request,
            CancellationToken cancellationToken)
        {
            var status = new BuildStatusInfo("Done", 0);
            return Task.FromResult(new BuildSolutionResult(status, 0));
        }

        public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildStatusInfo("Idle", 0));
        }

        public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildConfigurationInfo("Debug", "Any CPU"));
        }

        public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(
            BuildConfigurationSetRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BuildConfigurationInfo(request.Configuration, request.Platform));
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

        public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<OutputPaneInfo> panes = [new("Build"), new("Debug")];
            return Task.FromResult(new OutputPaneListResult(panes));
        }

        public Task<OutputReadResult> OutputClearAsync(
            OutputPaneRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new OutputReadResult(request.PaneName ?? "Build", string.Empty, false));
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

        public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebuggerStateInfo("Run"));
        }

        public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken)
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
            return Task.FromResult(CreateBreakpoint(request.Condition, request));
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

        public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebuggedProcessInfo> processes = [new(1234, "NetVsMcp.Broker.exe", "Default", "alex")];
            return Task.FromResult(new DebuggedProcessListResult(processes));
        }

        public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<LocalProcessInfo> processes = [new(1234, "NetVsMcp.Broker.exe", "Default", "alex", true)];
            return Task.FromResult(new LocalProcessListResult(processes));
        }

        public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken)
        {
            LastDebugAttachRequest = request;
            return Task.FromResult(new DebugAttachResult(true, null, new DebuggedProcessInfo(request.ProcessId ?? 1234, request.ProcessName ?? "NetVsMcp.Broker.exe", "Default", "alex")));
        }

        public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken)
        {
            LastProcessDetachRequest = request;
            return Task.FromResult(new ProcessDetachResult(true, null, new DebuggedProcessInfo(request.ProcessId ?? 1234, request.ProcessName ?? "NetVsMcp.Broker.exe", "Default", "alex"), new DebuggerStateInfo("Break")));
        }

        public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new WatchOperationResult(true, true, "Added.", new DebugExpressionInfo(request.Expression, "1", "int", true)));

        public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new WatchOperationResult(true, true, "Removed.", new DebugExpressionInfo(request.Expression, "1", "int", true)));

        public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> watches = [new("count", "42", "int", true)];
            return Task.FromResult(new WatchListResult(true, null, watches));
        }

        public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugThreadInfo> threads = [new(1, "Main Thread", true)];
            return Task.FromResult(new DebugThreadListResult(true, null, threads));
        }

        public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ThreadSwitchResult(true, true, null, new DebugThreadInfo(request.ThreadId, "Main Thread", true)));

        public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugModuleInfo> modules = [new("NetVsMcp.Broker.dll", @"C:\Code\NetVsMcp\NetVsMcp.Broker.dll")];
            return Task.FromResult(new ModuleListResult(true, null, modules));
        }

        public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ImmediateExecuteResult(true, true, null, "ok"));

        public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExceptionSettingsResult(true, true, null));

        public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExceptionSettingsResult(true, true, null));

        public Task<MemoryReadResult> MemoryReadAsync(MemoryReadRequest request, CancellationToken cancellationToken)
        {
            LastMemoryReadRequest = request;
            return Task.FromResult(new MemoryReadResult(false, false, "Memory reads require native debugger APIs.", request.AddressExpression, request.ByteCount, null));
        }

        public Task<RegisterListResult> RegisterListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<RegisterInfo> registers = [new("rip", "0x00000001", "pointer")];
            return Task.FromResult(new RegisterListResult(true, null, registers));
        }

        public Task<RegisterGetResult> RegisterGetAsync(RegisterGetRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new RegisterGetResult(true, true, null, new RegisterInfo(request.Name, "0x00000001", "pointer")));

        public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ParallelStackFrameInfo> frames = [new(1, "Main Thread", "Program.Main", "Program.cs", 42, 1)];
            return Task.FromResult(new ParallelStacksResult(true, null, frames));
        }

        public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<DebugExpressionInfo> expressions = [new("count", "42", "int", true)];
            return Task.FromResult(new ParallelWatchResult(true, null, expressions));
        }

        public Task<ParallelTasksResult> ParallelTasksListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ParallelTaskInfo> tasks = [new("1", "Running", "Program.cs:42", 1)];
            return Task.FromResult(new ParallelTasksResult(true, null, tasks));
        }

        private static BreakpointInfo CreateBreakpoint(string? condition, BreakpointSetRequest? request = null)
        {
            return new BreakpointInfo(
                Name: "bp-1",
                File: @"C:\Code\NetVsMcp\Program.cs",
                Line: 42,
                Column: 3,
                FunctionName: null,
                Condition: condition,
                Enabled: true,
                Action: request?.Action,
                ActionMessage: request?.ActionMessage,
                ContinueAfterAction: request?.ContinueAfterAction ?? false,
                HitCount: request?.HitCount,
                HitCountType: request?.HitCountType,
                DependsOnBreakpointName: request?.DependsOnBreakpointName,
                GroupName: request?.GroupName ?? "critical");
        }

        private static NugetMutationResult CreateNugetMutation(NugetPackageMutationRequest request, string message)
        {
            return new NugetMutationResult(true, message, CreateProject(request.ProjectName), request.PackageId, request.Version, 0);
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

        private static TextSearchResult CreateTextSearchResult(string query, string path)
        {
            IReadOnlyCollection<TextSearchMatch> matches =
            [
                new(path, 3, 9, "var value = needle;", query)
            ];

            return new TextSearchResult(query, matches.Count, false, matches);
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

        private static ProjectInfo CreateProject(string name)
        {
            return new ProjectInfo(
                Name: name,
                UniqueName: $@"src\{name}\{name}.csproj",
                FullName: $@"C:\Code\NetVsMcp\src\{name}\{name}.csproj",
                Kind: "CSharp",
                IsLoaded: true,
                Language: "CSharp",
                OutputFileName: $"{name}.dll");
        }

        private static IReadOnlyCollection<TestResultInfo> CreateTestResults()
        {
            return
            [
                new("BrokerToolServiceTests.ProjectList", "Passed", "00:00:00.100", null)
            ];
        }

        private static DocumentSymbolInfo CreateSymbol(CodePositionRequest request)
        {
            return new DocumentSymbolInfo(
                "Run",
                "Method",
                request.DocumentPath,
                request.Line,
                request.Column,
                "Program",
                "NetVsMcp");
        }
    }
}
