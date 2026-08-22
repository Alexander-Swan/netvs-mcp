using System;
using System.IO;

namespace NetVsMcp.Vsix;

/// <summary>
/// Pure, COM-free matching logic shared by local and remote-transport process attach (see
/// DebuggerCapabilityService.FindProcesses and .AttachRemote). Kept separate from the EnvDTE-
/// touching code specifically so it can be unit tested without a live Visual Studio host -
/// everything here takes plain primitives in and returns a plain bool/string out.
/// </summary>
internal static class AttachSelectors
{
    /// <summary>
    /// True if a candidate transport's name matches the requested selector: exact match first,
    /// then substring match, both case-insensitive. Mirrors "Attach to Process" dialog behavior
    /// where a partial transport name (e.g. "SSH") is enough to select "SSH" or "My SSH Host".
    /// </summary>
    public static bool MatchesTransportName(string candidateName, string requestedTransport)
    {
        return string.Equals(candidateName, requestedTransport, StringComparison.OrdinalIgnoreCase)
            || candidateName.IndexOf(requestedTransport, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// True if a candidate process matches an optional process id filter and/or optional process
    /// name filter. A null filter always matches. The name filter matches against either the
    /// candidate's full name or just its file name, since some debugger transports/engines report
    /// process names as a bare file name and others as a full path.
    /// </summary>
    public static bool MatchesProcessSelector(
        int candidateProcessId,
        string candidateProcessName,
        int? filterProcessId,
        string? filterProcessName)
    {
        if (filterProcessId is not null && candidateProcessId != filterProcessId.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filterProcessName)
            && !string.Equals(Path.GetFileName(candidateProcessName), filterProcessName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidateProcessName, filterProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
