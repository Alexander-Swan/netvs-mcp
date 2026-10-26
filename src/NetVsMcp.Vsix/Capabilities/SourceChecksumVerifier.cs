using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal static class SourceChecksumVerifier
{
    internal const int MaximumBytes = 8 * 1024 * 1024;

    internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

    internal static SourceChecksumComparison CompareDisk(string path, IReadOnlyCollection<string> expected, CancellationToken cancellationToken)
    {
        try
        {
            if (!Path.IsPathRooted(path)) return new("unavailable", Message: "The engine source name is not an absolute local path; no path guessing or download was attempted.");
            // Prevent concurrent writes during the bounded read; sharing conflicts remain unavailable evidence.
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumBytes) return new("tooLarge", Message: "Source exceeds the 8 MiB checksum limit.");
            using var hash = SHA256.Create();
            var buffer = new byte[32768];
            int read;
            while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.TransformBlock(buffer, 0, read, buffer, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Compare(Hex(hash.Hash!), expected);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new("unavailable", Message: ex.Message); }
    }

    internal static SourceChecksumComparison CompareEditor(SourceText text, IReadOnlyCollection<string> expected)
    {
        if (text.Length > MaximumBytes / 2) return new("tooLarge", Message: "Editor source exceeds the 4 Mi UTF-16 character checksum limit.");
        if (text.Encoding is null) return new("unavailable", Message: "The editor snapshot has no encoding; source bytes cannot be inferred safely.");
        try
        {
            var content = text.ToString();
            if ((long)text.Encoding.GetByteCount(content) + text.Encoding.GetPreamble().Length > MaximumBytes)
                return new("tooLarge", Message: "Encoded editor source exceeds the 8 MiB checksum limit.");
            // Roslyn uses SourceText checksums when emitting document records into portable PDBs.
            var snapshot = SourceText.From(content, text.Encoding, SourceHashAlgorithm.Sha256);
            var checksum = Hex(snapshot.GetChecksum().ToArray());
            var compared = Compare(checksum, expected);
            return compared with { TextVersion = TextContentVersion.Create(content), Encoding = text.Encoding.WebName };
        }
        catch (ArgumentException ex) { return new("unavailable", Message: ex.Message); }
    }

    private static SourceChecksumComparison Compare(string checksum, IReadOnlyCollection<string> expected) =>
        new(expected.Contains(checksum, StringComparer.OrdinalIgnoreCase) ? "match" : "mismatch", checksum);
}
