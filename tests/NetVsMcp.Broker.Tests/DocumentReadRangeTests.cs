using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public class DocumentReadRangeTests
{
    [Theory]
    [InlineData("", 1, null, "", 1, 1, false)]
    [InlineData("one", 1, null, "one", 1, 1, false)]
    [InlineData("one\r\ntwo\r\nthree", 2, 1, "two\r\n", 3, 1, true)]
    [InlineData("one\ntwo\nthree", 2, 1, "two\n", 3, 1, true)]
    [InlineData("one\rtwo\rthree", 2, 1, "two\r", 3, 1, true)]
    [InlineData("one\r\ntwo\nthree\rfour", 2, 2, "two\nthree\r", 4, 2, true)]
    [InlineData("one\ntwo", 1, null, "one\ntwo", 2, 2, false)]
    [InlineData("one\ntwo", 1, 20, "one\ntwo", 2, 2, false)]
    [InlineData("one\ntwo", 2, null, "two", 2, 1, true)]
    [InlineData("one\ntwo", 3, 1, "", 2, 0, true)]
    [InlineData("one\n", 2, 1, "", 2, 1, true)]
    [InlineData("one\n", 1, 1, "one\n", 2, 1, true)]
    [InlineData("one\n", 1, 2, "one\n", 2, 2, false)]
    [InlineData("\n\n", 2, 1, "\n", 3, 1, true)]
    [InlineData("", 2, null, "", 1, 0, true)]
    [InlineData("one\ntwo", 2, int.MaxValue, "two", 2, 1, true)]
    [InlineData("one", int.MaxValue, int.MaxValue, "", 1, 0, true)]
    public void Select_PreservesTextAndReportsRange(string text, int startLine, int? lineCount,
        string expectedText, int totalLines, int returnedLines, bool truncated)
    {
        var original = new DocumentReadResult(new EditorDocumentInfo("File.cs", "File.cs", "CSharp", true, false), text, "live", true);
        var result = DocumentReadRange.Select(original, startLine, lineCount);

        Assert.Equal(expectedText, result.Text);
        Assert.Equal(startLine, result.StartLine);
        Assert.Equal(returnedLines, result.LineCount);
        Assert.Equal(totalLines, result.TotalLines);
        Assert.Equal(truncated, result.Truncated);
        Assert.Equal("live", result.Source);
        Assert.True(result.UsedLiveBuffer);
        Assert.Same(original.Document, result.Document);
        Assert.Equal(text, original.Text);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Select_RejectsInvalidRanges(int startLine, int? lineCount)
    {
        var document = new DocumentReadResult(new EditorDocumentInfo(null, null, null, false, true), "text", "disk", false);
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentReadRange.Select(document, startLine, lineCount));
    }

    [Fact]
    public void Select_PreservesDiskSource()
    {
        var document = new DocumentReadResult(new EditorDocumentInfo(null, null, null, false, true), "first\nsecond", "disk", false);
        var result = DocumentReadRange.Select(document, 2, 1);
        Assert.Equal("second", result.Text);
        Assert.Equal("disk", result.Source);
        Assert.False(result.UsedLiveBuffer);
    }
}
