using System;
using System.Security.Principal;

namespace NetVsMcp.Vsix;

internal static class BrokerPipeName
{
    public static string CurrentUserDefault()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userKey = identity.User?.Value ?? Environment.UserName;
        return "netvs-mcp-" + Sanitize(userKey);
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
