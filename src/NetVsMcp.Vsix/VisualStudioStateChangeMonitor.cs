using System;
using System.Collections.Generic;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed class VisualStudioStateChangedEventArgs : EventArgs
{
    public VisualStudioStateChangedEventArgs(VisualStudioStateChangeKind kind)
        : this(kind, null)
    {
    }

    public VisualStudioStateChangedEventArgs(VisualStudioStateChangeKind kind, IReadOnlyCollection<BrokerEventNotification>? brokerEvents)
    {
        Kind = kind;
        BrokerEvents = brokerEvents ?? Array.Empty<BrokerEventNotification>();
    }

    public VisualStudioStateChangeKind Kind { get; }

    public IReadOnlyCollection<BrokerEventNotification> BrokerEvents { get; }
}

internal enum VisualStudioStateChangeKind
{
    SolutionOpened,
    SolutionClosed,
    ActiveDocumentChanged,
    DebuggerModeChanged,
    ActiveWindowChanged,
    BuildStarted,
    BuildCompleted,
    TestRunStarted,
    TestRunCompleted,
    BreakpointChanged
}

internal sealed class VisualStudioStateChangeMonitor : IVisualStudioStateChangeMonitor
{
    private readonly AsyncPackage package;

    private SolutionEvents? solutionEvents;
    private WindowEvents? windowEvents;
    private DebuggerEvents? debuggerEvents;
    private BuildEvents? buildEvents;
    private DTE? dte;

    public VisualStudioStateChangeMonitor(AsyncPackage package)
    {
        this.package = package;
    }

    public event EventHandler<VisualStudioStateChangedEventArgs>? StateChanged;

    public void PublishBrokerEvent(VisualStudioStateChangeKind kind, BrokerEventNotification brokerEvent)
    {
        Raise(kind, brokerEvent);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
        var events = dte?.Events;
        if (events is null)
        {
            return;
        }

        solutionEvents = events.SolutionEvents;
        solutionEvents.Opened += OnSolutionOpened;
        solutionEvents.AfterClosing += OnSolutionClosed;

        windowEvents = events.WindowEvents;
        windowEvents.WindowActivated += OnWindowActivated;

        debuggerEvents = events.DebuggerEvents;
        debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;
        debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;
        debuggerEvents.OnEnterRunMode += OnEnterRunMode;

        buildEvents = events.BuildEvents;
        buildEvents.OnBuildBegin += OnBuildBegin;
        buildEvents.OnBuildDone += OnBuildDone;
    }

    private void OnSolutionOpened()
    {
        Raise(VisualStudioStateChangeKind.SolutionOpened);
    }

    private void OnSolutionClosed()
    {
        Raise(VisualStudioStateChangeKind.SolutionClosed);
    }

    private void OnWindowActivated(Window gotFocus, Window lostFocus)
    {
        _ = gotFocus;
        _ = lostFocus;
        Raise(VisualStudioStateChangeKind.ActiveWindowChanged);
        Raise(VisualStudioStateChangeKind.ActiveDocumentChanged);
    }

    private void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
    {
        _ = executionAction;
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged, CreateDebuggerEvents("Break", reason, BrokerEventTypes.DebuggerBreak, "Visual Studio debugger entered break mode."));
    }

    private void OnEnterDesignMode(dbgEventReason reason)
    {
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged, CreateDebuggerEvents("Design", reason, BrokerEventTypes.DebuggerStopped, "Visual Studio debugger entered design mode."));
    }

    private void OnEnterRunMode(dbgEventReason reason)
    {
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged, CreateDebuggerEvents("Run", reason, specificEventType: null, specificSummary: null));
    }

    private void OnBuildBegin(vsBuildScope scope, vsBuildAction action)
    {
        Raise(
            VisualStudioStateChangeKind.BuildStarted,
            new BrokerEventNotification(
                SessionIdentity.CurrentProcessSessionId(),
                BrokerEventTypes.BuildStarted,
                $"Visual Studio build started ({scope}, {action}).",
                new Dictionary<string, string>
                {
                    ["scope"] = scope.ToString(),
                    ["action"] = action.ToString()
                }));
    }

    private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
    {
        var lastBuildInfo = dte?.Solution?.SolutionBuild?.LastBuildInfo ?? 0;
        Raise(
            VisualStudioStateChangeKind.BuildCompleted,
            new BrokerEventNotification(
                SessionIdentity.CurrentProcessSessionId(),
                BrokerEventTypes.BuildCompleted,
                lastBuildInfo == 0
                    ? "Visual Studio build completed successfully."
                    : $"Visual Studio build completed with {lastBuildInfo} error(s).",
                new Dictionary<string, string>
                {
                    ["scope"] = scope.ToString(),
                    ["action"] = action.ToString(),
                    ["lastBuildInfo"] = lastBuildInfo.ToString(),
                    ["succeeded"] = (lastBuildInfo == 0).ToString()
                }));
    }

    private void Raise(VisualStudioStateChangeKind kind)
    {
        Raise(kind, (IReadOnlyCollection<BrokerEventNotification>?)null);
    }

    private void Raise(VisualStudioStateChangeKind kind, BrokerEventNotification? brokerEvent)
    {
        Raise(kind, brokerEvent is null ? null : new[] { brokerEvent });
    }

    private void Raise(VisualStudioStateChangeKind kind, IReadOnlyCollection<BrokerEventNotification>? brokerEvents)
    {
        StateChanged?.Invoke(this, new VisualStudioStateChangedEventArgs(kind, brokerEvents));
    }

    private static IReadOnlyCollection<BrokerEventNotification> CreateDebuggerEvents(
        string mode,
        dbgEventReason reason,
        string? specificEventType,
        string? specificSummary)
    {
        var data = new Dictionary<string, string>
        {
            ["mode"] = mode,
            ["reason"] = reason.ToString()
        };
        var events = new List<BrokerEventNotification>
        {
            new(
                SessionIdentity.CurrentProcessSessionId(),
                BrokerEventTypes.DebuggerModeChanged,
                $"Visual Studio debugger mode changed to {mode}.",
                data)
        };

        if (specificEventType is { Length: > 0 })
        {
            events.Add(new BrokerEventNotification(
                SessionIdentity.CurrentProcessSessionId(),
                specificEventType,
                specificSummary,
                data));
        }

        return events;
    }
}
