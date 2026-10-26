using System.Threading;

namespace NetVsMcp.Vsix.Tests;

public sealed class BreakpointGroupHandlesTests
{
    private sealed class Handle(string tag) { internal string Tag = tag; internal bool Mutated; }

    [Fact]
    public void ColocatedCallerAndRetaggedOwnedHandlesArePreserved()
    {
        var caller = new Handle("developer");
        var owned = new Handle("owned");
        var adopted = new Handle("owned");
        var scope = new BreakpointGroupHandles<Handle>();
        scope.Add(owned, owned.Tag);
        scope.Add(adopted, adopted.Tag);
        adopted.Tag = "developer";
        Assert.Equal(1, scope.Mutate(h => h.Tag, h => h.Mutated = true, default));
        Assert.True(owned.Mutated);
        Assert.False(caller.Mutated);
        Assert.False(adopted.Mutated);
    }

    [Fact]
    public void CancellationBeforeMutationPreservesEveryHandle()
    {
        var owned = new Handle("owned");
        var scope = new BreakpointGroupHandles<Handle>();
        scope.Add(owned, owned.Tag);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<System.OperationCanceledException>(() => scope.Mutate(h => h.Tag, h => h.Mutated = true, cancel.Token));
        Assert.False(owned.Mutated);
    }
}
