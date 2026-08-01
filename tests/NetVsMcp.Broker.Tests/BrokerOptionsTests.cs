using NetVsMcp.Broker.Services;
using System.Text.Json;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerOptionsTests
{
    [Fact]
    public void FromArgs_UsesDefaultEndpointWhenNoOverridesAreProvided()
    {
        var options = BrokerOptions.FromArgs([]);

        Assert.Equal($"http://127.0.0.1:{BrokerOptions.DefaultPort}/mcp", options.McpEndpoint);
        Assert.Equal($@"\\.\pipe\{BrokerOptions.DefaultPipeName}", options.PipeName);
    }

    [Fact]
    public void FromArgs_AcceptsMcpPortArgument()
    {
        var options = BrokerOptions.FromArgs(["--mcp-port", "5051"]);

        Assert.Equal("http://127.0.0.1:5051/mcp", options.McpEndpoint);
    }

    [Fact]
    public void FromArgs_AcceptsEqualsStyleEndpointArgument()
    {
        var options = BrokerOptions.FromArgs(["--mcp-endpoint=http://localhost:6060/mcp"]);

        Assert.Equal("http://localhost:6060/mcp", options.McpEndpoint);
    }

    [Fact]
    public void FromArgs_NormalizesBarePipeName()
    {
        var options = BrokerOptions.FromArgs(["--pipe-name", "netvs-mcp-debug"]);

        Assert.Equal(@"\\.\pipe\netvs-mcp-debug", options.PipeName);
    }

    [Fact]
    public void FromArgs_RejectsInvalidPort()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BrokerOptions.FromArgs(["--mcp-port", "70000"]));

        Assert.Contains("Invalid MCP port", exception.Message);
    }

    [Fact]
    public void ApplyPersistedSettings_OverridesOnlyProvidedFields()
    {
        var options = BrokerOptions.LocalDefault.ApplyPersistedSettings(
            new BrokerSettings(Port: 5099));

        Assert.Equal("http://127.0.0.1:5099/mcp", options.McpEndpoint);
        Assert.Equal(BrokerOptions.LocalDefault.PipeName, options.PipeName);
    }

    [Fact]
    public void McpRegistrationJson_IncludesDefaultAndWebAutomationServers()
    {
        using var document = JsonDocument.Parse(BrokerOptions.LocalDefault.McpRegistrationJson);
        var servers = document.RootElement.GetProperty("mcpServers");

        Assert.Equal("http", servers.GetProperty("netvs").GetProperty("type").GetString());
        Assert.Equal(BrokerOptions.LocalDefault.McpEndpoint, servers.GetProperty("netvs").GetProperty("url").GetString());
        Assert.Equal("http", servers.GetProperty("netvs-web-automation").GetProperty("type").GetString());
        Assert.Equal(BrokerOptions.LocalDefault.McpWebAutomationEndpoint, servers.GetProperty("netvs-web-automation").GetProperty("url").GetString());
    }
}
