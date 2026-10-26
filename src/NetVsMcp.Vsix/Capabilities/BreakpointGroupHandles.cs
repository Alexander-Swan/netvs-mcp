using System;
using System.Collections.Generic;
using System.Threading;

namespace NetVsMcp.Vsix;

/// <summary>Mutates only enumerated handles whose ownership tag is still unchanged.</summary>
internal sealed class BreakpointGroupHandles<T> where T : class
{
    private readonly List<(T Handle, string? Tag)> handles = new();
    internal int Count => handles.Count;
    internal void Add(T handle, string? tag) => handles.Add((handle, tag));
    internal int Mutate(Func<T, string?> readTag, Action<T> mutation, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var entry in handles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(entry.Tag, readTag(entry.Handle), StringComparison.Ordinal)) continue;
            mutation(entry.Handle);
            count++;
        }
        return count;
    }
}
