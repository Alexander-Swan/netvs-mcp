using System.Text.Json;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class BestPracticeGuideVersionTests
{
    [Fact]
    public void DefaultListExposesVersionWithoutReadingAFile()
    {
        var catalog = new BestPracticeGuideCatalog();
        var response = catalog.Read(null, null);

        Assert.True(response.Success);
        Assert.Null(response.Value!.Content);
        Assert.NotEmpty(response.Value.Guides);
        Assert.False(string.IsNullOrWhiteSpace(response.Value.Version));
        Assert.Equal(catalog.List().Version, response.Value.Version);
        var json = JsonSerializer.SerializeToElement(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(response.Value.Version, json.GetProperty("value").GetProperty("version").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("references/inspection.md")]
    public void FileReadsExposeTheSameStructuredVersionAsTheDefaultList(string? file)
    {
        var catalog = new BestPracticeGuideCatalog();
        var response = catalog.Read("debug-visual-studio", file);

        Assert.True(response.Success);
        Assert.Equal(catalog.List().Version, response.Value!.Version);
        Assert.StartsWith($"NetVsMcp version: {response.Value.Version}\n\n", response.Value.Content!.Text);
    }
}
