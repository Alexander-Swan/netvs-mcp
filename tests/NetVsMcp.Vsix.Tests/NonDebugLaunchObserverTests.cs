using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class NonDebugLaunchObserverTests
{
    [Fact]
    public async Task UnsupportedStartupProfilesDoNotInventAnId()
    {
        var ids = await NonDebugLaunchObserver.ObserveAsync(Array.Empty<string>(), new HashSet<int>(),
            DateTime.UtcNow, CancellationToken.None);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task AnAlreadyRunningMatchingExecutableIsNeverReportedAsTheNewLaunch()
    {
        using var current = Process.GetCurrentProcess();
        var ids = await NonDebugLaunchObserver.ObserveAsync(new[] { current.MainModule.FileName },
            NonDebugLaunchObserver.ProcessIds(), DateTime.UtcNow, CancellationToken.None);
        Assert.DoesNotContain(current.Id, ids);
    }
}
