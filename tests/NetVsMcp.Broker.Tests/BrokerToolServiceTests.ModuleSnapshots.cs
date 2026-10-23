using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DebugWaitForBreak_CollectsModulesAfterRunningSessionPauses()
    {
        var runtime = CreateRuntime();
        var statusReads = 0;
        var moduleCalls = 0;
        var expected = new ModuleListResult(true, null, []);
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(new DebuggerStateInfo(
                ++statusReads < 3 ? "dbgRunMode" : "dbgBreakMode", BreakReason: "breakpoint")),
            ModuleListHandler = (_, _) =>
            {
                Assert.True(statusReads >= 3);
                moduleCalls++;
                return Task.FromResult(expected);
            }
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.DebugWaitForBreak(include: ["callStack", "modules"],
            moduleOptions: new ModuleListRequest { ProcessId = 1234, Name = "App.dll", IncludeSymbolLoadInformation = true },
            sessionId: "vs-1");

        Assert.True(response.Success, response.Message);
        Assert.False(response.Value!.TimedOut);
        Assert.Equal("breakpoint", response.Value.State.BreakReason);
        Assert.NotNull(response.Value.CallStack);
        Assert.Same(expected, response.Value.Modules);
        Assert.Null(response.Value.UnrecognizedInclude);
        Assert.Equal(1, moduleCalls);
        Assert.Equal(1234, session.LastModuleListRequest!.ProcessId);
        Assert.Equal("App.dll", session.LastModuleListRequest.Name);
        Assert.True(session.LastModuleListRequest.IncludeSymbolLoadInformation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DebugSnapshots_IncludeModulesWithDefaultOptions(bool wait)
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = wait
            ? await runtime.Tools.DebugWaitForBreak(include: ["modules"], sessionId: "vs-1")
            : await runtime.Tools.DebugSnapshot(include: ["modules"], sessionId: "vs-1");

        Assert.True(response.Success, response.Message);
        Assert.Same(session.ModulesResult, response.Value!.Modules);
        Assert.Null(response.Value.CallStack);
        Assert.Null(session.LastModuleListRequest!.ProcessId);
        Assert.Null(session.LastModuleListRequest.Name);
        Assert.False(session.LastModuleListRequest.IncludeSymbolLoadInformation);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DebugSnapshots_DoNotFetchModulesUnlessIncluded(bool wait, bool emptyInclude)
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgBreakMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        string[]? include = emptyInclude ? [] : null;

        var response = wait
            ? await runtime.Tools.DebugWaitForBreak(include: include, moduleOptions: new() { Name = "App", ProcessId = -1 }, sessionId: "vs-1")
            : await runtime.Tools.DebugSnapshot(include: include, moduleOptions: new() { Name = "App", ProcessId = -1 }, sessionId: "vs-1");

        Assert.True(response.Success, response.Message);
        Assert.Null(response.Value!.Modules);
        Assert.Null(session.LastModuleListRequest);
        Assert.Equal(emptyInclude, response.Value.CallStack is null);
    }

    [Theory]
    [InlineData(false, "dbgRunMode")]
    [InlineData(true, "dbgRunMode")]
    [InlineData(false, "dbgDesignMode")]
    [InlineData(true, "dbgDesignMode")]
    [InlineData(false, "dbgModalBlock")]
    [InlineData(true, "dbgModalBlock")]
    public async Task DebugSnapshots_SkipModulesOutsideBreakMode(bool wait, string mode)
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = mode };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = wait
            ? await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 1, include: ["modules"], sessionId: "vs-1")
            : await runtime.Tools.DebugSnapshot(include: ["modules"], sessionId: "vs-1");

        Assert.True(response.Success, response.Message);
        Assert.Equal(mode, response.Value!.State.Mode);
        Assert.Equal(wait && mode == "dbgRunMode", response.Value.TimedOut);
        Assert.Null(response.Value.Modules);
        Assert.Null(session.LastModuleListRequest);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DebugSnapshots_PreserveOtherSectionsWhenModuleListingIsUnsupported(bool wait, bool olderExtension)
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusMode = "dbgBreakMode",
            ModuleListHandler = (_, _) => olderExtension
                ? throw MissingFrameInspectionMethod()
                : Task.FromResult(new ModuleListResult(false, "No Concord support.", []))
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "App"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = wait
            ? await runtime.Tools.DebugWaitForBreak(include: ["callStack", "modules"], sessionId: "vs-1")
            : await runtime.Tools.DebugSnapshot(include: ["callStack", "modules"], sessionId: "vs-1");

        Assert.True(response.Success, response.Message);
        Assert.NotNull(response.Value!.CallStack);
        Assert.False(response.Value.Modules!.Supported);
        Assert.Empty(response.Value.Modules.Modules);
        Assert.Contains(olderExtension ? "Update the extension" : "No Concord support", response.Value.Modules.Message);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task DebugSnapshots_ValidateModuleProcessIdBeforeDispatch(bool wait, int processId)
    {
        var runtime = CreateRuntime();
        var response = wait
            ? await runtime.Tools.DebugWaitForBreak(include: ["modules"], moduleOptions: new() { ProcessId = processId })
            : await runtime.Tools.DebugSnapshot(action: DebugAdvanceAction.Continue, include: ["modules"], moduleOptions: new() { ProcessId = processId });

        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.InvalidRequest, response.Metadata!["error_code"]);
    }
}
