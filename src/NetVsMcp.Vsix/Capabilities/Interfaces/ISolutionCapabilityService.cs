using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface ISolutionCapabilityService
{
    Task<SolutionInfoResult> GetSolutionInfoAsync(CancellationToken cancellationToken);
    Task<SolutionInfoResult> OpenSolutionAsync(SolutionOpenRequest request, CancellationToken cancellationToken);
    Task<SolutionInfoResult> CloseSolutionAsync(CancellationToken cancellationToken);
    Task<ProjectListResult> ListProjectsAsync(CancellationToken cancellationToken);
    Task<ProjectInfo> AddProjectAsync(SolutionAddProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectInfo> RemoveProjectAsync(ProjectInfoRequest request, CancellationToken cancellationToken);
    Task<ProjectInfo?> GetProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken);
    Task<ProjectInfo> AddFileAsync(ProjectFileRequest request, CancellationToken cancellationToken);
    Task<ProjectFileResult> RemoveFileAsync(ProjectFileRequest request, CancellationToken cancellationToken);
    Task<ProjectReferenceResult> AddReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken);
    Task<ProjectReferenceResult> RemoveReferenceAsync(ProjectReferenceRequest request, CancellationToken cancellationToken);
    Task<NugetListResult> ListNugetPackagesAsync(NugetListRequest request, CancellationToken cancellationToken);
    Task<NugetSearchResult> SearchNugetPackagesAsync(NugetSearchRequest request, CancellationToken cancellationToken);
    Task<NugetMutationResult> InstallNugetPackageAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken);
    Task<NugetMutationResult> UpdateNugetPackageAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken);
    Task<NugetMutationResult> UninstallNugetPackageAsync(NugetPackageMutationRequest request, CancellationToken cancellationToken);
    Task<StartupProjectResult> GetStartupProjectAsync(CancellationToken cancellationToken);
    Task<StartupProjectResult> SetStartupProjectAsync(StartupProjectSetRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> DiscoverTestsAsync(TestDiscoverRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> RunTestsAsync(TestRunRequest request, CancellationToken cancellationToken);
    Task<TestDebugResult> DebugTestAsync(TestDebugRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> GetTestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken);
    Task<PackageRestoreResult> RestorePackagesAsync(PackageRestoreRequest request, CancellationToken cancellationToken);
}
