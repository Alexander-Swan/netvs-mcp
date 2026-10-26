using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal interface IHotReloadInitializationNotification : IDisposable
{
    Task<bool> Completed { get; }
    void Arm();
}

internal sealed class HotReloadInitializationSignal
{
    private readonly object gate = new();
    private TaskCompletionSource<bool> completed = NewSignal();
    private bool armed;
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<bool> Completed { get { lock (gate) return completed.Task; } }
    internal void Arm() { lock (gate) { completed = NewSignal(); armed = true; } }
    internal void Notify(string result)
    {
        lock (gate)
            if (armed) completed.TrySetResult(result is "Applied" or "NoChanges");
    }
    internal void Fail(Exception exception) { lock (gate) if (armed) completed.TrySetException(exception); }
}

// VS owns this public notification contract. Older SDK packages do not expose it;
// qualify the known public shape rather than shipping or privately binding VS binaries.
internal sealed class HotReloadInitializationNotification : IHotReloadInitializationNotification
{
    private readonly HotReloadInitializationSignal signal = new();
    private IDisposable? subscription;
    public Task<bool> Completed => signal.Completed;
    public void Arm() => signal.Arm();

    internal static async Task<IHotReloadInitializationNotification?> TrySubscribeAsync(CancellationToken cancellationToken, Action<string>? unavailable = null)
    {
        HotReloadInitializationNotification? result = null;
        IHotReloadInitializationNotification? Unavailable(string reason) { unavailable?.Invoke(reason); return null; }
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var components = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (components is null) return Unavailable("components");
            var assembly = Assembly.Load("Microsoft.VisualStudio.Debugger.Contracts");
            var contract = assembly.GetType("Microsoft.VisualStudio.Debugger.Contracts.HotReload.IHotReloadApplyChangesNotificationService");
            var notification = assembly.GetType("Microsoft.VisualStudio.Debugger.Contracts.HotReload.HotReloadApplyChangesResult");
            var resultProperty = notification?.GetProperty("Result");
            if (contract?.IsPublic != true || notification?.IsPublic != true || resultProperty?.PropertyType.IsEnum != true ||
                !Enum.GetNames(resultProperty.PropertyType).Contains("NoChanges") || !Enum.GetNames(resultProperty.PropertyType).Contains("Applied"))
                return Unavailable("contractShape");
            var subscribe = contract.GetMethod("SubscribeAsync", new[] { typeof(IObserver<>).MakeGenericType(notification), typeof(CancellationToken) });
            if (subscribe?.ReturnType != typeof(ValueTask<IDisposable>)) return Unavailable("subscriptionShape");
            var service = typeof(IComponentModel).GetMethod("GetService")!.MakeGenericMethod(contract).Invoke(components, null);
            if (service is null) return Unavailable("service");
            result = new HotReloadInitializationNotification();
            var observer = Activator.CreateInstance(typeof(Observer<>).MakeGenericType(notification), result.signal)!;
            result.subscription = await (ValueTask<IDisposable>)subscribe.Invoke(service, new[] { observer, (object)cancellationToken })!;
            return result;
        }
        catch (OperationCanceledException) { result?.Dispose(); throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.IO.IOException or
            System.Runtime.InteropServices.COMException or TargetInvocationException or TypeLoadException or MissingMemberException)
        {
            result?.Dispose();
            System.Diagnostics.Trace.WriteLine("NetVsMcp: Hot Reload apply notification unavailable: " + ex.Message);
            return Unavailable(ex.GetType().Name);
        }
    }

    public void Dispose()
    {
        try { subscription?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("NetVsMcp: Hot Reload notification unsubscribe failed: {0}", ex.Message); }
        subscription = null;
        // MEF owns the service; only our observer subscription is disposed.
    }

    private sealed class Observer<T>(HotReloadInitializationSignal signal) : IObserver<T>
    {
        public void OnNext(T value) => signal.Notify(value?.GetType().GetProperty("Result")?.GetValue(value)?.ToString() ?? "Unavailable");
        public void OnError(Exception error) => signal.Fail(error);
        public void OnCompleted() => signal.Notify("Unavailable");
    }
}
