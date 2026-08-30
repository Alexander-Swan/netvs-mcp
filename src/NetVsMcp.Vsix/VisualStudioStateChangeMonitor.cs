using System;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed class VisualStudioStateChangedEventArgs : EventArgs
{
    public VisualStudioStateChangedEventArgs(VisualStudioStateChangeKind kind)
    {
        Kind = kind;
    }

    public VisualStudioStateChangeKind Kind { get; }
}

internal enum VisualStudioStateChangeKind
{
    SolutionOpened,
    SolutionClosed,
    ActiveDocumentChanged,
    DebuggerModeChanged,
    ActiveWindowChanged
}

internal sealed class VisualStudioStateChangeMonitor : IVisualStudioStateChangeMonitor
{
    private readonly AsyncPackage package;

    private SolutionEvents? solutionEvents;
    private WindowEvents? windowEvents;
    private DebuggerEvents? debuggerEvents;

    public VisualStudioStateChangeMonitor(AsyncPackage package)
    {
        this.package = package;
    }

    public event EventHandler<VisualStudioStateChangedEventArgs>? StateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
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
        _ = reason;
        _ = executionAction;
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged);
    }

    private void OnEnterDesignMode(dbgEventReason reason)
    {
        _ = reason;
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged);
    }

    private void OnEnterRunMode(dbgEventReason reason)
    {
        _ = reason;
        Raise(VisualStudioStateChangeKind.DebuggerModeChanged);
    }

    private void Raise(VisualStudioStateChangeKind kind)
    {
        StateChanged?.Invoke(this, new VisualStudioStateChangedEventArgs(kind));
    }
}
