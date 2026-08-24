using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix.Tests;

/// <summary>
/// Covers <c>SolutionCapabilityService</c>'s pure TRX/`dotnet test` output parsing
/// (<c>ParseTrxResults</c>, <c>ParseListedTests</c>). Both methods are private, so tests reach them via reflection
/// instead of widening their visibility.
/// </summary>
public class SolutionCapabilityServiceHelperTests
{
    private static readonly Type ServiceType = typeof(SolutionCapabilityService);

    private static IReadOnlyCollection<TestCaseInfo> ParseListedTests(string output, string? projectName)
    {
        var method = ServiceType.GetMethod("ParseListedTests", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ParseListedTests method not found.");
        return (IReadOnlyCollection<TestCaseInfo>)method.Invoke(null, new object?[] { output, projectName })!;
    }

    private static IReadOnlyCollection<TestResultInfo> ParseTrxResults(string resultPath)
    {
        var method = ServiceType.GetMethod("ParseTrxResults", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ParseTrxResults method not found.");
        return (IReadOnlyCollection<TestResultInfo>)method.Invoke(null, new object?[] { resultPath })!;
    }

    [Fact]
    public void ParseListedTests_ExtractsNamesBetweenHeaderAndSummary()
    {
        var output = """
The following Tests are available:
NetVsMcp.Vsix.Tests.FooTests.Bar_DoesX
NetVsMcp.Vsix.Tests.FooTests.Baz_DoesY

Passed!
""";

        var result = ParseListedTests(output, "NetVsMcp.Vsix.Tests");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "NetVsMcp.Vsix.Tests.FooTests.Bar_DoesX");
        Assert.Contains(result, t => t.Name == "NetVsMcp.Vsix.Tests.FooTests.Baz_DoesY");
        Assert.All(result, t => Assert.Equal("NetVsMcp.Vsix.Tests", t.ProjectName));
    }

    [Fact]
    public void ParseListedTests_IgnoresLinesBeforeHeader()
    {
        var output = """
Determining projects to restore...
Restored ...
The following Tests are available:
Some.Test.Name
""";

        var result = ParseListedTests(output, null);

        Assert.Single(result);
        Assert.Equal("Some.Test.Name", result.Single().Name);
    }

    [Fact]
    public void ParseListedTests_NoHeader_ReturnsEmpty()
    {
        var output = "Just some random dotnet output\nwith no test list header\n";

        var result = ParseListedTests(output, null);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseListedTests_StopsAtFailedBanner()
    {
        var output = """
The following Tests are available:
Test.One
Test.Two
Failed!  - Failed: 1
""";

        var result = ParseListedTests(output, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, t => t.Name.StartsWith("Failed!"));
    }

    [Fact]
    public void ParseListedTests_SkipsBlankLines()
    {
        var output = "The following Tests are available:\n\nTest.One\n\n\nTest.Two\n";

        var result = ParseListedTests(output, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseTrxResults_ExtractsNameOutcomeDurationAndMessage()
    {
        var trx = """
<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="Foo.Bar_Passes" testId="a1" outcome="Passed" duration="00:00:00.123" />
    <UnitTestResult testName="Foo.Bar_Fails" testId="a2" outcome="Failed" duration="00:00:00.456">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() failure</Message>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
  </Results>
</TestRun>
""";
        var path = Path.Combine(Path.GetTempPath(), $"netvsmcp-test-{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, trx);
        try
        {
            var result = ParseTrxResults(path);

            Assert.Equal(2, result.Count);

            var passed = result.Single(r => r.Name == "Foo.Bar_Passes");
            Assert.Equal("Passed", passed.Outcome);
            Assert.Equal("00:00:00.123", passed.Duration);
            Assert.Null(passed.Message);

            var failed = result.Single(r => r.Name == "Foo.Bar_Fails");
            Assert.Equal("Failed", failed.Outcome);
            Assert.Equal("Assert.Equal() failure", failed.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseTrxResults_MissingTestName_FallsBackToTestId()
    {
        var trx = """
<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="only-id-42" outcome="Passed" />
  </Results>
</TestRun>
""";
        var path = Path.Combine(Path.GetTempPath(), $"netvsmcp-test-{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, trx);
        try
        {
            var result = ParseTrxResults(path);

            Assert.Single(result);
            Assert.Equal("only-id-42", result.Single().Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseTrxResults_NoResults_ReturnsEmpty()
    {
        var trx = """
<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
  </Results>
</TestRun>
""";
        var path = Path.Combine(Path.GetTempPath(), $"netvsmcp-test-{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, trx);
        try
        {
            var result = ParseTrxResults(path);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
