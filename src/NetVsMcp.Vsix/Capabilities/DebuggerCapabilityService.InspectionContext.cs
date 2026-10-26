using System;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    private static DebugInspectionContext CaptureInspectionContext(DebuggerStateInfo state, EnvDTE.Thread thread,
        StackFrame frame, StackFrames frames, int requestedIndex)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        int? processId = null, index = requestedIndex > 0 ? requestedIndex : null;
        try { processId = thread.Program?.Process?.ProcessID; }
        catch (Exception ex) when (ex is COMException or NotSupportedException or NotImplementedException) { }
        if (processId is <= 0) processId = null;
        // Never identify recursive frames by function name; depth is unique within this thread.
        index ??= DebuggerFrameSelector.FindIndex(frame,
            () => { ThreadHelper.ThrowIfNotOnUIThread(); return frames.Count; },
            i => { ThreadHelper.ThrowIfNotOnUIThread(); return frames.Item(i); }, GetFrameDepth);
        CallStackFrameInfo? source = null;
        try { source = CallStackFrameInfo.FromStackFrame(frame, index); }
        catch (Exception ex) when (ex is COMException or NotSupportedException or NotImplementedException) { }
        return new(state.StopId!, state.Generation!.Value, processId, thread.ID, index, GetFrameDepth(frame),
            source?.FunctionName, source?.File, source?.Line > 0 ? source.Line : null);
    }
}
