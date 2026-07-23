using System.IO;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed record BrokerOptions(
    string McpEndpoint,
    string PipeName,
    string? LogsDirectory = null,
    string? TokenFilePath = null,
    string? SessionsDirectory = null,
    BrokerCapabilityProfile CapabilityProfile = BrokerCapabilityProfile.Admin)
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

    public static string DefaultTokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetVsMcp",
        "broker.token");

    public string EffectiveTokenFilePath =>
        string.IsNullOrWhiteSpace(TokenFilePath) ? DefaultTokenFilePath : TokenFilePath;

    public static string DefaultSessionsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetVsMcp",
        "Sessions");

    public string EffectiveSessionsDirectory =>
        string.IsNullOrWhiteSpace(SessionsDirectory) ? DefaultSessionsDirectory : SessionsDirectory;

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
