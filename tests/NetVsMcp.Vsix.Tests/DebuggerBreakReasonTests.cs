using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using EnvDTE;

namespace NetVsMcp.Vsix.Tests;

public class DebuggerBreakReasonTests
{
    [Theory]
    [InlineData(dbgEventReason.dbgEventReasonBreakpoint, "breakpoint")]
    [InlineData(dbgEventReason.dbgEventReasonStep, "stepCompleted")]
    [InlineData(dbgEventReason.dbgEventReasonExceptionThrown, "exception")]
    [InlineData(dbgEventReason.dbgEventReasonExceptionNotHandled, "exception")]
    [InlineData(dbgEventReason.dbgEventReasonUserBreak, "manualPause")]
    [InlineData(dbgEventReason.dbgEventReasonNone, "unknown")]
    [InlineData(dbgEventReason.dbgEventReasonContextSwitch, "other")]
    [InlineData((dbgEventReason)999, "other")]
    public void PausedState_MapsNativeReasonAndPreservesItAcrossWire(dbgEventReason reason, string expected)
    {
        var state = new DebuggerStateInfo("dbgBreakMode",
            DebuggerBreakReason.Read(dbgDebugMode.dbgBreakMode, () => reason));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var wire = JsonSerializer.Serialize(state, options);
        var result = JsonSerializer.Deserialize<Contracts.DebuggerStateInfo>(wire, options);

        Assert.Equal(expected, result!.BreakReason);
    }

    [Theory]
    [InlineData(dbgDebugMode.dbgRunMode)]
    [InlineData(dbgDebugMode.dbgDesignMode)]
    public void OutsidePause_DoesNotReadOrReturnPreviousReason(dbgDebugMode mode)
    {
        Assert.Null(DebuggerBreakReason.Read(mode, () => throw new InvalidOperationException("Must not read")));
    }

    [Fact]
    public void UnsupportedReason_StillReturnsPausedState()
    {
        Assert.Equal("unknown", DebuggerBreakReason.Read(dbgDebugMode.dbgBreakMode,
            () => throw new COMException("Unsupported by debug engine")));
        Assert.Equal("unknown", DebuggerBreakReason.Read(dbgDebugMode.dbgBreakMode,
            () => throw new NotImplementedException()));
    }

    [Fact]
    public void OlderPayload_HasNoBreakReason()
    {
        var state = JsonSerializer.Deserialize<Contracts.DebuggerStateInfo>("{\"Mode\":\"dbgBreakMode\"}");
        Assert.Null(state!.BreakReason);
    }
}
