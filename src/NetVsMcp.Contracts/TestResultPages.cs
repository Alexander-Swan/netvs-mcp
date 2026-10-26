namespace NetVsMcp.Contracts;

public sealed class TestResultsPageRequest
{
    public string RunId { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int MaxItems { get; set; } = 25;
}

public sealed record TestResultPage(string RunId, string Backend, string? Outcome, int? RunnerExitCode,
    int Offset, int TotalResults, int RetainedResults, int? NextOffset, bool ResultsTruncated,
    DateTimeOffset CapturedUtc, IReadOnlyCollection<TestResultInfo> Results);
