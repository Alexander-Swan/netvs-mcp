using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace NetVsMcp.Vsix;

[ComVisible(true)]
internal sealed class DebugExceptionEventObserver(DebugExecutionTracker execution, DebugExceptionTracker exceptions) : IDebugEventCallback2
{
    public int Event(IDebugEngine2 engine, IDebugProcess2 process, IDebugProgram2 program, IDebugThread2 thread,
        IDebugEvent2 debugEvent, ref Guid eventId, uint attributes)
    {
        if (eventId != typeof(IDebugExceptionEvent2).GUID && eventId != typeof(IDebugExceptionEvent150).GUID) return VSConstants.S_OK;
        try
        {
            var info = new EXCEPTION_INFO[1];
            string? description = null, message = null;
            if (debugEvent is IDebugExceptionEvent2 exception)
            {
                ErrorHandler.ThrowOnFailure(exception.GetException(info));
                if (ErrorHandler.Succeeded(exception.GetExceptionDescription(out var text))) description = text;
            }
            else
            {
                var modern = new EXCEPTION_INFO150[1];
                ErrorHandler.ThrowOnFailure(((IDebugExceptionEvent150)debugEvent).GetException(modern));
                info[0] = new EXCEPTION_INFO { bstrExceptionName = modern[0].bstrExceptionName, dwCode = modern[0].dwCode,
                    dwState = (enum_EXCEPTION_STATE)modern[0].dwState, guidType = modern[0].guidType };
            }
            try
            {
                if (debugEvent is IDebugManagedExceptionInfo2 managed && ErrorHandler.Succeeded(managed.GetExceptionMessage(out var text))) message = text;
                else if (debugEvent is IDebugExceptionEvent150 modern && ErrorHandler.Succeeded(modern.GetExceptionDetails(out var details)) &&
                    details is not null && ErrorHandler.Succeeded(details.GetExceptionMessage(out text))) message = text;
            }
            catch (COMException ex) { Trace.WriteLine("NetVsMcp: optional exception message unavailable: " + ex.Message); }
            int? pid = null, tid = null;
            if (process is null && program is not null) program.GetProcess(out process);
            var identity = new AD_PROCESS_ID[1];
            if (process is not null && ErrorHandler.Succeeded(process.GetPhysicalProcessId(identity)) &&
                identity[0].ProcessIdType == (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
                pid = checked((int)identity[0].dwProcessId);
            if (thread is not null && ErrorHandler.Succeeded(thread.GetThreadId(out var threadId))) tid = checked((int)threadId);
            var flags = info[0].dwState;
            var first = (flags & enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE) != 0;
            var second = (flags & enum_EXCEPTION_STATE.EXCEPTION_STOP_SECOND_CHANCE) != 0;
            var user = (flags & enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT) != 0;
            var kind = first && !second ? "thrown" : second && !first ? "notHandled" : user ? "userUnhandled" : "unknown";
            var state = execution.ObservedState();
            exceptions.Capture(info[0].guidType.ToString("D"), info[0].bstrExceptionName, description,
                (flags & enum_EXCEPTION_STATE.EXCEPTION_CODE_SUPPORTED) != 0 ? unchecked((int)info[0].dwCode) : (int?)null,
                second && !first, pid, tid, state, kind, flags.ToString(), message);
            if (state.StopId is not null && state.BreakReason == "exception") exceptions.Bind(state);
        }
        catch (Exception ex)
        {
            // Observers must never alter execution or fault the debug engine. No COM objects are retained.
            Trace.WriteLine("NetVsMcp: exception event evidence was unavailable: " + ex.Message);
        }
        return VSConstants.S_OK;
    }
}
