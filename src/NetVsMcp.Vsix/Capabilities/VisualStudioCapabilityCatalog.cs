using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal interface IVisualStudioCapabilityCatalog
{
    IReadOnlyCollection<string> CapabilityNames { get; }
    IEditorCapabilityService Editor { get; }
    INavigationCapabilityService Navigation { get; }
    IBuildCapabilityService Build { get; }
    IDebuggerCapabilityService Debugger { get; }
    ISolutionCapabilityService Solution { get; }
}

internal sealed class VisualStudioCapabilityCatalog : IVisualStudioCapabilityCatalog
{
    public VisualStudioCapabilityCatalog(
        IEditorCapabilityService editor,
        INavigationCapabilityService navigation,
        IBuildCapabilityService build,
        IDebuggerCapabilityService debugger,
        ISolutionCapabilityService solution)
    {
        Editor = editor;
        Navigation = navigation;
        Build = build;
        Debugger = debugger;
        Solution = solution;
    }

    public IReadOnlyCollection<string> CapabilityNames { get; } =
    [
        "editor",
        "navigation",
        "build",
        "debugger",
        "projectSystem",
        "tests"
    ];

    public IEditorCapabilityService Editor { get; }
    public INavigationCapabilityService Navigation { get; }
    public IBuildCapabilityService Build { get; }
    public IDebuggerCapabilityService Debugger { get; }
    public ISolutionCapabilityService Solution { get; }
}
