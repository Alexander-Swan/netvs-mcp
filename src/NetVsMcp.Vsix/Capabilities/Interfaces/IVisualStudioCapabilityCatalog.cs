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
