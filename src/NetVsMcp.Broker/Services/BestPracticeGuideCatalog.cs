using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public sealed class BestPracticeGuideCatalog
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
        ["manage-visual-studio"] = "Session routing, launching Visual Studio, windows, solutions, projects, and tests.",
        ["navigate-visual-studio"] = "Definitions, references, symbols, diagnostics, code fixes, and workspace search.",
        ["edit-visual-studio"] = "Documents, direct editor edits, selections, formatting, and safe-edit previews.",
        ["build-visual-studio"] = "Build, rebuild, clean, NuGet/package operations, output panes, and error lists.",
        ["debug-visual-studio"] = "Debugger start/attach/step, breakpoints, locals, watches, threads, modules, and processes.",
        ["automate-visual-studio"] = "Debuggee UI automation, browser control, screenshots, DOM access, and console I/O."
    };

    private readonly string? bundledGuidesRoot;
    private readonly string? repositoryGuidesRoot;
    private readonly string? userGuidesRoot;

    public BestPracticeGuideCatalog()
    {
        bundledGuidesRoot = ResolveDirectory(Path.Combine("BundledGuides", "skills"), null);
        repositoryGuidesRoot = ResolveDirectory(null, Path.Combine(".agents", "skills"));
        userGuidesRoot = ResolveUserDirectory("best-practices");
    }

    public BestPracticeGuideToolResult List()
    {
        return new BestPracticeGuideToolResult(
            "NetVsMcp best-practices guides are available as MCP resources and through this tool. Read the matching guide before using Visual Studio management, navigation, editing, build, debug, or automation tools. These guides are agent-neutral defaults, not locked policy; user or project instructions can layer additional guidance on top.",
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
        foreach (var root in EnumerateExistingRoots(userGuidesRoot, bundledGuidesRoot, repositoryGuidesRoot))
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

        return new BestPracticeGuideInfo(
            guideName,
            Descriptions.TryGetValue(guideName, out var description) ? description : "NetVsMcp Visual Studio best-practices guide.",
            CreateResourceUri(guideName),
            files);
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

        foreach (var root in EnumerateExistingRoots(userGuidesRoot, bundledGuidesRoot, repositoryGuidesRoot))
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

    private static string? ResolveUserDirectory(string relativePath)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            return null;
        }

        var path = Path.Combine(appData, "NetVsMcp", relativePath);
        return Directory.Exists(path) ? path : null;
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
public sealed class BestPracticeGuideResources
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
