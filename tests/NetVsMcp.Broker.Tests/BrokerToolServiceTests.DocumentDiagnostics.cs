using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DiagnosticsForDocument_LimitsOnlyAfterFilteringTheFullErrorList()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var otherErrors = Enumerable.Range(0, 150).Select(index => Diagnostic(@"C:\Code\NetVsMcp\Other.cs", index));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DiagnosticItems = otherErrors.Concat([
                Diagnostic(@"C:\Code\NetVsMcp\Program.cs", 151),
                Diagnostic(@"C:\Code\NetVsMcp\Program.cs", 152)]).ToArray()
        };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DiagnosticsForDocument("Program.cs", maxItems: 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(151, Assert.Single(response.Value!.Items).Line);
        Assert.Equal(int.MaxValue, session.LastErrorListRequest!.MaxItems);
    }

    [Theory]
    [InlineData("src/Program.cs")]
    [InlineData("C:/Code/NetVsMcp/src/../src/Program.cs")]
    [InlineData("c:/code/netvsmcp/SRC/program.cs")]
    public async Task DiagnosticsForDocument_ResolvesFullPathWithoutMatchingOtherSameNamedFiles(string documentPath)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DiagnosticItems = [
                Diagnostic(@"C:\Code\Different\src\Program.cs", 1),
                Diagnostic(@"C:\Code\NetVsMcp\other\Program.cs", 2),
                Diagnostic(@"C:\Code\NetVsMcp\src\Program.cs", 3),
                Diagnostic(null, 4)]
        });

        var response = await runtime.Tools.DiagnosticsForDocument(documentPath, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(3, Assert.Single(response.Value!.Items).Line);
    }

    [Fact]
    public async Task DiagnosticsForDocument_ExcludesWarningsBeforeApplyingTheLimit()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DiagnosticItems = [Diagnostic("Program.cs", 1, "Warning"), Diagnostic("Program.cs", 2)]
        };
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DiagnosticsForDocument("Program.cs", includeWarnings: false, maxItems: 1, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal(2, Assert.Single(response.Value!.Items).Line);
        Assert.False(session.LastErrorListRequest!.IncludeWarnings);
    }

    private static ErrorListItemInfo Diagnostic(string? file, int line, string level = "Error") =>
        new("Diagnostic", file, line, 1, level, "Project");
}
