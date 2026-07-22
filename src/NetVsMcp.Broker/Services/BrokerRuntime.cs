using System.Diagnostics;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRuntime
{
    private readonly LocalMcpHttpHost _httpHost;
    private readonly VsixRegistrationPipeListener _registrationPipeListener;

    public BrokerRuntime(BrokerOptions options, SessionRegistry sessions)
    {
        Options = options;
        Sessions = sessions;
        StartedUtc = DateTimeOffset.UtcNow;
        Connections = new VsSessionConnectionMap();
        Dispatcher = new VsSessionDispatcher(sessions, Connections);
        Registration = new BrokerRegistrationRpcService(sessions);
        Tools = new BrokerToolService(this);
        _httpHost = new LocalMcpHttpHost(options, Tools);
        _registrationPipeListener = new VsixRegistrationPipeListener(options, Registration);
    }

    public BrokerOptions Options { get; }

    public SessionRegistry Sessions { get; }

    public IVsSessionConnectionMap Connections { get; }

    public IVsSessionDispatcher Dispatcher { get; }

    public BrokerToolService Tools { get; }

    public BrokerRegistrationRpcService Registration { get; }

    public DateTimeOffset StartedUtc { get; }

    public bool IsHttpEndpointRunning => _httpHost.IsRunning;

    public bool IsRegistrationPipeRunning => _registrationPipeListener.IsRunning;

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
        await _registrationPipeListener.StartAsync(cancellationToken);
        Trace.WriteLine($"NetVsMcp broker endpoint listening at {Options.McpEndpoint}.");
        Trace.WriteLine($"NetVsMcp VSIX registration pipe listening at {Options.PipeName}.");
    }

    public async Task StopAsync()
    {
        await _registrationPipeListener.StopAsync();
        await _httpHost.StopAsync();
    }
}
