using System.Runtime.CompilerServices;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    private const string TestBackendDescription = "Test runner backend: cli (default; dotnet-cli is an alias) runs dotnet test outside Test Explorer. test-explorer uses Visual Studio Test Explorer; test execution updates its UI/results. It requires an updated VSIX, supported IDE APIs, and a built test project. An explicit Test Explorer request never falls back to CLI.";
    private const string TestProjectDescription = "Test project name. Required for backend=test-explorer and for test_debug; optional for CLI discovery and runs.";
    private const string TestFilterDescription = "CLI: dotnet test --filter expression, e.g. FullyQualifiedName=MyTests.CustomerTests.Search or FullyQualifiedName~CustomerTests. Test Explorer: id:<testId> from test_discover or an exact, case-sensitive fullyQualifiedName, e.g. MyTests.CustomerTests.Search; CLI expressions, substring and category filters are not accepted. An exact name can match multiple parameterized cases; use a discovered id:<testId> to target one case. For test_run/test_run_and_get_results, omit to run all tests in the selected scope; test_debug always requires a non-empty filter. IDE selections spanning multiple sources/frameworks or IDs that cannot map uniquely to IDE cases are rejected.";

    private static bool ValidTestBackend(string? backend) => backend is null or "cli" or "dotnet-cli" or "test-explorer";
    private Task<ToolResponse<TestOperationResult>> RunExplorerTestsAsync(string? projectName, string? filter,
        string? sessionId, string? solutionName, string? solutionPath, CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new TestExplorerRequest { ProjectName = NormalizeOptional(projectName), Filter = NormalizeOptional(filter), RunId = Guid.NewGuid().ToString("N") };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, async (connection, _) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Keep the run RPC alive while a separate, ownership-scoped recovery RPC
            // requests cancellation. Canceling its wait alone cannot confirm IDE stop.
            var running = connection.TestExplorerRunAsync(request, CancellationToken.None);
            if (!cancellationToken.CanBeCanceled) return await running;
            using var signal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                var canceled = Task.Delay(Timeout.Infinite, signal.Token);
                if (await Task.WhenAny(running, canceled) == canceled)
                {
                    while (!running.IsCompleted)
                    {
                        if (await connection.TestExplorerCancelAsync(request, CancellationToken.None)) break;
                        await Task.WhenAny(running, Task.Delay(100));
                    }
                }
                return await running;
            }
            finally { await signal.CancelAsync(); }
        }, CancellationToken.None, toolName: toolName,
            unsupportedByVsixMessage: "Test Explorer requires an updated VSIX; no CLI fallback was started.");
    }

    private async Task<ToolResponse<TestRunAndGetResultsResult>> RunExplorerCompositeAsync(string? projectName, string? filter,
        string? sessionId, string? solutionName, string? solutionPath, CancellationToken ct)
    {
        var result = await RunExplorerTestsAsync(projectName, filter, sessionId, solutionName, solutionPath, ct, nameof(TestRunAndGetResults));
        return new(result.Success, result.Value is null ? null : new(result.Value, result.Value), result.Message, result.Metadata);
    }
}
