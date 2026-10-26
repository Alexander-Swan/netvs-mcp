using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal static class ToolDiscoveryProfile
{
    public static readonly string[] Names = ["all", "manage", "read", "edit", "build", "debug", "test"];

    public static bool Includes(string? profile, BrokerToolDescriptor tool)
    {
        profile = string.IsNullOrWhiteSpace(profile) ? "all" : profile.ToLowerInvariant();
        if (!Names.Contains(profile)) throw new ArgumentException("profile must be " + string.Join(", ", Names));
        if (profile == "all") return true;
        // Routing, recovery and guide entrypoints stay discoverable in every profile.
        if (tool.Name.StartsWith("vs_", StringComparison.Ordinal) ||
            tool.Name.StartsWith("session_", StringComparison.Ordinal) || tool.Name is "get_help" or "netvs_doctor" or "netvs_get_best_practices" or "dialog_list" or "dialog_respond") return true;
        if (profile != "manage" && tool.Name is "solution_info" or "project_list" or "document_read" or "document_open" or "errors_list") return true;
        if (profile == "debug" && new[] { "debug_", "breakpoint_", "watch_", "thread_", "module_", "parallel_", "exception_", "process_" }
            .Any(prefix => tool.Name.StartsWith(prefix, StringComparison.Ordinal))) return true;
        if (profile == "test" && tool.Name.StartsWith("test_", StringComparison.Ordinal)) return true;
        return profile switch
        {
            "manage" => !tool.RequiresVisualStudioSession,
            "read" => tool.Category == BrokerToolCategory.Read,
            "edit" => tool.Category is BrokerToolCategory.EditPreview or BrokerToolCategory.EditDirect,
            "build" => tool.Category == BrokerToolCategory.Build,
            "debug" => tool.Category == BrokerToolCategory.Debug,
            "test" => tool.Category == BrokerToolCategory.Test,
            _ => false
        };
    }
}
