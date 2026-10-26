using System;
using Microsoft.VisualStudio.Debugger.Interop;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class BreakpointErrorTrackerTests
{
    [Fact]
    public void NotificationsAreBoundedNewestFirstAndDoNotLeakIntoTheNextGeneration()
    {
        var tracker = new BreakpointErrorTracker();
        for (var i = 0; i < 70; i++) tracker.Capture(1, 123, "engine", "warning", new string('x', 3000));
        var first = tracker.Read(1);
        Assert.True(first.Truncated);
        Assert.Equal(32, first.Items.Count);
        Assert.Equal(70, first.Items[0].Sequence);
        Assert.Equal(2048, first.Items[0].Message.Length);
        Assert.True(first.Items[0].MessageTruncated);
        tracker.Capture(2, 456, "newEngine", "error", "new");
        var next = tracker.Read(2);
        Assert.False(next.Truncated);
        Assert.Equal("new", Assert.Single(next.Items).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObserverCapturesAvailableErrorFieldsAndNeverMutatesTheEngine(bool fail)
    {
        var execution = new DebugExecutionTracker();
        execution.Initialize("dbgRunMode");
        var tracker = new BreakpointErrorTracker();
        var observer = new DebugExceptionEventObserver(execution, new DebugExceptionTracker(), tracker);
        var notification = new SyntheticError { Fail = fail };
        var id = typeof(IDebugBreakpointErrorEvent2).GUID;
        Assert.Equal(0, observer.Event(null!, null!, null!, null!, notification, ref id, 0));
        var captured = tracker.Read(execution.ObservedState().Generation);
        if (fail) Assert.Empty(captured.Items);
        else
        {
            var item = Assert.Single(captured.Items);
            Assert.Equal("No symbols for this document", item.Message);
            Assert.Equal("warning", item.ErrorType);
            Assert.Equal((uint)(enum_BP_ERROR_TYPE.BPET_TYPE_WARNING | enum_BP_ERROR_TYPE.BPET_SEV_HIGH), item.RawFlags);
            Assert.Null(item.ProcessId);
        }
    }

    private sealed class SyntheticError : IDebugEvent2, IDebugBreakpointErrorEvent2, IDebugErrorBreakpoint2, IDebugErrorBreakpointResolution2
    {
        public bool Fail;
        public int GetAttributes(out uint attributes) { attributes = 0; return 0; }
        public int GetErrorBreakpoint(out IDebugErrorBreakpoint2 error) { error = this; return 0; }
        public int GetBreakpointResolution(out IDebugErrorBreakpointResolution2 resolution) { resolution = this; return 0; }
        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 pending) => throw new InvalidOperationException("Do not change or infer ownership of a pending breakpoint.");
        public int GetBreakpointType(enum_BP_TYPE[] type) => 0;
        public int GetResolutionInfo(enum_BPERESI_FIELDS fields, BP_ERROR_RESOLUTION_INFO[] info)
        {
            info[0] = new() { dwFields = enum_BPERESI_FIELDS.BPERESI_MESSAGE | enum_BPERESI_FIELDS.BPERESI_TYPE,
                dwType = enum_BP_ERROR_TYPE.BPET_TYPE_WARNING | enum_BP_ERROR_TYPE.BPET_SEV_HIGH, bstrMessage = "No symbols for this document" };
            return Fail ? unchecked((int)0x80004005) : 0;
        }
    }
}
