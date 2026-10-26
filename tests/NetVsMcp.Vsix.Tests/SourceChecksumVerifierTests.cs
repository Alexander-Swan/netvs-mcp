using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class SourceChecksumVerifierTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EditorChecksumIncludesOriginalEncodingPreambleAndLineEndings(bool bom, bool utf16)
    {
        var encoding = utf16 ? (Encoding)new UnicodeEncoding(false, bom) : new UTF8Encoding(bom);
        const string code = "// héllo\r\nclass C {}\r\n";
        var bytes = Combine(encoding.GetPreamble(), encoding.GetBytes(code));
        using var hash = SHA256.Create();
        var expected = SourceChecksumVerifier.Hex(hash.ComputeHash(bytes));
        var text = SourceText.From(code, encoding);
        var match = SourceChecksumVerifier.CompareEditor(text, new[] { expected });
        Assert.Equal("match", match.Status);
        Assert.NotNull(match.TextVersion);
        Assert.Equal("mismatch", SourceChecksumVerifier.CompareEditor(SourceText.From(code + "// unsaved", encoding), new[] { expected }).Status);
        Assert.Equal("mismatch", SourceChecksumVerifier.CompareEditor(SourceText.From(code.Replace("\r\n", "\n"), encoding), new[] { expected }).Status);
    }

    [Fact]
    public void MissingEncodingAndOversizedEditorAreNotGuessed()
    {
        Assert.Equal("unavailable", SourceChecksumVerifier.CompareEditor(SourceText.From("text"), Array.Empty<string>()).Status);
        Assert.Equal("tooLarge", SourceChecksumVerifier.CompareEditor(SourceText.From(new string('x', SourceChecksumVerifier.MaximumBytes / 2 + 1), Encoding.UTF8), Array.Empty<string>()).Status);
    }

    [Fact]
    public void DiskComparisonMatchesExactBytesAndSupportsAlternateExpectedChecksums()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(file, new byte[] { 0xef, 0xbb, 0xbf, 65, 13, 10 });
            using var hash = SHA256.Create();
            var expected = SourceChecksumVerifier.Hex(hash.ComputeHash(File.ReadAllBytes(file)));
            Assert.Equal("match", SourceChecksumVerifier.CompareDisk(file, new[] { "alternate", expected }, CancellationToken.None).Status);
            File.AppendAllText(file, "changed");
            Assert.Equal("mismatch", SourceChecksumVerifier.CompareDisk(file, new[] { expected }, CancellationToken.None).Status);
            using var locked = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None);
            Assert.Equal("unavailable", SourceChecksumVerifier.CompareDisk(file, new[] { expected }, CancellationToken.None).Status);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void MissingRelativeAndOversizedFilesRemainExplicit()
    {
        Assert.Equal("unavailable", SourceChecksumVerifier.CompareDisk("relative.cs", Array.Empty<string>(), CancellationToken.None).Status);
        Assert.Equal("unavailable", SourceChecksumVerifier.CompareDisk(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), Array.Empty<string>(), CancellationToken.None).Status);
        var file = Path.GetTempFileName();
        try
        {
            using (var large = new FileStream(file, FileMode.Open)) large.SetLength(SourceChecksumVerifier.MaximumBytes + 1L);
            Assert.Equal("tooLarge", SourceChecksumVerifier.CompareDisk(file, Array.Empty<string>(), CancellationToken.None).Status);
        }
        finally { File.Delete(file); }
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
}
