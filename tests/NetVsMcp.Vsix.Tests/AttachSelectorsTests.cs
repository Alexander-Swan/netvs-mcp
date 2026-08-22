using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class AttachSelectorsTests
{
    [Theory]
    [InlineData("SSH", "SSH", true)]
    [InlineData("ssh", "SSH", true)]
    [InlineData("My SSH Host", "SSH", true)]
    [InlineData("Default", "SSH", false)]
    [InlineData("Docker", "dock", true)]
    public void MatchesTransportName_ExactThenSubstring_CaseInsensitive(string candidateName, string requested, bool expected)
    {
        Assert.Equal(expected, AttachSelectors.MatchesTransportName(candidateName, requested));
    }

    [Fact]
    public void MatchesProcessSelector_NoFilters_MatchesAnything()
    {
        Assert.True(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", null, null));
    }

    [Fact]
    public void MatchesProcessSelector_ProcessIdFilter_MatchesOnlyThatId()
    {
        Assert.True(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", 1234, null));
        Assert.False(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", 5678, null));
    }

    [Theory]
    [InlineData("dotnet.exe", "dotnet.exe", true)]
    [InlineData("DOTNET.EXE", "dotnet.exe", true)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", "dotnet.exe", true)]
    [InlineData("dotnet.exe", "other.exe", false)]
    public void MatchesProcessSelector_ProcessNameFilter_MatchesFullNameOrFileName(string candidateName, string filterName, bool expected)
    {
        Assert.Equal(expected, AttachSelectors.MatchesProcessSelector(1234, candidateName, null, filterName));
    }

    [Fact]
    public void MatchesProcessSelector_BothFilters_RequireBothToMatch()
    {
        Assert.True(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", 1234, "dotnet.exe"));
        Assert.False(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", 5678, "dotnet.exe"));
        Assert.False(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", 1234, "other.exe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MatchesProcessSelector_BlankNameFilter_IsIgnored(string? filterName)
    {
        Assert.True(AttachSelectors.MatchesProcessSelector(1234, "dotnet.exe", null, filterName));
    }
}
