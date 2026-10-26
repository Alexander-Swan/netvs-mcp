using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix.Tests;

internal sealed class DebuggerIntegrationFixture : IDisposable
{
    private readonly Dictionary<string, Delegate> eventHandlers = new();
    private readonly List<Breakpoint> breakpoints = new();
    private dbgDebugMode mode = dbgDebugMode.dbgBreakMode;
    private string tag = string.Empty;
    private string filter = string.Empty;
    internal readonly Breakpoint2 Target;
    internal int ProcessId = 123;
    internal int TargetLine = 2;
    internal bool TargetEnabled = true;
    internal string SolutionPath = @"C:\Fixture\Fixture.sln";
    internal readonly string Source = Path.GetTempFileName();
    internal DebuggerCapabilityService Service { get; }
    internal Action OnGo = () => { };
    internal int GoCalls;
    internal int BaselineCalls;
    internal Action OnBaselineApply = () => { };

    internal DebuggerIntegrationFixture(bool designMode = false, IHotReloadInitializationNotification? notification = null)
    {
        if (designMode) mode = dbgDebugMode.dbgDesignMode;
        Target = Proxy<Breakpoint2>.Create(call => call.MethodName switch
        {
            "get_File" => Source,
            "get_FileLine" => TargetLine,
            "get_FileColumn" => 1,
            "get_Tag" => tag,
            "set_Tag" => Set(() => tag = (string)call.Args[0]),
            "get_FilterBy" => filter,
            "set_FilterBy" => Set(() => filter = (string)call.Args[0]),
            "get_Enabled" => TargetEnabled,
            "get_BreakWhenHit" => true,
            "get_Condition" => string.Empty,
            "get_HitCountType" => dbgHitCountType.dbgHitCountTypeNone,
            "Delete" => Set(() => breakpoints.Clear()),
            _ => throw new NotSupportedException(call.MethodName)
        });
        var all = Collection(breakpoints, add: () => { breakpoints.Add((Breakpoint)Target); return Collection(new[] { (Breakpoint)Target }); });
        var process = Proxy<Process>.Create(call => call.MethodName == "get_ProcessID" ? ProcessId : throw new NotSupportedException(call.MethodName));
        var processes = Proxy<Processes>.Create(call => call.MethodName == "get_Count" ? 1 : throw new NotSupportedException(call.MethodName));
        var debugger = Proxy<Debugger>.Create(call => call.MethodName switch
        {
            "get_CurrentMode" => mode,
            "get_CurrentProcess" => process,
            "get_DebuggedProcesses" => processes,
            "get_CurrentThread" => throw new COMException("Optional current source is unavailable"),
            "get_Breakpoints" => all,
            "get_AllBreakpointsLastHit" => Collection(new[] { (Breakpoint)Target }),
            "get_LastBreakReason" => dbgEventReason.dbgEventReasonBreakpoint,
            "Go" => Set(() => { GoCalls++; OnGo(); }),
            _ => throw new NotSupportedException(call.MethodName)
        });
        var events = Proxy<DebuggerEvents>.Create(call =>
        {
            if (call.MethodName.StartsWith("add_")) eventHandlers[call.MethodName.Substring(4)] = (Delegate)call.Args[0];
            else if (call.MethodName.StartsWith("remove_")) eventHandlers.Remove(call.MethodName.Substring(7));
            else throw new NotSupportedException(call.MethodName);
            return null;
        });
        var dteEvents = Proxy<Events>.Create(call => call.MethodName == "get_DebuggerEvents" ? events : throw new NotSupportedException(call.MethodName));
        var solution = Proxy<Solution>.Create(call => call.MethodName == "get_FullName" ? SolutionPath : throw new NotSupportedException(call.MethodName));
        var command = Proxy<Command>.Create(call => call.MethodName == "get_IsAvailable" ? true : throw new NotSupportedException(call.MethodName));
        var commands = Proxy<Commands>.Create(call => call.MethodName == "Item" ? command : throw new NotSupportedException(call.MethodName));
        var dte = Proxy<DTE>.Create(call => call.MethodName switch
        {
            "get_Debugger" => debugger,
            "get_Commands" => commands,
            "ExecuteCommand" => Set(() => { BaselineCalls++; OnBaselineApply(); }),
            "get_Events" => dteEvents,
            "get_Solution" => solution,
            _ => throw new NotSupportedException(call.MethodName)
        });
        Service = new DebuggerCapabilityService(() => Task.FromResult<DTE?>(dte), _ => Task.FromResult(notification));
    }

    internal async Task<DebugRunToRequest> RequestAsync()
    {
        var state = await Service.GetStatusAsync(CancellationToken.None);
        return new() { DocumentPath = Source, Line = 2, ExpectedProcessId = 123,
            ExpectedGeneration = state.Generation!.Value, ExpectedStopId = state.StopId!, TimeoutSeconds = 1 };
    }

    internal void HitTarget()
    {
        mode = dbgDebugMode.dbgRunMode;
        eventHandlers["OnEnterRunMode"].DynamicInvoke(dbgEventReason.dbgEventReasonGo);
        mode = dbgDebugMode.dbgBreakMode;
        eventHandlers["OnEnterBreakMode"].DynamicInvoke(dbgEventReason.dbgEventReasonBreakpoint, dbgExecutionAction.dbgExecutionActionDefault);
    }

    public void Dispose() { Service.Dispose(); File.Delete(Source); }

    private static object? Set(Action action) { action(); return null; }

    private static Breakpoints Collection(IEnumerable<Breakpoint> values, Func<Breakpoints>? add = null) =>
        Proxy<Breakpoints>.Create(call => call.MethodName switch
        {
            "get_Count" => new List<Breakpoint>(values).Count,
            "GetEnumerator" => ((IEnumerable)values).GetEnumerator(),
            "Item" => new List<Breakpoint>(values)[(int)call.Args[0] - 1],
            "Add" => add!(),
            _ => throw new NotSupportedException(call.MethodName)
        });
    private sealed class Proxy<T>(Func<IMethodCallMessage, object?> invoke) : RealProxy(typeof(T)) where T : class
    {
        internal static T Create(Func<IMethodCallMessage, object?> invoke) => (T)new Proxy<T>(invoke).GetTransparentProxy();
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try { return new ReturnMessage(invoke(call), call.Args, call.ArgCount, call.LogicalCallContext, call); }
            catch (Exception ex) { return new ReturnMessage(ex, call); }
        }
    }
}
