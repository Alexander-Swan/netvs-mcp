using System.Text.Json;
using ModelContextProtocol.Protocol;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class GuideDisclosureTests
{
    private static CallToolResult Result(object value) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) }]
    };

    [Fact]
    public void FocusedReadReturnsOnlyItsGuideAndKeepsFullDiscoveryAvailable()
    {
        var catalog = new BestPracticeGuideCatalog();
        var read = catalog.Read("debug-visual-studio", "references/inspection.md");
        Assert.True(read.Success);
        Assert.Equal("debug-visual-studio", Assert.Single(read.Value!.Guides).Name);
        Assert.Equal(6, catalog.List().Guides.Count);
        var focused = JsonSerializer.Serialize(read);
        var oldShape = JsonSerializer.Serialize(read with { Value = read.Value with { Guides = catalog.List().Guides } });
        Assert.True(focused.Length < oldShape.Length * 0.65, $"Focused read: {focused.Length}; full catalog: {oldShape.Length}");
    }

    [Fact]
    public void SuccessfulEntrypointReadSuppressesOnlyItsOwnClientAndFamily()
    {
        var catalog = new BestPracticeGuideCatalog();
        var surfacer = new BestPracticeGuideSurfacer(catalog);
        surfacer.AppendGuideIfFirstUse("client-a", "netvs_get_best_practices", Result(catalog.Read("debug-visual-studio", null)));
        var debug = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client-a", "debug_status", debug);
        Assert.Single(debug.Content);
        var anotherClient = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client-b", "debug_status", anotherClient);
        Assert.Equal(2, anotherClient.Content.Count);
        var anotherFamily = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client-a", "document_read", anotherFamily);
        Assert.Equal(2, anotherFamily.Content.Count);
    }

    [Theory]
    [InlineData("session_automation_control", "manage-visual-studio.md")]
    [InlineData("vs_events", "manage-visual-studio.md")]
    [InlineData("document_diagnostics", "edit-visual-studio.md")]
    [InlineData("code_actions_preview", "navigate-visual-studio.md")]
    [InlineData("code_actions_revert", "navigate-visual-studio.md")]
    public void NewToolFamiliesSurfaceTheMatchingEntrypoint(string tool, string entrypoint)
    {
        var surfacer = new BestPracticeGuideSurfacer(new BestPracticeGuideCatalog());
        var result = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client", tool, result);
        Assert.Equal(2, result.Content.Count);
        Assert.Contains(entrypoint, Assert.IsType<TextContentBlock>(result.Content.Last()).Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("references/inspection.md")]
    [InlineData("missing.md")]
    public void IndexReferenceAndFailedReadsDoNotAcknowledgeAnEntrypoint(string? file)
    {
        var catalog = new BestPracticeGuideCatalog();
        var surfacer = new BestPracticeGuideSurfacer(catalog);
        var response = file is null ? catalog.Read(null, null) : catalog.Read("debug-visual-studio", file);
        surfacer.AppendGuideIfFirstUse("client", "netvs_get_best_practices", Result(response));
        var tool = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client", "debug_status", tool);
        Assert.Equal(2, tool.Content.Count);
        var repeated = Result(new { success = true });
        surfacer.AppendGuideIfFirstUse("client", "debug_status", repeated);
        Assert.Single(repeated.Content);
    }
}
