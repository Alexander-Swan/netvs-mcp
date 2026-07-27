using System;
using System.IO;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal static class DocumentPathResolver
{
    public static string Resolve(DTE? dte, string? documentPath, bool allowActiveDocument = false, string? parameterName = null)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var path = string.IsNullOrWhiteSpace(documentPath)
            ? allowActiveDocument ? dte?.ActiveDocument?.FullName : null
            : documentPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                allowActiveDocument
                    ? "Document path is required when there is no active document."
                    : "Document path is required.",
                parameterName ?? nameof(documentPath));
        }

        var normalizedPath = NormalizeIncomingPath(path!);
        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        var solutionPath = dte?.Solution?.FullName;
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath);
        return Path.GetFullPath(Path.Combine(solutionDirectory ?? Environment.CurrentDirectory, normalizedPath));
    }

    public static string? ResolveOptional(DTE? dte, string? documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return null;
        }

        return Resolve(dte, documentPath);
    }

    private static string NormalizeIncomingPath(string path)
    {
        var repaired = path.Trim()
            .Replace("\r", @"\r")
            .Replace("\n", @"\n")
            .Replace("\t", @"\t")
            .Replace("\b", @"\b")
            .Replace("\f", @"\f")
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return CollapseRepeatedDirectorySeparators(repaired);
    }

    private static string CollapseRepeatedDirectorySeparators(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var prefixLength = path.StartsWith(@"\\", StringComparison.Ordinal) ? 2 : 0;
        var builder = new StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            var current = path[i];
            if (current == Path.DirectorySeparatorChar
                && i >= prefixLength
                && builder.Length > prefixLength
                && builder[builder.Length - 1] == Path.DirectorySeparatorChar)
            {
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
