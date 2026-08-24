using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix.Tests;

/// <summary>
/// Covers <see cref="DocumentPathResolver"/> -- pure path-resolution logic with no VS/COM dependency
/// beyond the <see cref="ThreadHelper.ThrowIfNotOnUIThread"/> guard at the top of <c>Resolve</c>, which
/// is why every test switches to the mocked main thread first. This file was implicated in a
/// previously-shipped breakpoint set/remove path-mismatch bug and had zero tests before this.
/// </summary>
[Collection(MockedVS.Collection)]
public class DocumentPathResolverTests
{
    public DocumentPathResolverTests(GlobalServiceProvider sp)
    {
        sp.Reset();
    }

    [Fact]
    public async Task Resolve_AbsolutePath_ReturnsFullPathUnchanged()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var absolute = Path.Combine(Path.GetTempPath(), "Foo.cs");

        var result = DocumentPathResolver.Resolve(null, absolute);

        Assert.Equal(Path.GetFullPath(absolute), result);
    }

    [Fact]
    public async Task Resolve_RelativePath_NoDte_ResolvesAgainstCurrentDirectory()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var result = DocumentPathResolver.Resolve(null, "Foo.cs");

        Assert.Equal(Path.GetFullPath("Foo.cs"), result);
    }

    [Fact]
    public async Task Resolve_NullOrWhitespacePath_NoActiveDocumentAllowed_Throws()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var ex = Assert.Throws<ArgumentException>(() => DocumentPathResolver.Resolve(null, "   "));
        Assert.Contains("Document path is required.", ex.Message);
    }

    [Fact]
    public async Task Resolve_NullPath_ActiveDocumentAllowed_NoDte_ThrowsWithActiveDocumentMessage()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentPathResolver.Resolve(null, null, allowActiveDocument: true));
        Assert.Contains("no active document", ex.Message);
    }

    [Fact]
    public async Task Resolve_UsesGivenParameterNameInException()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentPathResolver.Resolve(null, null, parameterName: "documentPath2"));
        Assert.Equal("documentPath2", ex.ParamName);
    }

    [Theory]
    [InlineData(@"foo\\bar.cs", @"foo\bar.cs")]
    [InlineData("foo//bar.cs", @"foo\bar.cs")]
    [InlineData(@"foo\/bar.cs", @"foo\bar.cs")]
    public async Task Resolve_CollapsesRepeatedDirectorySeparators(string input, string expectedRelative)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var result = DocumentPathResolver.Resolve(null, input);

        Assert.Equal(Path.GetFullPath(expectedRelative), result);
    }

    [Fact]
    public async Task Resolve_PreservesUncPrefix_DoesNotCollapseLeadingDoubleSeparator()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var result = DocumentPathResolver.Resolve(null, @"\\server\share\foo.cs");

        Assert.Equal(Path.GetFullPath(@"\\server\share\foo.cs"), result);
    }

    [Fact]
    public async Task Resolve_TrimsWhitespaceAroundPath()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var result = DocumentPathResolver.Resolve(null, "  Foo.cs  ");

        Assert.Equal(Path.GetFullPath("Foo.cs"), result);
    }

    [Fact]
    public async Task ResolveOptional_NullOrWhitespace_ReturnsNull()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        Assert.Null(DocumentPathResolver.ResolveOptional(null, null));
        Assert.Null(DocumentPathResolver.ResolveOptional(null, "   "));
    }

    [Fact]
    public async Task ResolveOptional_NonEmptyPath_DelegatesToResolve()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var result = DocumentPathResolver.ResolveOptional(null, "Foo.cs");

        Assert.Equal(Path.GetFullPath("Foo.cs"), result);
    }
}
