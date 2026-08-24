using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>
/// VS-side snapshot of a session, captured with plain/string-typed fields (e.g.
/// <see cref="DebuggerMode"/> as the raw DTE mode string) before being mapped onto the shared
/// <see cref="NetVsMcp.Contracts"/> wire types via <see cref="VsContractMapping"/>.
/// </summary>
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

    /// <summary>Raw DTE debugger-mode string (e.g. "dbgBreakMode"), not yet mapped to <see cref="DebuggerMode"/>.</summary>
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

internal sealed class VsHeartbeatRequest
{
    public VsHeartbeatRequest(VsSessionSnapshot session, IReadOnlyCollection<string> capabilities)
    {
        Session = session;
        Capabilities = capabilities;
    }

    public VsSessionSnapshot Session { get; }
    public IReadOnlyCollection<string> Capabilities { get; }

    public static VsHeartbeatRequest FromSnapshot(
        VsSessionSnapshot snapshot,
        IVisualStudioCapabilityCatalog capabilityCatalog)
        => new(snapshot, capabilityCatalog.CapabilityNames);
}

/// <summary>
/// Maps VS-side string/DTE-flavored session data onto the shared <see cref="NetVsMcp.Contracts"/>
/// wire types (<see cref="VsSessionRegistration"/>, <see cref="VsSessionUpdate"/>,
/// <see cref="VsSessionInfo"/>) that both this project and the broker consume directly. There used to
/// be a second, hand-maintained "Wire" copy of every DTO in this file; it's gone now that NetVsMcp.Vsix
/// can reference NetVsMcp.Contracts (multi-targeted to netstandard2.0) directly, so only this mapping
/// layer (raw strings -> enums) remains VSIX-specific.
/// </summary>
internal static class VsContractMapping
{
    public static VsSessionRegistration ToRegistration(VsRegistrationRequest request)
    {
        var session = request.Session;
        return new VsSessionRegistration(
            session.SessionId,
            session.ProcessId,
            session.VisualStudioVersion,
            session.Edition,
            session.SolutionName,
            session.SolutionPath,
            session.ActiveDocument,
            ToDebuggerMode(session.DebuggerMode),
            session.IsActiveWindow,
            ToCapabilities(request.Capabilities),
            VsRpcProtocol.CurrentVersion);
    }

    public static VsSessionUpdate ToUpdate(VsHeartbeatRequest request)
    {
        var session = request.Session;
        return new VsSessionUpdate(
            session.SessionId,
            session.SolutionName,
            session.SolutionPath,
            session.ActiveDocument,
            ToDebuggerMode(session.DebuggerMode),
            session.IsActiveWindow,
            ToCapabilities(request.Capabilities),
            VsRpcProtocol.CurrentVersion);
    }

    public static VsSessionInfo ToSessionInfo(VsSessionSnapshot snapshot, IVisualStudioCapabilityCatalog capabilities)
        => new(
            snapshot.SessionId,
            snapshot.ProcessId,
            snapshot.VisualStudioVersion,
            snapshot.Edition,
            snapshot.SolutionName,
            snapshot.SolutionPath,
            snapshot.ActiveDocument,
            ToDebuggerMode(snapshot.DebuggerMode),
            snapshot.IsActiveWindow,
            snapshot.LastSeenUtc,
            ToCapabilities(capabilities.CapabilityNames));

    public static DebuggerMode ToDebuggerMode(string? debuggerMode)
    {
        return debuggerMode switch
        {
            "Break" or "dbgBreakMode" => DebuggerMode.Break,
            "Run" or "dbgRunMode" => DebuggerMode.Run,
            "Design" or "dbgDesignMode" => DebuggerMode.Design,
            _ => DebuggerMode.Unknown
        };
    }

    public static IReadOnlyCollection<VsCapability> ToCapabilities(IReadOnlyCollection<string> capabilities)
    {
        return capabilities
            .Select(ToCapability)
            .Where(capability => capability.HasValue)
            .Select(capability => capability!.Value)
            .Distinct()
            .ToArray();
    }

    private static VsCapability? ToCapability(string capability)
    {
        return capability switch
        {
            "editor" or "Editor" => VsCapability.Editor,
            "navigation" or "Navigation" => VsCapability.Navigation,
            "build" or "Build" => VsCapability.Build,
            "debugger" or "Debugger" => VsCapability.Debugger,
            "diagnostics" or "Diagnostics" => VsCapability.Diagnostics,
            "tests" or "Tests" => VsCapability.Tests,
            "projectSystem" or "ProjectSystem" => VsCapability.ProjectSystem,
            _ => null
        };
    }
}

internal static class SessionIdentity
{
    public static string CurrentProcessSessionId()
    {
        return $"vs-{Process.GetCurrentProcess().Id}";
    }
}
