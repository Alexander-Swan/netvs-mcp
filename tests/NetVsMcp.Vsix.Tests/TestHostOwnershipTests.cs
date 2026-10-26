using System;
using System.Diagnostics;
using System.Linq;

namespace NetVsMcp.Vsix.Tests;

public class TestHostOwnershipTests
{
    private static readonly DateTime Start = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
    private static TestProcessSnapshot P(int id, int parent, int seconds, string name = "dotnet.exe", string? command = null) =>
        new(id, parent, Start.AddSeconds(seconds), name, command);

    [Fact]
    public void OnlyExactRunnerDescendantWithAHostEntryPointIsEligible()
    {
        var snapshot = new[]
        {
            P(1, 99, 0), P(2, 1, 1, command: "dotnet vstest.console.dll"),
            P(3, 2, 2, command: "dotnet exec --runtimeconfig x.json C:\\tests\\testhost.dll --port 12"),
            P(4, 99, 3, command: "dotnet testhost.dll"), // unrelated, newer host
            P(5, 1, 4, command: "dotnet build-server.dll"),
            P(6, 1, 5, command: "dotnet application.dll testhost.dll"),
            P(7, 99, 6, "testhost.exe")
        };
        Assert.Equal(new[] { 3 }, TestHostOwnership.FindOwnedHosts(snapshot, 1, Start).Select(p => p.Id));
        Assert.Empty(TestHostOwnership.FindOwnedHosts(snapshot, 1, Start.AddSeconds(1)));
    }

    [Fact]
    public void MissingParentsCyclesAndReusedParentIdsFailClosed()
    {
        var snapshot = new[] { P(1, 99, 0), P(2, 3, 1), P(3, 2, 2, "testhost.exe"),
            P(4, 999, 2, "testhost.exe"), P(5, 1, -1, "testhost.exe") };
        Assert.Empty(TestHostOwnership.FindOwnedHosts(snapshot, 1, Start));
    }

    [Theory]
    [InlineData("dotnet testhost.dll", true)]
    [InlineData("dotnet exec --depsfile deps.json \"C:\\some path\\testhost.dll\" --port 1", true)]
    [InlineData("dotnet other.dll testhost.dll", false)]
    [InlineData("dotnet test --filter testhost.dll", false)]
    [InlineData("dotnet exec --unknown testhost.dll", false)]
    [InlineData("dotnet exec --depsfile testhost.dll", false)]
    public void ManagedHostMustBeTheEntryPoint(string command, bool expected) =>
        Assert.Equal(expected, TestHostOwnership.IsTestHost(P(1, 0, 1, command: command)));

    [Fact]
    public void MultipleOwnedHostsRemainExplicitlyAmbiguous()
    {
        var snapshot = new[] { P(1, 99, 0), P(2, 1, 1, "testhost.exe"), P(3, 1, 2, "testhost.x86.exe") };
        Assert.Equal(2, TestHostOwnership.FindOwnedHosts(snapshot, 1, Start).Count);
    }

    [Fact]
    public void NativeSnapshotAndVerifiedHandleUseTheSameProcessIdentity()
    {
        using var current = Process.GetCurrentProcess();
        var row = TestHostOwnership.Capture().Single(p => p.Id == current.Id);
        using var verified = TestHostOwnership.OpenVerified(row);
        Assert.Equal(current.Id, verified.Id);
        Assert.True(TestHostOwnership.SameStart(row.StartedUtc, current.StartTime.ToUniversalTime()));
        Assert.Throws<InvalidOperationException>(() => TestHostOwnership.OpenVerified(
            new(row.Id, row.ParentId, row.StartedUtc.AddSeconds(-1), row.Name, row.CommandLine)));
    }
}
