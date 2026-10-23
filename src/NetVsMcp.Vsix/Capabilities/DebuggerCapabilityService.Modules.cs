using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    public async Task<ModuleListResult> ListModulesAsync(ModuleListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (request.ProcessId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.ProcessId));

        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode)
            throw new InvalidOperationException("Pause the debugger before listing modules and symbol status.");

        return ReadModules(request, cancellationToken);
    }

    // Read debugger-owned state; never load symbols or evaluate debuggee expressions.
    private static ModuleListResult ReadModules(ModuleListRequest request, CancellationToken cancellationToken)
    {
        var processes = DkmProcess.GetProcesses();
        if (processes.Length == 0)
            return new ModuleListResult(false, "The active debugger engine does not expose Concord modules.", Array.Empty<ModuleInfo>());

        var result = new List<ModuleInfo>();
        var matchedProcess = false;
        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.IsUnloaded || (request.ProcessId.HasValue && process.LivePart?.Id != request.ProcessId))
                continue;

            matchedProcess = true;
            foreach (var runtime in process.GetRuntimeInstances())
            {
                foreach (var module in runtime.GetModuleInstances())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (module.IsUnloaded || (!string.IsNullOrWhiteSpace(request.Name)
                        && module.Name.IndexOf(request.Name, StringComparison.OrdinalIgnoreCase) < 0
                        && module.FullName.IndexOf(request.Name, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;

                    var symbols = module.Module;
                    result.Add(new ModuleInfo(process.LivePart?.Id, process.UniqueId, module.UniqueId,
                        module.Name, module.FullName, module.Version?.FileVersionString, module.BaseAddress, module.Size,
                        symbols is not null,
                        symbols is not null ? "loaded" : (module.Flags & DkmModuleFlags.SymbolsUnavailable) != 0 ? "unavailable" : "notLoaded",
                        symbols?.FilePath,
                        request.IncludeSymbolLoadInformation ? module.GetSymbolLoadInformation() : null));
                }
            }
        }

        if (!matchedProcess)
            throw new InvalidOperationException("The requested process is not available through the active Concord debugger.");

        return new ModuleListResult(true, null, result);
    }
}
