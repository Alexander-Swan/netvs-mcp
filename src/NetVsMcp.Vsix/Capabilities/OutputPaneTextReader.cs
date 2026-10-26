using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal static class OutputPaneTextReader
{
    public static async Task<string> ReadAsync(AsyncPackage package, OutputWindowPane pane, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        try
        {
            if (pane.TextDocument is TextDocument document)
            {
                var text = document.StartPoint.CreateEditPoint().GetText(document.EndPoint);
                if (!string.IsNullOrEmpty(text)) return text;
            }
        }
        catch (COMException) { }

        // Some VS output panes expose their editor buffer only through the shell,
        // even while the DTE text document returns empty. Reading it does not activate
        // the pane or move the developer's focus.
        try
        {
            if (!Guid.TryParse(pane.Guid, out var id)) return string.Empty;
            var output = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (output is null || output.GetPane(ref id, out var native) < 0 || native is null) return string.Empty;
            IVsTextLines? buffer = null;
            if (native is IVsTextBufferProvider provider) provider.GetTextBuffer(out buffer);
            if (buffer is null && native is IVsTextView view) view.GetBuffer(out buffer);
            if (buffer is not null && buffer.GetLastLineIndex(out var line, out var column) >= 0 &&
                buffer.GetLineText(0, 0, line, column, out var text) >= 0) return text ?? string.Empty;
        }
        catch (COMException) { }
        return string.Empty;
    }
}
