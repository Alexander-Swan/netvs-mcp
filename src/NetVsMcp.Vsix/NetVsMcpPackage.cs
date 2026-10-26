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
    private BrokerStatusInfoBarService? brokerNotificationService;
    private DebuggerCapabilityService? debuggerService;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var snapshotProvider = new VisualStudioSessionSnapshotProvider(this);
        var stateMonitor = new VisualStudioStateChangeMonitor(this);
        await stateMonitor.InitializeAsync(cancellationToken);

        debuggerService = new DebuggerCapabilityService(this);
        await debuggerService.InitializeAsync(cancellationToken);
        var capabilities = new VisualStudioCapabilityCatalog(
            new GeneralIdeCapabilityService(this),
            new EditorCapabilityService(this),
            new NavigationCapabilityService(this),
            new CodeActionsCapabilityService(this),
            new BuildCapabilityService(this),
            debuggerService,
            new AutomationCapabilityService(this),
            new SolutionCapabilityService(this));
        var capabilityRpcTarget = new VisualStudioCapabilityRpcTarget(capabilities, snapshotProvider);
        brokerNotificationService = new BrokerStatusInfoBarService(this);
        var brokerLauncher = new BundledBrokerProcessLauncher();

        lifecycle = new BrokerRegistrationLifecycle(
            snapshotProvider,
            capabilities,
            stateMonitor,
            new NamedPipeBrokerConnectionFactory(
                BrokerPipeName.CurrentUserDefault(),
                capabilityRpcTarget,
                new BrokerInstallationDetector(brokerLauncher),
                brokerLauncher),
            brokerNotificationService,
            debuggerService.Events);

        stateMonitor.StateChanged += (_, args) => debuggerService?.Events.Publish(args.Kind.ToString());

        await lifecycle.StartAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lifecycle?.Dispose();
            lifecycle = null;
            JoinableTaskFactory.Run(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                debuggerService?.Dispose();
                debuggerService = null;
            });
            brokerNotificationService = null;
        }

        base.Dispose(disposing);
    }
}
