using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public class NavigationCapabilityServiceTests
{
    private static Solution CreateSolution(string source)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var solution = workspace.CurrentSolution
            .AddProject(projectInfo)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        return solution;
    }

    [Fact]
    public async Task SearchWorkspaceSymbolsAsync_MatchesTypesAndOrdersResults()
    {
        var solution = CreateSolution(@"
public class Widget
{
    public void Frobnicate() { }
    public int Count { get; set; }
}

public class WidgetFactory
{
}
");

        var result = await NavigationCapabilityService.SearchWorkspaceSymbolsAsync(
            solution,
            "Widget",
            requestedMaxResults: 100,
            CancellationToken.None);

        Assert.False(result.Truncated);
        var names = result.Symbols.Select(symbol => symbol.Name).ToArray();
        Assert.Contains("Widget", names);
        Assert.Contains("WidgetFactory", names);
        Assert.DoesNotContain("Frobnicate", names);
        Assert.DoesNotContain("Count", names);
    }

    [Fact]
    public async Task SearchWorkspaceSymbolsAsync_FindsMembersByName()
    {
        var solution = CreateSolution(@"
public class Widget
{
    public void Frobnicate() { }
    public int Count { get; set; }
}
");

        var result = await NavigationCapabilityService.SearchWorkspaceSymbolsAsync(
            solution,
            "Frobnicate",
            requestedMaxResults: 100,
            CancellationToken.None);

        var match = Assert.Single(result.Symbols);
        Assert.Equal("Frobnicate", match.Name);
        Assert.Equal("Method", match.Kind);
    }

    [Fact]
    public async Task SearchWorkspaceSymbolsAsync_TruncatesAtMaxResults()
    {
        var source = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"public class Foo{i} {{ }}"));
        var solution = CreateSolution(source);

        var result = await NavigationCapabilityService.SearchWorkspaceSymbolsAsync(
            solution,
            "Foo",
            requestedMaxResults: 3,
            CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.Equal(3, result.Symbols.Count);
    }

    [Fact]
    public async Task SearchWorkspaceSymbolsAsync_ReturnsEmptyWhenNoMatch()
    {
        var solution = CreateSolution(@"public class Widget { }");

        var result = await NavigationCapabilityService.SearchWorkspaceSymbolsAsync(
            solution,
            "NoSuchSymbolXyz",
            requestedMaxResults: 100,
            CancellationToken.None);

        Assert.False(result.Truncated);
        Assert.Empty(result.Symbols);
    }
}
