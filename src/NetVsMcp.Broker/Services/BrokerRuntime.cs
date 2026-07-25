using System.Diagnostics;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRuntime
{
    private readonly LocalMcpHttpHost _httpHost;
    private readonly VsixRegistrationPipeListener _registrationPipeListener;
    private readonly IBrokerSettingsStore _settingsStore;
    private BrokerCapabilityProfile _capabilityProfile;

    public BrokerRuntime(BrokerOptions options, SessionRegistry sessions)
    {
        Options = options;
        Sessions = sessions;
        StartedUtc = DateTimeOffset.UtcNow;
        Connections = new VsSessionConnectionMap();
        Dispatcher = new VsSessionDispatcher(sessions, Connections);
        Registration = new BrokerRegistrationRpcService(sessions);
        AuditLog = new AuditLogService(options.EffectiveLogsDirectory);
        SessionManifests = new SessionManifestService(options.EffectiveSessionsDirectory);
        _settingsStore = new BrokerSettingsStore(options.EffectiveSettingsFilePath);
        _capabilityProfile = options.CapabilityProfile;
        Tools = new BrokerToolService(this);
        _httpHost = new LocalMcpHttpHost(options, Tools);
        _registrationPipeListener = new VsixRegistrationPipeListener(options, sessions, Connections);
        Sessions.SessionsChanged += OnSessionsChanged;
    }

    public BrokerOptions Options { get; }

    public BrokerCapabilityProfile CapabilityProfile
    {
        get => _capabilityProfile;
        set
        {
            if (_capabilityProfile == value)
            {
                return;
            }

            _capabilityProfile = value;
            _settingsStore.Update(s => s with { CapabilityProfile = value });
            Trace.WriteLine($"NetVsMcp broker capability profile changed to '{value}'.");
            CapabilityProfileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CapabilityProfileChanged;

    /// <summary>The port the broker is actually listening on for this run.</summary>
    public int CurrentPort => Options.Port;

    /// <summary>The named pipe the broker is actually listening on for this run.</summary>
    public string CurrentPipeName => Options.PipeName;

    /// <summary>The logs folder the broker is actually using for this run.</summary>
    public string CurrentLogsDirectory => Options.EffectiveLogsDirectory;

    /// <summary>The sessions folder the broker is actually using for this run.</summary>
    public string CurrentSessionsDirectory => Options.EffectiveSessionsDirectory;

    /// <summary>
    /// The port saved for the next broker start, or <c>null</c> if no override is configured
    /// and the compiled-in default (5050 Release / 5051 Debug) will be used. Setting this does
    /// not affect the currently running HTTP listener; the broker must be restarted to apply it.
    /// </summary>
    public int? PendingPort
    {
        get => _settingsStore.Load().Port;
        set => UpdatePendingSetting(s => s with { Port = value }, $"port override to '{value?.ToString() ?? "(default)"}'");
    }

    /// <summary>The logs folder saved for the next broker start. Requires a restart to apply.</summary>
    public string? PendingLogsDirectory
    {
        get => _settingsStore.Load().LogsDirectory;
        set => UpdatePendingSetting(s => s with { LogsDirectory = value }, $"logs folder override to '{value ?? "(default)"}'");
    }

    /// <summary>The sessions folder saved for the next broker start. Requires a restart to apply.</summary>
    public string? PendingSessionsDirectory
    {
        get => _settingsStore.Load().SessionsDirectory;
        set => UpdatePendingSetting(s => s with { SessionsDirectory = value }, $"sessions folder override to '{value ?? "(default)"}'");
    }

    public event EventHandler? PendingSettingsChanged;

    private void UpdatePendingSetting(Func<BrokerSettings, BrokerSettings> mutate, string description)
    {
        _settingsStore.Update(mutate);
        Trace.WriteLine($"NetVsMcp broker {description}. Restart the broker to apply.");
        PendingSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public SessionRegistry Sessions { get; }

    public IVsSessionConnectionMap Connections { get; }

    public IVsSessionDispatcher Dispatcher { get; }

    public BrokerToolService Tools { get; }

    public BrokerRegistrationRpcService Registration { get; }

    public IAuditLogService AuditLog { get; }

    public ISessionManifestService SessionManifests { get; }

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
        CapabilityProfile,
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
        Sessions.SessionsChanged -= OnSessionsChanged;
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        try
        {
            SessionManifests.Sync(Sessions.ListSessions());
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp session manifest sync failed: {ex}");
        }
    }
}
