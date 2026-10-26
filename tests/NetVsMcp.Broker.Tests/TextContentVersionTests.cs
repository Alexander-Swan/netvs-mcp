using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public class TextContentVersionTests
{
    [Fact]
    public void ExactUtf16AndLineEndingsRemainDistinct()
    {
        Assert.Equal(TextContentVersion.Create("same"), TextContentVersion.Create("same"));
        Assert.NotEqual(TextContentVersion.Create("line\r\n"), TextContentVersion.Create("line\n"));
        Assert.NotEqual(TextContentVersion.Create("\ud800"), TextContentVersion.Create("\ud801"));
    }

    [Fact]
    public void EveryReadPageIdentifiesTheEntireOriginalDocument()
    {
        var original = new DocumentReadResult(new("fixture.cs", "fixture.cs", null, false, true), "first\nsecond", "live", true);
        var first = DocumentReadRange.Select(original, 1, 1);
        var second = DocumentReadRange.Select(original, 2, 1);
        Assert.Equal(first.TextVersion, second.TextVersion);
        Assert.Equal(TextContentVersion.Create(original.Text), first.TextVersion);
        Assert.NotEqual(TextContentVersion.Create(first.Text), first.TextVersion);
    }
}
