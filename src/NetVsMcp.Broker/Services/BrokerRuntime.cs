using System.Diagnostics;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BrokerRuntime
{
    // Stale-session sweeping previously only happened as a side effect of the WPF
    // MainWindowViewModel.Refresh() - a UI-layer method - so it would silently stop happening
    // if the status window were never opened. Own it here instead, as a runtime-level timer.
    private static readonly TimeSpan StaleSessionSweepInterval = TimeSpan.FromSeconds(15);

    // Audit-yyyyMMdd.jsonl is a daily rolling log. Keep today's file by default and prune older
    // files once at startup and then daily, similar in spirit to SessionManifestService.CleanupStale.
    private static readonly TimeSpan AuditLogPruneInterval = TimeSpan.FromHours(24);
    public const int DefaultAuditLogRetentionDays = 1;

    private readonly LocalMcpHttpHost _httpHost;
    private readonly VsixRegistrationPipeListener _registrationPipeListener;
    private readonly IBrokerSettingsStore _settingsStore;
    private System.Threading.Timer? _staleSessionSweepTimer;
    private System.Threading.Timer? _auditLogPruneTimer;

    public BrokerRuntime(BrokerOptions options, SessionRegistry sessions)
    {
        Options = options;
        Sessions = sessions;
        StartedUtc = DateTimeOffset.UtcNow;
        Connections = new VsSessionConnectionMap();
        Dispatcher = new VsSessionDispatcher(sessions, Connections);
        Launcher = new VisualStudioLauncher(sessions);
        Registration = new BrokerRegistrationRpcService(sessions);
        AuditLog = new AuditLogService(options.EffectiveLogsDirectory);
        SessionManifests = new SessionManifestService(options.EffectiveSessionsDirectory);
        _settingsStore = new BrokerSettingsStore(options.EffectiveSettingsFilePath);
        BestPracticeGuides = new BestPracticeGuideCatalog();
        Tools = new BrokerToolService(this);
        _httpHost = new LocalMcpHttpHost(options, Tools, BestPracticeGuides);
        _registrationPipeListener = new VsixRegistrationPipeListener(options, sessions, Connections);
        Sessions.SessionsChanged += OnSessionsChanged;
    }

    public BrokerOptions Options { get; }

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

    /// <summary>Whether update checks should include dev/pre-release versions. Applies immediately, no restart required.</summary>
    public bool IncludeDevVersionUpdates
    {
        get => _settingsStore.Load().IncludeDevVersionUpdates;
        set => _settingsStore.Update(s => s with { IncludeDevVersionUpdates = value });
    }

    /// <summary>Whether registering NetVsMcp with a client backs up its existing config file first. Applies immediately.</summary>
    public bool BackupConfigBeforeRegistering
    {
        get => _settingsStore.Load().BackupConfigBeforeRegistering;
        set => _settingsStore.Update(s => s with { BackupConfigBeforeRegistering = value });
    }

    /// <summary>
    /// The version of the last update the user chose to ignore, or <c>null</c> if none was ignored.
    /// Suppresses the update banner for that specific version only; a newer release still surfaces.
    /// </summary>
    public string? IgnoredUpdateVersion
    {
        get => _settingsStore.Load().IgnoredUpdateVersion;
        set => _settingsStore.Update(s => s with { IgnoredUpdateVersion = value });
    }

    /// <summary>How many calendar-day audit log files to keep, including today.</summary>
    public int AuditLogRetentionDays => DefaultAuditLogRetentionDays;

    public BrokerLogLevel MinimumLogLevel
    {
        get => _settingsStore.Load().MinimumLogLevel;
        set => _settingsStore.Update(s => s with { MinimumLogLevel = value });
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

    public VisualStudioLauncher Launcher { get; }

    public BrokerToolService Tools { get; }

    public BrokerRegistrationRpcService Registration { get; }

    public IAuditLogService AuditLog { get; }

    public ISessionManifestService SessionManifests { get; }

    public BestPracticeGuideCatalog BestPracticeGuides { get; }

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

        _staleSessionSweepTimer = new System.Threading.Timer(
            _ => SweepStaleSessions(),
            null,
            StaleSessionSweepInterval,
            StaleSessionSweepInterval);

        _auditLogPruneTimer = new System.Threading.Timer(
            _ => PruneAuditLogs(),
            null,
            TimeSpan.Zero,
            AuditLogPruneInterval);
    }

    public async Task StopAsync()
    {
        _staleSessionSweepTimer?.Dispose();
        _staleSessionSweepTimer = null;

        _auditLogPruneTimer?.Dispose();
        _auditLogPruneTimer = null;

        await _registrationPipeListener.StopAsync();
        await _httpHost.StopAsync();
        Sessions.SessionsChanged -= OnSessionsChanged;
    }

    private void SweepStaleSessions()
    {
        try
        {
            var removed = Sessions.RemoveStaleSessions();
            if (removed > 0)
            {
                Trace.WriteLine($"NetVsMcp broker swept {removed} stale Visual Studio session(s).");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp broker stale-session sweep failed: {ex}");
        }
    }

    private void PruneAuditLogs()
    {
        try
        {
            var removed = AuditLog.PruneOldLogs(AuditLogRetentionDays);
            if (removed > 0)
            {
                Trace.WriteLine($"NetVsMcp broker pruned {removed} audit log file(s) older than {AuditLogRetentionDays} day(s).");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp broker audit log pruning failed: {ex}");
        }
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
