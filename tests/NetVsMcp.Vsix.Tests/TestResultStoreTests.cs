using System;
using System.Linq;

namespace NetVsMcp.Vsix.Tests;

public class TestResultStoreTests
{
    private static TestOperationResult Run(string id, params TestResultInfo[] results) =>
        new(true, "Captured", new TestCaseInfo[0], results, runId: id, outcome: "completed", runnerExitCode: 0);

    [Fact]
    public void ExactOlderRunsAndParameterizedCaseIdentitySurviveANewerRun()
    {
        var store = new TestResultStore();
        store.Add(Run("one", new("Theory(1)", "Passed", null, null, "id-1", "execution-one"),
            new("Theory(2)", "Passed", null, null, "id-2", "execution-two")), "project");
        store.Add(Run("two", new("Theory(1)", "Passed", null, null, "id-1", "execution-three"),
            new("Theory(2)", "Passed", null, null, "id-2", "execution-four")), "project");
        var original = store.Page(new() { RunId = "one" });
        var newer = store.Page(new() { RunId = "two" });
        Assert.Equal(original.Results.Select(r => r.CaseId), newer.Results.Select(r => r.CaseId));
        Assert.NotEqual(original.Results.First().CaseId, original.Results.Last().CaseId);
        Assert.NotEqual(original.Results.First().ExecutionId, newer.Results.First().ExecutionId);
        Assert.Equal("one", store.Get("one")!.RunId);
        Assert.Equal("two", store.Get(null)!.RunId);
    }

    [Fact]
    public void RichFailureEvidencePagesByActualReturnedCount()
    {
        var store = new TestResultStore();
        var cases = Enumerable.Range(0, 20).Select(i => new TestResultInfo("case-" + i, "Failed", null,
            new string('m', 10000), "id-" + i, "execution-" + i, new string('s', 12000))).ToArray();
        var summary = store.Add(Run("rich", cases), "project");
        Assert.NotNull(summary.NextOffset);
        Assert.True(summary.Results.Count < cases.Length);
        var count = 0;
        int? offset = 0;
        while (offset is not null)
        {
            var page = store.Page(new() { RunId = "rich", Offset = offset.Value, MaxItems = 100 });
            Assert.All(page.Results, r => { Assert.True(r.EvidenceTruncated); Assert.True(r.Message!.Length <= 4096); Assert.True(r.StackTrace!.Length <= 8192); });
            Assert.True(page.Results.Count < 100);
            count += page.Results.Count;
            offset = page.NextOffset;
        }
        Assert.Equal(20, count);
    }

    [Fact]
    public void RetentionLimitsAndUnavailableHistoryAreExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new TestResultStore(capacity: 1, now: () => now);
        store.Add(Run("old"), "project");
        now = now.AddSeconds(1);
        var many = Enumerable.Range(0, 500).Select(i => new TestResultInfo("case-" + i, "Failed", null,
            new string('m', 5000), "id-" + i, "e-" + i, new string('s', 9000))).ToArray();
        var bounded = store.Add(Run("bounded", many), "project");
        Assert.True(bounded.ResultsTruncated);
        Assert.Equal(500, bounded.TotalResults);
        Assert.True(bounded.RetainedResults < bounded.TotalResults);
        Assert.Throws<HistoryUnavailableException>(() => store.Page(new() { RunId = "old" }));
        Assert.Throws<ArgumentException>(() => store.Page(new() { RunId = "bounded", MaxItems = 101 }));
        now = now.AddMinutes(16);
        Assert.Throws<HistoryUnavailableException>(() => store.Page(new() { RunId = "bounded" }));
    }

    [Fact]
    public void ARunIdentityCannotBeOverwritten()
    {
        var store = new TestResultStore();
        store.Add(Run("same"), "project");
        Assert.Throws<ArgumentException>(() => store.Add(Run("same"), "different"));
    }
}
