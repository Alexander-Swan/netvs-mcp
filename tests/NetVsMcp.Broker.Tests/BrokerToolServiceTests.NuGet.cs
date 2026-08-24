using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
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
}
