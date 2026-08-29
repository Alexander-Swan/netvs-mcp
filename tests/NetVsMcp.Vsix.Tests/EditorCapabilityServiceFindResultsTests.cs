using System.Linq;
using System.Threading;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public class EditorCapabilityServiceFindResultsTests
{
    [Fact]
    public void ParseVisualStudioFindResults_ReturnsMatchingFileLines()
    {
        var output = """
            Find all "Run", Find Results 1, "Entire Solution", "*.*"
              D:\Code\App\Program.cs(12):     void Run() { }
              D:\Code\App\Other.cs(24):     runner.Run();
            Matching lines: 2    Matching files: 2    Total files searched: 10
            """;

        var result = EditorCapabilityService.ParseVisualStudioFindResults(
            output,
            "Run",
            matchCase: false,
            wholeWord: false,
            useRegex: false,
            maxResults: 100,
            CancellationToken.None);

        Assert.False(result.Truncated);
        Assert.Equal(2, result.MatchCount);

        var first = result.Matches.First();
        Assert.Equal(@"D:\Code\App\Program.cs", first.Path);
        Assert.Equal(12, first.Line);
        Assert.Equal(10, first.Column);
        Assert.Equal("Run", first.MatchText);
    }

    [Fact]
    public void ParseVisualStudioFindResults_TruncatesAtMaxResults()
    {
        var output = """
            C:\Code\App\One.cs(1): Run();
            C:\Code\App\Two.cs(2): Run();
            """;

        var result = EditorCapabilityService.ParseVisualStudioFindResults(
            output,
            "Run",
            matchCase: false,
            wholeWord: false,
            useRegex: false,
            maxResults: 1,
            CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.Equal(1, result.MatchCount);
        Assert.Equal(@"C:\Code\App\One.cs", Assert.Single(result.Matches).Path);
    }

    [Fact]
    public void ParseVisualStudioFindResults_UsesLastLineSuffixAsLocation()
    {
        var output = @"C:\Code\App (copy)\Program.cs(42): Runner.Run();";

        var result = EditorCapabilityService.ParseVisualStudioFindResults(
            output,
            @"Runner\.\w+",
            matchCase: true,
            wholeWord: false,
            useRegex: true,
            maxResults: 100,
            CancellationToken.None);

        var match = Assert.Single(result.Matches);
        Assert.Equal(@"C:\Code\App (copy)\Program.cs", match.Path);
        Assert.Equal(42, match.Line);
        Assert.Equal(1, match.Column);
        Assert.Equal("Runner.Run", match.MatchText);
    }
}
