using System.Text.Json;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class AuditLogServiceTests
{
    [Fact]
    public void RecordToolCall_WritesJsonLine()
    {
        var logsDirectory = Path.Combine(
            Path.GetTempPath(),
            "NetVsMcp.Broker.Tests",
            Guid.NewGuid().ToString("N"));
        var audit = new AuditLogService(logsDirectory);

        audit.RecordToolCall(new AuditToolCall(
            TimestampUtc: DateTimeOffset.Parse("2026-07-22T12:00:00Z"),
            ToolName: "document_read",
            Success: false,
            SessionId: "vs-1",
            SolutionName: "NetVsMcp",
            SolutionPath: @"C:\Code\NetVsMcp\NetVsMcp.slnx",
            FailureReason: "MissingConnection",
            Message: "No connection."));

        var file = Directory.GetFiles(logsDirectory, "audit-*.jsonl").Single();
        var line = File.ReadLines(file).Single();
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        Assert.Equal("document_read", root.GetProperty("toolName").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("vs-1", root.GetProperty("sessionId").GetString());
        Assert.Equal("MissingConnection", root.GetProperty("failureReason").GetString());
    }

    [Fact]
    public void PruneOldLogs_DeletesFilesOlderThanRetentionWindow()
    {
        var logsDirectory = Path.Combine(
            Path.GetTempPath(),
            "NetVsMcp.Broker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDirectory);
        var audit = new AuditLogService(logsDirectory);

        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");
        var oldFile = Path.Combine(logsDirectory, "audit-20260601.jsonl");
        var recentFile = Path.Combine(logsDirectory, "audit-20260822.jsonl");
        var malformedFile = Path.Combine(logsDirectory, "audit-not-a-date.jsonl");
        File.WriteAllText(oldFile, "{}\n");
        File.WriteAllText(recentFile, "{}\n");
        File.WriteAllText(malformedFile, "{}\n");

        var removed = audit.PruneOldLogs(retentionDays: 30, now: now);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(recentFile));
        Assert.True(File.Exists(malformedFile));
    }

    [Fact]
    public void PruneOldLogs_ReturnsZero_WhenLogsDirectoryDoesNotExist()
    {
        var logsDirectory = Path.Combine(
            Path.GetTempPath(),
            "NetVsMcp.Broker.Tests",
            Guid.NewGuid().ToString("N"));
        var audit = new AuditLogService(logsDirectory);

        var removed = audit.PruneOldLogs(retentionDays: 30);

        Assert.Equal(0, removed);
    }

    [Fact]
    public void PruneOldLogs_ReturnsZero_WhenRetentionDaysIsNotPositive()
    {
        var logsDirectory = Path.Combine(
            Path.GetTempPath(),
            "NetVsMcp.Broker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDirectory);
        var audit = new AuditLogService(logsDirectory);
        File.WriteAllText(Path.Combine(logsDirectory, "audit-20200101.jsonl"), "{}\n");

        var removed = audit.PruneOldLogs(retentionDays: 0);

        Assert.Equal(0, removed);
    }
}
