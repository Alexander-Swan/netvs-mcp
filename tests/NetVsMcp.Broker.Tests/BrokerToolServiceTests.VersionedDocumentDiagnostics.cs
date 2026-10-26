using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task VersionedDiagnosticsForwardTheGuardAndReportStaleTextAsAFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DocumentDiagnosticsHandler = _ => new("Editor.cs", "current", "p", "Project", "C#", false, false,
                0, 0, null, [], Message: "Different source version")
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var response = await runtime.Tools.DocumentDiagnostics("Editor.cs", "expected", maxItems: 1, sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal("stale_document", response.Metadata!["error_code"]);
        Assert.Equal("current", response.Value!.TextVersion);
        Assert.Equal("expected", rpc.LastDocumentDiagnosticsRequest!.ExpectedTextVersion);
        Assert.Equal(1, rpc.LastDocumentDiagnosticsRequest.MaxItems);
    }

    [Fact]
    public async Task UnsupportedVersionedDiagnosticsDoNotFallBackToUnversionedErrors()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DocumentDiagnosticsHandler = _ => throw MissingFrameInspectionMethod()
        });
        var response = await runtime.Tools.DocumentDiagnostics("Editor.cs", sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
    }
}
