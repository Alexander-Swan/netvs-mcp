using System;
using System.Security.Principal;

namespace NetVsMcp.Vsix;

internal static class BrokerPipeName
{
    public static string CurrentUserDefault()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userKey = identity.User?.Value ?? Environment.UserName;

        // Debug builds use a different pipe name than Release builds so a developer can run a
        // locally-built Debug broker side by side with a Release broker installed via the MSI.
#if DEBUG
        return "netvs-mcp-dev-" + Sanitize(userKey);
#else
        return "netvs-mcp-" + Sanitize(userKey);
#endif
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', ' ' })
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }
}
