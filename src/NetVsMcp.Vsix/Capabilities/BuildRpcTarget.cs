using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class BuildRpcTarget
{
    private readonly IBuildCapabilityService build;

    public BuildRpcTarget(IBuildCapabilityService build)
    {
        this.build = build;
    }

    public Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        return build.BuildSolutionAsync(request, cancellationToken);
    }

    public Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken)
    {
        return build.BuildProjectAsync(request, cancellationToken);
    }

    public Task<BuildStatusInfo> BuildCancelAsync(CancellationToken cancellationToken)
    {
        return build.CancelBuildAsync(cancellationToken);
    }

    public Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken)
    {
        return build.CleanSolutionAsync(cancellationToken);
    }

    public Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        return build.RebuildSolutionAsync(request, cancellationToken);
    }

    public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
    {
        return build.GetBuildStatusAsync(cancellationToken);
    }

    public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken)
    {
        return build.GetBuildConfigurationAsync(cancellationToken);
    }

    public Task<BuildConfigurationInfo> BuildConfigurationSetAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken)
    {
        return build.SetBuildConfigurationAsync(request, cancellationToken);
    }

    public Task<ErrorListResult> ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken)
    {
        return build.ListErrorsAsync(request, cancellationToken);
    }

    public Task<OutputReadResult> OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken)
    {
        return build.ReadOutputAsync(request, cancellationToken);
    }

    public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken)
    {
        return build.ListOutputPanesAsync(cancellationToken);
    }

    public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken)
    {
        return build.ClearOutputAsync(request, cancellationToken);
    }
}
