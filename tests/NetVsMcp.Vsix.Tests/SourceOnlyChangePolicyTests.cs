using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetVsMcp.Vsix.Tests;

public class SourceOnlyChangePolicyTests
{
    [Fact]
    public void SupportsChangesAcrossExistingDocuments()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var first = workspace.AddDocument(project.Id, "a.cs", SourceText.From("class A {}"));
        var second = workspace.AddDocument(project.Id, "b.cs", SourceText.From("class B {}"));
        var before = workspace.CurrentSolution;
        var after = before.WithDocumentText(first.Id, SourceText.From("class AA {}"))
            .WithDocumentText(second.Id, SourceText.From("class BB {}"));
        Assert.True(SourceOnlyChangePolicy.Accepts(before, after));
    }

    [Fact]
    public void RejectsInvisibleStructuralAndMetadataChanges()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "a.cs", SourceText.From("class A {}"));
        var before = workspace.CurrentSolution;
        Assert.False(SourceOnlyChangePolicy.Accepts(before, before.WithDocumentName(document.Id, "renamed.cs")));
        Assert.False(SourceOnlyChangePolicy.Accepts(before, before.RemoveDocument(document.Id)));
        Assert.False(SourceOnlyChangePolicy.Accepts(before, before.AddDocument(DocumentId.CreateNewId(project.Id), "b.cs", SourceText.From(""))));
        Assert.False(SourceOnlyChangePolicy.Accepts(before, before.WithProjectAssemblyName(project.Id, "different")));
    }
}
