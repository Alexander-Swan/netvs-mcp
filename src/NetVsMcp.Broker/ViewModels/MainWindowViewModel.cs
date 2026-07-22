using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BrokerRuntime _runtime;
    private string _statusText = string.Empty;

    public MainWindowViewModel(BrokerRuntime runtime)
    {
        _runtime = runtime;
        _runtime.Sessions.SessionsChanged += (_, _) => Refresh();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<VsSessionInfo> Sessions { get; } = new();

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

    public string McpEndpoint => _runtime.Options.McpEndpoint;

    public string PipeName => _runtime.Options.PipeName;

    public string McpRegistrationJson => _runtime.Options.McpRegistrationJson;

    public void Refresh()
    {
        var status = _runtime.GetStatus();
        StatusText = status.IsRunning
            ? $"Broker running since {status.StartedUtc.LocalDateTime:g}"
            : "Broker stopped";

        Sessions.Clear();
        foreach (var session in status.Sessions.Select(sessionStatus => sessionStatus.Session))
        {
            Sessions.Add(session);
        }
    }

    public void CopyMcpConfig() => System.Windows.Clipboard.SetText(McpRegistrationJson);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
