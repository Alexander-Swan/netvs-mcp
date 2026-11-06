using System;
using System.Collections.Generic;
using System.Linq;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

/// <summary>Bounded immutable run history. Cache reads do not need the VS UI thread.</summary>
internal sealed class TestResultStore
{
    private sealed class Entry(TestOperationResult result, DateTimeOffset captured)
    {
        public TestOperationResult Result { get; } = result;
        public DateTimeOffset Captured { get; } = captured;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTimeOffset> now;
    private readonly int capacity;
    private string? latest;

    public TestResultStore(int capacity = 8, Func<DateTimeOffset>? now = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        this.now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public TestOperationResult Add(TestOperationResult original, string sourceScope)
    {
        if (string.IsNullOrWhiteSpace(original.RunId) || original.RunId!.Length > 200) throw new ArgumentException("A run identity of 1–200 characters is required.");
        var retained = new List<TestResultInfo>();
        var characters = 0;
        foreach (var test in original.Results.OrderBy(r => r.TestId ?? r.Name, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.ExecutionId, StringComparer.Ordinal))
        {
            var clipped = false;
            string? Clip(string? value, int limit)
            {
                if (value is null || value.Length <= limit) return value;
                clipped = true; return value.Substring(0, limit);
            }
            var name = Clip(test.Name, 1024)!;
            var outcome = Clip(test.Outcome, 100)!;
            var duration = Clip(test.Duration, 100);
            var message = Clip(test.Message, 4096);
            var id = Clip(test.TestId, 1024);
            var execution = Clip(test.ExecutionId, 1024);
            var stack = Clip(test.StackTrace, 8192);
            var fingerprint = TextContentVersion.Create(sourceScope + "\0" + test.TestId + "\0" + test.Name);
            var caseId = "case:" + fingerprint.Substring(fingerprint.IndexOf(':') + 1);
            var value = new TestResultInfo(name, outcome, duration, message, id, execution, stack, caseId, clipped || test.EvidenceTruncated);
            var size = Size(value);
            if (retained.Count == 10000 || characters + size > 2097152) break;
            retained.Add(value); characters += size;
        }
        var passed = original.Results.Count(r => string.Equals(r.Outcome, "Passed", StringComparison.OrdinalIgnoreCase));
        var failed = original.Results.Count(r => string.Equals(r.Outcome, "Failed", StringComparison.OrdinalIgnoreCase));
        var skipped = original.Results.Count(r => string.Equals(r.Outcome, "Skipped", StringComparison.OrdinalIgnoreCase));
        var counts = new TestRunCounts(passed, failed, skipped, original.Results.Count - passed - failed - skipped);
        var failureSample = original.Results.Where(r => string.Equals(r.Outcome, "Failed", StringComparison.OrdinalIgnoreCase)).Take(3)
            .Select(r => new TestFailureSample(Shorten(r.Name, 256)!, Shorten(r.Message, 500))).ToArray();
        var result = new TestOperationResult(original.Supported, original.Message.Length > 4096 ? original.Message.Substring(0, 4080) + " [truncated]" : original.Message,
            original.Tests, retained, original.Backend, original.RunId, original.Outcome, original.RunnerExitCode,
            original.TotalResults ?? original.Results.Count, retained.Count, retained.Count < original.Results.Count || original.ResultsTruncated,
            coverage: original.Coverage, counts: counts, failureSample: failureSample);
        lock (gate)
        {
            Prune();
            if (runs.ContainsKey(original.RunId!)) throw new ArgumentException("runId is already retained; results were not overwritten.");
            if (runs.Count >= capacity) runs.Remove(runs.OrderBy(p => p.Value.Captured).First().Key);
            var entry = new Entry(result, now());
            runs[original.RunId!] = entry;
            latest = original.RunId;
            var page = Slice(entry, 0, 25);
            return new(result.Supported, result.Message, result.Tests, page, result.Backend, result.RunId, result.Outcome, result.RunnerExitCode,
                result.TotalResults, result.RetainedResults, result.ResultsTruncated, page.Count < result.Results.Count ? page.Count : null,
                coverage: result.Coverage, counts: result.Counts, failureSample: result.FailureSample);
        }
    }

    public TestOperationResult? Get(string? runId)
    {
        lock (gate)
        {
            Prune();
            var id = runId ?? latest;
            return id is not null && runs.TryGetValue(id, out var entry) ? entry.Result : null;
        }
    }

    public TestResultPage Page(TestResultsPageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RunId) || request.Offset < 0 || request.MaxItems is < 1 or > 100)
            throw new ArgumentException("runId is required, offset must be nonnegative, and maxItems must be 1–100.");
        lock (gate)
        {
            Prune();
            if (!runs.TryGetValue(request.RunId, out var entry))
                throw new HistoryUnavailableException("Test run is unknown, expired, evicted, or was lost on VS restart. Reconcile current state; do not rerun tests merely to recover output.");
            if (request.Offset > entry.Result.Results.Count) throw new ArgumentException("offset exceeds the retained result count.");
            var page = Slice(entry, request.Offset, request.MaxItems);
            var result = entry.Result;
            return new(result.RunId!, result.Backend, result.Outcome, result.RunnerExitCode, request.Offset,
                result.TotalResults ?? result.Results.Count, result.Results.Count,
                request.Offset + page.Count < result.Results.Count ? request.Offset + page.Count : null, result.ResultsTruncated,
                entry.Captured, page.Select(r => new NetVsMcp.Contracts.TestResultInfo(r.Name, r.Outcome, r.Duration, r.Message,
                    r.TestId, r.ExecutionId, r.StackTrace, r.CaseId, r.EvidenceTruncated)).ToArray());
        }
    }

    private static IReadOnlyCollection<TestResultInfo> Slice(Entry entry, int offset, int maxItems)
    {
        var page = new List<TestResultInfo>();
        var characters = 0;
        foreach (var result in entry.Result.Results.Skip(offset))
        {
            var size = Size(result);
            if (page.Count == maxItems || (page.Count > 0 && characters + size > 65536)) break;
            page.Add(result); characters += size;
        }
        return page;
    }

    private static string? Shorten(string? text, int limit) =>
        text is not null && text.Length > limit ? text.Substring(0, limit - 1) + "�" : text;

    private static int Size(TestResultInfo value) => value.Name.Length + value.Outcome.Length +
        (value.Duration?.Length ?? 0) + (value.Message?.Length ?? 0) + (value.TestId?.Length ?? 0) +
        (value.ExecutionId?.Length ?? 0) + (value.StackTrace?.Length ?? 0) + (value.CaseId?.Length ?? 0) + 200;

    private void Prune()
    {
        var cutoff = now().AddMinutes(-15);
        foreach (var id in runs.Where(p => p.Value.Captured < cutoff).Select(p => p.Key).ToArray()) runs.Remove(id);
        if (latest is not null && !runs.ContainsKey(latest)) latest = runs.OrderByDescending(p => p.Value.Captured).FirstOrDefault().Key;
    }
}
