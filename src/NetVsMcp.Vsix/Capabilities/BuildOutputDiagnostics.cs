using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetVsMcp.Contracts;
using Diagnostic = NetVsMcp.Contracts.ErrorListItemInfo;

namespace NetVsMcp.Vsix;

internal static class BuildOutputDiagnostics
{
    private static readonly Regex DiagnosticLine = new(
        @"^\s*(?:\d+>)?\s*(?:(?<origin>.+?)\s*:\s*)?(?:fatal\s+)?(?<level>error|warning)\s*(?<code>[A-Za-z]+\d+)?\s*:\s*(?<message>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex FileLocation = new(
        @"^(?<file>.+?)\((?<line>\d+)(?:,(?<column>\d+)(?:,\d+,\d+)?)?\)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex ProjectSuffix = new(
        @"\s+\[(?<project>[^\[\]]+\.(?:[A-Za-z]*proj))\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    internal static BuildDiagnosticsCapture Read(string? before, string? after, int clears, int failedProjects, ErrorListRequest request)
    {
        if (after is null || clears > 1)
            return Unavailable("The Build output pane could not be captured for this build.");

        string output;
        if (clears == 1)
            output = after;
        else if (before is not null && after.StartsWith(before, StringComparison.Ordinal))
            output = after.Substring(before.Length);
        else
            return Unavailable("The Build output pane changed without an observed clear; its contents could not be attributed to this build.");

        if (string.IsNullOrWhiteSpace(output))
            return Unavailable("Visual Studio supplied no fresh Build output for this build.");

        var errors = new List<Diagnostic>();
        var warnings = new List<Diagnostic>();
        var maximum = Math.Max(1, request.MaxItems);
        var truncated = false;
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            Diagnostic? diagnostic;
            try { diagnostic = Parse(line); }
            catch (RegexMatchTimeoutException) { truncated = true; continue; }
            if (diagnostic is null) continue;
            var destination = diagnostic.Level == "Error" ? errors : warnings;
            if (destination == warnings && !request.IncludeWarnings) continue;
            if (destination.Count < maximum) destination.Add(diagnostic);
            else truncated = true;
        }

        if (failedProjects != 0 && errors.Count == 0)
            return new BuildDiagnosticsCapture(warnings, truncated,
                "The build did not report success, but its Build output contained no recognizable error diagnostics.");

        return new BuildDiagnosticsCapture(errors.Concat(warnings).Take(maximum).ToArray(),
            truncated || (long)errors.Count + warnings.Count > maximum);
    }

    private static BuildDiagnosticsCapture Unavailable(string message) => new(Array.Empty<Diagnostic>(), Unavailable: message);

    private static Diagnostic? Parse(string line)
    {
        var entry = DiagnosticLine.Match(line);
        if (!entry.Success) return null;
        var origin = entry.Groups["origin"].Value.Trim();
        var location = FileLocation.Match(origin);
        var file = location.Success ? location.Groups["file"].Value : null;
        var message = entry.Groups["message"].Value.Trim();
        var suffix = ProjectSuffix.Match(message);
        var project = suffix.Success ? suffix.Groups["project"].Value : null;
        if (suffix.Success) message = message.Substring(0, suffix.Index);
        var code = entry.Groups["code"].Value;
        if (code.Length != 0) message = code + ": " + message;
        if (file is null && origin.Length != 0)
        {
            if (origin.IndexOfAny(new[] { '\\', '/' }) >= 0 || origin.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                file = origin;
            else
                message = origin + ": " + message;
        }
        return new Diagnostic(message, file, Coordinate(location, "line"), Coordinate(location, "column"),
            entry.Groups["level"].Value.Equals("error", StringComparison.OrdinalIgnoreCase) ? "Error" : "Warning", project);
    }

    private static int Coordinate(Match location, string name) =>
        int.TryParse(location.Groups[name].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
