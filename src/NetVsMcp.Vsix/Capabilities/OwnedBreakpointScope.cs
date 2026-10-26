using System;
using System.Collections.Generic;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>Only handles returned by this request are eligible; never searches by source location for deletion.</summary>
internal sealed class OwnedBreakpointScope<T> where T : class
{
    private sealed class Entry(T value, string? initialTag, string assignedTag)
    {
        internal T Value { get; } = value;
        internal string? InitialTag { get; } = initialTag;
        internal string AssignedTag { get; } = assignedTag;
        internal bool TagConfirmed { get; set; }
    }
    private readonly List<Entry> entries = new();
    internal void Add(T value, string? initialTag, string assignedTag) => entries.Add(new(value, initialTag, assignedTag));
    internal void ConfirmTag(T value) => entries.Find(entry => ReferenceEquals(entry.Value, value))!.TagConfirmed = true;

    internal TemporaryBreakpointCleanup Cleanup(Func<T, string?> readTag, Action<T> delete, bool creationFullyObserved)
    {
        var removed = 0;
        var preserved = 0;
        var failures = new List<string>();
        foreach (var entry in entries)
        {
            try
            {
                var tag = readTag(entry.Value);
                if (!string.Equals(tag, entry.AssignedTag, StringComparison.Ordinal) &&
                    (entry.TagConfirmed || !string.Equals(tag, entry.InitialTag, StringComparison.Ordinal)))
                { preserved++; continue; }
                delete(entry.Value);
                removed++;
            }
            catch (Exception ex)
            {
                if (failures.Count < 10) failures.Add(ex.Message.Length <= 512 ? ex.Message : ex.Message.Substring(0, 512));
            }
        }
        if (!creationFullyObserved) failures.Add("Breakpoint creation did not expose all ownership handles; reconcile the breakpoint list before retrying.");
        return new(entries.Count, removed, preserved, failures, creationFullyObserved && removed == entries.Count);
    }
}
