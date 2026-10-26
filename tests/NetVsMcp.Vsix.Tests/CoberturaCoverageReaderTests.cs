using System;
using System.IO;
using System.Linq;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class CoberturaCoverageReaderTests
{
    [Fact]
    public void CoverageSurvivesRetentionAndWireSerialization()
    {
        var coverage = new NetVsMcp.Contracts.CoverageReport("run", "coverlet-cobertura", "collected", 1, 1, []);
        var original = new TestOperationResult(true, "ok", [], [], runId: "run", coverage: coverage);
        var store = new TestResultStore();
        var retained = store.Add(original, "project");
        Assert.Same(coverage, retained.Coverage);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(retained);
        var wire = Newtonsoft.Json.JsonConvert.DeserializeObject<NetVsMcp.Contracts.TestOperationResult>(json);
        Assert.NotNull(wire!.Coverage);
        Assert.Equal("run", wire.Coverage!.RunId);
    }

    [Fact]
    public void MergesDuplicateSourceLinesAndKeepsRunIdentity()
    {
        WithDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "coverage.cobertura.xml"), """
                <coverage><packages><package><classes>
                <class filename="Example.cs"><lines><line number="10" hits="0"/><line number="11" hits="2"/></lines></class>
                <class filename="Example.cs"><lines><line number="10" hits="1"/></lines></class>
                </classes></package></packages></coverage>
                """);
            var report = CoberturaCoverageReader.Read("run-1", directory);
            Assert.Equal("run-1", report.RunId);
            Assert.Equal("collected", report.Outcome);
            Assert.Equal(2, report.LinesCovered);
            Assert.Equal(2, report.LinesValid);
            Assert.Equal(1, report.Files.Single().Lines.First().Hits);
        });
    }

    [Theory]
    [InlineData("<!DOCTYPE coverage [<!ENTITY x SYSTEM 'file:///missing'>]><coverage>&x;</coverage>")]
    [InlineData("<coverage><packages><class filename='a'><lines><line number='-1' hits='0'/></lines></class></packages></coverage>")]
    public void InvalidOrExternalEntityReportsAreExplicit(string xml)
    {
        WithDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "coverage.cobertura.xml"), xml);
            Assert.Equal("invalidReport", CoberturaCoverageReader.Read("run", directory).Outcome);
        });
    }

    [Fact]
    public void MissingCollectorEvidenceIsUnavailable()
    { WithDirectory(directory => Assert.Equal("unavailable", CoberturaCoverageReader.Read("run", directory).Outcome)); }

    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "NetVsMcp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, true); }
    }
}
