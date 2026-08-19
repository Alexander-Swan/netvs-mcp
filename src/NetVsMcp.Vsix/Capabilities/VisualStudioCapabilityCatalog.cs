using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal interface IVisualStudioCapabilityCatalog
{
    IReadOnlyCollection<string> CapabilityNames { get; }
    IGeneralIdeCapabilityService GeneralIde { get; }
    IEditorCapabilityService Editor { get; }
    INavigationCapabilityService Navigation { get; }
    ICodeActionsCapabilityService CodeActions { get; }
    IBuildCapabilityService Build { get; }
    IDebuggerCapabilityService Debugger { get; }
    IAutomationCapabilityService Automation { get; }
    ISolutionCapabilityService Solution { get; }
}

internal sealed class VisualStudioCapabilityCatalog : IVisualStudioCapabilityCatalog
{
    public VisualStudioCapabilityCatalog(
        IGeneralIdeCapabilityService generalIde,
        IEditorCapabilityService editor,
        INavigationCapabilityService navigation,
        ICodeActionsCapabilityService codeActions,
        IBuildCapabilityService build,
        IDebuggerCapabilityService debugger,
        IAutomationCapabilityService automation,
        ISolutionCapabilityService solution)
    {
        GeneralIde = generalIde;
        Editor = editor;
        Navigation = navigation;
        CodeActions = codeActions;
        Build = build;
        Debugger = debugger;
        Automation = automation;
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
        "automation",
        "projectSystem",
        "tests"
    ];

    public IGeneralIdeCapabilityService GeneralIde { get; }
    public IEditorCapabilityService Editor { get; }
    public INavigationCapabilityService Navigation { get; }
    public ICodeActionsCapabilityService CodeActions { get; }
    public IBuildCapabilityService Build { get; }
    public IDebuggerCapabilityService Debugger { get; }
    public IAutomationCapabilityService Automation { get; }
    public ISolutionCapabilityService Solution { get; }
}
