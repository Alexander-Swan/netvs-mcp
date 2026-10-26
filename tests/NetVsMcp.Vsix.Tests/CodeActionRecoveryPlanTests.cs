using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetVsMcp.Vsix.Tests;

public class CodeActionRecoveryPlanTests
{
    [Fact]
    public async Task PartialApplicationRevertsOnlyVerifiedOwnedChanges()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var first = workspace.AddDocument(project.Id, "a.cs", SourceText.From("old-a"));
        var second = workspace.AddDocument(project.Id, "b.cs", SourceText.From("old-b"));
        var developer = workspace.AddDocument(project.Id, "developer.cs", SourceText.From("old-developer"));
        var before = workspace.CurrentSolution;
        var expected = before.WithDocumentText(first.Id, SourceText.From("new-a")).WithDocumentText(second.Id, SourceText.From("new-b"));
        var observed = expected.WithDocumentText(second.Id, SourceText.From("old-b")).WithDocumentText(developer.Id, SourceText.From("developer edit"));
        var plan = await CodeActionRecoveryPlan.CreateAsync(before, expected, observed, default);
        Assert.False(plan.MatchesPreview);
        Assert.True(plan.HasObservedChanges);
        Assert.False(plan.Ambiguous);
        Assert.NotNull(plan.UndoSolution);
        Assert.Equal("old-a", (await plan.UndoSolution!.GetDocument(first.Id)!.GetTextAsync()).ToString());
        Assert.Equal("old-b", (await plan.UndoSolution.GetDocument(second.Id)!.GetTextAsync()).ToString());
        Assert.Equal("developer edit", (await plan.UndoSolution.GetDocument(developer.Id)!.GetTextAsync()).ToString());
    }

    [Fact]
    public async Task AmbiguousSourceChangesNeverProduceAutomaticRecovery()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "a.cs", SourceText.From("old"));
        var before = workspace.CurrentSolution;
        var expected = before.WithDocumentText(document.Id, SourceText.From("planned"));
        var observed = before.WithDocumentText(document.Id, SourceText.From("concurrent developer edit"));
        var plan = await CodeActionRecoveryPlan.CreateAsync(before, expected, observed, default);
        Assert.False(plan.MatchesPreview);
        Assert.True(plan.Ambiguous);
        Assert.Null(plan.UndoSolution);
    }

    [Fact]
    public async Task RecoveryPreservesOriginalEncodingAndLineEndings()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var original = SourceText.From("old\r\nline", Encoding.Unicode);
        var document = workspace.AddDocument(project.Id, "a.cs", original);
        var before = workspace.CurrentSolution;
        var expected = before.WithDocumentText(document.Id, SourceText.From("new\r\nline", Encoding.Unicode));
        var plan = await CodeActionRecoveryPlan.CreateAsync(before, expected, expected, default);
        Assert.True(plan.MatchesPreview);
        var restored = await plan.UndoSolution!.GetDocument(document.Id)!.GetTextAsync();
        Assert.Equal(original.ToString(), restored.ToString());
        Assert.Equal(Encoding.Unicode.CodePage, restored.Encoding!.CodePage);
        Assert.NotNull(Assert.Single(plan.Versions).ObservedAfterTextVersion);
    }

    [Fact]
    public async Task PreviewVersionsDoNotClaimObservedApplication()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("fixture", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "a.cs", SourceText.From("old"));
        var before = workspace.CurrentSolution;
        var plan = await CodeActionRecoveryPlan.CreateAsync(before, before.WithDocumentText(document.Id, SourceText.From("new")), null, default);
        Assert.False(plan.HasObservedChanges);
        Assert.Null(plan.UndoSolution);
        Assert.Null(Assert.Single(plan.Versions).ObservedAfterTextVersion);
    }
}
