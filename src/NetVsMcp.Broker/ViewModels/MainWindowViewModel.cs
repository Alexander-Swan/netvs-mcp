using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SessionStatusViewModel> Sessions { get; } = new();

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

    public async Task CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var currentVersion = Version.TrimStart('v');
        _updateInfo = await _updateCheckService.CheckAsync(currentVersion, ct);
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

    public IReadOnlyList<BrokerCapabilityProfile> AvailableCapabilityProfiles { get; } =
        Enum.GetValues<BrokerCapabilityProfile>();

    public BrokerCapabilityProfile CapabilityProfile
    {
        get => _runtime.CapabilityProfile;
        set
        {
            if (_runtime.CapabilityProfile == value)
            {
                return;
            }

            _runtime.CapabilityProfile = value;
            OnPropertyChanged();
        }
    }

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

        _runtime.Sessions.RemoveStaleSessions();

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
    private SessionStatusViewModel(int processId, string solutionFileName, string solutionDirectory, string solutionPath, string debuggerMode, string capabilities, string sessionId)
    {
        ProcessId = processId;
        SolutionFileName = solutionFileName;
        SolutionDirectory = solutionDirectory;
        SolutionPath = solutionPath;
        DebuggerMode = debuggerMode;
        Capabilities = capabilities;
        SessionId = sessionId;
    }

    public int ProcessId { get; }
    public string SolutionFileName { get; }
    public string SolutionDirectory { get; }
    public string SolutionPath { get; }
    public string DebuggerMode { get; }
    public string Capabilities { get; }
    public string SessionId { get; }

#if DEBUG
    public static IReadOnlyList<SessionStatusViewModel> DebugSamples { get; } =
    [
        new(12345, "MyApp.sln", @"C:\Work\MyApp\", @"C:\Work\MyApp\MyApp.sln", "Design", "Editor, Navigation, Build, Debugger", "vs-12345"),
        new(67890, "WebApi.sln", @"C:\Projects\WebApi\", @"C:\Projects\WebApi\WebApi.sln", "Break", "Editor, Build, Debugger", "vs-67890"),
        new(54321, "SharedLib.sln", @"C:\Work\Shared\", @"C:\Work\Shared\SharedLib.sln", "Run", "Editor, Navigation", "vs-54321"),
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
            string.Join(", ", session.Capabilities),
            session.SessionId);
    }
}
