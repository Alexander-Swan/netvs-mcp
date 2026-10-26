using System;
using System.Linq;
using System.Xml.Linq;

namespace NetVsMcp.Vsix.Tests;

public class TrxResultReaderTests
{
    [Fact]
    public void PreservesParameterizedCaseIdentityAndFailureEvidence()
    {
        var document = XDocument.Parse("""
            <TestRun xmlns="urn:trx"><Results>
              <UnitTestResult testId="case-1" executionId="run-1" testName="Theory(value: 1)" outcome="Failed" duration="00:00:00.01">
                <Output><ErrorInfo><Message>Expected 2</Message><StackTrace>at Fixture.Theory():line 8</StackTrace></ErrorInfo></Output>
              </UnitTestResult>
              <UnitTestResult testId="case-2" executionId="run-2" testName="Theory(value: 2)" outcome="Passed" />
            </Results></TestRun>
            """);
        var results = TrxResultReader.Read(document).ToArray();
        Assert.Equal(2, results.Length);
        Assert.Equal("case-1", results[0].TestId);
        Assert.Equal("run-1", results[0].ExecutionId);
        Assert.Equal("Theory(value: 1)", results[0].Name);
        Assert.Contains("line 8", results[0].StackTrace);
        Assert.Equal("testsFailed", TrxResultReader.Classify(1, results));
    }

    [Theory]
    [InlineData(0, null, "noTests")]
    [InlineData(1, null, "runnerFailed")]
    [InlineData(0, "Passed", "completed")]
    [InlineData(1, "Passed", "runnerFailed")]
    [InlineData(1, "Failed", "testsFailed")]
    [InlineData(0, "Aborted", "incomplete")]
    public void SeparatesRunnerFailureFromTestFailureAndNoMatchingCases(int exit, string? outcome, string expected)
    {
        var results = outcome is null ? Array.Empty<TestResultInfo>() : new[] { new TestResultInfo("test", outcome, null, null) };
        Assert.Equal(expected, TrxResultReader.Classify(exit, results));
    }
}
