using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BestPracticeGuideCatalogTests
{
    [Fact]
    public void List_TagsMostGuidesAsDefaultEndpointOnly()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsGetBestPractices();

        Assert.True(response.Success);
        foreach (var guideName in new[] { "manage-visual-studio", "navigate-visual-studio", "edit-visual-studio", "build-visual-studio", "debug-visual-studio" })
        {
            var guide = response.Value!.Guides.Single(g => g.Name == guideName);
            var endpoint = Assert.Single(guide.Endpoints);
            Assert.Equal("*", endpoint.ToolNamePattern);
            Assert.Equal(McpEndpointRouting.DefaultEndpointPath, endpoint.McpEndpointPath);
        }
    }

    [Fact]
    public void List_TagsAutomateGuideAsSpanningBothEndpoints()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsGetBestPractices();

        Assert.True(response.Success);
        var guide = response.Value!.Guides.Single(g => g.Name == "automate-visual-studio");

        Assert.Contains(guide.Endpoints, e => e.McpEndpointPath == McpEndpointRouting.DefaultEndpointPath);
        Assert.Contains(guide.Endpoints, e => e.McpEndpointPath == McpEndpointRouting.WebAutomationEndpointPath);
    }

    [Fact]
    public void List_IncludesGuideReferenceFiles()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsGetBestPractices();

        Assert.True(response.Success);
        var guide = response.Value!.Guides.Single(g => g.Name == "debug-visual-studio");
        Assert.Contains(guide.Files, file => file.Path == "debug-visual-studio.md");
        Assert.Contains(guide.Files, file => file.Path == "debug-visual-studio/references/breakpoints.md");
    }

    [Fact]
    public void Read_CanReadReferenceFileScopedToGuide()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsGetBestPractices("debug-visual-studio", "references/breakpoints.md");

        Assert.True(response.Success);
        Assert.Equal("debug-visual-studio/references/breakpoints.md", response.Value!.Content!.File);
        Assert.Equal("guide://netvsmcp/debug-visual-studio/references/breakpoints.md", response.Value.Content.ResourceUri);
        Assert.Contains("Breakpoints And Tracepoints", response.Value.Content.Text);
    }

    [Fact]
    public void Read_RejectsReferenceFileOutsideGuideDirectory()
    {
        var runtime = CreateRuntime();

        var response = runtime.Tools.NetVsGetBestPractices("debug-visual-studio", "../manage-visual-studio.md");

        Assert.False(response.Success);
        Assert.Null(response.Value);
    }

    private static BrokerRuntime CreateRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "NetVsMcp.Broker.Tests", Guid.NewGuid().ToString("N"));
        var options = BrokerOptions.LocalDefault with
        {
            LogsDirectory = Path.Combine(root, "Logs"),
            SessionsDirectory = Path.Combine(root, "Sessions"),
            SettingsFilePath = Path.Combine(root, "settings.json")
        };

        return new BrokerRuntime(options, new SessionRegistry());
    }
}
