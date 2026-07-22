using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BrokerRuntime _runtime;
    private readonly IAutostartService _autostart;
    private string _statusText = string.Empty;
    private string _runningState = string.Empty;
    private string _autostartStatus = string.Empty;
    private string _lastRefreshedText = string.Empty;

    public MainWindowViewModel(BrokerRuntime runtime, IAutostartService autostart)
    {
        _runtime = runtime;
        _autostart = autostart;
        _runtime.Sessions.SessionsChanged += (_, _) => Refresh();
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
            }
        }
    }

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

    public string McpEndpoint => _runtime.Options.McpEndpoint;

    public string PipeName => _runtime.Options.PipeName;

    public string McpRegistrationJson => _runtime.Options.McpRegistrationJson;

    public string LogsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetVsMcp",
        "Logs");

    public void Refresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke((Action)Refresh);
            return;
        }

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
    }

    public void CopyMcpConfig() => System.Windows.Clipboard.SetText(McpRegistrationJson);

    public void CopyEndpoint() => System.Windows.Clipboard.SetText(McpEndpoint);

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
    private SessionStatusViewModel(
        string sessionId,
        int processId,
        string solutionName,
        string solutionPath,
        string health,
        string lastSeen,
        string age,
        string debuggerMode,
        string activeDocument,
        string capabilities,
        bool isActiveWindow)
    {
        SessionId = sessionId;
        ProcessId = processId;
        SolutionName = solutionName;
        SolutionPath = solutionPath;
        Health = health;
        LastSeen = lastSeen;
        Age = age;
        DebuggerMode = debuggerMode;
        ActiveDocument = activeDocument;
        Capabilities = capabilities;
        IsActiveWindow = isActiveWindow;
    }

    public string SessionId { get; }
    public int ProcessId { get; }
    public string SolutionName { get; }
    public string SolutionPath { get; }
    public string Health { get; }
    public string LastSeen { get; }
    public string Age { get; }
    public string DebuggerMode { get; }
    public string ActiveDocument { get; }
    public string Capabilities { get; }
    public bool IsActiveWindow { get; }

    public static SessionStatusViewModel FromStatus(VsSessionStatus status)
    {
        var session = status.Session;
        return new SessionStatusViewModel(
            session.SessionId,
            session.ProcessId,
            string.IsNullOrWhiteSpace(session.SolutionName) ? "(no solution)" : session.SolutionName!,
            string.IsNullOrWhiteSpace(session.SolutionPath) ? string.Empty : session.SolutionPath!,
            status.Health.ToString(),
            session.LastSeenUtc.LocalDateTime.ToString("g"),
            FormatAge(status.Age),
            session.DebuggerMode.ToString(),
            session.ActiveDocument ?? string.Empty,
            string.Join(", ", session.Capabilities),
            session.IsActiveWindow);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalSeconds < 1)
        {
            return "now";
        }

        if (age.TotalMinutes < 1)
        {
            return $"{Math.Round(age.TotalSeconds)}s ago";
        }

        if (age.TotalHours < 1)
        {
            return $"{Math.Round(age.TotalMinutes)}m ago";
        }

        return $"{Math.Round(age.TotalHours)}h ago";
    }
}
