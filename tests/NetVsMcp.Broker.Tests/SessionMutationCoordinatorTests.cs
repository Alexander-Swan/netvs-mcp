using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public class SessionMutationCoordinatorTests
{
    [Fact]
    public async Task AbandonedRpcKeepsItsLeaseUntilTheActualWorkFinishes()
    {
        var coordinator = new SessionMutationCoordinator();
        var finish = new TaskCompletionSource<DebuggerStateInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new FakeVisualStudioSessionRpc("Editor.cs") { ContinueHandler = () => finish.Task };
        var proxy = CoordinatedSessionRpc.Wrap(target, "one", coordinator);
        var original = proxy.DebugContinueAsync(default);
        // A caller can stop waiting; ownership belongs to the underlying RPC, not that wait.
        await Assert.ThrowsAsync<TimeoutException>(() => original.WaitAsync(TimeSpan.FromMilliseconds(10)));
        await Assert.ThrowsAsync<SessionMutationConflictException>(() => proxy.DebugContinueAsync(default));
        Assert.NotNull(coordinator.Inspect("one").Active);
        finish.SetResult(new DebuggerStateInfo("dbgRunMode"));
        await original;
        Assert.Null(coordinator.Inspect("one").Active);
    }

    [Fact]
    public void ConflictingCommandsFailButOtherSessionsRemainIndependent()
    {
        var coordinator = new SessionMutationCoordinator();
        using (coordinator.Enter("one", "build"))
        {
            Assert.Throws<SessionMutationConflictException>(() => coordinator.Enter("one", "continue"));
            using var other = coordinator.Enter("two", "continue");
            Assert.Equal("build", coordinator.Inspect("one").Active);
        }
        using var released = coordinator.Enter("one", "continue");
    }

    [Fact]
    public async Task PausingRejectsMutationButPreservesReadOnlyRpc()
    {
        var coordinator = new SessionMutationCoordinator();
        coordinator.Inspect("one", paused: true);
        var proxy = CoordinatedSessionRpc.Wrap(new FakeVisualStudioSessionRpc("Editor.cs"), "one", coordinator);
        await Assert.ThrowsAsync<NotSupportedException>(() => proxy.GetStatusAsync(default));
        await Assert.ThrowsAsync<SessionMutationConflictException>(() => proxy.DebugContinueAsync(default));
        coordinator.Inspect("one", paused: false);
        await Assert.ThrowsAsync<NotSupportedException>(() => proxy.DebugContinueAsync(default));
        Assert.Null(coordinator.Inspect("one").Active);
    }

    [Fact]
    public void PausingDoesNotReleaseAnActiveCommand()
    {
        var coordinator = new SessionMutationCoordinator();
        using var lease = coordinator.Enter("one", "build");
        Assert.Equal("build", coordinator.Inspect("one", paused: true).Active);
        Assert.Equal("build", coordinator.Inspect("one", paused: false).Active);
        Assert.Throws<SessionMutationConflictException>(() => coordinator.Enter("one", "continue"));
    }
}
