using System.Collections.Concurrent;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class BestPracticeGuideSurfacer
{
    private readonly BestPracticeGuideCatalog catalog;
    private readonly ConcurrentDictionary<string, byte> surfacedGuides = new(StringComparer.OrdinalIgnoreCase);

    public BestPracticeGuideSurfacer(BestPracticeGuideCatalog catalog)
    {
        this.catalog = catalog;
    }

    public void AppendGuideIfFirstUse(RequestContext<CallToolRequestParams> request, CallToolResult result)
    {
        var toolName = request.Params?.Name;
        var guideName = ResolveGuideName(toolName);
        if (guideName is null)
        {
            return;
        }

        var sessionKey = CreateSessionKey(request);
        if (!surfacedGuides.TryAdd($"{sessionKey}:{guideName}", 0))
        {
            return;
        }

        var guide = catalog.Read(guideName, null);
        var content = guide.Value?.Content;
        if (!guide.Success || content is null)
        {
            return;
        }

        result.Content ??= [];
        result.Content.Add(new TextContentBlock
        {
            Text = CreateGuideNotice(toolName!, content)
        });
    }

    private static string? ResolveGuideName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName) ||
            string.Equals(toolName, "netvs_get_best_practices", StringComparison.Ordinal))
        {
            return null;
        }

        if (toolName.StartsWith("ui_", StringComparison.Ordinal) ||
            toolName.StartsWith("web_", StringComparison.Ordinal) ||
            toolName.StartsWith("console_", StringComparison.Ordinal))
        {
            return "automate-visual-studio";
        }

        if (toolName.StartsWith("debug_", StringComparison.Ordinal) ||
            toolName.StartsWith("breakpoint_", StringComparison.Ordinal) ||
            toolName.StartsWith("watch_", StringComparison.Ordinal) ||
            toolName.StartsWith("thread_", StringComparison.Ordinal) ||
            toolName.StartsWith("process_", StringComparison.Ordinal) ||
            toolName.StartsWith("exception_settings_", StringComparison.Ordinal) ||
            toolName.StartsWith("parallel_", StringComparison.Ordinal) ||
            string.Equals(toolName, "immediate_execute", StringComparison.Ordinal) ||
            string.Equals(toolName, "test_debug", StringComparison.Ordinal))
        {
            return "debug-visual-studio";
        }

        if (toolName.StartsWith("build_", StringComparison.Ordinal) ||
            toolName.StartsWith("output_", StringComparison.Ordinal) ||
            toolName.StartsWith("nuget_", StringComparison.Ordinal) ||
            string.Equals(toolName, "clean_solution", StringComparison.Ordinal) ||
            string.Equals(toolName, "rebuild_solution", StringComparison.Ordinal) ||
            string.Equals(toolName, "errors_list", StringComparison.Ordinal) ||
            string.Equals(toolName, "package_restore", StringComparison.Ordinal) ||
            string.Equals(toolName, "project_dependencies", StringComparison.Ordinal) ||
            string.Equals(toolName, "project_add_reference", StringComparison.Ordinal) ||
            string.Equals(toolName, "project_remove_reference", StringComparison.Ordinal))
        {
            return "build-visual-studio";
        }

        if (toolName.StartsWith("code_", StringComparison.Ordinal) ||
            toolName.StartsWith("rename_symbol_", StringComparison.Ordinal) ||
            toolName.StartsWith("code_actions_", StringComparison.Ordinal) ||
            toolName.StartsWith("diagnostics_", StringComparison.Ordinal) ||
            string.Equals(toolName, "symbol_context", StringComparison.Ordinal) ||
            string.Equals(toolName, "document_outline", StringComparison.Ordinal) ||
            string.Equals(toolName, "find_implementations", StringComparison.Ordinal) ||
            string.Equals(toolName, "call_hierarchy_get", StringComparison.Ordinal) ||
            string.Equals(toolName, "editor_find", StringComparison.Ordinal) ||
            string.Equals(toolName, "find_in_files", StringComparison.Ordinal) ||
            string.Equals(toolName, "open_relevant_files", StringComparison.Ordinal))
        {
            return "navigate-visual-studio";
        }

        if (toolName.StartsWith("document_", StringComparison.Ordinal) ||
            toolName.StartsWith("editor_", StringComparison.Ordinal) ||
            toolName.StartsWith("selection_", StringComparison.Ordinal) ||
            toolName.StartsWith("edit_", StringComparison.Ordinal) ||
            string.Equals(toolName, "document_cleanup", StringComparison.Ordinal) ||
            string.Equals(toolName, "format_and_organize", StringComparison.Ordinal) ||
            string.Equals(toolName, "prepare_safe_edit", StringComparison.Ordinal) ||
            string.Equals(toolName, "apply_safe_edit_and_build", StringComparison.Ordinal))
        {
            return "edit-visual-studio";
        }

        if (toolName.StartsWith("vs_", StringComparison.Ordinal) ||
            toolName.StartsWith("solution_", StringComparison.Ordinal) ||
            toolName.StartsWith("project_", StringComparison.Ordinal) ||
            toolName.StartsWith("startup_project_", StringComparison.Ordinal) ||
            toolName.StartsWith("test_", StringComparison.Ordinal) ||
            toolName.StartsWith("window_", StringComparison.Ordinal) ||
            toolName.StartsWith("toolwindow_", StringComparison.Ordinal) ||
            toolName.StartsWith("task_list_", StringComparison.Ordinal) ||
            string.Equals(toolName, "git_context", StringComparison.Ordinal) ||
            string.Equals(toolName, "execute_command", StringComparison.Ordinal) ||
            string.Equals(toolName, "get_status", StringComparison.Ordinal))
        {
            return "manage-visual-studio";
        }

        return null;
    }

    private static string CreateSessionKey(RequestContext<CallToolRequestParams> request)
    {
        if (!string.IsNullOrWhiteSpace(request.Server.SessionId))
        {
            return request.Server.SessionId;
        }

        var clientInfo = request.Server.ClientInfo;
        if (!string.IsNullOrWhiteSpace(clientInfo?.Name))
        {
            return $"{clientInfo.Name}:{clientInfo.Version}";
        }

        return "stateless";
    }

    private static string CreateGuideNotice(string toolName, BestPracticeGuideContent content) =>
        $"NetVsMcp best-practices guide surfaced for `{toolName}`. Read this entrypoint before continuing with this tool family; use the listed reference files only when they match the operation.\n\n" +
        $"Guide: {content.ResourceUri}\n\n" +
        content.Text;
}
