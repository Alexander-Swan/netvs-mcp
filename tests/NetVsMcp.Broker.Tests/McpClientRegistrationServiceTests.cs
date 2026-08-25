using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class McpClientRegistrationServiceTests
{
    private readonly BrokerOptions _options = BrokerOptions.LocalDefault;

    [Fact]
    public void IsDetected_FalseWhenNeitherFileNorDirectoryExists()
    {
        var service = new McpClientRegistrationService();
        var client = ClientFor(CreateTempFilePath());

        Assert.False(service.IsDetected(client));
    }

    [Fact]
    public void IsDetected_TrueWhenParentDirectoryExists()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        Assert.True(service.IsDetected(client: ClientFor(path)));
    }

    [Fact]
    public void IsRegistered_FalseWhenFileDoesNotExist()
    {
        var service = new McpClientRegistrationService();
        var client = ClientFor(CreateTempFilePath());

        Assert.False(service.IsRegistered(client, _options));
    }

    [Fact]
    public void Register_CreatesFileWithNetvsEntries()
    {
        var service = new McpClientRegistrationService();
        var client = ClientFor(CreateTempFilePath());

        service.Register(client, _options);

        Assert.True(service.IsRegistered(client, _options));
        var json = File.ReadAllText(client.ConfigPath);
        Assert.Contains(_options.McpEndpoint, json);
        Assert.Contains(_options.McpWebAutomationEndpoint, json);
    }

    [Fact]
    public void Register_UsesConfiguredPortFromOptions()
    {
        var service = new McpClientRegistrationService();
        var client = ClientFor(CreateTempFilePath());
        var options = BrokerOptions.LocalDefault.ApplyPersistedSettings(new BrokerSettings(Port: 5099));

        service.Register(client, options);

        var json = File.ReadAllText(client.ConfigPath);
        Assert.Contains("http://127.0.0.1:5099/mcp", json);
        Assert.Contains("http://127.0.0.1:5099/mcp-wu", json);
        Assert.DoesNotContain($"http://127.0.0.1:{BrokerOptions.DefaultPort}/mcp", json);
    }

    [Fact]
    public void Register_PreservesUnrelatedExistingContent()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
        {
          "mcpServers": {
            "other-tool": { "type": "http", "url": "http://127.0.0.1:9999/mcp" }
          },
          "someUnrelatedSetting": true
        }
        """);
        var client = ClientFor(path);

        service.Register(client, _options);

        var json = File.ReadAllText(path);
        Assert.Contains("other-tool", json);
        Assert.Contains("someUnrelatedSetting", json);
        Assert.Contains("netvs", json);
        Assert.True(service.IsRegistered(client, _options));
    }

    [Fact]
    public void Register_BacksUpExistingFile()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        var client = ClientFor(path);

        service.Register(client, _options);

        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Register_SkipsBackupWhenDisabled()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        var client = ClientFor(path);

        service.Register(client, _options, backupExisting: false);

        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void CopilotCli_Register_SetsToolsWildcard()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        var client = new McpClientDefinition("copilot-cli-test", "Copilot CLI Test", path, "mcpServers", AllToolsFieldName: "tools");

        service.Register(client, _options);

        var json = File.ReadAllText(path);
        Assert.Contains("\"tools\"", json);
        Assert.Contains("\"*\"", json);
    }

    [Fact]
    public void IsRegistered_TrueWhenMatchingUrlExistsUnderADifferentName()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
        {
          "mcpServers": {
            "netvs-mcp": { "type": "http", "url": "{{_options.McpEndpoint}}" }
          }
        }
        """);
        var client = ClientFor(path);

        Assert.True(service.IsRegistered(client, _options));
    }

    [Fact]
    public void Register_UpdatesExistingDifferentlyNamedEntryInPlace_InsteadOfAddingADuplicate()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
        {
          "mcpServers": {
            "netvs-mcp": { "type": "http", "url": "{{_options.McpEndpoint}}" }
          }
        }
        """);
        var client = ClientFor(path);

        service.Register(client, _options);

        var json = File.ReadAllText(path);
        Assert.Contains("netvs-mcp", json);
        // The pre-existing "netvs-mcp" entry already matched the target URL, so it gets updated in
        // place instead of a new "netvs" key being added alongside it.
        Assert.DoesNotContain("\"netvs\"", json);
        // No pre-existing entry matched the web-automation URL, so that one is still added fresh.
        Assert.Contains("netvs-web-automation", json);
    }

    [Fact]
    public void UsesServersPropertyNameFromDefinition()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath();
        var client = new McpClientDefinition("vscode-like", "VS Code-like", path, "servers");

        service.Register(client, _options);

        var json = File.ReadAllText(path);
        Assert.Contains("\"servers\"", json);
        Assert.DoesNotContain("\"mcpServers\"", json);
    }

    [Fact]
    public void Toml_Register_CreatesFileWithNetvsEntries()
    {
        var service = new McpClientRegistrationService();
        var client = TomlClientFor(CreateTempFilePath(extension: "toml"));

        service.Register(client, _options);

        Assert.True(service.IsRegistered(client, _options));
        var toml = File.ReadAllText(client.ConfigPath);
        Assert.Contains(_options.McpEndpoint, toml);
        Assert.Contains(_options.McpWebAutomationEndpoint, toml);
    }

    [Fact]
    public void Toml_Register_PreservesUnrelatedTablesAndSecrets()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath(extension: "toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
        model = "gpt-5.5"

        [mcp_servers.sample_server]
        command = "uvx"
        args = ["sample-mcp@latest"]

        [mcp_servers.sample_server.env]
        SAMPLE_SERVER_TOKEN = "super-secret-token"

        [mcp_servers.netvs]
        url = "http://127.0.0.1:9999/mcp"

        [mcp_servers.netvs.tools.debug_start]
        approval_mode = "approve"
        """);
        var client = TomlClientFor(path);

        service.Register(client, _options);

        var toml = File.ReadAllText(path);
        Assert.Contains("sample_server", toml);
        Assert.Contains("super-secret-token", toml);
        Assert.Contains("debug_start", toml);
        Assert.Contains("approve", toml);
        Assert.Contains(_options.McpEndpoint, toml);
        Assert.True(service.IsRegistered(client, _options));
    }

    [Fact]
    public void Toml_Register_BacksUpExistingFile()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath(extension: "toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "model = \"gpt-5.5\"");
        var client = TomlClientFor(path);

        service.Register(client, _options);

        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Toml_IsRegistered_FalseWhenUrlDiffers()
    {
        var service = new McpClientRegistrationService();
        var path = CreateTempFilePath(extension: "toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
        [mcp_servers.netvs]
        url = "http://127.0.0.1:9999/mcp"
        """);
        var client = TomlClientFor(path);

        Assert.False(service.IsRegistered(client, _options));
    }

    private static McpClientDefinition TomlClientFor(string path) =>
        new("test-toml-client", "Test TOML Client", path, "mcp_servers", McpConfigFormat.Toml);

    private static McpClientDefinition ClientFor(string path) =>
        new("test-client", "Test Client", path, "mcpServers");

    private static string CreateTempFilePath(string extension = "json") => Path.Combine(
        Path.GetTempPath(),
        "NetVsMcp.Broker.Tests",
        Guid.NewGuid().ToString("N"),
        $"mcp.{extension}");
}
