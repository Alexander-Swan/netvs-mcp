using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NetVsMcp.Vsix;

internal sealed class VsSessionSnapshot
{
    public VsSessionSnapshot(
        string sessionId,
        int processId,
        string? visualStudioVersion,
        string? edition,
        string? solutionName,
        string? solutionPath,
        string? activeDocument,
        string debuggerMode,
        bool isActiveWindow,
        DateTimeOffset lastSeenUtc)
    {
        SessionId = sessionId;
        ProcessId = processId;
        VisualStudioVersion = visualStudioVersion;
        Edition = edition;
        SolutionName = solutionName;
        SolutionPath = solutionPath;
        ActiveDocument = activeDocument;
        DebuggerMode = debuggerMode;
        IsActiveWindow = isActiveWindow;
        LastSeenUtc = lastSeenUtc;
    }

    public string SessionId { get; }
    public int ProcessId { get; }
    public string? VisualStudioVersion { get; }
    public string? Edition { get; }
    public string? SolutionName { get; }
    public string? SolutionPath { get; }
    public string? ActiveDocument { get; }
    public string DebuggerMode { get; }
    public bool IsActiveWindow { get; }
    public DateTimeOffset LastSeenUtc { get; }
}

internal sealed class VsRegistrationRequest
{
    public VsRegistrationRequest(VsSessionSnapshot session, IReadOnlyCollection<string> capabilities)
    {
        Session = session;
        Capabilities = capabilities;
    }

    public VsSessionSnapshot Session { get; }
    public IReadOnlyCollection<string> Capabilities { get; }

    public static VsRegistrationRequest FromSnapshot(
        VsSessionSnapshot snapshot,
        IVisualStudioCapabilityCatalog capabilityCatalog)
        => new(snapshot, capabilityCatalog.CapabilityNames);
}

internal static class SessionIdentity
{
    public static string CurrentProcessSessionId()
    {
        return $"vs-{Process.GetCurrentProcess().Id}";
    }
}
