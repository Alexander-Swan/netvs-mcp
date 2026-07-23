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
}
