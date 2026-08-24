using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
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
    public async Task CodeGoToImplementation_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeGoToImplementation("Program.cs", 1, 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal("Program.cs", session.LastCodeFindImplementationsRequest!.DocumentPath);
    }

    [Fact]
    public async Task CodeWorkspaceSymbols_RoutesQueryToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.CodeWorkspaceSymbols("Run", maxResults: 25, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Run", session.LastCodeWorkspaceSymbolsRequest!.Query);
        Assert.Equal(25, session.LastCodeWorkspaceSymbolsRequest.MaxResults);
        Assert.Equal("Run", Assert.Single(response.Value!.Symbols).Name);
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
    public async Task RenameSymbolApply_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.RenameSymbolApply("Program.cs", 1, 1, "NewName", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Success);
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
    public async Task DocumentRead_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.DocumentRead("Program.cs", sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
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
    public async Task DiagnosticsBindingErrors_RoutesThroughVsixSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.DiagnosticsBindingErrors(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("diagnostics_binding_errors", response.Value!.Metadata!["toolName"]);
    }
}
