using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IAutomationCapabilityService
{
    Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken);
    Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken);
}
