using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal static class CoberturaCoverageReader
{
    // These budgets bound retained RPC evidence independently of runner output.
    internal const int MaxLines = 50000;
    internal const int MaxFiles = 1000;

    public static CoverageReport Read(string runId, string directory)
    {
        try { return ReadCore(runId, directory); }
        catch (Exception ex) when (ex is XmlException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { return new(runId, "coverlet-cobertura", "invalidReport", 0, 0, [], false, "Coverage report could not be parsed: " + ex.Message); }
    }

    private static CoverageReport ReadCore(string runId, string directory)
    {
        var files = new Dictionary<string, SortedDictionary<int, long>>(StringComparer.OrdinalIgnoreCase);
        var truncated = false;
        var count = 0;
        var reports = Directory.EnumerateFiles(directory, "coverage.cobertura.xml", SearchOption.AllDirectories).Take(33).ToArray();
        if (reports.Length == 0) return new(runId, "coverlet-cobertura", "unavailable", 0, 0, [], false,
            "No Cobertura report was produced. Install coverlet.collector in the selected VSTest project; no package was installed automatically.");
        foreach (var path in reports.Take(32))
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
            var document = XDocument.Load(reader);
            if (document.Root?.Name.LocalName != "coverage") throw new InvalidDataException("Expected a Cobertura coverage document.");
            var sources = document.Descendants("source").Select(s => s.Value).Where(Path.IsPathRooted).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var type in document.Descendants("class"))
            {
                var name = (string?)type.Attribute("filename");
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (name!.Length > 4096) { truncated = true; continue; }
                // Keep relative names when multiple source roots make an absolute location ambiguous.
                if (!Path.IsPathRooted(name) && sources.Length == 1) name = Path.Combine(sources[0], name!);
                name = name!.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(name)) name = Path.GetFullPath(name);
                if (!files.TryGetValue(name!, out var lines))
                {
                    if (files.Count >= MaxFiles) { truncated = true; continue; }
                    files.Add(name!, lines = new());
                }
                foreach (var line in type.Element("lines")?.Elements("line") ?? Enumerable.Empty<XElement>())
                {
                    if (!int.TryParse((string?)line.Attribute("number"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 ||
                        !long.TryParse((string?)line.Attribute("hits"), NumberStyles.None, CultureInfo.InvariantCulture, out var hits))
                        throw new InvalidDataException("Coverage line number/hits are invalid.");
                    if (lines.TryGetValue(number, out var prior)) lines[number] = Math.Max(prior, hits);
                    else if (count < MaxLines) { lines.Add(number, hits); count++; }
                    else truncated = true;
                }
            }
        }
        var result = files.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => new CoverageFile(p.Key,
            p.Value.Count(l => l.Value > 0), p.Value.Count, p.Value.Select(l => new CoverageLine(l.Key, l.Value)).ToArray())).ToArray();
        truncated |= reports.Length > 32;
        return new(runId, "coverlet-cobertura", "collected", result.Sum(f => f.LinesCovered), result.Sum(f => f.LinesValid), result,
            truncated, truncated ? "Summary counts cover retained lines only; evidence exceeded the report budget." : "Line coverage merged by source path and line; branch coverage is not reported.");
    }
}
