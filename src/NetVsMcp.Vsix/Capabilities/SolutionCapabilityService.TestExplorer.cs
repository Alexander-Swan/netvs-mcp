using System;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed partial class SolutionCapabilityService
{
    private Task<TestExplorerCoordinator>? explorer;
    private volatile TestExplorerCoordinator? explorerInstance;
    internal string ExplorerStatus => explorerInstance is null ? "requiresProbe" : "availableWhenTestExplorerReady";
    private void EnsureNoExplorerRun()
    {
        if (explorerInstance?.IsActive == true)
            throw new InvalidOperationException("An owned Test Explorer operation is active; it was not replaced by a CLI run.");
    }
    private async Task<TestExplorerCoordinator> GetExplorerAsync(CancellationToken ct)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        if (explorer is null || explorer.IsFaulted || explorer.IsCanceled) explorer = CreateAsync();
        return await explorer;
        async Task<TestExplorerCoordinator> CreateAsync()
        {
            var instance = new TestExplorerCoordinator(await TestExplorerAdapter.CreateAsync(package, ct), testResults, events);
            explorerInstance = instance;
            return instance;
        }
    }

    internal async Task<TestOperationResult> DiscoverExplorerAsync(TestExplorerRequest request, CancellationToken ct)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        _ = ResolveTestTarget(await GetDteAsync(), request.ProjectName);
        return await (await GetExplorerAsync(ct)).DiscoverAsync(request, ct);
    }
    internal async Task<TestOperationResult> RunExplorerAsync(TestExplorerRequest request, CancellationToken ct)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        var target = ResolveTestTarget(await GetDteAsync(), request.ProjectName);
        return await (await GetExplorerAsync(ct)).RunAsync(request, target.Path, ct);
    }
    internal async Task<NetVsMcp.Contracts.TestDebugResult> DebugExplorerAsync(TestExplorerRequest request, CancellationToken ct)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        var dte = await GetDteAsync();
        if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode) throw new InvalidOperationException("The debugger is occupied; Test Explorer debugging was not started.");
        var target = ResolveTestTarget(dte, request.ProjectName);
        return await (await GetExplorerAsync(ct)).DebugAsync(request, target.Path, ct);
    }
    internal Task<bool> CancelExplorerAsync(TestExplorerRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(explorerInstance?.Cancel(request.RunId) == true);
    }
}
