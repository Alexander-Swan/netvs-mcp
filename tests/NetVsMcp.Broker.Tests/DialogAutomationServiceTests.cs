using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class DialogAutomationServiceTests
{
    [Fact]
    public async Task ModalDialog_ListsTextAndButtons_RejectsStaleIds_InvokesButton()
    {
        var ready = new TaskCompletionSource<Window>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var owner = new Window { Title = "Dialog test owner", Width = 240, Height = 120, ShowInTaskbar = false };
                var dialog = new Window { Title = "Modal dialog test", Width = 240, Height = 140, ShowInTaskbar = false };
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = "Save test changes?" });
                var button = new Button { Content = "Cancel" };
                button.Click += (_, _) => dialog.Close();
                panel.Children.Add(button);
                panel.Children.Add(new Button { Content = "Unavailable", IsEnabled = false });
                dialog.Content = panel;
                dialog.Loaded += (_, _) => ready.TrySetResult(dialog);
                owner.Show();
                dialog.Owner = owner;
                try { dialog.ShowDialog(); }
                finally { owner.Close(); }
                closed.TrySetResult();
            }
            catch (Exception ex) { ready.TrySetException(ex); closed.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Window? window = null;
        try
        {
            window = await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var service = new DialogAutomationService();
            await Task.Run(() =>
            {
                var listed = service.List(Environment.ProcessId, default);
                Assert.DoesNotContain(listed, d => d.Title == "Dialog test owner");
                var first = Assert.Single(listed, d => d.Title == "Modal dialog test");
                Assert.Contains("Save test changes?", first.Message);
                Assert.False(first.Truncated);
                Assert.False(Assert.Single(first.Buttons, b => b.Name == "Unavailable").IsEnabled);
                var firstButton = Assert.Single(first.Buttons, b => b.Name == "Cancel");
                Assert.True(firstButton.IsEnabled);
                Assert.Throws<InvalidOperationException>(() => service.Respond(Environment.ProcessId + 1, first.DialogId, firstButton.ButtonId, default));
                var current = Assert.Single(service.List(Environment.ProcessId, default), d => d.Title == "Modal dialog test");
                Assert.Throws<InvalidOperationException>(() => service.Respond(Environment.ProcessId, first.DialogId, firstButton.ButtonId, default));
                var currentButton = Assert.Single(current.Buttons, b => b.Name == "Cancel");
                Assert.True(service.Respond(Environment.ProcessId, current.DialogId, currentButton.ButtonId, default));
                Assert.Throws<InvalidOperationException>(() => service.Respond(Environment.ProcessId, current.DialogId, currentButton.ButtonId, default));
            }).WaitAsync(TimeSpan.FromSeconds(15));
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (window is not null && !closed.Task.IsCompleted)
                await window.Dispatcher.InvokeAsync(window.Close, DispatcherPriority.Send);
        }
    }
}
