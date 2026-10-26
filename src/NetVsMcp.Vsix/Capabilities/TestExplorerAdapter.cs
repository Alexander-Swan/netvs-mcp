using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NetVsMcp.Vsix;

internal sealed class ExplorerCase(string id, string name, string fullyQualifiedName, string project, string source, string framework, object native)
{
    internal string Id { get; } = id;
    internal string Name { get; } = name;
    internal string FullyQualifiedName { get; } = fullyQualifiedName;
    internal string Project { get; } = project;
    internal string Source { get; } = source;
    internal string Framework { get; } = framework;
    internal object Native { get; } = native;
}
internal sealed class ExplorerExecution(bool completed, bool canceled, IReadOnlyList<TestResultInfo> results, string? message = null)
{
    internal bool Completed { get; } = completed;
    internal bool Canceled { get; } = canceled;
    internal IReadOnlyList<TestResultInfo> Results { get; } = results;
    internal string? Message { get; } = message;
}
internal interface ITestExplorerAdapter
{
    string Phase { get; }
    Task<IReadOnlyList<ExplorerCase>> DiscoverAsync(string project, CancellationToken ct);
    Task<ExplorerExecution> ExecuteAsync(IReadOnlyList<ExplorerCase> tests, bool debug, CancellationToken ct);
}

// Test Window's control/query contracts are internal and version-dependent. Keep the
// optional adapter isolated: no private VS assemblies are packaged or statically bound.
internal sealed class TestExplorerAdapter : ITestExplorerAdapter
{
    private volatile string phase = "initializing";
    public string Phase => phase;
    private readonly Assembly contracts;
    private readonly Type serviceType;
    private readonly Type stateType;
    private readonly Type queryType;
    private readonly object service;
    private readonly object factory;
    private readonly object operations;
    private readonly Type factoryType;
    private readonly object containers;
    private readonly Type containerProviderType;
    private readonly HashSet<string> refreshed = new(StringComparer.OrdinalIgnoreCase);

    private TestExplorerAdapter(Assembly contracts, IComponentModel components)
    {
        this.contracts = contracts;
        serviceType = Required("IVsTestServiceInternal");
        stateType = Required("IOperationState");
        queryType = Required("TestQuery");
        service = GetService(components, serviceType);
        operations = GetService(components, stateType);
        var host = Assembly.Load("Microsoft.VisualStudio.TestWindow");
        factoryType = host.GetType("Microsoft.VisualStudio.TestWindow.Controller.IRequestFactory", true)!;
        factory = GetService(components, factoryType);
        containerProviderType = host.GetType("Microsoft.VisualStudio.TestWindow.ITestContainerProvider", true)!;
        containers = GetService(components, containerProviderType);
        if (contracts.GetName().Version?.Major != 18 || factoryType.GetProperty("CanRunTests") is null ||
            ServiceMethod("GetTestResultDetailsAsync") is null || ServiceMethod("RunTestsAsync") is null ||
            ServiceMethod("DebugTestsAsync") is null || queryType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).All(c => c.GetParameters().Length != 3))
            throw new NotSupportedException("This Test Explorer API shape is not qualified. Use the CLI backend.");
    }

    internal static async Task<ITestExplorerAdapter> CreateAsync(AsyncPackage package, CancellationToken ct)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        var components = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel
            ?? throw new NotSupportedException("Visual Studio MEF services are unavailable.");
        try
        {
            var shell = await package.GetServiceAsync(typeof(SVsShell)) as IVsShell;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            var packageId = new Guid("bfc24bf4-b994-4757-bcdc-1d5d2768bf29");
            if (shell is null || shell.LoadPackage(ref packageId, out _) < 0)
                throw new NotSupportedException("Test Explorer could not be initialized.");
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Microsoft.VisualStudio.TestWindow.Interfaces");
            if (assembly is null)
            {
                var ide = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)!;
                assembly = Assembly.LoadFrom(Path.Combine(ide, "CommonExtensions", "Microsoft", "TestWindow", "Microsoft.VisualStudio.TestWindow.Interfaces.dll"));
            }
            return new TestExplorerAdapter(assembly, components);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException or MissingMemberException or InvalidOperationException or TargetInvocationException)
        { throw new NotSupportedException("Test Explorer services are unavailable or incompatible; no CLI fallback was started.", ex); }
    }

    private Type Required(string name) => contracts.GetType("Microsoft.VisualStudio.TestWindow.Extensibility." + name, true)!;
    private static object GetService(IComponentModel components, Type contract) =>
        typeof(IComponentModel).GetMethod("GetService")!.MakeGenericMethod(contract).Invoke(components, null)
        ?? throw new NotSupportedException("Test Explorer has no export for " + contract.Name + ". Build the test project and initialize Test Explorer.");
    private MethodInfo? ServiceMethod(string name) => serviceType.GetInterfaces().Concat(new[] { serviceType })
        .SelectMany(t => t.GetMethods()).FirstOrDefault(m => m.Name == name);
    private static object? Property(object value, string name) =>
        (value.GetType().GetProperty(name) ?? value.GetType().GetInterfaces().Select(t => t.GetProperty(name)).FirstOrDefault(p => p is not null))?.GetValue(value);

    private object Query(string property, IEnumerable<string> values) => Activator.CreateInstance(queryType,
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        new[] { Enum.Parse(Required("TestPropertyType"), property), values, Enum.Parse(Required("FilterMatchKind"), "ExactMatch") }, null)!;

    private object ExecutionQuery(IReadOnlyList<ExplorerCase> tests)
    {
        var query = Query("FullyQualifiedName", tests.Select(t => t.FullyQualifiedName).Distinct());
        var add = queryType.GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var filter in new[] { new { Name = "Source", Values = tests.Select(t => t.Source).Distinct().ToArray() },
            new { Name = "ProjectName", Values = tests.Select(t => t.Project).Distinct().ToArray() },
            new { Name = "DisplayName", Values = tests.Select(t => t.Name).Distinct().ToArray() } })
            add.Invoke(query, new object[] { Enum.Parse(Required("TestPropertyType"), filter.Name), filter.Values, Enum.Parse(Required("FilterMatchKind"), "ExactMatch") });
        return query;
    }

    private async Task<object?> CallAsync(string method, params object?[] args)
    {
        Task task;
        try { task = (Task)ServiceMethod(method)!.Invoke(service, args)!; }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    public async Task<IReadOnlyList<ExplorerCase>> DiscoverAsync(string project, CancellationToken ct)
    {
        phase = "discovery";
        // Querying forces Test Window's lazy core service to initialize. Waiting for
        // IsServiceAvailable first would prevent that very initialization.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        IEnumerable found;
        try { found = (IEnumerable)(await CallAsync("GetTestsAsync", Query("ProjectName", new[] { project }), deadline.Token).ConfigureAwait(false))!; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new NotSupportedException("Test Explorer discovery was not ready within 25 seconds. Build the test project and retry this read."); }
        var sources = found.Cast<object>().Select(n => Property(n, "Source") as string).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var source in sources.Where(s => !refreshed.Contains(s!)))
        {
            ct.ThrowIfCancellationRequested();
            if (factoryType.GetProperty("CanRunTests")!.GetValue(factory) is not true)
                throw new InvalidOperationException("IDE discovery is busy; retry the discovery read after it settles. No run was submitted.");
            var lookup = (Task)containerProviderType.GetMethod("TryGetTestContainerAsync")!.Invoke(containers, new object?[] { source, null, false })!;
            await lookup.ConfigureAwait(false);
            var container = lookup.GetType().GetProperty("Result")!.GetValue(lookup);
            if (container is null) throw new NotSupportedException("The IDE has no built test container for " + source);
            var selected = Array.CreateInstance(Required("ITestContainer"), 1);
            selected.SetValue(container, 0);
            // false prevents the IDE's optional run-after-build behavior: this is
            // real adapter discovery for the selected source, never test execution.
            await ((Task)factoryType.GetMethod("ProcessChangedContainersAsync")!.Invoke(factory, new object[] { selected, false })!).ConfigureAwait(false);
            refreshed.Add(source!);
        }
        if (sources.Length > 0)
            found = (IEnumerable)(await CallAsync("GetTestsAsync", Query("ProjectName", new[] { project }), ct).ConfigureAwait(false))!;
        var result = found.Cast<object>().Where(n => Property(n, "IsTest") is true)
            .Select(n => new ExplorerCase(Property(n, "TestCaseRecordId")!.ToString()!, (string)Property(n, "DisplayName")!,
                (string)Property(n, "FullyQualifiedName")!, (string)Property(n, "ProjectName")!,
                (string)Property(n, "Source")!, (string)Property(n, "TargetFramework")!, n)).Take(10001).ToArray();
        if (result.Length > 10000) throw new NotSupportedException("Test Explorer project exceeds the 10000-case adapter limit.");
        return result.Where(t => string.Equals(t.Project, project, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public async Task<ExplorerExecution> ExecuteAsync(IReadOnlyList<ExplorerCase> tests, bool debug, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var ownedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (factoryType.GetProperty("CanRunTests")!.GetValue(factory) is not true)
            throw new InvalidOperationException("Test Explorer is busy; the existing operation was not interrupted.");
        var starts = 0;
        var startedOperations = new List<object?>();
        var canceled = 0;
        var notification = stateType.GetEvent("StateChanged")!;
        Action<object> observed = args =>
        {
            var name = Property(args, "State")?.ToString() ?? "";
            if (name.Contains("TestExecutionStarted"))
            {
                var operation = Property(args, "Operation");
                var conflict = false;
                lock (startedOperations)
                    if (!startedOperations.Any(old => ReferenceEquals(old, operation))) { startedOperations.Add(operation); conflict = ++starts > 1; }
                if (conflict) ownedCancellation.Cancel(); // Never cancel a different IDE request.
            }
            if (name.Contains("CancelAndFinished") || name.Contains("Canceled")) Interlocked.Exchange(ref canceled, 1);
        };
        var parameters = notification.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        var handler = Expression.Lambda(notification.EventHandlerType,
            Expression.Invoke(Expression.Constant(observed), Expression.Convert(parameters[1], typeof(object))), parameters).Compile();
        notification.AddEventHandler(operations, handler);
        try
        {
            phase = "IDE execution";
            bool completed;
            try { completed = (bool)(await CallAsync(debug ? "DebugTestsAsync" : "RunTestsAsync", ExecutionQuery(tests), ownedCancellation.Token).ConfigureAwait(false))!; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            { return new(false, canceled == 1, Array.Empty<TestResultInfo>()); }
            if (!completed || starts > 1) return new(false, ct.IsCancellationRequested && canceled == 1 && starts <= 1, Array.Empty<TestResultInfo>(),
                $"IDE completion={completed}; observed execution requests={starts}; cancellation evidence={canceled == 1}.");
            phase = "result capture";
            using var readDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            // Outcome grouping can replace view-node IDs. Resolve the same exact
            // source/name selection again before querying details, never a list index.
            var current = ((IEnumerable)(await CallAsync("GetTestsAsync", ExecutionQuery(tests), readDeadline.Token).ConfigureAwait(false))!).Cast<object>().ToArray();
            var results = new List<TestResultInfo>();
            foreach (var test in tests)
            {
                var match = current.Where(n => Property(n, "IsTest") is true &&
                    (string?)Property(n, "DisplayName") == test.Name && (string?)Property(n, "Source") == test.Source &&
                    (string?)Property(n, "FullyQualifiedName") == test.FullyQualifiedName).ToArray();
                if (match.Length != 1) return new(false, false, Array.Empty<TestResultInfo>(), "IDE test identity changed before result capture.");
                var rows = (IEnumerable)(await CallAsync("GetTestResultDetailsAsync", match[0], null, readDeadline.Token).ConfigureAwait(false))!;
                foreach (var row in rows.Cast<object>())
                    results.Add(new((string)Property(row, "DisplayName")!, Property(row, "Outcome")!.ToString()!,
                        TimeSpan.FromMilliseconds(Convert.ToInt64(Property(row, "DurationInMs"))).ToString("c"),
                        Property(row, "ErrorMessage") as string, test.Id, stackTrace: Property(row, "ErrorStackTrace") as string));
            }
            phase = "completed";
            return starts > 1 ? new(false, false, Array.Empty<TestResultInfo>(), "Another IDE run overlapped result capture.") : new(true, false, results);
        }
        finally { notification.RemoveEventHandler(operations, handler); }
    }
}
