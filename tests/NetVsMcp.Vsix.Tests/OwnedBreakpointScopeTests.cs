using System;
using System.Collections.Generic;

namespace NetVsMcp.Vsix.Tests;

public class OwnedBreakpointScopeTests
{
    private sealed class Handle(string? tag) { internal string? Tag = tag; internal bool Deleted; }

    [Fact]
    public void CleanupNeverSearchesForUnrelatedHandlesOrDeletesAdoptedBreakpoints()
    {
        var existing = new Handle("developer");
        var owned = new Handle("owner");
        var adopted = new Handle("owner");
        var scope = new OwnedBreakpointScope<Handle>();
        scope.Add(owned, null, "owner"); scope.ConfirmTag(owned);
        scope.Add(adopted, null, "owner"); scope.ConfirmTag(adopted);
        adopted.Tag = null; // A developer removed the ownership tag after setup.
        var result = scope.Cleanup(h => h.Tag, h => h.Deleted = true, true);
        Assert.True(owned.Deleted);
        Assert.False(existing.Deleted);
        Assert.False(adopted.Deleted);
        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Preserved);
        Assert.False(result.Complete);
    }

    [Fact]
    public void FailedTagSetupCanStillCleanTheExactNewHandle()
    {
        var owned = new Handle(null);
        var scope = new OwnedBreakpointScope<Handle>();
        scope.Add(owned, null, "owner");
        var result = scope.Cleanup(h => h.Tag, h => h.Deleted = true, true);
        Assert.True(result.Complete);
        Assert.True(owned.Deleted);
    }

    [Fact]
    public void CleanupContinuesAfterADeleteFailureAndReportsUnknownCreation()
    {
        var failed = new Handle("owner");
        var remaining = new Handle("owner");
        var scope = new OwnedBreakpointScope<Handle>();
        scope.Add(failed, null, "owner"); scope.Add(remaining, null, "owner");
        var result = scope.Cleanup(h => h.Tag, h =>
        {
            if (ReferenceEquals(h, failed)) throw new InvalidOperationException(new string('e', 2000));
            h.Deleted = true;
        }, false);
        Assert.True(remaining.Deleted);
        Assert.False(result.Complete);
        Assert.Equal(512, result.Failures[0].Length);
        Assert.Equal(2, result.Failures.Count);
        Assert.Equal(1, result.Removed);
    }

    [Fact]
    public void UnknownUnreturnedHandlesCannotBeReportedAsCompleteCleanup()
    {
        var result = new OwnedBreakpointScope<Handle>().Cleanup(h => h.Tag, _ => throw new Exception(), false);
        Assert.Equal(0, result.Created);
        Assert.False(result.Complete);
        Assert.Single(result.Failures);
    }
}
