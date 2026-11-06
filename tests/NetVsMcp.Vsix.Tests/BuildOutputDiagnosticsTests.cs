using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class BuildOutputDiagnosticsTests
{
    [Fact]
    public void AppendedOutputExcludesOldDiagnosticsAndKeepsErrorsBeforeWarnings()
    {
        const string before = "C:\\Old.cs(1,2): error CS1002: Old failure\r\n";
        const string fresh = "1>C:\\App\\New.cs(4,5): warning CS0168: Unused local\r\n" +
            "2>C:\\App\\New.cs(6,7): error CS1002: Expected semicolon [C:\\App\\App.csproj]\r\n";

        var result = BuildOutputDiagnostics.Read(before, before + fresh, 0, 1, new() { MaxItems = 1 });

        var error = Assert.Single(result.Items);
        Assert.Equal("C:\\App\\New.cs", error.File);
        Assert.Equal(6, error.Line);
        Assert.Equal(7, error.Column);
        Assert.Equal("CS1002: Expected semicolon", error.Description);
        Assert.Equal("C:\\App\\App.csproj", error.Project);
        Assert.True(result.Truncated);
        Assert.Null(result.Unavailable);
    }

    [Fact]
    public void IdenticalOutputCanBeFreshOnlyWhenThePaneWasCleared()
    {
        const string output = "CSC : error CS5001: No entry point\r\n";
        Assert.Empty(BuildOutputDiagnostics.Read(output, output, 0, 1, new()).Items);

        var result = BuildOutputDiagnostics.Read(output, output, 1, 1, new());
        Assert.Single(result.Items);
        Assert.Null(result.Unavailable);
    }

    [Fact]
    public void UnobservedReplacementIsUnavailable()
    {
        var result = BuildOutputDiagnostics.Read("old log", "CSC : error CS5001: No entry point", 0, 1, new());
        Assert.Empty(result.Items);
        Assert.NotNull(result.Unavailable);
    }
}
