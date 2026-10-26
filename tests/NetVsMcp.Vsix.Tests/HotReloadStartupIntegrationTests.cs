using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix.Tests;

[Collection(MockedVS.Collection)]
public sealed class HotReloadStartupIntegrationTests
{
    public HotReloadStartupIntegrationTests(GlobalServiceProvider services) => services.Reset();

    [Fact]
    public async Task StartDoesNotReturnBeforeHotReloadInitializationCompletes()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = fixture.HitTarget;
        var start = fixture.Service.StartAsync(CancellationToken.None);
        Assert.Equal(1, fixture.GoCalls);
        Assert.False(start.IsCompleted);
        notification.Signal.Notify("NoChanges");
        var result = await start;
        Assert.Equal("dbgBreakMode", result.Mode);
        Assert.Equal(1, fixture.GoCalls);
        Assert.True(notification.Disposed);
        Assert.Contains(fixture.Service.Events.Snapshot().Events, e => e.Kind == "hotReload.initialized");
    }

    [Fact]
    public async Task FastCompletionInsideTheInitialApplyIsRetained()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = fixture.HitTarget;
        fixture.OnBaselineApply = () => notification.Signal.Notify("NoChanges");
        await fixture.Service.StartAsync(CancellationToken.None);
        Assert.Equal(1, fixture.GoCalls);
        Assert.True(notification.Disposed);
    }

    [Fact]
    public async Task FailedApplyCannotBeReportedAsInitialized()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = fixture.HitTarget;
        fixture.OnBaselineApply = () => notification.Signal.Notify("ApplyUpdateFailure");
        await fixture.Service.StartAsync(CancellationToken.None);
        var apply = await fixture.Service.ApplyHotReloadAsync(CancellationToken.None);
        Assert.False(apply.Success);
        Assert.Contains("not confirmed", apply.Message);
        Assert.Contains(fixture.Service.Events.Snapshot().Events, e => e.Kind == "hotReload.unconfirmed");
        Assert.Equal(1, fixture.GoCalls);
    }

    [Fact]
    public async Task CancellationAfterGoCannotEnableUnconfirmedHotReload()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        using var cancellation = new CancellationTokenSource();
        fixture.OnGo = fixture.HitTarget;
        var start = fixture.Service.StartAsync(cancellation.Token);
        Assert.False(start.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        var apply = await fixture.Service.ApplyHotReloadAsync(CancellationToken.None);
        Assert.False(apply.Success);
        Assert.True(notification.Disposed);
        Assert.Equal(1, fixture.GoCalls);
    }

    [Fact]
    public async Task ChangedProcessCannotConfirmTheOriginalStartup()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = fixture.HitTarget;
        var start = fixture.Service.StartAsync(CancellationToken.None);
        fixture.ProcessId++;
        notification.Signal.Notify("NoChanges");
        await start;
        Assert.False((await fixture.Service.ApplyHotReloadAsync(CancellationToken.None)).Success);
        Assert.Equal(1, fixture.GoCalls);
    }

    [Fact]
    public async Task NotificationFailureCannotEnableUnconfirmedHotReload()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = fixture.HitTarget;
        var start = fixture.Service.StartAsync(CancellationToken.None);
        notification.Signal.Fail(new InvalidOperationException("Notification connection was lost"));
        await start;
        Assert.False((await fixture.Service.ApplyHotReloadAsync(CancellationToken.None)).Success);
        Assert.True(notification.Disposed);
        Assert.Equal(1, fixture.GoCalls);
    }

    [Fact]
    public async Task CancellationInsideTheInitialApplyKeepsInitializationUnconfirmed()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        using var cancellation = new CancellationTokenSource();
        fixture.OnGo = fixture.HitTarget;
        fixture.OnBaselineApply = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.StartAsync(cancellation.Token));
        Assert.False((await fixture.Service.ApplyHotReloadAsync(CancellationToken.None)).Success);
        Assert.Equal(1, fixture.BaselineCalls);
    }

    [Fact]
    public async Task NotificationBeforeTheInitialApplyCannotCompleteItsWait()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notification = new Notification();
        using var fixture = new DebuggerIntegrationFixture(true, notification);
        fixture.OnGo = () => { fixture.HitTarget(); notification.Signal.Notify("NoChanges"); };
        var start = fixture.Service.StartAsync(CancellationToken.None);
        Assert.False(start.IsCompleted);
        notification.Signal.Notify("NoChanges");
        await start;
        Assert.Equal(1, fixture.BaselineCalls);
    }

    [Fact]
    public async Task MissingNotificationHasABoundedWait()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(await HotReloadBaseline.WaitForInitializationAsync(pending.Task, TimeSpan.FromMilliseconds(1), CancellationToken.None));
        Assert.False(pending.Task.IsCompleted);
    }

    private sealed class Notification : IHotReloadInitializationNotification
    {
        internal readonly HotReloadInitializationSignal Signal = new();
        internal bool Disposed;
        public Task<bool> Completed => Signal.Completed;
        public void Arm() => Signal.Arm();
        public void Dispose() => Disposed = true;
    }
}
