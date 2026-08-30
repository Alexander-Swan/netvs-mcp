using System;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IVisualStudioStateChangeMonitor
{
    event EventHandler<VisualStudioStateChangedEventArgs>? StateChanged;
}
