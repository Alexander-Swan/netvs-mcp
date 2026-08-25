using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;

namespace NetVsMcp.Broker.Services;

/// <summary>The on-disk format a known client's MCP config file uses.</summary>
public enum McpConfigFormat
{
    Json,
    Toml
}

/// <summary>Describes a known local MCP client and where it keeps its server registration file.</summary>
/// <param name="AllToolsFieldName">
/// When set, the JSON array field name (e.g. "tools") this client uses on a server entry to filter
/// which tools are exposed; registration sets it to ["*"] so every tool is available without extra
/// per-tool setup. Only set this when a client has a real, documented field for it (verified against
/// the client itself) - most clients keep tool approval outside the server entry entirely, and Codex
/// CLI's is per-tool-name only with no wildcard, so it's deliberately left unset for those.
/// </param>
public sealed record McpClientDefinition(
    string Id,
    string DisplayName,
    string ConfigPath,
    string ServersPropertyName,
    McpConfigFormat Format = McpConfigFormat.Json,
    string? AllToolsFieldName = null);

/// <summary>
/// Detects known local MCP clients and writes the "netvs"/"netvs-web-automation" server entries into
/// their config files. <see cref="Register"/> backs up an existing file first when asked to.
/// </summary>
public sealed class McpClientRegistrationService
{
    public static IReadOnlyList<McpClientDefinition> KnownClients { get; } = BuildKnownClients();

    private static IReadOnlyList<McpClientDefinition> BuildKnownClients()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return
        [
            new McpClientDefinition("claude-desktop", "Claude Desktop",
                Path.Combine(appData, "Claude", "claude_desktop_config.json"), "mcpServers"),
            new McpClientDefinition("claude-code", "Claude Code (CLI)",
                Path.Combine(userProfile, ".claude.json"), "mcpServers"),
            new McpClientDefinition("codex", "Codex CLI",
                Path.Combine(userProfile, ".codex", "config.toml"), "mcp_servers", McpConfigFormat.Toml),
            new McpClientDefinition("copilot-cli", "GitHub Copilot CLI",
                Path.Combine(userProfile, ".copilot", "mcp-config.json"), "mcpServers",
                AllToolsFieldName: "tools"),
            new McpClientDefinition("cursor", "Cursor",
                Path.Combine(userProfile, ".cursor", "mcp.json"), "mcpServers"),
            new McpClientDefinition("windsurf", "Windsurf",
                Path.Combine(userProfile, ".codeium", "windsurf", "mcp_config.json"), "mcpServers"),
            new McpClientDefinition("vscode", "VS Code",
                Path.Combine(appData, "Code", "User", "mcp.json"), "servers"),
        ];
    }

    /// <summary>True when the client's user-data folder already exists, i.e. the client has been run before.</summary>
    public bool IsDetected(McpClientDefinition client)
    {
        if (File.Exists(client.ConfigPath))
            return true;

        var directory = Path.GetDirectoryName(client.ConfigPath);
        return directory is not null && Directory.Exists(directory);
    }

    public bool IsRegistered(McpClientDefinition client, BrokerOptions options)
    {
        if (!File.Exists(client.ConfigPath))
            return false;

        try
        {
            var text = File.ReadAllText(client.ConfigPath);
            return client.Format == McpConfigFormat.Toml
                ? IsRegisteredToml(text, client, options)
                : IsRegisteredJson(text, client, options);
        }
        catch (Exception ex) when (ex is JsonException or TomlException)
        {
            return false;
        }
    }

    private static bool IsRegisteredJson(string text, McpClientDefinition client, BrokerOptions options)
    {
        var root = JsonNode.Parse(text) as JsonObject;
        var servers = root?[client.ServersPropertyName] as JsonObject;
        return servers is not null && FindJsonKeyByUrl(servers, options.McpEndpoint) is not null;
    }

    private static bool IsRegisteredToml(string text, McpClientDefinition client, BrokerOptions options)
    {
        var root = TomlSerializer.Deserialize<TomlTable>(text) ?? [];
        return root.TryGetValue(client.ServersPropertyName, out var serversObj)
            && serversObj is TomlTable servers
            && FindTomlKeyByUrl(servers, options.McpEndpoint) is not null;
    }

    /// <summary>
    /// Finds the name of an existing server entry whose "url" already matches, regardless of what
    /// it's called - a user may have hand-registered NetVsMcp under a different name (e.g. "netvs-mcp"
    /// instead of "netvs"). Matching by URL rather than by a fixed key name means re-registering the
    /// same broker updates that entry in place instead of adding a confusing duplicate.
    /// </summary>
    private static string? FindJsonKeyByUrl(JsonObject servers, string url)
    {
        foreach (var (key, value) in servers)
        {
            if (value is JsonObject entry && string.Equals((string?)entry["url"], url, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    private static string? FindTomlKeyByUrl(TomlTable servers, string url)
    {
        foreach (var (key, value) in servers)
        {
            if (value is TomlTable entry
                && entry.TryGetValue("url", out var urlObj)
                && urlObj is string existingUrl
                && string.Equals(existingUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static string BuildJsonPreview(McpClientDefinition client, BrokerOptions options)
    {
        var root = LoadExistingJsonOrEmpty(client);
        ApplyJsonRegistration(root, client, options);
        return SerializeJson(root);
    }

    private static string BuildTomlPreview(McpClientDefinition client, BrokerOptions options)
    {
        var root = LoadExistingTomlOrEmpty(client);
        ApplyTomlRegistration(root, client, options);
        return TomlSerializer.Serialize(root);
    }

    /// <summary>
    /// Writes the "netvs"/"netvs-web-automation" entries into the client's config file, merging with
    /// whatever else is already there. Backs up an existing file to "&lt;path&gt;.bak" first unless
    /// <paramref name="backupExisting"/> is false.
    /// </summary>
    public void Register(McpClientDefinition client, BrokerOptions options, bool backupExisting = true)
    {
        var content = client.Format == McpConfigFormat.Toml
            ? BuildTomlPreview(client, options)
            : BuildJsonPreview(client, options);

        var directory = Path.GetDirectoryName(client.ConfigPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (backupExisting && File.Exists(client.ConfigPath))
            File.Copy(client.ConfigPath, client.ConfigPath + ".bak", overwrite: true);

        File.WriteAllText(client.ConfigPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static JsonObject LoadExistingJsonOrEmpty(McpClientDefinition client)
    {
        if (!File.Exists(client.ConfigPath))
            return new JsonObject();

        var text = File.ReadAllText(client.ConfigPath);
        if (string.IsNullOrWhiteSpace(text))
            return new JsonObject();

        if (JsonNode.Parse(text) is JsonObject existing)
            return existing;

        throw new InvalidOperationException(
            $"'{client.ConfigPath}' does not contain a JSON object at its root and can't be merged automatically.");
    }

    private static void ApplyJsonRegistration(JsonObject root, McpClientDefinition client, BrokerOptions options)
    {
        if (root[client.ServersPropertyName] is not JsonObject servers)
        {
            servers = new JsonObject();
            root[client.ServersPropertyName] = servers;
        }

        var netvsKey = FindJsonKeyByUrl(servers, options.McpEndpoint) ?? "netvs";
        var automationKey = FindJsonKeyByUrl(servers, options.McpWebAutomationEndpoint) ?? "netvs-web-automation";

        servers[netvsKey] = BuildJsonServerEntry(client, options.McpEndpoint);
        servers[automationKey] = BuildJsonServerEntry(client, options.McpWebAutomationEndpoint);
    }

    private static JsonObject BuildJsonServerEntry(McpClientDefinition client, string url)
    {
        var entry = new JsonObject
        {
            ["type"] = "http",
            ["url"] = url
        };

        if (client.AllToolsFieldName is { Length: > 0 } toolsField)
        {
            entry[toolsField] = new JsonArray("*");
        }

        return entry;
    }

    private static string SerializeJson(JsonObject root) =>
        root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static TomlTable LoadExistingTomlOrEmpty(McpClientDefinition client)
    {
        if (!File.Exists(client.ConfigPath))
            return [];

        var text = File.ReadAllText(client.ConfigPath);
        return string.IsNullOrWhiteSpace(text) ? [] : TomlSerializer.Deserialize<TomlTable>(text) ?? [];
    }

    private static void ApplyTomlRegistration(TomlTable root, McpClientDefinition client, BrokerOptions options)
    {
        if (!root.TryGetValue(client.ServersPropertyName, out var serversObj) || serversObj is not TomlTable servers)
        {
            servers = [];
            root[client.ServersPropertyName] = servers;
        }

        var netvsKey = FindTomlKeyByUrl(servers, options.McpEndpoint) ?? "netvs";
        var automationKey = FindTomlKeyByUrl(servers, options.McpWebAutomationEndpoint) ?? "netvs-web-automation";

        // Preserve any existing sub-tables under the matched entry (e.g. Codex's per-tool
        // "[mcp_servers.<name>.tools.*]" approval settings) - only (re)set the "url" key.
        if (!servers.TryGetValue(netvsKey, out var netvsObj) || netvsObj is not TomlTable netvs)
        {
            netvs = [];
            servers[netvsKey] = netvs;
        }
        netvs["url"] = options.McpEndpoint;

        if (!servers.TryGetValue(automationKey, out var automationObj) || automationObj is not TomlTable netvsWebAutomation)
        {
            netvsWebAutomation = [];
            servers[automationKey] = netvsWebAutomation;
        }
        netvsWebAutomation["url"] = options.McpWebAutomationEndpoint;
    }
}
