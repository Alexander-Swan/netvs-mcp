using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

internal enum VsCapabilityWire
{
    Editor,
    Navigation,
    Build,
    Debugger,
    Diagnostics,
    Tests,
    ProjectSystem
}

internal enum DebuggerModeWire
{
    Unknown,
    Design,
    Run,
    Break
}

internal sealed class VsSessionRegistrationWire
{
    public VsSessionRegistrationWire(
        string sessionId,
        int processId,
        string? visualStudioVersion,
        string? edition,
        string? solutionName,
        string? solutionPath,
        string? activeDocument,
        DebuggerModeWire debuggerMode,
        bool isActiveWindow,
        IReadOnlyCollection<VsCapabilityWire> capabilities)
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
        Capabilities = capabilities;
    }

    public string SessionId { get; }
    public int ProcessId { get; }
    public string? VisualStudioVersion { get; }
    public string? Edition { get; }
    public string? SolutionName { get; }
    public string? SolutionPath { get; }
    public string? ActiveDocument { get; }
    public DebuggerModeWire DebuggerMode { get; }
    public bool IsActiveWindow { get; }
    public IReadOnlyCollection<VsCapabilityWire> Capabilities { get; }

    public static VsSessionRegistrationWire FromRequest(VsRegistrationRequest request)
    {
        var session = request.Session;
        return new VsSessionRegistrationWire(
            session.SessionId,
            session.ProcessId,
            session.VisualStudioVersion,
            session.Edition,
            session.SolutionName,
            session.SolutionPath,
            session.ActiveDocument,
            VsContractWire.ToDebuggerMode(session.DebuggerMode),
            session.IsActiveWindow,
            VsContractWire.ToCapabilities(request.Capabilities));
    }
}

internal sealed class VsSessionUpdateWire
{
    public VsSessionUpdateWire(
        string sessionId,
        string? solutionName,
        string? solutionPath,
        string? activeDocument,
        DebuggerModeWire debuggerMode,
        bool isActiveWindow,
        IReadOnlyCollection<VsCapabilityWire>? capabilities)
    {
        SessionId = sessionId;
        SolutionName = solutionName;
        SolutionPath = solutionPath;
        ActiveDocument = activeDocument;
        DebuggerMode = debuggerMode;
        IsActiveWindow = isActiveWindow;
        Capabilities = capabilities;
    }

    public string SessionId { get; }
    public string? SolutionName { get; }
    public string? SolutionPath { get; }
    public string? ActiveDocument { get; }
    public DebuggerModeWire DebuggerMode { get; }
    public bool IsActiveWindow { get; }
    public IReadOnlyCollection<VsCapabilityWire>? Capabilities { get; }

    public static VsSessionUpdateWire FromRequest(VsHeartbeatRequest request)
    {
        var session = request.Session;
        return new VsSessionUpdateWire(
            session.SessionId,
            session.SolutionName,
            session.SolutionPath,
            session.ActiveDocument,
            VsContractWire.ToDebuggerMode(session.DebuggerMode),
            session.IsActiveWindow,
            VsContractWire.ToCapabilities(request.Capabilities));
    }
}

internal sealed class VsSessionInfoWire
{
    public VsSessionInfoWire(
        string sessionId,
        int processId,
        string? visualStudioVersion,
        string? edition,
        string? solutionName,
        string? solutionPath,
        string? activeDocument,
        DebuggerModeWire debuggerMode,
        bool isActiveWindow,
        DateTimeOffset lastSeenUtc,
        IReadOnlyCollection<VsCapabilityWire> capabilities)
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
        Capabilities = capabilities;
    }

    public string SessionId { get; }
    public int ProcessId { get; }
    public string? VisualStudioVersion { get; }
    public string? Edition { get; }
    public string? SolutionName { get; }
    public string? SolutionPath { get; }
    public string? ActiveDocument { get; }
    public DebuggerModeWire DebuggerMode { get; }
    public bool IsActiveWindow { get; }
    public DateTimeOffset LastSeenUtc { get; }
    public IReadOnlyCollection<VsCapabilityWire> Capabilities { get; }

    public static VsSessionInfoWire FromSnapshot(VsSessionSnapshot snapshot, IVisualStudioCapabilityCatalog capabilities)
        => new(
            snapshot.SessionId,
            snapshot.ProcessId,
            snapshot.VisualStudioVersion,
            snapshot.Edition,
            snapshot.SolutionName,
            snapshot.SolutionPath,
            snapshot.ActiveDocument,
            VsContractWire.ToDebuggerMode(snapshot.DebuggerMode),
            snapshot.IsActiveWindow,
            snapshot.LastSeenUtc,
            VsContractWire.ToCapabilities(capabilities.CapabilityNames));
}

internal sealed class ToolResponseWire
{
    public ToolResponseWire(bool success, string? message = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Success = success;
        Message = message;
        Metadata = metadata;
    }

    public bool Success { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    public static ToolResponseWire Ok(string? message = null) => new(true, message);
    public static ToolResponseWire Fail(string message) => new(false, message);
}

internal sealed class ToolResponseWire<T>
{
    public ToolResponseWire(bool success, T? value, string? message = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Success = success;
        Value = value;
        Message = message;
        Metadata = metadata;
    }

    public bool Success { get; }
    public T? Value { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    public static ToolResponseWire<T> Ok(T value, string? message = null) => new(true, value, message);
    public static ToolResponseWire<T> Fail(string message) => new(false, default, message);
}

internal static class VsContractWire
{
    public static DebuggerModeWire ToDebuggerMode(string? debuggerMode)
    {
        return debuggerMode switch
        {
            "Break" or "dbgBreakMode" => DebuggerModeWire.Break,
            "Run" or "dbgRunMode" => DebuggerModeWire.Run,
            "Design" or "dbgDesignMode" => DebuggerModeWire.Design,
            _ => DebuggerModeWire.Unknown
        };
    }

    public static IReadOnlyCollection<VsCapabilityWire> ToCapabilities(IReadOnlyCollection<string> capabilities)
    {
        return capabilities
            .Select(ToCapability)
            .Where(capability => capability.HasValue)
            .Select(capability => capability!.Value)
            .Distinct()
            .ToArray();
    }

    private static VsCapabilityWire? ToCapability(string capability)
    {
        return capability switch
        {
            "editor" or "Editor" => VsCapabilityWire.Editor,
            "navigation" or "Navigation" => VsCapabilityWire.Navigation,
            "build" or "Build" => VsCapabilityWire.Build,
            "debugger" or "Debugger" => VsCapabilityWire.Debugger,
            "diagnostics" or "Diagnostics" => VsCapabilityWire.Diagnostics,
            "tests" or "Tests" => VsCapabilityWire.Tests,
            "projectSystem" or "ProjectSystem" => VsCapabilityWire.ProjectSystem,
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
