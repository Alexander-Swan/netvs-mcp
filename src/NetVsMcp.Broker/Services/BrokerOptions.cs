using System.IO;
using System.Security.Principal;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed record BrokerOptions(
    string McpEndpoint,
    string PipeName,
    string? LogsDirectory = null,
    string? TokenFilePath = null,
    string? SessionsDirectory = null,
    string? CapabilityProfileFilePath = null,
    BrokerCapabilityProfile CapabilityProfile = BrokerCapabilityProfile.Admin)
{
    public static BrokerOptions LocalDefault { get; } = new(
        $"http://127.0.0.1:{DefaultPort}/mcp",
        $@"\\.\pipe\{DefaultPipeName}",
        DefaultLogsDirectory);

    public static BrokerOptions FromEnvironmentAndArgs(string[]? args)
    {
        var options = LocalDefault;

        options = ApplyOption(options, "mcp-endpoint", Environment.GetEnvironmentVariable("NETVS_MCP_ENDPOINT"));
        options = ApplyOption(options, "mcp-port", Environment.GetEnvironmentVariable("NETVS_MCP_PORT"));
        options = ApplyOption(options, "pipe-name", Environment.GetEnvironmentVariable("NETVS_MCP_PIPE_NAME"));
        options = ApplyOption(options, "logs-dir", Environment.GetEnvironmentVariable("NETVS_MCP_LOGS_DIR"));
        options = ApplyOption(options, "token-file", Environment.GetEnvironmentVariable("NETVS_MCP_TOKEN_FILE"));
        options = ApplyOption(options, "sessions-dir", Environment.GetEnvironmentVariable("NETVS_MCP_SESSIONS_DIR"));
        options = ApplyOption(options, "profile-file", Environment.GetEnvironmentVariable("NETVS_MCP_PROFILE_FILE"));

        foreach (var (name, value) in ParseArgs(args ?? []))
        {
            options = ApplyOption(options, name, value);
        }

        return options;
    }

    // Debug builds use a different port/pipe name than Release builds so a developer can run a
    // locally-built Debug broker side by side with a Release broker installed via the MSI.
#if DEBUG
    public static int DefaultPort => 5051;

    public static string DefaultPipeName => "netvs-mcp-dev-" + SanitizeUserKey(CurrentUserKey);
#else
    public static int DefaultPort => 5050;

    public static string DefaultPipeName => "netvs-mcp-" + SanitizeUserKey(CurrentUserKey);
#endif

    private static string CurrentUserKey
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? Environment.UserName;
        }
    }

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

    public static string DefaultCapabilityProfileFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetVsMcp",
        "capability-profile.json");

    public string EffectiveCapabilityProfileFilePath =>
        string.IsNullOrWhiteSpace(CapabilityProfileFilePath) ? DefaultCapabilityProfileFilePath : CapabilityProfileFilePath;

    private static BrokerOptions ApplyOption(BrokerOptions options, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return options;
        }

        return NormalizeOptionName(name) switch
        {
            "mcp-endpoint" => options with { McpEndpoint = value },
            "mcp-port" => options with { McpEndpoint = ReplaceEndpointPort(options.McpEndpoint, value) },
            "pipe-name" => options with { PipeName = NormalizePipeName(value) },
            "logs-dir" => options with { LogsDirectory = value },
            "token-file" => options with { TokenFilePath = value },
            "sessions-dir" => options with { SessionsDirectory = value },
            "profile-file" => options with { CapabilityProfileFilePath = value },
            _ => options
        };
    }

    private static IEnumerable<(string Name, string Value)> ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var option = arg[2..];
            var separator = option.IndexOf('=');
            if (separator >= 0)
            {
                yield return (option[..separator], option[(separator + 1)..]);
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                yield return (option, args[++i]);
            }
        }
    }

    private static string ReplaceEndpointPort(string endpoint, string portText)
    {
        if (!int.TryParse(portText, out var port) || port is < 0 or > 65535)
        {
            throw new InvalidOperationException($"Invalid MCP port '{portText}'.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid MCP endpoint '{endpoint}'.");
        }

        var builder = new UriBuilder(uri)
        {
            Port = port
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string NormalizePipeName(string value)
    {
        return value.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase)
            ? value
            : $@"\\.\pipe\{value}";
    }

    private static string NormalizeOptionName(string name) =>
        name.Trim().Replace('_', '-').ToLowerInvariant();

    private static string SanitizeUserKey(string value)
    {
        foreach (var invalid in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', ' ' })
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }

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
