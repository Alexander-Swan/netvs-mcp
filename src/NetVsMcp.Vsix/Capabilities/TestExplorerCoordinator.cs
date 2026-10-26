using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed class TestExplorerCoordinator(ITestExplorerAdapter adapter, TestResultStore results, SessionEventBuffer? events)
{
    private sealed class Run(string id, CancellationTokenSource cancellation)
    {
        internal readonly string Id = id;
        internal readonly CancellationTokenSource Cancellation = cancellation;
    }
    private readonly object gate = new();
    private Run? active;
    internal bool IsActive { get { lock (gate) return active is not null; } }

    internal async Task<TestOperationResult> DiscoverAsync(TestExplorerRequest request, CancellationToken ct)
    {
        var tests = await adapter.DiscoverAsync(RequireProject(request.ProjectName), ct).ConfigureAwait(false);
        var page = tests.Take(200).Select(t => new TestCaseInfo(t.Name, t.Project, t.Source, t.Id, t.FullyQualifiedName)).ToArray();
        return new(true, tests.Count > 200 ? "First 200 IDE cases; select an exact native ID or fully qualified name." : "Test Explorer discovered cases.",
            page, [], "test-explorer", outcome: "completed");
    }

    internal Task<TestOperationResult> RunAsync(TestExplorerRequest request, string sourceScope, CancellationToken ct) =>
        ExecuteAsync(request, sourceScope, false, Reserve(request, ct));

    internal async Task<NetVsMcp.Contracts.TestDebugResult> DebugAsync(TestExplorerRequest request, string sourceScope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Filter)) throw new ArgumentException("Test Explorer debug requires an exact native test ID or fully qualified name.");
        ct.ThrowIfCancellationRequested();
        var run = Reserve(request, CancellationToken.None);
        using var preparing = ct.Register(() => Cancel(run.Id));
        // Retain completion independently of the short debug handoff. Exceptions are
        // observed here; failed submission is never reported as an attachment.
        var prepared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = ExecuteAsync(request, sourceScope, true, run, prepared);
        _ = completion.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        await prepared.Task.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return new(true, "Test Explorer debug accepted; inspect debugger state and this runId. No process attachment is claimed yet.",
            request.ProjectName, request.Filter, null, null, Backend: "test-explorer", RunId: run.Id, Outcome: "accepted");
    }

    internal bool Cancel(string? id)
    {
        Run? run;
        lock (gate) run = active is { } value && value.Id == id ? value : null;
        if (run is null) return false;
        try { run.Cancellation.Cancel(); return true; }
        catch (ObjectDisposedException) { return false; }
    }

    internal TestOperationResult? Current(string? id)
    {
        lock (gate)
            return active is { } run && run.Id == id ? new(true, "IDE request is active (" + adapter.Phase + "); results are not final.", [], [],
                "test-explorer", run.Id, run.Cancellation.IsCancellationRequested ? "cancelRequested" : "running") : null;
    }

    private Run Reserve(TestExplorerRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RequireProject(request.ProjectName);
        if (!Guid.TryParse(request.RunId, out _)) throw new ArgumentException("A fresh runId is required.");
        lock (gate)
        {
            if (active is not null) throw new InvalidOperationException("An owned Test Explorer run is still active; it was not replaced.");
            if (results.Get(request.RunId) is not null) throw new ArgumentException("This runId is already retained; it was not executed again.");
            return active = new(request.RunId!, CancellationTokenSource.CreateLinkedTokenSource(ct));
        }
    }

    private async Task<TestOperationResult> ExecuteAsync(TestExplorerRequest request, string scope, bool debug, Run run, TaskCompletionSource<bool>? prepared = null)
    {
        var entered = false;
        try
        {
            var available = await adapter.DiscoverAsync(RequireProject(request.ProjectName), run.Cancellation.Token).ConfigureAwait(false);
            var selected = Select(available, request.Filter);
            if (selected.Count == 0)
            {
                prepared?.TrySetException(new ArgumentException("No current IDE test matches this exact selector."));
                return results.Add(new(true, "No current Test Explorer case matched.", [], [], "test-explorer", run.Id, "noTests"), scope);
            }
            run.Cancellation.Token.ThrowIfCancellationRequested();
            events?.Publish("test.accepted", operationId: run.Id);
            var execution = adapter.ExecuteAsync(selected, debug, run.Cancellation.Token);
            if (execution.IsFaulted) await execution.ConfigureAwait(false);
            entered = true;
            prepared?.TrySetResult(true);
            var result = await execution.ConfigureAwait(false);
            var outcome = result.Canceled ? "canceled" : !result.Completed ? "outcomeUnknown" :
                selected.Any(test => !result.Results.Any(r => r.TestId == test.Id)) ? "incomplete" :
                result.Results.Any(r => r.Outcome == "Failed") ? "testsFailed" :
                result.Results.All(r => r.Outcome is "Passed" or "Skipped") ? "completed" : "incomplete";
            var retained = results.Add(new(true, "Test Explorer " + outcome + "." + (result.Message is null ? "" : " " + result.Message), [], result.Results, "test-explorer", run.Id, outcome), scope);
            events?.Publish(outcome == "outcomeUnknown" ? "test.outcomeUnknown" : "test.completed", operationId: run.Id);
            return retained;
        }
        catch (OperationCanceledException) when (!entered)
        {
            prepared?.TrySetCanceled();
            return results.Add(new(true, "Canceled before execution.", [], [], "test-explorer", run.Id, "canceled"), scope);
        }
        catch (Exception ex)
        {
            prepared?.TrySetException(ex);
            if (entered)
            {
                events?.Publish("test.outcomeUnknown", operationId: run.Id);
                return results.Add(new(true, "Test Explorer outcome unknown: " + ex.Message, [], [], "test-explorer", run.Id, "outcomeUnknown"), scope);
            }
            throw;
        }
        finally
        {
            lock (gate) { if (ReferenceEquals(active, run)) active = null; }
            run.Cancellation.Dispose();
        }
    }

    internal static IReadOnlyList<ExplorerCase> Select(IReadOnlyList<ExplorerCase> tests, string? filter)
    {
        var selected = string.IsNullOrWhiteSpace(filter) ? tests : tests.Where(t => filter!.StartsWith("id:", StringComparison.OrdinalIgnoreCase)
            ? Guid.TryParse(filter.Substring(3), out var id) && Guid.TryParse(t.Id, out var actual) && id == actual
            : string.Equals(t.FullyQualifiedName, filter, StringComparison.Ordinal)).ToArray();
        if (selected.Select(t => t.Source + "\0" + t.Framework).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            throw new ArgumentException("Select native test IDs from one source/framework; ambiguous multi-target execution was not started.");
        foreach (var item in selected)
            if (tests.Count(t => t.Source == item.Source && t.Framework == item.Framework && t.FullyQualifiedName == item.FullyQualifiedName && t.Name == item.Name) !=
                selected.Count(t => t.Source == item.Source && t.Framework == item.Framework && t.FullyQualifiedName == item.FullyQualifiedName && t.Name == item.Name))
                throw new ArgumentException("This native ID cannot be mapped to a unique IDE execution selector; no broader test selection was started.");
        return selected;
    }
    private static string RequireProject(string? project) => !string.IsNullOrWhiteSpace(project) ? project! : throw new ArgumentException("projectName is required for Test Explorer.");
}
