using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerOptionsTests
{
    [Fact]
    public void FromEnvironmentAndArgs_UsesDefaultEndpointWhenNoOverridesAreProvided()
    {
        var options = BrokerOptions.FromEnvironmentAndArgs([]);

        Assert.Equal($"http://127.0.0.1:{BrokerOptions.DefaultPort}/mcp", options.McpEndpoint);
        Assert.Equal($@"\\.\pipe\{BrokerOptions.DefaultPipeName}", options.PipeName);
    }

    [Fact]
    public void FromEnvironmentAndArgs_AcceptsMcpPortArgument()
    {
        var options = BrokerOptions.FromEnvironmentAndArgs(["--mcp-port", "5051"]);

        Assert.Equal("http://127.0.0.1:5051/mcp", options.McpEndpoint);
    }

    [Fact]
    public void FromEnvironmentAndArgs_AcceptsEqualsStyleEndpointArgument()
    {
        var options = BrokerOptions.FromEnvironmentAndArgs(["--mcp-endpoint=http://localhost:6060/mcp"]);

        Assert.Equal("http://localhost:6060/mcp", options.McpEndpoint);
    }

    [Fact]
    public void FromEnvironmentAndArgs_NormalizesBarePipeName()
    {
        var options = BrokerOptions.FromEnvironmentAndArgs(["--pipe-name", "netvs-mcp-debug"]);

        Assert.Equal(@"\\.\pipe\netvs-mcp-debug", options.PipeName);
    }

    [Fact]
    public void FromEnvironmentAndArgs_ArgsOverrideEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("NETVS_MCP_PORT");

        try
        {
            Environment.SetEnvironmentVariable("NETVS_MCP_PORT", "5052");

            var options = BrokerOptions.FromEnvironmentAndArgs(["--mcp-port", "5053"]);

            Assert.Equal("http://127.0.0.1:5053/mcp", options.McpEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETVS_MCP_PORT", previous);
        }
    }

    [Fact]
    public void FromEnvironmentAndArgs_RejectsInvalidPort()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BrokerOptions.FromEnvironmentAndArgs(["--mcp-port", "70000"]));

        Assert.Contains("Invalid MCP port", exception.Message);
    }
}
