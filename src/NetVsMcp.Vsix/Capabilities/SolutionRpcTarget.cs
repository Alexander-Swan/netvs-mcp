using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class SolutionRpcTarget
{
    private readonly ISolutionCapabilityService solution;

    public SolutionRpcTarget(ISolutionCapabilityService solution)
    {
        this.solution = solution;
    }

    public Task<SolutionInfoResult> SolutionInfoAsync(CancellationToken cancellationToken)
    {
        return solution.GetSolutionInfoAsync(cancellationToken);
    }

    public Task<SolutionInfoResult> SolutionOpenAsync(SolutionOpenRequest request, CancellationToken cancellationToken)
    {
        return solution.OpenSolutionAsync(request, cancellationToken);
    }

    public Task<SolutionInfoResult> SolutionCloseAsync(CancellationToken cancellationToken)
    {
        return solution.CloseSolutionAsync(cancellationToken);
    }

    public Task<ProjectListResult> ProjectListAsync(CancellationToken cancellationToken)
    {
        return solution.ListProjectsAsync(cancellationToken);
    }

    public Task<ProjectInfo> SolutionAddProjectAsync(SolutionAddProjectRequest request, CancellationToken cancellationToken)
    {
        return solution.AddProjectAsync(request, cancellationToken);
    }

    public Task<ProjectInfo> SolutionRemoveProjectAsync(ProjectInfoRequest request, CancellationToken cancellationToken)
    {
        return solution.RemoveProjectAsync(request, cancellationToken);
    }

    public Task<ProjectInfo?> ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken)
    {
        return solution.GetProjectInfoAsync(request, cancellationToken);
    }

    public Task<ProjectInfo> ProjectAddFileAsync(ProjectFileRequest request, CancellationToken cancellationToken)
    {
        return solution.AddFileAsync(request, cancellationToken);
    }

    public Task<ProjectFileResult> ProjectRemoveFileAsync(ProjectFileRequest request, CancellationToken cancellationToken)
    {
        return solution.RemoveFileAsync(request, cancellationToken);
    }

    public Task<ProjectReferenceResult> ProjectAddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken)
    {
        return solution.AddReferenceAsync(request, cancellationToken);
    }

    public Task<ProjectReferenceResult> ProjectRemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken)
    {
        return solution.RemoveReferenceAsync(request, cancellationToken);
    }

    public Task<NugetListResult> NugetListAsync(NugetListRequest request, CancellationToken cancellationToken)
    {
        return solution.ListNugetPackagesAsync(request, cancellationToken);
    }

    public Task<NugetSearchResult> NugetSearchAsync(NugetSearchRequest request, CancellationToken cancellationToken)
    {
        return solution.SearchNugetPackagesAsync(request, cancellationToken);
    }

    public Task<NugetMutationResult> NugetInstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken)
    {
        return solution.InstallNugetPackageAsync(request, cancellationToken);
    }

    public Task<NugetMutationResult> NugetUpdateAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken)
    {
        return solution.UpdateNugetPackageAsync(request, cancellationToken);
    }

    public Task<NugetMutationResult> NugetUninstallAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken)
    {
        return solution.UninstallNugetPackageAsync(request, cancellationToken);
    }

    public Task<StartupProjectResult> StartupProjectGetAsync(CancellationToken cancellationToken)
    {
        return solution.GetStartupProjectAsync(cancellationToken);
    }

    public Task<StartupProjectResult> StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken)
    {
        return solution.SetStartupProjectAsync(request, cancellationToken);
    }

    public Task<TestOperationResult> TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken)
    {
        return solution.DiscoverTestsAsync(request, cancellationToken);
    }

    public Task<TestOperationResult> TestRunAsync(TestRunRequest request, CancellationToken cancellationToken)
    {
        return solution.RunTestsAsync(request, cancellationToken);
    }

    public Task<TestOperationResult> TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken)
    {
        return solution.GetTestResultsAsync(request, cancellationToken);
    }

    public Task<PackageRestoreResult> PackageRestoreAsync(PackageRestoreRequest request, CancellationToken cancellationToken)
    {
        return solution.RestorePackagesAsync(request, cancellationToken);
    }
}
