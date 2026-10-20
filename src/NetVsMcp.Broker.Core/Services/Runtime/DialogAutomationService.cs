using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal interface IDialogAutomationService
{
    IReadOnlyList<DialogInfo> List(int processId, CancellationToken cancellationToken);
    bool Respond(int processId, string dialogId, string buttonId, CancellationToken cancellationToken);
}

// Runs in the broker, never on the VS UI thread or through the VSIX RPC queue.
internal sealed class DialogAutomationService : IDialogAutomationService
{
    private sealed record Button(AutomationElement Element, int[] RuntimeId);
    private sealed record Dialog(int ProcessId, long ProcessStarted, nint Handle, int[] RuntimeId,
        Dictionary<string, Button> Buttons);

    private readonly Dictionary<string, Dialog> dialogs = new();

    public IReadOnlyList<DialogInfo> List(int processId, CancellationToken cancellationToken)
    {
        using var process = Process.GetProcessById(processId);
        var started = process.StartTime.ToUniversalTime().Ticks;
        var result = new List<DialogInfo>();
        var found = new Dictionary<string, Dialog>();
        var handles = new List<nint>();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var pid);
            if (pid == processId && IsWindowVisible(handle)) handles.Add(handle);
            return true;
        }, 0);

        foreach (var handle in handles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var root = AutomationElement.FromHandle(handle);
                if (!IsModal(root, handle, processId)) continue;
                var texts = new List<string>();
                var buttons = new List<DialogButtonInfo>();
                var buttonRefs = new Dictionary<string, Button>();
                // Bound traversal for custom dialogs with very large accessibility trees.
                var queue = new Queue<AutomationElement>();
                queue.Enqueue(root);
                for (var count = 0; queue.Count > 0 && count < 500; count++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var element = queue.Dequeue();
                    var current = element.Current;
                    if (current.ControlType == ControlType.Text && !string.IsNullOrWhiteSpace(current.Name))
                        texts.Add(current.Name);
                    if (current.ControlType == ControlType.Button)
                    {
                        var id = Guid.NewGuid().ToString("N");
                        var invokable = element.TryGetCurrentPattern(InvokePattern.Pattern, out _);
                        buttons.Add(new DialogButtonInfo(id, current.Name, current.IsEnabled && invokable));
                        buttonRefs.Add(id, new Button(element, element.GetRuntimeId()));
                    }
                    for (var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
                         child is not null && queue.Count < 500;
                         child = TreeWalker.ControlViewWalker.GetNextSibling(child))
                        queue.Enqueue(child);
                }
                var dialogId = Guid.NewGuid().ToString("N");
                found.Add(dialogId, new Dialog(processId, started, handle, root.GetRuntimeId(), buttonRefs));
                result.Add(new DialogInfo(dialogId, root.Current.Name, string.Join(Environment.NewLine, texts.Distinct()), buttons, queue.Count > 0));
            }
            catch (ElementNotAvailableException) { /* The dialog closed during discovery. */ }
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (dialogs)
        {
            // Listing again invalidates previous IDs for this process.
            foreach (var id in dialogs.Where(pair => pair.Value.ProcessId == processId).Select(pair => pair.Key).ToArray())
                dialogs.Remove(id);
            foreach (var pair in found) dialogs.Add(pair.Key, pair.Value);
        }
        return result;
    }

    public bool Respond(int processId, string dialogId, string buttonId, CancellationToken cancellationToken)
    {
        Dialog dialog;
        Button button;
        lock (dialogs)
        {
            if (!dialogs.TryGetValue(dialogId, out dialog!) || dialog.ProcessId != processId ||
                !dialog.Buttons.TryGetValue(buttonId, out button!))
                throw new InvalidOperationException("Unknown dialog or button ID for this session. Call dialog_list again.");
            // One attempt per snapshot prevents accidental retries after an uncertain invocation.
            dialogs.Remove(dialogId);
        }
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.GetProcessById(processId);
        var root = AutomationElement.FromHandle(dialog.Handle);
        if (process.StartTime.ToUniversalTime().Ticks != dialog.ProcessStarted ||
            !root.GetRuntimeId().SequenceEqual(dialog.RuntimeId) || !IsModal(root, dialog.Handle, processId) ||
            !button.Element.GetRuntimeId().SequenceEqual(button.RuntimeId) || !button.Element.Current.IsEnabled)
            throw new InvalidOperationException("The dialog or button changed. Call dialog_list again.");

        // Recheck ancestry: a cached element must still belong to this exact dialog.
        var ancestor = button.Element;
        while (ancestor is not null && !ancestor.GetRuntimeId().SequenceEqual(dialog.RuntimeId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ancestor = TreeWalker.ControlViewWalker.GetParent(ancestor);
        }
        if (ancestor is null || !button.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
            throw new InvalidOperationException("The button is no longer invokable in this dialog. Call dialog_list again.");
        cancellationToken.ThrowIfCancellationRequested();
        ((InvokePattern)pattern).Invoke();
        return true;
    }

    private static bool IsModal(AutomationElement root, nint handle, int processId)
    {
        GetWindowThreadProcessId(handle, out var pid);
        if (pid != processId || !IsWindowVisible(handle)) return false;
        if (root.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern) && ((WindowPattern)pattern).Current.IsModal)
            return true;
        var owner = GetWindow(handle, 4 /* GW_OWNER */);
        GetWindowThreadProcessId(owner, out var ownerPid);
        return owner != 0 && ownerPid == processId && !IsWindowEnabled(owner);
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint handle);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
}
