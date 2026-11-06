using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DocumentRead_ReturnsRequestedRangeFromLiveRpcText()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DocumentText = "first\r\nunsaved\r\nlast" };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentRead("Editor.cs", sessionId: "vs-1", startLine: 2, lineCount: 1);

        Assert.True(response.Success);
        Assert.Equal("unsaved\r\n", response.Value!.Text);
        Assert.True(response.Value.UsedLiveBuffer);
        Assert.Equal(2, response.Value.StartLine);
        Assert.Equal(1, response.Value.LineCount);
        Assert.Equal(3, response.Value.TotalLines);
        Assert.True(response.Value.Truncated);
        Assert.Equal("Editor.cs", session.LastDocumentReadRequest!.Path);
        Assert.Equal(2, session.LastDocumentReadRequest.StartLine);
        Assert.Equal(1, session.LastDocumentReadRequest.LineCount);
    }

    [Fact]
    public async Task DocumentRead_DoesNotResliceWhenVsixReturnsRange()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DocumentText = "first\nsecond\nlast", PageDocumentRead = true };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DocumentRead("Editor.cs", sessionId: "vs-1", startLine: 2, lineCount: 1, startColumn: 3);

        Assert.True(response.Success);
        Assert.Equal("cond\n", response.Value!.Text);
        Assert.Equal(3, response.Value.TotalLines);
        Assert.Equal(3, session.LastDocumentReadRequest!.StartColumn);
    }

    [Fact]
    public async Task DocumentRead_DefaultStillReturnsTheWholeDocument()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var text = "first\nsecond\n";
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs") { DocumentText = text });

        var response = await runtime.Tools.DocumentRead("Editor.cs", sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(text, response.Value!.Text);
        Assert.Equal(3, response.Value.TotalLines);
        Assert.False(response.Value.Truncated);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task DocumentRead_InvalidRangeFailsBeforeRouting(int startLine, int? lineCount)
    {
        var response = await CreateRuntime().Tools.DocumentRead("Editor.cs", startLine: startLine, lineCount: lineCount);
        Assert.False(response.Success);
        Assert.Contains("greater than zero", response.Message);
    }

}
