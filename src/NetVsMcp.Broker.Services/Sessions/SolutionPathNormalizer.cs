using System.IO;

namespace NetVsMcp.Broker.Services;

internal static class SolutionPathNormalizer
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var trimmed = path.Trim();

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            return TrimTrailingSeparators(fullPath);
        }
        catch (ArgumentException)
        {
            return NormalizeSeparators(trimmed);
        }
        catch (NotSupportedException)
        {
            return NormalizeSeparators(trimmed);
        }
        catch (PathTooLongException)
        {
            return NormalizeSeparators(trimmed);
        }
    }

    private static string NormalizeSeparators(string path)
    {
        return TrimTrailingSeparators(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
    }

    private static string TrimTrailingSeparators(string path)
    {
        var normalized = NormalizeSeparatorsOnly(path);
        var root = Path.GetPathRoot(normalized);

        while (normalized.Length > root?.Length
               && normalized.EndsWith(Path.DirectorySeparatorChar))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static string NormalizeSeparatorsOnly(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}
