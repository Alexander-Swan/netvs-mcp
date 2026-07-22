using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal interface IDebuggerCapabilityService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task ContinueAsync(CancellationToken cancellationToken);
    Task BreakAsync(CancellationToken cancellationToken);
    Task StepAsync(DebugStepKind stepKind, CancellationToken cancellationToken);
    Task SetBreakpointAsync(string documentPath, int line, string? condition, CancellationToken cancellationToken);
}

internal enum DebugStepKind
{
    Into,
    Over,
    Out
}

internal sealed class DebuggerCapabilityService : IDebuggerCapabilityService
{
    private readonly AsyncPackage package;

    public DebuggerCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = package;
        _ = cancellationToken;
        throw new System.NotImplementedException("Start debugging through VS debugger services.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Stop debugging through VS debugger services.");
    }

    public Task ContinueAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Continue debugging through VS debugger services.");
    }

    public Task BreakAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Break all through VS debugger services.");
    }

    public Task StepAsync(DebugStepKind stepKind, CancellationToken cancellationToken)
    {
        _ = stepKind;
        _ = cancellationToken;
        throw new System.NotImplementedException("Map step commands to VS debugger services.");
    }

    public Task SetBreakpointAsync(string documentPath, int line, string? condition, CancellationToken cancellationToken)
    {
        _ = documentPath;
        _ = line;
        _ = condition;
        _ = cancellationToken;
        throw new System.NotImplementedException("Create file, conditional, hit-count, and function breakpoints through VS debugger services.");
    }
}
