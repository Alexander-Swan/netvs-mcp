using System.Diagnostics;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRuntime
{
    public BrokerRuntime(BrokerOptions options, SessionRegistry sessions)
    {
        Options = options;
        Sessions = sessions;
        StartedUtc = DateTimeOffset.UtcNow;
    }

    public BrokerOptions Options { get; }

    public SessionRegistry Sessions { get; }

    public DateTimeOffset StartedUtc { get; }

    public bool IsHttpEndpointRunning { get; private set; }

    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public BrokerStatus GetStatus() => new(
        IsHttpEndpointRunning,
        Options.McpEndpoint,
        Options.PipeName,
        StartedUtc,
        Version,
        Sessions.ListSessionStatuses());

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Placeholder until the MCP HTTP host is wired in. Keeping the runtime
        // boundary now makes it easy to replace with the real server later.
        IsHttpEndpointRunning = true;
        Trace.WriteLine($"NetVsMcp broker endpoint reserved at {Options.McpEndpoint}.");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsHttpEndpointRunning = false;
        return Task.CompletedTask;
    }
}
