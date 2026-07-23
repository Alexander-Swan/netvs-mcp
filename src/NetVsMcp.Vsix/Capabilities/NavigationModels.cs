using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace NetVsMcp.Vsix;

internal sealed class DocumentSymbolsRequest
{
    public string? DocumentPath { get; set; }
}

internal sealed class CodePositionRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}

internal sealed class RenameSymbolRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string NewName { get; set; } = string.Empty;
}

internal sealed class DocumentSymbolsResult
{
    public DocumentSymbolsResult(string documentPath, IReadOnlyCollection<DocumentSymbolInfo> symbols)
    {
        DocumentPath = documentPath;
        Symbols = symbols;
    }

    public string DocumentPath { get; }
    public IReadOnlyCollection<DocumentSymbolInfo> Symbols { get; }
}

internal sealed class DocumentSymbolInfo
{
    public DocumentSymbolInfo(
        string name,
        string kind,
        string? file,
        int line,
        int column,
        string? containingType,
        string? containingNamespace)
    {
        Name = name;
        Kind = kind;
        File = file;
        Line = line;
        Column = column;
        ContainingType = containingType;
        ContainingNamespace = containingNamespace;
    }

    public string Name { get; }
    public string Kind { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public string? ContainingType { get; }
    public string? ContainingNamespace { get; }
}

internal sealed class CodeLocationInfo
{
    public CodeLocationInfo(string? file, int line, int column, DocumentSymbolInfo symbol)
    {
        File = file;
        Line = line;
        Column = column;
        Symbol = symbol;
    }

    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public DocumentSymbolInfo Symbol { get; }
}

internal sealed class CodeReferenceInfo
{
    public CodeReferenceInfo(
        string? file,
        int line,
        int column,
        bool isImplicit,
        DocumentSymbolInfo symbol)
    {
        File = file;
        Line = line;
        Column = column;
        IsImplicit = isImplicit;
        Symbol = symbol;
    }

    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public bool IsImplicit { get; }
    public DocumentSymbolInfo Symbol { get; }
}

internal sealed class GoToDefinitionResult
{
    public GoToDefinitionResult(
        DocumentSymbolInfo? symbol,
        IReadOnlyCollection<CodeLocationInfo> definitions,
        bool navigated)
    {
        Symbol = symbol;
        Definitions = definitions;
        Navigated = navigated;
    }

    public DocumentSymbolInfo? Symbol { get; }
    public IReadOnlyCollection<CodeLocationInfo> Definitions { get; }
    public bool Navigated { get; }
}

internal sealed class FindReferencesResult
{
    public FindReferencesResult(DocumentSymbolInfo? symbol, IReadOnlyCollection<CodeReferenceInfo> references)
    {
        Symbol = symbol;
        References = references;
    }

    public DocumentSymbolInfo? Symbol { get; }
    public IReadOnlyCollection<CodeReferenceInfo> References { get; }
}

internal sealed class FindImplementationsResult
{
    public FindImplementationsResult(
        bool supported,
        string message,
        CodePositionRequest position,
        IReadOnlyCollection<CodeLocationInfo> implementations)
    {
        Supported = supported;
        Message = message;
        Position = position;
        Implementations = implementations;
    }

    public bool Supported { get; }
    public string Message { get; }
    public CodePositionRequest Position { get; }
    public IReadOnlyCollection<CodeLocationInfo> Implementations { get; }
}

internal sealed class RenameSymbolChangeInfo
{
    public RenameSymbolChangeInfo(
        string? file,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string newText)
    {
        File = file;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
        NewText = newText;
    }

    public string? File { get; }
    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public int EndColumn { get; }
    public string NewText { get; }
}

internal sealed class RenameSymbolPreviewResult
{
    public RenameSymbolPreviewResult(
        bool supported,
        string message,
        CodePositionRequest position,
        string newName,
        DocumentSymbolInfo? symbol,
        IReadOnlyCollection<RenameSymbolChangeInfo>? changes)
    {
        Supported = supported;
        Message = message;
        Position = position;
        NewName = newName;
        Symbol = symbol;
        Changes = changes;
    }

    public bool Supported { get; }
    public string Message { get; }
    public CodePositionRequest Position { get; }
    public string NewName { get; }
    public DocumentSymbolInfo? Symbol { get; }
    public IReadOnlyCollection<RenameSymbolChangeInfo>? Changes { get; }
}

internal static class DocumentSymbolInfoFactory
{
    public static DocumentSymbolInfo FromSymbol(ISymbol symbol, string? file, int line, int column)
    {
        var containingNamespace = symbol.ContainingNamespace is { IsGlobalNamespace: false } symbolNamespace
            ? symbolNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            : null;
        var containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        return new DocumentSymbolInfo(
            symbol.Name,
            symbol.Kind.ToString(),
            file,
            line,
            column,
            containingType,
            containingNamespace);
    }
}
