using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.Text.Json;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
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
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "netvs_doctor", RequiresVisualStudioSession: false, Category: BrokerToolCategory.Broker });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_get_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_select_session", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_ping", RequiresVisualStudioSession: false });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "vs_context_snapshot", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "execute_command", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "get_status", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "get_help", RequiresVisualStudioSession: false, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "window_list", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "window_activate", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "toolwindow_show", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "toolwindow_hide", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_active", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_document_symbols", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_go_to_definition", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "code_find_references", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "symbol_context", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "rename_symbol_apply", RequiresVisualStudioSession: true, Category: BrokerToolCategory.EditDirect });
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
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_list", Category: BrokerToolCategory.Read });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_enable", Category: BrokerToolCategory.Debug });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "breakpoint_group_remove", Category: BrokerToolCategory.Debug });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_evaluate", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_read", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_write", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "prepare_safe_edit", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_preview", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "edit_list_pending", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "apply_safe_edit_and_build", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_open", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_close", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_info", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_add_project", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_remove_project", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "solution_overview", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_list", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_info", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_add_file", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Admin });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "project_dependencies", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "startup_project_set", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "test_run", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "test_debug", RequiresVisualStudioSession: true, Category: BrokerToolCategory.Test });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "test_run_and_get_results", RequiresVisualStudioSession: true });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "document_write", Category: BrokerToolCategory.EditDirect });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "prepare_safe_edit", Category: BrokerToolCategory.EditPreview });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "build_and_get_errors", Category: BrokerToolCategory.Build });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "debug_start", Category: BrokerToolCategory.Debug });
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
    public void GetHelp_TagsEachToolWithTheEndpointThatServesIt()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.GetHelp();

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Tools, tool => tool is { Name: "vs_list_sessions", McpEndpointPath: McpEndpointRouting.DefaultEndpointPath });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "console_get_info", McpEndpointPath: McpEndpointRouting.DefaultEndpointPath });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "ui_capture_region", McpEndpointPath: McpEndpointRouting.WebAutomationEndpointPath });
        Assert.Contains(response.Value.Tools, tool => tool is { Name: "web_connect", McpEndpointPath: McpEndpointRouting.WebAutomationEndpointPath });
        Assert.NotNull(response.Message);
    }

    [Fact]
    public void GetHelp_EndpointTagging_StaysInSyncWithMcpEndpointRouting()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.GetHelp();

        Assert.True(response.Success);
        foreach (var tool in response.Value!.Tools)
        {
            Assert.Equal(McpEndpointRouting.ResolveEndpointPath(tool.Name), tool.McpEndpointPath);
        }
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
    public async Task GetStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
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
        var runtime = CreateRuntime();
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
        var runtime = CreateRuntime();
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
        var runtime = CreateRuntime();
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
        var runtime = CreateRuntime();
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
    public void NetVsDoctor_ReturnsBrokerAndSessionDiagnostics()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = runtime.Tools.NetVsDoctor();

        Assert.True(response.Success);
        Assert.False(response.Value!.Healthy);
        Assert.Contains(response.Value.Checks, check => check is
        {
            Name: "broker_http_endpoint",
            Severity: BrokerDoctorSeverity.Error,
            Passed: false
        });
        Assert.Contains(response.Value.Checks, check => check is
        {
            Name: "registered_sessions",
            Severity: BrokerDoctorSeverity.Warning,
            Passed: true
        });
    }

    [Fact]
    public void NetVsDoctor_IncludesMcpConfigGuidance()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsDoctor();

        Assert.True(response.Success);
        Assert.Contains(response.Value!.Checks, check => check.Name == "mcp_client_config");
        Assert.Contains("broker status window", response.Value.Checks.Single(check => check.Name == "registered_sessions").Message);
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
        var runtime = CreateRuntime();
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
    public async Task SolutionClose_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.SolutionClose(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.SolutionClosed);
        Assert.False(response.Value!.IsOpen);
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
        var runtime = CreateRuntime();
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
    public async Task SolutionRemoveProject_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
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
        var runtime = CreateRuntime();
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
    public async Task ProjectRemoveFile_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ProjectRemoveFile(
            "NetVsMcp.Broker",
            @"C:\Code\NetVsMcp\OldFile.cs",
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
        Assert.Equal("NetVsMcp.Broker", session.LastProjectRemoveFileRequest!.ProjectName);
        Assert.Equal(@"C:\Code\NetVsMcp\OldFile.cs", response.Value.FilePath);
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
    public void VsGetLogs_ReturnsBoundedLogText()
    {
        var runtime = CreateRuntime();
        Directory.CreateDirectory(runtime.Options.EffectiveLogsDirectory);
        File.WriteAllText(Path.Combine(runtime.Options.EffectiveLogsDirectory, "broker.log"), "abcdef");

        var response = runtime.Tools.VsGetLogs(maxFiles: 1, maxCharsPerFile: 3);

        Assert.True(response.Success);
        var entry = Assert.Single(response.Value!.Files);
        Assert.Equal("def", entry.Text);
        Assert.True(entry.Truncated);
    }
}
