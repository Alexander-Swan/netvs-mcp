using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Contracts;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class TestExplorerCoordinatorTests
{
    private static readonly ExplorerCase Case = new(Guid.NewGuid().ToString(), "row(1)", "Tests.Parameterized", "Fixture", "C:/Fixture.dll", "net10.0", new object());
    private sealed class Adapter : ITestExplorerAdapter
    {
        public string Phase => "fixture";
        internal int Calls;
        internal Func<CancellationToken, Task<ExplorerExecution>> Execute = _ => Task.FromResult(new ExplorerExecution(true, false,
            new[] { new NetVsMcp.Vsix.TestResultInfo(Case.Name, "Passed", null, null, Case.Id) }));
        public Task<IReadOnlyList<ExplorerCase>> DiscoverAsync(string project, CancellationToken ct) => Task.FromResult<IReadOnlyList<ExplorerCase>>(new[] { Case });
        public Task<ExplorerExecution> ExecuteAsync(IReadOnlyList<ExplorerCase> tests, bool debug, CancellationToken ct) { Calls++; return Execute(ct); }
    }
    private static TestExplorerRequest Request() => new() { ProjectName = "Fixture", Filter = "id:" + Case.Id, RunId = Guid.NewGuid().ToString("N") };

    [Fact]
    public async Task NativeRunRetainsExactIdentityAndCannotBeExecutedTwice()
    {
        var adapter = new Adapter(); var store = new TestResultStore();
        var service = new TestExplorerCoordinator(adapter, store, null);
        var request = Request();
        var result = await service.RunAsync(request, "Fixture", default);
        Assert.Equal("completed", result.Outcome);
        Assert.Equal("test-explorer", result.Backend);
        Assert.Equal(Case.Id, Assert.Single(result.Results).TestId);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(request, "Fixture", default));
        Assert.Equal(1, adapter.Calls);
        Assert.Equal(request.RunId, store.Page(new() { RunId = request.RunId! }).RunId);
    }

    [Fact]
    public async Task CancellationTargetsOnlyTheOwnedRunAndWaitsForConfirmedCompletion()
    {
        var started = new TaskCompletionSource<bool>();
        var adapter = new Adapter { Execute = async ct => { started.SetResult(true); try { await Task.Delay(-1, ct); } catch (OperationCanceledException) { } return new(false, true, Array.Empty<NetVsMcp.Vsix.TestResultInfo>()); } };
        var service = new TestExplorerCoordinator(adapter, new(), null);
        var request = Request(); var running = service.RunAsync(request, "Fixture", default);
        await started.Task;
        Assert.False(service.Cancel("another-run"));
        Assert.False(running.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(Request(), "Fixture", default));
        Assert.True(service.Cancel(request.RunId));
        Assert.Equal("canceled", (await running).Outcome);
        Assert.False(service.IsActive);
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public async Task DebugHandoffReportsAcceptanceWithoutClaimingAttachment()
    {
        var completion = new TaskCompletionSource<ExplorerExecution>(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable VSTHRD003 // The fixture deliberately controls completion independently of the debug handoff.
        var adapter = new Adapter { Execute = _ => completion.Task };
#pragma warning restore VSTHRD003
        var store = new TestResultStore(); var service = new TestExplorerCoordinator(adapter, store, null);
        using var caller = new CancellationTokenSource();
        var receipt = await service.DebugAsync(Request(), "Fixture", caller.Token);
        Assert.Equal("accepted", receipt.Outcome);
        Assert.Null(receipt.TestHostProcessId);
        Assert.True(service.IsActive);
        caller.Cancel(); // The accepted operation must survive its original RPC lifetime.
        Assert.Equal("running", service.Current(receipt.RunId)!.Outcome);
        Assert.Null(service.Current("unrelated-run"));
        completion.SetResult(new(false, false, Array.Empty<NetVsMcp.Vsix.TestResultInfo>()));
        for (var i = 0; i < 100 && service.IsActive; i++) await Task.Delay(10);
        Assert.Equal("outcomeUnknown", store.Get(receipt.RunId)!.Outcome);
    }

    [Fact]
    public void SelectionRejectsAmbiguousTargetsAndNeverTreatsCliFiltersAsNativeSelectors()
    {
        Assert.Empty(TestExplorerCoordinator.Select(new[] { Case }, "FullyQualifiedName~Tests"));
        Assert.Single(TestExplorerCoordinator.Select(new[] { Case }, "id:" + Case.Id));
        var other = new ExplorerCase(Guid.NewGuid().ToString(), "other", Case.FullyQualifiedName, "Fixture", "C:/Other.dll", "net9.0", new());
        Assert.Throws<ArgumentException>(() => TestExplorerCoordinator.Select(new[] { Case, other }, Case.FullyQualifiedName));
        var duplicate = new ExplorerCase(Guid.NewGuid().ToString(), Case.Name, Case.FullyQualifiedName, Case.Project, Case.Source, Case.Framework, new());
        Assert.Throws<ArgumentException>(() => TestExplorerCoordinator.Select(new[] { Case, duplicate }, "id:" + Case.Id));
        var row = new ExplorerCase(Guid.NewGuid().ToString(), "row(2)", Case.FullyQualifiedName, Case.Project, Case.Source, Case.Framework, new());
        Assert.Single(TestExplorerCoordinator.Select(new[] { Case, row }, "id:" + Case.Id));
    }
}
