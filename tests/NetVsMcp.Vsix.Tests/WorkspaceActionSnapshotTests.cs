using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetVsMcp.Vsix.Tests;

public class WorkspaceActionSnapshotTests
{
    [Fact]
    public async Task SameSourceContentWithANewRoslynTextVersionDoesNotConflict()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "a.cs", SourceText.From("class A {}"));
        var before = workspace.CurrentSolution;
        var original = await WorkspaceActionSnapshot.CaptureAsync(before, default);
        var reloaded = before.WithDocumentText(document.Id, SourceText.From("class A {}"));
        var sameContent = await WorkspaceActionSnapshot.CaptureAsync(reloaded, default);
        Assert.Equal(original, sameContent);
        Assert.True(original.MatchesCurrent(reloaded));
    }

    [Fact]
    public async Task EquivalentWorkspaceVersionsMatchWithoutObjectIdentity()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "a.cs", SourceText.From("class A {}"));
        var original = await WorkspaceActionSnapshot.CaptureAsync(workspace.CurrentSolution, default);
        var another = await WorkspaceActionSnapshot.CaptureAsync(document.Project.Solution, default);
        Assert.Equal(original, another);
        Assert.True(original.MatchesCurrent(workspace.CurrentSolution));
    }

    [Fact]
    public async Task BodyTextAndOtherDocumentChangesInvalidateTheSnapshot()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var first = workspace.AddDocument(project.Id, "a.cs", SourceText.From("class A { int M() => 1; }"));
        var other = workspace.AddDocument(project.Id, "b.cs", SourceText.From("class B {}"));
        var before = workspace.CurrentSolution;
        var original = await WorkspaceActionSnapshot.CaptureAsync(before, default);
        var bodyEdit = before.WithDocumentText(first.Id, SourceText.From("class A { int M() => 2; }"));
        Assert.NotEqual(original, await WorkspaceActionSnapshot.CaptureAsync(bodyEdit, default));
        var otherEdit = before.WithDocumentText(other.Id, SourceText.From("class Changed {}"));
        Assert.NotEqual(original, await WorkspaceActionSnapshot.CaptureAsync(otherEdit, default));
        Assert.False(original.MatchesCurrent(otherEdit));
        Assert.NotEqual(original, await WorkspaceActionSnapshot.CaptureAsync(before.WithProjectAssemblyName(project.Id, "changed"), default));
    }
}
