namespace NetVsMcp.Contracts;

public sealed record CoverageLine(int Number, long Hits);
public sealed record CoverageFile(string Path, int LinesCovered, int LinesValid, IReadOnlyList<CoverageLine> Lines);
public sealed record CoverageReport(string RunId, string Backend, string Outcome, int LinesCovered, int LinesValid,
    IReadOnlyList<CoverageFile> Files, bool Truncated = false, string? Message = null);
public sealed record CoverageResultPage(string RunId, string Outcome, int? RunnerExitCode, CoverageReport? Coverage,
    int Offset, int TotalFiles, int? NextOffset);
public sealed record CoverageLinePage(string RunId, string Path, int Offset, int TotalLines, int? NextOffset,
    IReadOnlyList<CoverageLine> Lines, bool Truncated);
