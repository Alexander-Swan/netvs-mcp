using System.Diagnostics;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRuntime
{
    private readonly LocalMcpHttpHost _httpHost;

    public BrokerRuntime(BrokerOptions options, SessionRegistry sessions)
    {
        Options = options;
        Sessions = sessions;
        StartedUtc = DateTimeOffset.UtcNow;
        Tools = new BrokerToolService(this);
        _httpHost = new LocalMcpHttpHost(options, Tools);
    }

    public BrokerOptions Options { get; }

    public SessionRegistry Sessions { get; }

    public BrokerToolService Tools { get; }

    public DateTimeOffset StartedUtc { get; }

    public bool IsHttpEndpointRunning => _httpHost.IsRunning;

    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public BrokerStatus GetStatus() => new(
        IsHttpEndpointRunning,
        Options.McpEndpoint,
        Options.PipeName,
        StartedUtc,
        Version,
        Sessions.ListSessionStatuses());

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _httpHost.StartAsync(cancellationToken);
        Trace.WriteLine($"NetVsMcp broker endpoint listening at {Options.McpEndpoint}.");
    }

    public async Task StopAsync()
    {
        await _httpHost.StopAsync();
    }
}
