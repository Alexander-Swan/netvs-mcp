using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface IAuditLogService
{
    string LogsDirectory { get; }
    string CurrentLogFilePath { get; }
    void RecordToolCall(AuditToolCall entry);

    /// <summary>
    /// Deletes "audit-yyyyMMdd.jsonl" files outside the requested calendar-day retention window,
    /// where one day means "today only", mirroring <see cref="SessionManifestService.CleanupStale"/>'s cleanup-pass shape.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    int PruneOldLogs(int retentionDays, DateTimeOffset? now = null);
}
