using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix.Tests;

public class DocumentDiagnosticReaderTests
{
    private static Microsoft.CodeAnalysis.Document Add(AdhocWorkspace workspace, string text)
    {
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        workspace.TryApplyChanges(workspace.CurrentSolution.AddMetadataReference(project.Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location)));
        return workspace.AddDocument(project.Id, "fixture.cs", SourceText.From(text));
    }

    [Fact]
    public async Task CompilerDiagnosticsArePagedAndBoundToExactSourceText()
    {
        using var workspace = new AdhocWorkspace();
        const string text = "#error first\n#error second\nclass C {}";
        var document = Add(workspace, text);
        var request = new DocumentDiagnosticsRequest { Path = "fixture.cs", MaxItems = 1 };
        var first = await DocumentDiagnosticReader.ReadDocumentAsync(document, request, () => true, default);
        Assert.Equal("roslynCompiler", first.Source);
        Assert.Equal(TextContentVersion.Create(text), first.TextVersion);
        Assert.Equal(2, first.TotalItems);
        Assert.Equal(1, first.NextOffset);
        Assert.Single(first.Items);
        request.Offset = 1; request.ExpectedTextVersion = first.TextVersion;
        var second = await DocumentDiagnosticReader.ReadDocumentAsync(document, request, () => true, default);
        Assert.Null(second.NextOffset);
        Assert.Equal(2, Assert.Single(second.Items).Line);
    }

    [Fact]
    public async Task StaleVersionDoesNotReturnAFalseEmptyDiagnosticSuccess()
    {
        using var workspace = new AdhocWorkspace();
        var document = Add(workspace, "#error current\nclass C {}");
        var mismatch = await DocumentDiagnosticReader.ReadDocumentAsync(document,
            new() { Path = "fixture.cs", ExpectedTextVersion = "old-version" }, () => true, default);
        Assert.False(mismatch.VersionMatches);
        Assert.Empty(mismatch.Items);
        Assert.NotNull(mismatch.Message);
        var changed = await DocumentDiagnosticReader.ReadDocumentAsync(document, new() { Path = "fixture.cs" }, () => false, default);
        Assert.True(changed.VersionMatches);
        Assert.False(changed.IsCurrent);
        Assert.Single(changed.Items);
    }

    [Fact]
    public async Task WarningFilterAndMessageLimitsAreExplicit()
    {
        using var workspace = new AdhocWorkspace();
        var document = Add(workspace, "#warning ignore\n#error " + new string('x', 5000) + "\nclass C {}");
        var result = await DocumentDiagnosticReader.ReadDocumentAsync(document, new() { IncludeWarnings = false }, () => true, default);
        var diagnostic = Assert.Single(result.Items);
        Assert.True(diagnostic.MessageTruncated);
        Assert.Equal(4096, diagnostic.Message.Length);
    }
}
