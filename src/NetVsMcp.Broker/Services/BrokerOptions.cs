namespace NetVsMcp.Broker.Services;

public sealed record BrokerOptions(
    string McpEndpoint,
    string PipeName)
{
    public static BrokerOptions LocalDefault { get; } = new(
        "http://127.0.0.1:5050",
        $@"\\.\pipe\netvs-mcp-{Environment.UserName}");

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
