using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed class BestPracticeGuideCatalog
{
    private const string MimeTypeMarkdown = "text/markdown";
    private static readonly string[] GuideNames =
    [
        "manage-visual-studio",
        "navigate-visual-studio",
        "edit-visual-studio",
        "build-visual-studio",
        "debug-visual-studio",
        "automate-visual-studio"
    ];

    private static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["manage-visual-studio"] = "Session routing, launching Visual Studio, windows, solutions, projects, and tests. " +
            "TRIGGER BEFORE CALLING: vs_list_sessions, vs_get_status, vs_get_session, vs_select_session, vs_ping, vs_launch_instance, " +
            "vs_context_snapshot, execute_command, get_status, window_list, window_activate, toolwindow_show, toolwindow_hide, " +
            "solution_info, solution_open, solution_close, solution_add_project, solution_remove_project, solution_overview, " +
            "project_list, project_info, project_add_file, project_remove_file, project_dependencies, startup_project_get, " +
            "startup_project_set, test_discover, test_run, test_results, test_run_and_get_results, task_list_get, task_list_add, " +
            "task_list_remove, task_list_set_checked, git_context, vs_get_logs.",
        ["navigate-visual-studio"] = "Definitions, references, symbols, diagnostics, code fixes, and workspace search. " +
            "TRIGGER BEFORE CALLING: document_active, code_document_symbols, code_go_to_definition, code_go_to_implementation, " +
            "code_find_references, code_workspace_symbols, symbol_context, document_outline, find_implementations, " +
            "rename_symbol_preview, rename_symbol_apply, call_hierarchy_get, code_actions_list, code_actions_apply, document_read, " +
            "document_open, document_list, document_close, open_relevant_files, errors_list, diagnostics_for_document, " +
            "diagnostics_binding_errors, editor_find, find_in_files.",
        ["edit-visual-studio"] = "Documents, direct editor edits, selections, formatting, and safe-edit previews. " +
            "TRIGGER BEFORE CALLING: selection_get, selection_set, document_write, document_save, editor_insert, editor_replace, " +
            "editor_goto_line, document_cleanup, format_and_organize, edit_preview, prepare_safe_edit, edit_approve, " +
            "apply_safe_edit_and_build, edit_reject, edit_list_pending.",
        ["build-visual-studio"] = "Build, rebuild, clean, NuGet/package operations, output panes, and error lists. " +
            "TRIGGER BEFORE CALLING: build_solution, build_status, build_and_get_errors, build_project, build_cancel, " +
            "clean_solution, rebuild_solution, build_configuration_get, build_configuration_set, output_read, output_list_panes, " +
            "output_write, output_clear, package_restore, project_add_reference, project_remove_reference, nuget_list, " +
            "nuget_search, nuget_install, nuget_update, nuget_uninstall.",
        ["debug-visual-studio"] = "Debugger start/attach/step, breakpoints, locals, watches, threads, modules, and processes. " +
            "TRIGGER BEFORE CALLING: debug_status, debug_hot_reload_apply, debug_get_mode, debug_start, debug_stop, " +
            "debug_continue, debug_break, debug_step, debug_start_without_debugging, debug_restart, debug_attach, " +
            "debug_get_callstack, debug_get_locals, debug_evaluate, debug_eval_many, debug_snapshot, debug_wait_for_break, " +
            "debug_get_threads, debug_set_variable, breakpoint_set, breakpoint_list, breakpoint_group_list, breakpoint_remove, " +
            "breakpoint_enable, breakpoint_group_enable, breakpoint_group_remove, watch_add, watch_remove, watch_list, " +
            "thread_switch, thread_set_frozen, thread_get_callstack, process_list_debugged, process_list_local, process_detach, " +
            "process_terminate, immediate_execute, module_list, exception_settings_get, exception_settings_set, parallel_stacks, " +
            "parallel_watch, test_debug.",
        ["automate-visual-studio"] = "Debuggee UI automation, browser control, screenshots, DOM access, and console I/O. " +
            "TRIGGER BEFORE CALLING: console_read, console_send, console_get_info, ui_capture_window, ui_capture_region, " +
            "ui_snapshot, ui_get_tree, ui_find_elements, ui_get_element, ui_click, ui_double_click, ui_right_click, ui_drag, " +
            "ui_set_value, ui_invoke, ui_send_keys, ui_wait_for_element, ui_wait_idle, web_connect, web_disconnect, web_status, " +
            "web_navigate, web_screenshot, web_dom_get, web_dom_query, web_console, web_js_execute, web_network, " +
            "web_element_click, web_element_set_value."
    };

    private static readonly IReadOnlyDictionary<string, string> DefaultEndpointOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["*"] = McpEndpointRouting.DefaultEndpointPath
    };

    // Every guide's tools live on the default endpoint except automate-visual-studio, which
    // spans both: console_* tools are on the default endpoint, ui_*/web_* tools require the
    // separate opt-in "/mcp-wu" endpoint (see McpEndpointRouting and LocalMcpHttpHost). Keep
    // this in sync with McpEndpointRouting.IsWebAutomationTool if the tool split ever changes.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> EndpointsByGuide =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["automate-visual-studio"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["console_*"] = McpEndpointRouting.DefaultEndpointPath,
                ["ui_*, web_*"] = McpEndpointRouting.WebAutomationEndpointPath
            }
        };

    private readonly string? bundledGuidesRoot;
    private readonly string? repositoryGuidesRoot;
    public BestPracticeGuideCatalog()
    {
        bundledGuidesRoot = ResolveDirectory(Path.Combine("BundledGuides", "skills"), null);
        repositoryGuidesRoot = ResolveDirectory(null, Path.Combine(".agents", "skills"));
    }

    public BestPracticeGuideToolResult List()
    {
        return new BestPracticeGuideToolResult(
            "NetVsMcp best-practices guides are available as MCP resources and through this tool. Each guide below lists the tool-name prefixes it covers ('TRIGGER BEFORE CALLING') — read the matching guide before calling any of those tools, e.g. read 'debug-visual-studio' before debug_start/breakpoint_set/etc. These guides are agent-neutral defaults, not locked policy; user or project instructions can layer additional guidance on top.",
            GuideNames.Select(CreateInfo).ToArray(),
            null);
    }

    public ToolResponse<BestPracticeGuideToolResult> Read(string? guide, string? file)
    {
        if (string.IsNullOrWhiteSpace(guide))
        {
            return ToolResponse<BestPracticeGuideToolResult>.Ok(List());
        }

        var guideName = guide.Trim();
        if (!GuideNames.Contains(guideName, StringComparer.OrdinalIgnoreCase))
        {
            return ToolResponse<BestPracticeGuideToolResult>.Fail($"Unknown NetVsMcp best-practices guide '{guideName}'. Call without arguments to list available guides.");
        }

        var guideFile = string.IsNullOrWhiteSpace(file) ? $"{guideName}.md" : file.Trim();
        if (!TryReadFile(guideName, guideFile, out var content, out var resourceUri, out var mimeType))
        {
            return ToolResponse<BestPracticeGuideToolResult>.Fail($"Guide file '{guideFile}' was not found for '{guideName}'. Call without arguments to list available files.");
        }

        return ToolResponse<BestPracticeGuideToolResult>.Ok(new BestPracticeGuideToolResult(
            $"Read NetVsMcp best-practices guide '{guideName}' file '{guideFile}'.",
            GuideNames.Select(CreateInfo).ToArray(),
            new BestPracticeGuideContent(guideName, guideFile.Replace('\\', '/'), resourceUri, mimeType, content)));
    }

    public TextResourceContents ReadResource(string guideName)
    {
        var file = $"{guideName}.md";
        if (!TryReadFile(guideName, file, out var content, out var resourceUri, out var mimeType))
        {
            throw new InvalidOperationException($"NetVsMcp guide not found: {guideName}");
        }

        return new TextResourceContents
        {
            Uri = resourceUri,
            MimeType = mimeType,
            Text = content
        };
    }

    private BestPracticeGuideInfo CreateInfo(string guideName)
    {
        var files = new List<BestPracticeGuideFileInfo>();
        foreach (var root in EnumerateExistingRoots(bundledGuidesRoot, repositoryGuidesRoot))
        {
            var path = Path.Combine(root, $"{guideName}.md");
            if (File.Exists(path) && !files.Any(file => string.Equals(file.Path, $"{guideName}.md", StringComparison.OrdinalIgnoreCase)))
            {
                files.Add(new BestPracticeGuideFileInfo($"{guideName}.md", CreateResourceUri(guideName), MimeTypeMarkdown));
            }
        }

        if (files.Count == 0)
        {
            files.Add(new BestPracticeGuideFileInfo($"{guideName}.md", CreateResourceUri(guideName), MimeTypeMarkdown));
        }

        var endpoints = (EndpointsByGuide.TryGetValue(guideName, out var guideEndpoints) ? guideEndpoints : DefaultEndpointOnly)
            .Select(entry => new BestPracticeGuideEndpointInfo(entry.Key, entry.Value))
            .ToArray();

        return new BestPracticeGuideInfo(
            guideName,
            Descriptions.TryGetValue(guideName, out var description) ? description : "NetVsMcp Visual Studio best-practices guide.",
            CreateResourceUri(guideName),
            files,
            endpoints);
    }

    private bool TryReadFile(string guideName, string file, out string content, out string resourceUri, out string mimeType)
    {
        content = string.Empty;
        resourceUri = CreateResourceUri(guideName);
        mimeType = MimeTypeMarkdown;

        var normalizedFile = file.Replace('\\', '/').TrimStart('/');
        if (!string.Equals(normalizedFile, $"{guideName}.md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var root in EnumerateExistingRoots(bundledGuidesRoot, repositoryGuidesRoot))
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, normalizedFile));
            var fullRoot = Path.GetFullPath(root);
            if (!IsWithinDirectory(fullRoot, fullPath) || !File.Exists(fullPath))
            {
                continue;
            }

            content = File.ReadAllText(fullPath);
            return true;
        }

        return false;
    }

    private static string CreateResourceUri(string guideName) =>
        $"guide://netvsmcp/{guideName}.md";

    private static bool IsWithinDirectory(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveDirectory(string? outputRelativePath, string? repoRelativePath)
    {
        foreach (var root in CandidateRoots())
        {
            if (!string.IsNullOrWhiteSpace(outputRelativePath))
            {
                var outputPath = Path.GetFullPath(Path.Combine(root, outputRelativePath));
                if (Directory.Exists(outputPath))
                {
                    return outputPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(repoRelativePath))
            {
                var repoPath = Path.GetFullPath(Path.Combine(root, repoRelativePath));
                if (Directory.Exists(repoPath))
                {
                    return repoPath;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateExistingRoots(params string?[] roots)
    {
        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}

[McpServerResourceType]
internal sealed class BestPracticeGuideResources
{
    private readonly BestPracticeGuideCatalog catalog;

    public BestPracticeGuideResources(BestPracticeGuideCatalog catalog)
    {
        this.catalog = catalog;
    }

    [McpServerResource(UriTemplate = "guide://netvsmcp/manage-visual-studio.md", Name = "NetVsMcp Manage Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for routing sessions, launching Visual Studio, windows, solutions, projects, and tests.")]
    public TextResourceContents ManageVisualStudio() => catalog.ReadResource("manage-visual-studio");

    [McpServerResource(UriTemplate = "guide://netvsmcp/navigate-visual-studio.md", Name = "NetVsMcp Navigate Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for definitions, references, symbols, diagnostics, code fixes, and workspace search.")]
    public TextResourceContents NavigateVisualStudio() => catalog.ReadResource("navigate-visual-studio");

    [McpServerResource(UriTemplate = "guide://netvsmcp/edit-visual-studio.md", Name = "NetVsMcp Edit Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for documents, direct editor edits, selections, formatting, and safe-edit previews.")]
    public TextResourceContents EditVisualStudio() => catalog.ReadResource("edit-visual-studio");

    [McpServerResource(UriTemplate = "guide://netvsmcp/build-visual-studio.md", Name = "NetVsMcp Build Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for build, rebuild, clean, NuGet/package operations, output panes, and error lists.")]
    public TextResourceContents BuildVisualStudio() => catalog.ReadResource("build-visual-studio");

    [McpServerResource(UriTemplate = "guide://netvsmcp/debug-visual-studio.md", Name = "NetVsMcp Debug Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for debugger start/attach/step, breakpoints, locals, watches, threads, modules, and processes.")]
    public TextResourceContents DebugVisualStudio() => catalog.ReadResource("debug-visual-studio");

    [McpServerResource(UriTemplate = "guide://netvsmcp/automate-visual-studio.md", Name = "NetVsMcp Automate Visual Studio Best Practices", MimeType = "text/markdown")]
    [Description("Agent-neutral best-practices guide for debuggee UI automation, browser control, screenshots, DOM access, and console I/O.")]
    public TextResourceContents AutomateVisualStudio() => catalog.ReadResource("automate-visual-studio");
}
