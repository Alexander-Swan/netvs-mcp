using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task EditPreview_ReturnsPendingEdit()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.EditPreview("write", "Editor.cs", "updated", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("edit-1", response.Value!.PendingEdit!.EditId);
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
}
