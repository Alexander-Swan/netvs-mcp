using System.ComponentModel;

namespace NetVsMcp.Contracts;

public sealed class ModuleListRequest
{
    [Description("Optional positive OS process ID; omit to enumerate all Concord processes, including dumps.")]
    public int? ProcessId { get; set; }
    [Description("Optional case-insensitive substring of the module name or full path.")]
    public string? Name { get; set; }
    [Description("Include debugger symbol-search diagnostics for each matching module. Does not load symbols.")]
    public bool IncludeSymbolLoadInformation { get; set; }
}

public sealed record ModuleInfo(
    int? ProcessId,
    Guid ProcessUniqueId,
    Guid ModuleId,
    string Name,
    string Path,
    string? Version,
    ulong LoadAddress,
    ulong Size,
    bool SymbolsLoaded,
    string SymbolStatus,
    string? SymbolFile,
    string? SymbolLoadInformation);

public sealed record ModuleListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<ModuleInfo> Modules);
