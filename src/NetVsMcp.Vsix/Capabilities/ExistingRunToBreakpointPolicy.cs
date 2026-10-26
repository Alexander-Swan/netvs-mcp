using System;
using System.Linq;

namespace NetVsMcp.Vsix;

internal static class ExistingRunToBreakpointPolicy
{
    internal static bool CanReuse(bool enabled, bool stopping, bool conditional, bool hasHitCount, string? filter, int processId)
    {
        if (!enabled || !stopping || conditional || hasHitCount || processId <= 0) return false;
        if (string.IsNullOrWhiteSpace(filter)) return true; // The workflow requires a single debugged process.
        var normalized = new string(filter!.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return string.Equals(normalized, "ProcessID==" + processId, StringComparison.OrdinalIgnoreCase);
    }
}
