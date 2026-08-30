using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IBrokerNotificationService
{
    void Show(BrokerConnectivityIssue issue);

    void Clear();
}
