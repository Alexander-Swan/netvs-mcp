using System.Security.Cryptography;

namespace NetVsMcp.Contracts;

public static class TextContentVersion
{
    /// <summary>Hashes exact UTF-16 code units, including line endings and unpaired surrogates.</summary>
    public static string Create(string text)
    {
        var bytes = new byte[checked(text.Length * 2)];
        for (var i = 0; i < text.Length; i++) { bytes[i * 2] = (byte)text[i]; bytes[i * 2 + 1] = (byte)(text[i] >> 8); }
        using var hash = SHA256.Create();
        return "sha256-utf16:" + BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}

public sealed class DocumentDiagnosticsRequest
{
    public string Path { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? ExpectedTextVersion { get; set; }
    public bool IncludeWarnings { get; set; } = true;
    public int Offset { get; set; }
    public int MaxItems { get; set; } = 100;
}

public sealed record DocumentDiagnostic(string Id, string Severity, string Message, string? File,
    int Line, int Column, int EndLine, int EndColumn, bool MessageTruncated = false);

public sealed record DocumentDiagnosticsResult(string DocumentPath, string TextVersion, string ProjectId,
    string ProjectName, string Language, bool VersionMatches, bool IsCurrent,
    int Offset, int TotalItems, int? NextOffset, IReadOnlyCollection<DocumentDiagnostic> Items,
    string Source = "roslynCompiler", string? Message = null, bool LinkedFile = false);
