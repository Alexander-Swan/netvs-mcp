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

/// <summary>
/// Facade over the desktop UIA, Win32-console, and web-debug (CDP) automation backends.
/// This class previously contained all three concerns directly (~1600 lines); it now composes
/// <see cref="UiAutomationCapabilityService"/>, <see cref="ConsoleAutomationCapabilityService"/>,
/// and <see cref="WebDebugCapabilityService"/> so each backend can be read, tested, and changed
/// independently. The public
/// <see cref="IAutomationCapabilityService"/> surface, and this class's constructor signature,
/// are unchanged so callers (DI wiring in <c>NetVsMcpPackage</c>, the RPC target) need no changes.
/// </summary>
internal sealed class AutomationCapabilityService : IAutomationCapabilityService
{
    private readonly UiAutomationCapabilityService ui;
    private readonly ConsoleAutomationCapabilityService console;
    private readonly WebDebugCapabilityService web;

    public AutomationCapabilityService(AsyncPackage package)
    {
        ui = new UiAutomationCapabilityService(package);
        console = new ConsoleAutomationCapabilityService(package, ui);
        web = new WebDebugCapabilityService(ui);
    }

    public Task<AutomationResult> ConsoleReadAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        console.ConsoleReadAsync(request, cancellationToken);

    public Task<AutomationResult> DiagnosticsBindingErrorsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        console.DiagnosticsBindingErrorsAsync(request, cancellationToken);

    public Task<AutomationResult> ConsoleSendAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        console.ConsoleSendAsync(request, cancellationToken);

    public Task<AutomationResult> ConsoleGetInfoAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        console.ConsoleGetInfoAsync(request, cancellationToken);

    public Task<AutomationResult> UiCaptureWindowAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiCaptureWindowAsync(request, cancellationToken);

    public Task<AutomationResult> UiCaptureRegionAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiCaptureRegionAsync(request, cancellationToken);

    public Task<AutomationResult> UiSnapshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiSnapshotAsync(request, cancellationToken);

    public Task<AutomationResult> UiGetTreeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiGetTreeAsync(request, cancellationToken);

    public Task<AutomationResult> UiFindElementsAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiFindElementsAsync(request, cancellationToken);

    public Task<AutomationResult> UiGetElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiGetElementAsync(request, cancellationToken);

    public Task<AutomationResult> UiClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiDoubleClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiDoubleClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiRightClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiRightClickAsync(request, cancellationToken);

    public Task<AutomationResult> UiDragAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiDragAsync(request, cancellationToken);

    public Task<AutomationResult> UiSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiSetValueAsync(request, cancellationToken);

    public Task<AutomationResult> UiInvokeAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiInvokeAsync(request, cancellationToken);

    public Task<AutomationResult> UiSendKeysAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiSendKeysAsync(request, cancellationToken);

    public Task<AutomationResult> UiWaitForElementAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiWaitForElementAsync(request, cancellationToken);

    public Task<AutomationResult> UiWaitIdleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        ui.UiWaitIdleAsync(request, cancellationToken);

    public Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebConnectAsync(request, cancellationToken);

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebDisconnectAsync(request, cancellationToken);

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebStatusAsync(request, cancellationToken);

    public Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebNavigateAsync(request, cancellationToken);

    public Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebScreenshotAsync(request, cancellationToken);

    public Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebDomGetAsync(request, cancellationToken);

    public Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebDomQueryAsync(request, cancellationToken);

    public Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebConsoleAsync(request, cancellationToken);

    public Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebJsExecuteAsync(request, cancellationToken);

    public Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebNetworkAsync(request, cancellationToken);

    public Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebElementClickAsync(request, cancellationToken);

    public Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken) =>
        web.WebElementSetValueAsync(request, cancellationToken);
}
