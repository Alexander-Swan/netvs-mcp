using System;
using System.Reflection;

namespace NetVsMcp.Vsix;

/// <summary>
/// Static information shown on the About dialog. Keep <see cref="Version"/> in sync with
/// source.extension.vsixmanifest by updating the &lt;Version&gt; property in the csproj.
/// </summary>
internal static class AboutInfo
{
    public const string DisplayName = "NetVsMcp Visual Studio Bridge";

    public const string Description =
        "Registers this Visual Studio instance with the local NetVsMcp broker and lets MCP " +
        "clients (like Claude) read, edit, build, and debug your solution through the broker.";

    /// <summary>Placeholder until the broker ships a dedicated download/landing page.</summary>
    public const string BrokerDownloadUrl = "https://github.com/Alexander-Swan/netvs-mcp/releases/latest";

    public const string ProjectUrl = "https://github.com/Alexander-Swan/netvs-mcp";

    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
