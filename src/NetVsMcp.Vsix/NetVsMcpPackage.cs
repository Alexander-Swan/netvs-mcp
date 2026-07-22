using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class NetVsMcpPackage : AsyncPackage
{
    public const string PackageGuidString = "8e51bd56-6a22-4461-a578-b23c67fbc087";

    private BrokerRegistrationLifecycle? lifecycle;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var snapshotProvider = new VisualStudioSessionSnapshotProvider(this);
        var stateMonitor = new VisualStudioStateChangeMonitor(this);
        await stateMonitor.InitializeAsync(cancellationToken);

        var capabilities = new VisualStudioCapabilityCatalog(
            new EditorCapabilityService(this),
            new NavigationCapabilityService(this),
            new BuildCapabilityService(this),
            new DebuggerCapabilityService(this));
        var capabilityRpcTarget = new VisualStudioCapabilityRpcTarget(capabilities, snapshotProvider);

        lifecycle = new BrokerRegistrationLifecycle(
            snapshotProvider,
            capabilities,
            stateMonitor,
            new NamedPipeBrokerConnectionFactory(BrokerPipeName.CurrentUserDefault(), capabilityRpcTarget));

        await lifecycle.StartAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lifecycle?.Dispose();
            lifecycle = null;
        }

        base.Dispose(disposing);
    }
}
