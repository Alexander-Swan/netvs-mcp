using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BrokerRuntime _runtime;
    private readonly IAutostartService _autostart;
    private readonly UpdateCheckService _updateCheckService;
    private readonly McpClientRegistrationService _clientRegistration = new();
    private readonly List<ClientRegistrationViewModel> _allClients = [];
    private string _statusText = string.Empty;
    private string _runningState = string.Empty;
    private string _autostartStatus = string.Empty;
    private string _lastRefreshedText = string.Empty;
    private string _portText = string.Empty;
    private string _logsDirectoryText = string.Empty;
    private string _sessionsDirectoryText = string.Empty;
    private UpdateInfo? _updateInfo;
    private bool _isInstallingUpdate;

    public MainWindowViewModel(BrokerRuntime runtime, IAutostartService autostart, UpdateCheckService updateCheckService)
    {
        _runtime = runtime;
        _autostart = autostart;
        _updateCheckService = updateCheckService;
        _runtime.Sessions.SessionsChanged += (_, _) => Refresh();
        _portText = (_runtime.PendingPort ?? _runtime.CurrentPort).ToString();
        _logsDirectoryText = _runtime.PendingLogsDirectory ?? _runtime.CurrentLogsDirectory;
        _sessionsDirectoryText = _runtime.PendingSessionsDirectory ?? _runtime.CurrentSessionsDirectory;

        foreach (var client in McpClientRegistrationService.KnownClients)
            _allClients.Add(new ClientRegistrationViewModel(client));

        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SessionStatusViewModel> Sessions { get; } = new();

    /// <summary>Only the known MCP clients actually detected on this machine - nothing you can't act on.</summary>
    public ObservableCollection<ClientRegistrationViewModel> DetectedClients { get; } = new();

    public bool HasDetectedClients => DetectedClients.Count > 0;

    public bool NoDetectedClients => DetectedClients.Count == 0;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public string RunningState
    {
        get => _runningState;
        private set
        {
            if (_runningState != value)
            {
                _runningState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    public bool IsRunning => _runningState == "Running";

    public string AutostartStatus
    {
        get => _autostartStatus;
        private set
        {
            if (_autostartStatus != value)
            {
                _autostartStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        private set
        {
            if (_lastRefreshedText != value)
            {
                _lastRefreshedText = value;
                OnPropertyChanged();
            }
        }
    }

    public string Version { get; } = GetVersion();

    private static string GetVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informationalVersion is { Length: > 0 })
        {
            var plusIndex = informationalVersion.IndexOf('+');
            var clean = plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
            return $"v{clean}";
        }

        return $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";
    }

    public bool UpdateAvailable => _updateInfo is not null;

    public string UpdateVersionText => _updateInfo?.Version ?? string.Empty;

    public string UpdateBannerText => _updateInfo is null
        ? string.Empty
        : $"Update available: v{_updateInfo.Version} — A new version of NetVsMcp Broker is ready to install.";

    public bool IsInstallingUpdate
    {
        get => _isInstallingUpdate;
        private set
        {
            if (_isInstallingUpdate != value)
            {
                _isInstallingUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotInstallingUpdate));
                OnPropertyChanged(nameof(InstallButtonContent));
            }
        }
    }

    public bool IsNotInstallingUpdate => !_isInstallingUpdate;

    public string InstallButtonContent => _isInstallingUpdate ? "Downloading..." : "Install Update";

    public bool IncludeDevVersions
    {
        get => _runtime.IncludeDevVersionUpdates;
        set
        {
            if (_runtime.IncludeDevVersionUpdates == value)
                return;

            _runtime.IncludeDevVersionUpdates = value;
            OnPropertyChanged();
            _ = CheckForUpdatesAsync();
        }
    }

    /// <summary>Whether Register/Update backs up a client's existing config file to "&lt;path&gt;.bak" first.</summary>
    public bool BackupConfigBeforeRegistering
    {
        get => _runtime.BackupConfigBeforeRegistering;
        set
        {
            if (_runtime.BackupConfigBeforeRegistering == value)
                return;

            _runtime.BackupConfigBeforeRegistering = value;
            OnPropertyChanged();
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var currentVersion = Version.TrimStart('v');
        _updateInfo = await _updateCheckService.CheckAsync(
            currentVersion,
            _runtime.IncludeDevVersionUpdates,
            _runtime.IgnoredUpdateVersion,
            ct);
        OnPropertyChanged(nameof(UpdateAvailable));
        OnPropertyChanged(nameof(UpdateVersionText));
        OnPropertyChanged(nameof(UpdateBannerText));
    }

    public void IgnoreUpdate()
    {
        if (_updateInfo is null)
            return;

        _runtime.IgnoredUpdateVersion = _updateInfo.Version;
        _updateInfo = null;
        OnPropertyChanged(nameof(UpdateAvailable));
        OnPropertyChanged(nameof(UpdateVersionText));
        OnPropertyChanged(nameof(UpdateBannerText));
    }

    public async Task InstallUpdateAsync()
    {
        if (_updateInfo is null || _isInstallingUpdate)
            return;

        IsInstallingUpdate = true;
        try
        {
            await _updateCheckService.DownloadAndInstallAsync(_updateInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsInstallingUpdate = false;
            System.Windows.MessageBox.Show(
                $"Update failed: {ex.Message}",
                "NetVsMcp Update",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    public string McpEndpoint => _runtime.Options.McpEndpoint;

    public string McpWebAutomationEndpoint => _runtime.Options.McpWebAutomationEndpoint;

    public string PipeName => _runtime.CurrentPipeName;

    public string McpRegistrationJson => _runtime.Options.McpRegistrationJson;

    public string LogsFolder => _runtime.CurrentLogsDirectory;

    public string SessionsFolder => _runtime.CurrentSessionsDirectory;

    public string PortText
    {
        get => _portText;
        set
        {
            if (_portText != value)
            {
                _portText = value;
                OnPropertyChanged();
            }
        }
    }

    public string LogsDirectoryText
    {
        get => _logsDirectoryText;
        set
        {
            if (_logsDirectoryText != value)
            {
                _logsDirectoryText = value;
                OnPropertyChanged();
            }
        }
    }

    public string SessionsDirectoryText
    {
        get => _sessionsDirectoryText;
        set
        {
            if (_sessionsDirectoryText != value)
            {
                _sessionsDirectoryText = value;
                OnPropertyChanged();
            }
        }
    }

    public void ApplyStartupSettings()
    {
        if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
        {
            ShowSettingsMessage("Enter a port number between 1 and 65535.", MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(LogsDirectoryText))
        {
            ShowSettingsMessage("Enter a logs folder path.", MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SessionsDirectoryText))
        {
            ShowSettingsMessage("Enter a sessions folder path.", MessageBoxImage.Warning);
            return;
        }

        _runtime.PendingPort = port == _runtime.CurrentPort ? null : port;
        _runtime.PendingLogsDirectory = LogsDirectoryText == _runtime.CurrentLogsDirectory ? null : LogsDirectoryText;
        _runtime.PendingSessionsDirectory = SessionsDirectoryText == _runtime.CurrentSessionsDirectory ? null : SessionsDirectoryText;

        ShowSettingsMessage("Settings saved. Restart NetVsMcp Broker to apply them.", MessageBoxImage.Information);
    }

    private static void ShowSettingsMessage(string message, MessageBoxImage icon) =>
        System.Windows.MessageBox.Show(message, "NetVsMcp Settings", MessageBoxButton.OK, icon);

    public void Refresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke((Action)Refresh);
            return;
        }

        // Stale-session sweeping is owned by BrokerRuntime's own timer now, not this
        // UI-layer Refresh() - this method just reacts to SessionsChanged (via the constructor
        // subscription) and re-reads whatever the runtime currently reports.
        var status = _runtime.GetStatus();
        RunningState = status.IsRunning ? "Running" : "Stopped";
        StatusText = status.IsRunning
            ? $"Broker running since {status.StartedUtc.LocalDateTime:g}"
            : "Broker stopped";
        AutostartStatus = GetAutostartStatus();
        LastRefreshedText = $"Last refreshed {DateTimeOffset.Now.LocalDateTime:g}";

        Sessions.Clear();
        foreach (var session in status.Sessions)
        {
            Sessions.Add(SessionStatusViewModel.FromStatus(session));
        }

#if DEBUG
        if (Sessions.Count == 0)
        {
            foreach (var sample in SessionStatusViewModel.DebugSamples)
                Sessions.Add(sample);
        }
#endif

        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(NoSessions));

        foreach (var client in _allClients)
        {
            client.IsDetected = _clientRegistration.IsDetected(client.Definition);
            client.IsRegistered = client.IsDetected && _clientRegistration.IsRegistered(client.Definition, _runtime.Options);
        }

        DetectedClients.Clear();
        foreach (var client in _allClients.Where(c => c.IsDetected))
            DetectedClients.Add(client);

        OnPropertyChanged(nameof(HasDetectedClients));
        OnPropertyChanged(nameof(NoDetectedClients));
    }

    /// <summary>
    /// Registers or updates a client's config directly - no preview, no confirmation dialog. The
    /// "back up existing file first" checkbox is the safety net for this action, not a prompt.
    /// </summary>
    public void RegisterClient(ClientRegistrationViewModel client)
    {
        try
        {
            _clientRegistration.Register(client.Definition, _runtime.Options, BackupConfigBeforeRegistering);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to update {client.ConfigPath}:\n\n{ex.Message}",
                "NetVsMcp Client Registration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        Refresh();
    }

    public void OpenClientConfig(ClientRegistrationViewModel client)
    {
        var path = client.Definition.ConfigPath;
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
            return;
        }

        System.Windows.MessageBox.Show(
            $"Neither the config file nor its folder exist yet:\n{path}",
            "NetVsMcp Client Registration",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public bool HasSessions => Sessions.Count > 0;

    public bool NoSessions => Sessions.Count == 0;

    public void CopyMcpConfig() => System.Windows.Clipboard.SetText(McpRegistrationJson);

    public void CopyEndpoint() => System.Windows.Clipboard.SetText(McpEndpoint);

    public void CopyWebAutomationEndpoint() => System.Windows.Clipboard.SetText(McpWebAutomationEndpoint);

    public void CopyPipeName() => System.Windows.Clipboard.SetText(PipeName);

    public void ToggleAutostart()
    {
        if (!_autostart.IsSupported)
        {
            System.Windows.MessageBox.Show(
                _autostart.StatusText,
                "NetVsMcp Autostart",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Refresh();
            return;
        }

        try
        {
            _autostart.SetEnabled(!_autostart.IsEnabled());
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "NetVsMcp Autostart",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Refresh();
    }

    public void OpenLogsFolder()
    {
        Directory.CreateDirectory(LogsFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = LogsFolder,
            UseShellExecute = true
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private string GetAutostartStatus()
    {
        if (!_autostart.IsSupported)
        {
            return _autostart.StatusText;
        }

        return _autostart.IsEnabled()
            ? "Enabled at login"
            : "Disabled at login";
    }
}

public sealed class SessionStatusViewModel
{
    private SessionStatusViewModel(int processId, string solutionFileName, string solutionDirectory, string solutionPath, string debuggerMode, string sessionId, string vsixVersion)
    {
        ProcessId = processId;
        SolutionFileName = solutionFileName;
        SolutionDirectory = solutionDirectory;
        SolutionPath = solutionPath;
        DebuggerMode = debuggerMode;
        SessionId = sessionId;
        VsixVersion = vsixVersion;
    }

    public int ProcessId { get; }
    public string SolutionFileName { get; }
    public string SolutionDirectory { get; }
    public string SolutionPath { get; }
    public string DebuggerMode { get; }
    public string SessionId { get; }
    public string VsixVersion { get; }

#if DEBUG
    public static IReadOnlyList<SessionStatusViewModel> DebugSamples { get; } =
    [
        new(12345, "MyApp.sln", @"C:\Work\MyApp\", @"C:\Work\MyApp\MyApp.sln", "Design", "vs-12345", "1.0.2"),
        new(67890, "WebApi.sln", @"C:\Projects\WebApi\", @"C:\Projects\WebApi\WebApi.sln", "Break", "vs-67890", "1.0.2"),
        new(54321, "SharedLib.sln", @"C:\Work\Shared\", @"C:\Work\Shared\SharedLib.sln", "Run", "vs-54321", "1.0.2"),
    ];
#endif

    public static SessionStatusViewModel FromStatus(VsSessionStatus status)
    {
        var session = status.Session;
        var solutionPath = string.IsNullOrWhiteSpace(session.SolutionPath) ? string.Empty : session.SolutionPath!;
        var solutionName = string.IsNullOrWhiteSpace(session.SolutionName) ? "(no solution)" : session.SolutionName!;
        var solutionFileName = string.IsNullOrEmpty(solutionPath) ? solutionName : Path.GetFileName(solutionPath);
        var solutionDirectory = string.IsNullOrEmpty(solutionPath) ? string.Empty
            : Path.GetDirectoryName(solutionPath) is { Length: > 0 } dir ? dir + Path.DirectorySeparatorChar : string.Empty;
        return new SessionStatusViewModel(
            session.ProcessId,
            solutionFileName,
            solutionDirectory,
            solutionPath,
            session.DebuggerMode.ToString(),
            session.SessionId,
            string.IsNullOrWhiteSpace(session.VsixVersion) ? "Unknown" : session.VsixVersion!);
    }
}

public sealed class ClientRegistrationViewModel : INotifyPropertyChanged
{
    private bool _isDetected;
    private bool _isRegistered;

    public ClientRegistrationViewModel(McpClientDefinition definition)
    {
        Definition = definition;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public McpClientDefinition Definition { get; }

    public string DisplayName => Definition.DisplayName;

    public string ConfigPath => Definition.ConfigPath;

    public bool IsDetected
    {
        get => _isDetected;
        set
        {
            if (_isDetected != value)
            {
                _isDetected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRegistered
    {
        get => _isRegistered;
        set
        {
            if (_isRegistered != value)
            {
                _isRegistered = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RegisteredText));
                OnPropertyChanged(nameof(RegisterButtonText));
            }
        }
    }

    public string RegisteredText => IsRegistered ? "Registered" : "Not registered";

    public string RegisterButtonText => IsRegistered ? "Update" : "Register";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
