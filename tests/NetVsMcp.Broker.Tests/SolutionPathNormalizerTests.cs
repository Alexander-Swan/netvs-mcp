using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class SolutionPathNormalizerTests
{
    [Fact]
    public void Normalize_ReturnsNullOrWhitespaceUnchanged()
    {
        Assert.Null(SolutionPathNormalizer.Normalize(null));
        Assert.Equal("   ", SolutionPathNormalizer.Normalize("   "));
    }

    [Fact]
    public void Normalize_TrimsAndRemovesTrailingSeparators()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        var input = Path.Combine(root, "Workspace", "Project") + Path.DirectorySeparatorChar;

        var normalized = SolutionPathNormalizer.Normalize($"  {input}  ");

        Assert.Equal(Path.Combine(root, "Workspace", "Project"), normalized);
    }

    [Fact]
    public void Normalize_PreservesRootSeparator()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;

        var normalized = SolutionPathNormalizer.Normalize(root);

        Assert.Equal(root, normalized);
    }

    [Fact]
    public void Normalize_ConvertsAlternateSeparators()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var input = $"{trimmedRoot}/Workspace/Project/";

        var normalized = SolutionPathNormalizer.Normalize(input);

        Assert.Equal(Path.Combine(root, "Workspace", "Project"), normalized);
    }
}
