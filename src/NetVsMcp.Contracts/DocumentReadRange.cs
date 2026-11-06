namespace NetVsMcp.Contracts;

public static class DocumentReadRange
{
    public const int MaxPageCharacters = 65_536;

    // Slice the captured text without changing line endings or discarding a terminal empty line.
    public static DocumentReadResult Select(DocumentReadResult document, int startLine, int? lineCount, int startColumn = 1)
    {
        if (startLine < 1)
            throw new ArgumentOutOfRangeException(nameof(startLine));
        if (lineCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineCount));
        if (startColumn < 1)
            throw new ArgumentOutOfRangeException(nameof(startColumn));

        var text = document.Text;
        var totalLines = 1;
        var start = startLine == 1 ? 0 : -1;
        var firstLineEnd = text.Length;
        var end = -1;
        var endLine = lineCount.HasValue ? (long)startLine + lineCount.Value : long.MaxValue;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n'))
                continue;
            if (totalLines == startLine)
                firstLineEnd = index;
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                index++;
            totalLines++;
            if (totalLines == startLine)
                start = index + 1;
            if (totalLines == endLine)
                end = index + 1;
        }

        var returnedLines = start < 0 ? 0 : Math.Min(lineCount ?? int.MaxValue, totalLines - startLine + 1);
        int? nextLine = null;
        int? nextColumn = null;
        var selectedText = string.Empty;
        if (start >= 0)
        {
            if (startColumn - 1 > firstLineEnd - start)
                throw new ArgumentOutOfRangeException(nameof(startColumn), "startColumn exceeds the requested line.");
            start += startColumn - 1;
            if (SplitsPair(text, start))
                throw new ArgumentOutOfRangeException(nameof(startColumn), "startColumn splits a UTF-16 surrogate pair.");

            var length = (end < 0 ? text.Length : end) - start;
            if (length > MaxPageCharacters)
            {
                length = MaxPageCharacters;
                var nextIndex = start + length;
                if (SplitsPair(text, nextIndex) || (text[nextIndex - 1] == '\r' && text[nextIndex] == '\n'))
                {
                    length--;
                    nextIndex--;
                }

                var line = startLine;
                var column = startColumn;
                for (var index = start; index < nextIndex; index++)
                {
                    if (text[index] is '\r' or '\n')
                    {
                        if (text[index] == '\r' && index + 1 < nextIndex && text[index + 1] == '\n')
                            index++;
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                nextLine = line;
                nextColumn = column;
                returnedLines = line - startLine + (column > 1 ? 1 : 0);
            }
            selectedText = text.Substring(start, length);
        }
        return document with
        {
            Text = selectedText,
            TextVersion = TextContentVersion.Create(text),
            StartLine = startLine,
            LineCount = returnedLines,
            TotalLines = totalLines,
            Truncated = nextLine.HasValue || returnedLines < totalLines || startColumn > 1,
            StartColumn = startColumn > 1 ? startColumn : null,
            NextLine = nextLine,
            NextColumn = nextColumn
        };
    }

    private static bool SplitsPair(string text, int index) =>
        index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);
}
