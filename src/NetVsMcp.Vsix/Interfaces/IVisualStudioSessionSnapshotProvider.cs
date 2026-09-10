using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix.Interfaces;

internal interface IVisualStudioSessionSnapshotProvider
{
    Task<VsSessionSnapshot> CaptureAsync(CancellationToken cancellationToken);
}
