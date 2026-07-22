using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

[McpServerToolType]
public sealed class BrokerToolService
{
    private static readonly BrokerToolDescriptor[] ToolDescriptors =
    [
        new("vs_list_sessions", "Lists Visual Studio instances registered with the local broker.", false),
        new("vs_get_status", "Returns local broker endpoint, uptime, and registered session status.", false),
        new("vs_get_capabilities", "Lists broker tools and Visual Studio capability categories.", false),
        new("vs_get_session", "Resolves a Visual Studio session and returns its current broker status.", false),
        new("vs_select_session", "Resolves a Visual Studio session using broker routing rules without persisting selection.", false),
        new("vs_ping", "Returns lightweight broker health and optional routed Visual Studio session status.", false),
        new("document_active", "Returns the active document for a routed Visual Studio session.", true),
        new("code_document_symbols", "Lists document symbols through a routed Visual Studio session.", true),
        new("build_solution", "Starts a solution build in a routed Visual Studio session.", true),
        new("build_status", "Returns build status from a routed Visual Studio session.", true),
        new("errors_list", "Lists errors and warnings from a routed Visual Studio session.", true),
        new("output_read", "Reads an output pane from a routed Visual Studio session.", true)
    ];

    private static readonly VsCapability[] VisualStudioCapabilities =
    [
        VsCapability.Editor,
        VsCapability.Navigation,
        VsCapability.Build,
        VsCapability.Debugger,
        VsCapability.Diagnostics,
        VsCapability.Tests,
        VsCapability.ProjectSystem
    ];

    private readonly BrokerRuntime _runtime;

    public BrokerToolService(BrokerRuntime runtime)
    {
        _runtime = runtime;
    }

    [McpServerTool(Name = "vs_list_sessions")]
    [Description("Lists Visual Studio instances registered with the local NetVsMcp broker.")]
    public ToolResponse<IReadOnlyCollection<VsSessionInfo>> VsListSessions()
    {
        return ToolResponse<IReadOnlyCollection<VsSessionInfo>>.Ok(_runtime.Sessions.ListSessions());
    }

    [McpServerTool(Name = "vs_get_status")]
    [Description("Returns local broker endpoint, uptime, registration pipe, and registered Visual Studio session status.")]
    public ToolResponse<BrokerStatus> VsGetStatus()
    {
        return ToolResponse<BrokerStatus>.Ok(_runtime.GetStatus());
    }

    [McpServerTool(Name = "vs_get_capabilities")]
    [Description("Lists NetVsMcp broker tools and Visual Studio capability categories.")]
    public ToolResponse<BrokerCapabilities> VsGetCapabilities()
    {
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            ToolDescriptors,
            VisualStudioCapabilities);

        return ToolResponse<BrokerCapabilities>.Ok(capabilities);
    }

    [McpServerTool(Name = "vs_get_session")]
    [Description("Resolves a Visual Studio session using sessionId, solutionName, or solutionPath and returns its current broker status.")]
    public ToolResponse<VsSessionStatus> VsGetSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null)
    {
        var route = _runtime.Sessions.Resolve(CreateTarget(sessionId, solutionName, solutionPath));
        if (!route.Success || route.Session is null)
        {
            return new ToolResponse<VsSessionStatus>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
        }

        var status = GetSessionStatus(route.Session);
        return status is null
            ? ToolResponse<VsSessionStatus>.Fail($"Visual Studio session '{route.Session.SessionId}' is no longer registered.")
            : ToolResponse<VsSessionStatus>.Ok(status);
    }

    [McpServerTool(Name = "vs_select_session")]
    [Description("Resolves and returns a Visual Studio session using broker routing rules without storing global selection state.")]
    public ToolResponse<VsSessionInfo> VsSelectSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null)
    {
        var route = _runtime.Sessions.Resolve(CreateTarget(sessionId, solutionName, solutionPath));
        if (!route.Success || route.Session is null)
        {
            return new ToolResponse<VsSessionInfo>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
        }

        return ToolResponse<VsSessionInfo>.Ok(route.Session);
    }

    [McpServerTool(Name = "vs_ping")]
    [Description("Returns lightweight broker health and optional routed Visual Studio session status.")]
    public ToolResponse<BrokerPing> VsPing(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null)
    {
        if (!HasRoutingFields(sessionId, solutionName, solutionPath))
        {
            return ToolResponse<BrokerPing>.Ok(CreatePing(null));
        }

        var route = _runtime.Sessions.Resolve(CreateTarget(sessionId, solutionName, solutionPath));
        if (!route.Success || route.Session is null)
        {
            return new ToolResponse<BrokerPing>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
        }

        var status = GetSessionStatus(route.Session);
        return ToolResponse<BrokerPing>.Ok(CreatePing(status));
    }

    [McpServerTool(Name = "document_active")]
    [Description("Returns the active document for a routed Visual Studio session.")]
    public async Task<ToolResponse<string?>> DocumentActive(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.GetActiveDocumentAsync(ct),
            cancellationToken);

        return ToToolResponse(dispatch);
    }

    [McpServerTool(Name = "code_document_symbols")]
    [Description("Lists document symbols for a document in a routed Visual Studio session.")]
    public async Task<ToolResponse<IReadOnlyCollection<string>>> CodeDocumentSymbols(
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<IReadOnlyCollection<string>>.Fail("Document path is required.");
        }

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ListDocumentSymbolsAsync(documentPath, ct),
            cancellationToken);

        return ToToolResponse(dispatch);
    }

    [McpServerTool(Name = "build_solution")]
    [Description("Starts a solution build in a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildSolutionResult>> BuildSolution(
        bool waitForBuildToFinish = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new BuildSolutionRequest
        {
            WaitForBuildToFinish = waitForBuildToFinish
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.BuildSolutionAsync(request, ct),
            cancellationToken);

        return ToValueToolResponse(dispatch);
    }

    [McpServerTool(Name = "build_status")]
    [Description("Returns build status from a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildStatusInfo>> BuildStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.BuildStatusAsync(ct),
            cancellationToken);

        return ToValueToolResponse(dispatch);
    }

    [McpServerTool(Name = "errors_list")]
    [Description("Lists errors and warnings from a routed Visual Studio session.")]
    public async Task<ToolResponse<ErrorListResult>> ErrorsList(
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return ToolResponse<ErrorListResult>.Fail("Max items must be greater than zero.");
        }

        var request = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ErrorsListAsync(request, ct),
            cancellationToken);

        return ToValueToolResponse(dispatch);
    }

    [McpServerTool(Name = "output_read")]
    [Description("Reads an output pane from a routed Visual Studio session.")]
    public async Task<ToolResponse<OutputReadResult>> OutputRead(
        string? paneName = null,
        int maxChars = 20000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxChars < 1)
        {
            return ToolResponse<OutputReadResult>.Fail("Max chars must be greater than zero.");
        }

        var request = new OutputReadRequest
        {
            PaneName = NormalizeOptional(paneName),
            MaxChars = maxChars
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.OutputReadAsync(request, ct),
            cancellationToken);

        return ToValueToolResponse(dispatch);
    }

    private static RoutingTarget? CreateTarget(
        string? sessionId,
        string? solutionName,
        string? solutionPath)
    {
        if (string.IsNullOrWhiteSpace(sessionId) &&
            string.IsNullOrWhiteSpace(solutionName) &&
            string.IsNullOrWhiteSpace(solutionPath))
        {
            return null;
        }

        return new RoutingTarget(
            NormalizeOptional(sessionId),
            NormalizeOptional(solutionName),
            NormalizeOptional(solutionPath));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private VsSessionStatus? GetSessionStatus(VsSessionInfo session)
    {
        return _runtime.Sessions.ListSessionStatuses()
            .SingleOrDefault(status => string.Equals(
                status.Session.SessionId,
                session.SessionId,
                StringComparison.OrdinalIgnoreCase));
    }

    private BrokerPing CreatePing(VsSessionStatus? targetSession)
    {
        return new BrokerPing(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            IsRunning: _runtime.IsHttpEndpointRunning,
            McpEndpoint: _runtime.Options.McpEndpoint,
            PipeName: _runtime.Options.PipeName,
            Uptime: DateTimeOffset.UtcNow - _runtime.StartedUtc,
            RegisteredSessionCount: _runtime.Sessions.ListSessions().Count,
            TargetSession: targetSession);
    }

    private static bool HasRoutingFields(
        string? sessionId,
        string? solutionName,
        string? solutionPath)
    {
        return !string.IsNullOrWhiteSpace(sessionId) ||
            !string.IsNullOrWhiteSpace(solutionName) ||
            !string.IsNullOrWhiteSpace(solutionPath);
    }

    private static ToolResponse<T> ToToolResponse<T>(
        VsSessionDispatchResult<ToolResponse<T>> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        return dispatch.Value ?? ToolResponse<T>.Fail("Visual Studio session returned no response.");
    }

    private static ToolResponse<T> ToValueToolResponse<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        if (dispatch.Value is null)
        {
            return ToolResponse<T>.Fail("Visual Studio session returned no response.");
        }

        return ToolResponse<T>.Ok(dispatch.Value);
    }

    private static IReadOnlyDictionary<string, string> CreateRouteFailureMetadata(RouteResult route)
    {
        var metadata = new Dictionary<string, string>
        {
            ["failureReason"] = route.FailureReason.ToString()
        };

        AddCandidateMetadata(metadata, route.Candidates);
        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateFailureMetadata<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        var metadata = new Dictionary<string, string>
        {
            ["failureReason"] = dispatch.FailureReason.ToString()
        };

        if (dispatch.Session is not null)
        {
            metadata["sessionId"] = dispatch.Session.SessionId;
        }

        if (dispatch.Candidates.Count > 0)
        {
            AddCandidateMetadata(metadata, dispatch.Candidates);
        }

        return metadata;
    }

    private static void AddCandidateMetadata(
        IDictionary<string, string> metadata,
        IReadOnlyCollection<VsSessionInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        metadata["candidateCount"] = candidates.Count.ToString();
        metadata["candidateSessionIds"] = string.Join(",", candidates.Select(candidate => candidate.SessionId));
    }
}

public sealed record BrokerPing(
    DateTimeOffset ServerTimeUtc,
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    TimeSpan Uptime,
    int RegisteredSessionCount,
    VsSessionStatus? TargetSession);
