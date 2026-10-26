using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetVsMcp.Vsix;

internal static class TrxResultReader
{
    public static IReadOnlyCollection<TestResultInfo> Read(XDocument document) => document.Descendants()
        .Where(element => element.Name.LocalName == "UnitTestResult")
        .Select(element => new TestResultInfo(
            element.Attribute("testName")?.Value ?? element.Attribute("testId")?.Value ?? "Unknown",
            element.Attribute("outcome")?.Value ?? "Unknown", element.Attribute("duration")?.Value,
            element.Descendants().FirstOrDefault(child => child.Name.LocalName == "Message")?.Value,
            element.Attribute("testId")?.Value, element.Attribute("executionId")?.Value,
            element.Descendants().FirstOrDefault(child => child.Name.LocalName == "StackTrace")?.Value)).ToArray();

    public static string Classify(int exitCode, IReadOnlyCollection<TestResultInfo> results)
    {
        if (results.Any(r => string.Equals(r.Outcome, "Failed", StringComparison.OrdinalIgnoreCase))) return "testsFailed";
        if (exitCode != 0) return "runnerFailed";
        if (results.Count == 0) return "noTests";
        if (results.Any(r => !string.Equals(r.Outcome, "Passed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(r.Outcome, "Skipped", StringComparison.OrdinalIgnoreCase))) return "incomplete";
        return "completed";
    }
}
