using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace NetVsMcp.Vsix;

internal sealed class DocumentSymbolsRequest
{
    public string? DocumentPath { get; set; }
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
