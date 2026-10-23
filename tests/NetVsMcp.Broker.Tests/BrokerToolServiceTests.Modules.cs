using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task ModuleList_RoutesFiltersAndPreservesDebuggerSymbolData()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Other"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        runtime.Connections.AddOrUpdate("vs-2", new FakeVisualStudioSessionRpc("Other.cs"));

        var response = await runtime.Tools.ModuleList(processId: 1234, name: "App", includeSymbolLoadInformation: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.Supported);
        Assert.Equal(session.ModulesResult, response.Value);
        Assert.Equal(1234, session.LastModuleListRequest!.ProcessId);
        Assert.Equal("App", session.LastModuleListRequest.Name);
        Assert.True(session.LastModuleListRequest.IncludeSymbolLoadInformation);
        var module = Assert.Single(response.Value.Modules);
        Assert.True(module.SymbolsLoaded);
        Assert.Equal("C:/app/App.pdb", module.SymbolFile);
        Assert.Equal("Symbols loaded.", module.SymbolLoadInformation);
    }

    [Fact]
    public async Task ModuleList_PreservesUnsupportedEngineResult()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            ModulesResult = new ModuleListResult(false, "Unsupported debugger engine.", [])
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.ModuleList(sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.False(response.Value!.Supported);
        Assert.Equal("Unsupported debugger engine.", response.Value.Message);
        Assert.Empty(response.Value.Modules);
        Assert.Null(session.LastModuleListRequest!.ProcessId);
        Assert.False(session.LastModuleListRequest.IncludeSymbolLoadInformation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ModuleList_RejectsInvalidProcessIdBeforeDispatch(int processId)
    {
        var runtime = CreateRuntime();
        var response = await runtime.Tools.ModuleList(processId: processId);

        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }
}
