using NetVsMcp.Contracts;
using NetVsMcp.Broker.Analytics;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

[McpServerToolType]
internal sealed partial class BrokerToolService
{
    private const string DocumentPathParameterDescription = "Solution-relative or absolute path; prefer forward slashes.";
    private const string OptionalDocumentPathParameterDescription = "Optional solution-relative or absolute path; prefer forward slashes.";
    private const string DocumentPathsParameterDescription = "Solution-relative or absolute paths; prefer forward slashes.";
    private const string LineParameterDescription = "1-based line number.";
    private const string ColumnParameterDescription = "1-based column number.";

    private static readonly BrokerToolDescriptor[] ToolDescriptors = CreateToolDescriptors();
    private static readonly JsonSerializerOptions UsageSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

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

    private static RoutingTarget? CreateTarget(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId) &&
            string.IsNullOrWhiteSpace(solutionName) &&
            string.IsNullOrWhiteSpace(solutionPath) &&
            processId is null &&
            string.IsNullOrWhiteSpace(workspacePath) &&
            string.IsNullOrWhiteSpace(rootPath))
        {
            return null;
        }

        return new RoutingTarget(
            NormalizeOptional(sessionId),
            NormalizeOptional(solutionName),
            NormalizeOptional(solutionPath),
            processId,
            NormalizeOptional(workspacePath),
            NormalizeOptional(rootPath));
    }
    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(left) && Path.IsPathRooted(right))
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
    }
    private static string? GetRoutableWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.IsPathRooted(path.Trim()) ? path.Trim() : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
    private static string? GetInferredWorkspacePath(
        string? path,
        string? sessionId,
        string? solutionName,
        string? solutionPath)
    {
        return HasRoutingFields(sessionId, solutionName, solutionPath)
            ? null
            : GetRoutableWorkspacePath(path);
    }
    private static bool HasRoutingFields(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        return !string.IsNullOrWhiteSpace(sessionId) ||
            !string.IsNullOrWhiteSpace(solutionName) ||
            !string.IsNullOrWhiteSpace(solutionPath) ||
            processId is not null ||
            !string.IsNullOrWhiteSpace(workspacePath) ||
            !string.IsNullOrWhiteSpace(rootPath);
    }
    private static string? ValidateRequiredPath(string? path)
    {
        if (path is null)
        {
            return MissingRequiredParameter("path");
        }

        return string.IsNullOrWhiteSpace(path)
            ? "Path is required."
            : null;
    }
    private static string MissingRequiredParameter(string parameterName) =>
        $"Mandatory parameter '{parameterName}' was not provided by the agent.";
    private static string? ValidatePosition(int line, int column)
    {
        if (line < 1)
        {
            return "Line must be greater than zero.";
        }

        return column < 1
            ? "Column must be greater than zero."
            : null;
    }
    private static string? ValidatePosition(int? line, int? column)
    {
        if (line is null)
        {
            return MissingRequiredParameter("line");
        }

        if (column is null)
        {
            return MissingRequiredParameter("column");
        }

        return ValidatePosition(line.Value, column.Value);
    }
    private static string? ValidateRange(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        if (ValidatePosition(startLine, startColumn) is { } startValidation)
        {
            return startValidation;
        }

        if (ValidatePosition(endLine, endColumn) is { } endValidation)
        {
            return endValidation;
        }

        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            return "End position must be greater than or equal to start position.";
        }

        return null;
    }
    private static string? ValidateCodePosition(
        string? documentPath,
        int line,
        int column)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Document path is required.";
        }

        return ValidatePosition(line, column);
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

    private static ToolResponse<T> FailWithCode<T>(string message, string errorCode) =>
        new(false, default, message, new Dictionary<string, string>
        {
            ["error_code"] = errorCode
        });

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

    private async Task<ToolResponse<T>> DispatchValueAsync<T>(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        string? workspacePath = null,
        string? rootPath = null,
        object? requestPayload = null,
        [CallerMemberName] string toolName = "")
    {
        var stopwatch = Stopwatch.StartNew();
        var target = CreateTarget(
            sessionId,
            solutionName,
            solutionPath,
            workspacePath: GetInferredWorkspacePath(workspacePath, sessionId, solutionName, solutionPath),
            rootPath: rootPath);
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            operation,
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        stopwatch.Stop();
        AuditToolResult(
            toolName,
            target,
            response.Success,
            dispatch.Session?.SessionId,
            response.Message,
            dispatch.FailureReason.ToString(),
            requestPayload: requestPayload ?? target,
            responsePayload: response,
            duration: stopwatch.Elapsed);
        return response;
    }

    private void AuditToolResult(
        string toolName,
        RoutingTarget? target,
        bool success,
        string? selectedSessionId,
        string? message,
        string? failureReason = null,
        BrokerLogLevel? level = null,
        object? requestPayload = null,
        object? responsePayload = null,
        TimeSpan? duration = null)
    {
        RecordUsageAnalytics(
            toolName,
            success,
            requestPayload,
            responsePayload ?? message,
            duration);

        try
        {
            var effectiveLevel = level ?? (success ? BrokerLogLevel.Info : BrokerLogLevel.Error);
            if (effectiveLevel < _runtime.MinimumLogLevel)
            {
                return;
            }

            _runtime.AuditLog.RecordToolCall(new AuditToolCall(
                TimestampUtc: DateTimeOffset.UtcNow,
                ToolName: ToMcpToolName(toolName),
                Success: success,
                SessionId: selectedSessionId ?? target?.SessionId,
                SolutionName: target?.SolutionName,
                SolutionPath: target?.SolutionPath,
                FailureReason: success ? null : NormalizeFailureReason(failureReason),
                Message: TruncateAuditMessage(message),
                Level: effectiveLevel));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp audit logging failed: {ex}");
        }
    }

    private void RecordUsageAnalytics(
        string methodName,
        bool success,
        object? requestPayload,
        object? responsePayload,
        TimeSpan? duration)
    {
        try
        {
            if (!_runtime.UsageAnalyticsEnabled)
            {
                return;
            }

            var toolName = ToMcpToolName(methodName);
            var descriptor = ToolDescriptors.FirstOrDefault(tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
            var requestChars = EstimateSerializedChars(requestPayload);
            var responseChars = EstimateSerializedChars(responsePayload);
            var estimatedRequestTokens = EstimateTokens(requestChars);
            var estimatedResponseTokens = EstimateTokens(responseChars);
            _runtime.UsageAnalytics.Record(new ToolUsageRecord(
                TimestampUtc: DateTimeOffset.UtcNow,
                AppVersion: _runtime.Version,
                ToolName: toolName,
                Category: descriptor?.Category ?? BrokerToolCategory.Broker,
                Endpoint: descriptor?.McpEndpointPath ?? McpEndpointRouting.ResolveEndpointPath(toolName),
                Success: success,
                RequestChars: requestChars,
                ResponseChars: responseChars,
                EstimatedRequestTokens: estimatedRequestTokens,
                EstimatedResponseTokens: estimatedResponseTokens,
                EstimatedTotalTokens: estimatedRequestTokens + estimatedResponseTokens,
                DurationMs: duration is null ? 0 : Math.Max(0, (long)Math.Ceiling(duration.Value.TotalMilliseconds))));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp usage analytics recording failed: {ex}");
        }
    }

    private static long EstimateSerializedChars(object? payload)
    {
        if (payload is null)
        {
            return 0;
        }

        try
        {
            return JsonSerializer.Serialize(payload, UsageSerializerOptions).Length;
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException)
        {
            return payload.ToString()?.Length ?? 0;
        }
    }

    private static long EstimateTokens(long characters) =>
        characters <= 0 ? 0 : (long)Math.Ceiling(characters / 4.0);

    private ToolResponse<T> AuditLocalFailure<T>(
        string toolName,
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        string message)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        AuditToolResult(
            toolName,
            target,
            success: false,
            selectedSessionId: null,
            message,
            failureReason: "InvalidRequest",
            level: BrokerLogLevel.Warning);
        return ToolResponse<T>.Fail(message);
    }

    private static string? NormalizeFailureReason(string? failureReason)
    {
        return string.IsNullOrWhiteSpace(failureReason) || failureReason == "None"
            ? null
            : failureReason;
    }

    private static bool IsDocumentNotFoundFailure(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && (message.Contains("Document was not found on disk", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Document was not found in the live Visual Studio workspace", StringComparison.OrdinalIgnoreCase)
                || message.Contains("E_INVALIDARG", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TruncateAuditMessage(string? message)
    {
        const int maxLength = 500;
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message[..maxLength];
    }

    private static string ToMcpToolName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return "unknown";
        }

        var chars = new List<char>(methodName.Length + 8);
        for (var index = 0; index < methodName.Length; index++)
        {
            var character = methodName[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string([.. chars]);
    }

    private static BrokerToolDescriptor[] CreateToolDescriptors()
    {
        return typeof(BrokerToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(method => new
            {
                Method = method,
                Tool = method.GetCustomAttribute<McpServerToolAttribute>(),
                Description = method.GetCustomAttribute<DescriptionAttribute>(),
                BrokerMetadata = method.GetCustomAttribute<BrokerToolMetadataAttribute>()
            })
            .Where(entry => entry.Tool is not null)
            .Select(entry =>
            {
                var tool = entry.Tool!;
                var name = string.IsNullOrWhiteSpace(tool.Name)
                    ? ToMcpToolName(entry.Method.Name)
                    : tool.Name!;
                var description = entry.Description?.Description;
                var brokerMetadata = entry.BrokerMetadata ??
                    throw new InvalidOperationException($"Tool '{name}' is missing [BrokerToolMetadata].");

                return new BrokerToolDescriptor(
                    name,
                    string.IsNullOrWhiteSpace(description) ? name : description,
                    RequiresVisualStudioSession: brokerMetadata.RequiresVisualStudioSession,
                    Title: string.IsNullOrWhiteSpace(tool.Title) ? name : tool.Title,
                    ReadOnly: tool.ReadOnly,
                    Destructive: tool.Destructive,
                    Idempotent: tool.Idempotent,
                    OpenWorld: tool.OpenWorld,
                    UseStructuredContent: tool.UseStructuredContent,
                    IconSource: tool.IconSource,
                    Category: brokerMetadata.Category,
                    McpEndpointPath: McpEndpointRouting.ResolveEndpointPath(name));
            })
            .OrderBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> CreateRouteFailureMetadata(RouteResult route)
    {
        var metadata = new Dictionary<string, string>
        {
            ["error_code"] = ToolErrorCodes.SessionRoutingFailed,
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
            ["error_code"] = MapDispatchFailureToErrorCode(dispatch.FailureReason),
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

    private static string MapDispatchFailureToErrorCode(VsSessionDispatchFailureReason reason)
    {
        return reason switch
        {
            VsSessionDispatchFailureReason.StaleSession or
            VsSessionDispatchFailureReason.MissingConnection => ToolErrorCodes.SessionNotConnected,
            VsSessionDispatchFailureReason.RpcFailure => ToolErrorCodes.RpcFailure,
            VsSessionDispatchFailureReason.UnsupportedByVsix => ToolErrorCodes.UnsupportedByVsix,
            VsSessionDispatchFailureReason.OperationTimedOut => ToolErrorCodes.OperationTimedOut,
            _ => ToolErrorCodes.SessionRoutingFailed
        };
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
        metadata["candidateProcessIds"] = string.Join(",", candidates.Select(candidate => candidate.ProcessId));
        metadata["candidateSolutionNames"] = string.Join(",", candidates.Select(candidate => candidate.SolutionName ?? string.Empty));
        metadata["candidateSolutionPaths"] = string.Join("|", candidates.Select(candidate => candidate.SolutionPath ?? string.Empty));
        metadata["candidateActiveWindow"] = string.Join(",", candidates.Select(candidate => candidate.IsActiveWindow.ToString()));
        metadata["candidateLastSeenUtc"] = string.Join(",", candidates.Select(candidate => candidate.LastSeenUtc.ToString("O")));
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
