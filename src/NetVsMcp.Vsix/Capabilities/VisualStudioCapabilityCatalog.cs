using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal interface IVisualStudioCapabilityCatalog
{
    IReadOnlyCollection<string> CapabilityNames { get; }
    IGeneralIdeCapabilityService GeneralIde { get; }
    IEditorCapabilityService Editor { get; }
    INavigationCapabilityService Navigation { get; }
    IBuildCapabilityService Build { get; }
    IDebuggerCapabilityService Debugger { get; }
    ISolutionCapabilityService Solution { get; }
}

internal sealed class VisualStudioCapabilityCatalog : IVisualStudioCapabilityCatalog
{
    public VisualStudioCapabilityCatalog(
        IGeneralIdeCapabilityService generalIde,
        IEditorCapabilityService editor,
        INavigationCapabilityService navigation,
        IBuildCapabilityService build,
        IDebuggerCapabilityService debugger,
        ISolutionCapabilityService solution)
    {
        GeneralIde = generalIde;
        Editor = editor;
        Navigation = navigation;
        Build = build;
        Debugger = debugger;
        Solution = solution;
    }

    public IReadOnlyCollection<string> CapabilityNames { get; } =
    [
        "editor",
        "generalIde",
        "editing",
        "navigation",
        "build",
        "debugger",
        "projectSystem",
        "tests"
    ];

    public IGeneralIdeCapabilityService GeneralIde { get; }
    public IEditorCapabilityService Editor { get; }
    public INavigationCapabilityService Navigation { get; }
    public IBuildCapabilityService Build { get; }
    public IDebuggerCapabilityService Debugger { get; }
    public ISolutionCapabilityService Solution { get; }
}
