using System;
using System.Linq;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Symbols;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    private static (string File, int Line, int Column)? ReadThreadSourcePosition(int? processId, int? threadId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (processId is null || threadId is null) return null;
        var process = DkmProcess.GetProcesses().SingleOrDefault(p => !p.IsUnloaded && p.LivePart?.Id == processId);
        var thread = process?.GetThreads().SingleOrDefault(t => t.SystemPart?.Id == threadId);
        var frame = thread?.GetTopStackFrame();
        var source = frame?.GetInstructionSymbol()?.GetSourcePosition(DkmSourcePositionFlags.None, null, out _);
        return source is null || string.IsNullOrWhiteSpace(source.DocumentName) ? null :
            (source.DocumentName, checked((int)source.TextSpan.StartLine), checked((int)source.TextSpan.StartColumn));
    }
}
