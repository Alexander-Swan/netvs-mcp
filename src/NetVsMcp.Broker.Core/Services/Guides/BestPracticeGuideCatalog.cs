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
        ["manage-visual-studio"] = "Session routing, launching Visual Studio, windows, solutions, projects, references, and tests. Read before using vs_*, solution_*, project_*, startup_project_*, test_*, window_*, toolwindow_*, task_list_*, git_context, execute_command, or vs_context_snapshot.",
        ["navigate-visual-studio"] = "Definitions, references, symbols, diagnostics, code fixes, renames, call hierarchy, and workspace search. Read before using code_*, symbol_context, document_outline, find_implementations, rename_symbol_*, call_hierarchy_get, code_actions_*, diagnostics_*, editor_find, find_in_files, open_relevant_files, or related document read/open/list tools.",
        ["edit-visual-studio"] = "Documents, direct editor edits, selections, formatting, and safe-edit previews. Read before using document_*, editor_*, selection_*, document_cleanup, format_and_organize, edit_*, prepare_safe_edit, or apply_safe_edit_and_build.",
        ["build-visual-studio"] = "Build, rebuild, clean, NuGet/package operations, output panes, Task List, and error lists. Read before using build_*, clean_solution, rebuild_solution, errors_list, output_*, task_list_*, package_restore, project_dependencies, project_add_reference, project_remove_reference, or nuget_*.",
        ["debug-visual-studio"] = "Debugger start/attach/step, breakpoints, locals, watches, threads, modules, processes, and test debugging. Read before using debug_*, breakpoint_*, watch_*, thread_*, process_*, module_list, exception_settings_*, parallel_*, immediate_execute, or test_debug.",
        ["automate-visual-studio"] = "Debuggee UI automation, browser control, screenshots, DOM access, and console I/O. Read before using console_*, ui_*, or web_*."
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
        bundledGuidesRoot = ResolveDirectory("BestPractices", null);
        repositoryGuidesRoot = ResolveDirectory(null, "BestPractices");
    }

    public BestPracticeGuideToolResult List()
    {
        return new BestPracticeGuideToolResult(
            "NetVsMcp best-practices guides are available as MCP resources and through this tool. For tool use, read the matching guide's small entrypoint first, then only the reference files needed for the current operation. For learning NetVsMcp or creating local agent skills, read the entrypoints and the relevant references deliberately instead of loading every file by default. These guides are agent-neutral defaults, not locked policy; user or project instructions can layer additional guidance on top.",
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
        if (!TryReadFile(guideName, guideFile, out var resolvedFile, out var content, out var resourceUri, out var mimeType))
        {
            return ToolResponse<BestPracticeGuideToolResult>.Fail($"Guide file '{guideFile}' was not found for '{guideName}'. Call without arguments to list available files.");
        }

        return ToolResponse<BestPracticeGuideToolResult>.Ok(new BestPracticeGuideToolResult(
            $"Read NetVsMcp best-practices guide '{guideName}' file '{resolvedFile}'.",
            GuideNames.Select(CreateInfo).ToArray(),
            new BestPracticeGuideContent(guideName, resolvedFile, resourceUri, mimeType, content)));
    }

    public TextResourceContents ReadResource(string guideName)
    {
        var file = $"{guideName}.md";
        if (!TryReadFile(guideName, file, out _, out var content, out var resourceUri, out var mimeType))
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
            var mainFile = $"{guideName}.md";
            var mainPath = Path.Combine(root, mainFile);
            if (File.Exists(mainPath) && !files.Any(file => string.Equals(file.Path, mainFile, StringComparison.OrdinalIgnoreCase)))
            {
                files.Add(new BestPracticeGuideFileInfo(mainFile, CreateResourceUri(guideName, mainFile), MimeTypeMarkdown));
            }

            var guideDirectory = Path.Combine(root, guideName);
            if (!Directory.Exists(guideDirectory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(guideDirectory, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(guideDirectory, path).Replace('\\', '/');
                var guidePath = $"{guideName}/{relativePath}";
                if (!files.Any(file => string.Equals(file.Path, guidePath, StringComparison.OrdinalIgnoreCase)))
                {
                    files.Add(new BestPracticeGuideFileInfo(guidePath, CreateResourceUri(guideName, guidePath), MimeTypeMarkdown));
                }
            }
        }

        if (files.Count == 0)
        {
            var mainFile = $"{guideName}.md";
            files.Add(new BestPracticeGuideFileInfo(mainFile, CreateResourceUri(guideName, mainFile), MimeTypeMarkdown));
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

    private bool TryReadFile(string guideName, string file, out string resolvedFile, out string content, out string resourceUri, out string mimeType)
    {
        resolvedFile = string.Empty;
        content = string.Empty;
        resourceUri = CreateResourceUri(guideName);
        mimeType = MimeTypeMarkdown;

        var normalizedFile = file.Replace('\\', '/').TrimStart('/');
        var mainFile = $"{guideName}.md";
        var guidePrefix = $"{guideName}/";
        var scopedFile = string.Equals(normalizedFile, mainFile, StringComparison.OrdinalIgnoreCase)
            ? mainFile
            : normalizedFile.StartsWith(guidePrefix, StringComparison.OrdinalIgnoreCase)
                ? normalizedFile
                : $"{guideName}/{normalizedFile}";

        if (!string.Equals(scopedFile, mainFile, StringComparison.OrdinalIgnoreCase) &&
            !scopedFile.StartsWith(guidePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedFile = scopedFile;
        resourceUri = CreateResourceUri(guideName, scopedFile);

        foreach (var root in EnumerateExistingRoots(bundledGuidesRoot, repositoryGuidesRoot))
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, scopedFile));
            var containingDirectory = string.Equals(scopedFile, mainFile, StringComparison.OrdinalIgnoreCase)
                ? Path.GetFullPath(root)
                : Path.GetFullPath(Path.Combine(root, guideName));
            if (!IsWithinDirectory(containingDirectory, fullPath) || !File.Exists(fullPath))
            {
                continue;
            }

            content = File.ReadAllText(fullPath);
            return true;
        }

        return false;
    }

    private static string CreateResourceUri(string guideName) =>
        CreateResourceUri(guideName, $"{guideName}.md");

    private static string CreateResourceUri(string guideName, string file) =>
        $"guide://netvsmcp/{file.Replace('\\', '/')}";

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
