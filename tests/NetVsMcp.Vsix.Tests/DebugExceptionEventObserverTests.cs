using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace NetVsMcp.Vsix.Tests;

public class DebugExceptionEventObserverTests
{
    private sealed class Legacy : IDebugEvent2, IDebugExceptionEvent2, IDebugManagedExceptionInfo2
    {
        public enum_EXCEPTION_STATE Flags;
        public int Error;
        public int GetAttributes(out uint attributes) { attributes = 0; return 0; }
        public int GetException(EXCEPTION_INFO[] info)
        {
            info[0] = new() { bstrExceptionName = "System.InvalidOperationException", dwCode = 0xE0434352, dwState = Flags };
            return Error;
        }
        public int GetExceptionDescription(out string description) { description = "Engine description"; return 0; }
        public int GetExceptionMessage(out string message) { message = "Exact raw message"; return 0; }
        public int GetExceptionBoundaryType(EXCEPTION_BOUNDARY_TYPE[] boundary) => 0;
        public int CanPassToDebuggee() => throw new InvalidOperationException("Must not affect execution.");
        public int PassToDebuggee(int value) => throw new InvalidOperationException("Must not affect execution.");
    }

    private sealed class Modern : IDebugEvent2, IDebugExceptionEvent150
    {
        public int GetAttributes(out uint attributes) { attributes = 0; return 0; }
        public int GetException(EXCEPTION_INFO150[] info)
        {
            info[0] = new() { bstrExceptionName = "ModernException", dwState = (uint)enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE };
            return 0;
        }
        public int GetExceptionDetails(out IDebugExceptionDetails details) { details = null!; return unchecked((int)0x80004001); }
    }

    private static NetVsMcp.Contracts.DebugExceptionEvidence? Observe(IDebugEvent2 notification, Guid eventId)
    {
        var execution = new DebugExecutionTracker();
        execution.Initialize("dbgRunMode");
        var tracker = new DebugExceptionTracker();
        var observer = new DebugExceptionEventObserver(execution, tracker);
        Assert.Equal(0, observer.Event(null!, null!, null!, null!, notification, ref eventId, 0));
        execution.Record("dbgBreakMode", "exception");
        var state = execution.State("dbgBreakMode");
        tracker.Bind(state);
        return tracker.Read(state);
    }

    [Fact]
    public void LegacyEventsCaptureRawMessageWithoutChangingExceptionHandling()
    {
        var evidence = Observe(new Legacy { Flags = enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_CODE_SUPPORTED }, typeof(IDebugExceptionEvent2).GUID);
        Assert.Equal("Exact raw message", evidence!.Message);
        Assert.Equal("0xE0434352", evidence.Code);
        Assert.Equal("thrown", evidence.NotificationKind);
        Assert.Null(evidence.Handled);
    }

    [Fact]
    public void ModernEventsWorkWithoutTheLegacyEventInterface()
    {
        var evidence = Observe(new Modern(), typeof(IDebugExceptionEvent150).GUID);
        Assert.Equal("ModernException", evidence!.TypeName);
        Assert.Equal("notHandled", evidence.NotificationKind);
        Assert.False(evidence.Handled);
        Assert.Null(evidence.Message);
        Assert.Null(evidence.Code);
    }

    [Fact]
    public void UserUnhandledAndAmbiguousStageFlagsDoNotClaimFinalHandling()
    {
        foreach (var flags in new[] { enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT,
            enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE | enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE })
            Assert.Null(Observe(new Legacy { Flags = flags }, typeof(IDebugExceptionEvent2).GUID)!.Handled);
    }

    [Fact]
    public void FailedAndUnrelatedNotificationsDoNotFaultTheEngineOrInventEvidence()
    {
        Assert.Null(Observe(new Legacy { Error = unchecked((int)0x80004005) }, typeof(IDebugExceptionEvent2).GUID));
        Assert.Null(Observe(new Legacy(), Guid.NewGuid()));
    }
}
