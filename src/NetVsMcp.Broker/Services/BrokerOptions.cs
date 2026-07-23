using System.IO;

namespace NetVsMcp.Broker.Services;

public sealed record BrokerOptions(
    string McpEndpoint,
    string PipeName,
    string? LogsDirectory = null)
{
    public static BrokerOptions LocalDefault { get; } = new(
        "http://127.0.0.1:5050",
        $@"\\.\pipe\netvs-mcp-{Environment.UserName}",
        DefaultLogsDirectory);

    public static string DefaultLogsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetVsMcp",
        "Logs");

    public string EffectiveLogsDirectory =>
        string.IsNullOrWhiteSpace(LogsDirectory) ? DefaultLogsDirectory : LogsDirectory;

    public string McpRegistrationJson =>
        $$"""
        {
          "mcpServers": {
            "netvs": {
              "type": "http",
              "url": "{{McpEndpoint}}"
            }
          }
        }
        """;
}
