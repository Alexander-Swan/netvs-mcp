using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal static class DocumentReadRange
{
    // Preserve original line endings, including a terminal empty line. Slice the RPC's
    // returned text rather than reading disk: unsaved editor content remains authoritative.
    public static DocumentReadResult Select(DocumentReadResult document, int startLine, int? lineCount)
    {
        if (startLine < 1)
            throw new ArgumentOutOfRangeException(nameof(startLine));
        if (lineCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineCount));

        var text = document.Text;
        var totalLines = 1;
        var start = startLine == 1 ? 0 : -1;
        var end = -1;
        var endLine = lineCount.HasValue ? (long)startLine + lineCount.Value : long.MaxValue;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n'))
                continue;
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                index++;
            totalLines++;
            if (totalLines == startLine)
                start = index + 1;
            if (totalLines == endLine)
                end = index + 1;
        }

        var returnedLines = start < 0 ? 0 : Math.Min(lineCount ?? int.MaxValue, totalLines - startLine + 1);
        var selectedText = start < 0 ? string.Empty : text.Substring(start, (end < 0 ? text.Length : end) - start);
        return document with
        {
            Text = selectedText,
            StartLine = startLine,
            LineCount = returnedLines,
            TotalLines = totalLines,
            Truncated = returnedLines < totalLines
        };
    }
}
