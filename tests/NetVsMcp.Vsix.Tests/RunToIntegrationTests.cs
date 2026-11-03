using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;
using Fixture = NetVsMcp.Vsix.Tests.DebuggerIntegrationFixture;

namespace NetVsMcp.Vsix.Tests;

[Collection(MockedVS.Collection)]
public sealed class RunToIntegrationTests
{
    public RunToIntegrationTests(GlobalServiceProvider services) => services.Reset();

    [Fact]
    public async Task RunToRetriesRejectedGoAndReportsTheOwnedTargetHit()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        using var fixture = new Fixture();
        fixture.OnGo = () =>
        {
            if (fixture.GoCalls < 3) throw new COMException("Unable to execute method at this time.", unchecked((int)0x89711007));
            fixture.HitTarget();
        };
        var request = await fixture.RequestAsync();

        var result = await fixture.Service.RunToAsync(request, CancellationToken.None);

        Assert.Equal("reachedLocation", result.Outcome);
        Assert.Equal(3, fixture.GoCalls);
        Assert.NotEqual(request.ExpectedStopId, result.State.StopId);
        Assert.Equal(1, result.Cleanup.Created);
        Assert.Equal(1, result.Cleanup.Removed);
        Assert.True(result.Cleanup.Complete);
    }

    [Theory]
    [InlineData("process")]
    [InlineData("solution")]
    [InlineData("ownership")]
    [InlineData("filter")]
    [InlineData("location")]
    [InlineData("disabled")]
    public async Task RunToRevalidatesProcessSolutionAndBreakpointBeforeRetrying(string change)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        using var fixture = new Fixture();
        fixture.OnGo = () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            switch (change)
            {
                case "process": fixture.ProcessId = 456; break;
                case "solution": fixture.SolutionPath = @"C:\Other\Other.sln"; break;
                case "ownership": fixture.Target.Tag = "developer"; break;
                case "filter": fixture.Target.FilterBy = "ProcessID == 456"; break;
                case "location": fixture.TargetLine = 3; break;
                case "disabled": fixture.TargetEnabled = false; break;
            }
            throw new COMException("Unable to execute method at this time.", unchecked((int)0x89711007));
        };
        var result = await fixture.Service.RunToAsync(await fixture.RequestAsync(), CancellationToken.None);

        Assert.Equal("outcomeUnknown", result.Outcome);
        Assert.Equal(1, fixture.GoCalls);
        Assert.Equal(change == "ownership" ? 1 : 0, result.Cleanup.Preserved);
        Assert.Equal(change == "ownership" ? 0 : 1, result.Cleanup.Removed);
    }

    [Theory]
    [InlineData(unchecked((int)0x80004005))]
    [InlineData(unchecked((int)0x89711007))]
    public async Task RunToNeverReissuesAfterAnObservedStopEvenWhenGoThrows(int hresult)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        using var fixture = new Fixture();
        fixture.OnGo = () =>
        {
            fixture.HitTarget();
            throw new COMException("Go threw after execution notification", hresult);
        };
        var request = await fixture.RequestAsync();
        var result = await fixture.Service.RunToAsync(request, CancellationToken.None);

        Assert.Equal("outcomeUnknown", result.Outcome);
        Assert.Equal(1, fixture.GoCalls);
        Assert.Equal("stopped", result.State.Outcome);
        Assert.NotNull(result.State.StopId);
        Assert.NotEqual(request.ExpectedStopId, result.State.StopId);
        Assert.True(result.Cleanup.Complete);
    }

    [Fact]
    public async Task RunToCancellationDuringReadinessWaitCleansUpWithoutAnotherGo()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.OnGo = () =>
        {
            cancellation.Cancel();
            throw new COMException("Unable to execute method at this time.", unchecked((int)0x89711007));
        };
        var result = await fixture.Service.RunToAsync(await fixture.RequestAsync(), cancellation.Token);

        Assert.Equal("waitCanceled", result.Outcome);
        Assert.Equal(1, fixture.GoCalls);
        Assert.True(result.Cleanup.Complete);
        Assert.NotEqual("pending", result.State.Outcome);
    }

    [Fact]
    public async Task RunToRejectedGoDoesNotInventAStopAndStatusCanReconcileIt()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        using var fixture = new Fixture();
        fixture.OnGo = () => throw new COMException("Unable to execute method at this time.", unchecked((int)0x89711007));
        var request = await fixture.RequestAsync();
        var result = await fixture.Service.RunToAsync(request, CancellationToken.None);

        Assert.Equal("outcomeUnknown", result.Outcome);
        Assert.Equal(DebugCommandRetry.MaxAttempts, fixture.GoCalls);
        Assert.Null(result.State.StopId);
        Assert.True(result.Cleanup.Complete);
        var fresh = await fixture.Service.GetStatusAsync(CancellationToken.None);
        Assert.NotNull(fresh.StopId);
        Assert.NotEqual(request.ExpectedStopId, fresh.StopId);
    }

}
