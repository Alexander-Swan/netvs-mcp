using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetVsMcp.Broker.Services;

public interface IAuditLogService
{
    string LogsDirectory { get; }
    string CurrentLogFilePath { get; }
    void RecordToolCall(AuditToolCall entry);
}

public sealed record AuditToolCall(
    DateTimeOffset TimestampUtc,
    string ToolName,
    bool Success,
    string? SessionId = null,
    string? SolutionName = null,
    string? SolutionPath = null,
    string? FailureReason = null,
    string? Message = null);

public sealed class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly object _gate = new();

    public AuditLogService(string logsDirectory)
    {
        LogsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? BrokerOptions.DefaultLogsDirectory
            : logsDirectory;
    }

    public string LogsDirectory { get; }

    public string CurrentLogFilePath =>
        Path.Combine(LogsDirectory, $"audit-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");

    public void RecordToolCall(AuditToolCall entry)
    {
        Directory.CreateDirectory(LogsDirectory);

        var line = JsonSerializer.Serialize(entry, SerializerOptions);
        lock (_gate)
        {
            File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
        }
    }
}
