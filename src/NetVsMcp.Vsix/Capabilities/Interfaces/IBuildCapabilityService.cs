using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IBuildCapabilityService
{
    Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken);
    Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken);
    Task<BuildStatusInfo> CancelBuildAsync(CancellationToken cancellationToken);
    Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken);
    Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken);
    Task<BuildStatusInfo> GetBuildStatusAsync(CancellationToken cancellationToken);
    Task<BuildConfigurationInfo> GetBuildConfigurationAsync(CancellationToken cancellationToken);
    Task<BuildConfigurationInfo> SetBuildConfigurationAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken);
    Task<ErrorListResult> ListErrorsAsync(ErrorListRequest request, CancellationToken cancellationToken);
    Task<TaskListResult> ListTaskItemsAsync(TaskListRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> AddTaskItemAsync(TaskListAddRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> RemoveTaskItemAsync(TaskListMutationRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> SetTaskItemCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken);
    Task<OutputReadResult> ReadOutputAsync(OutputReadRequest request, CancellationToken cancellationToken);
    Task<OutputPaneListResult> ListOutputPanesAsync(CancellationToken cancellationToken);
    Task<OutputReadResult> ClearOutputAsync(OutputPaneRequest request, CancellationToken cancellationToken);
    Task<OutputReadResult> WriteOutputAsync(OutputWriteRequest request, CancellationToken cancellationToken);
}
