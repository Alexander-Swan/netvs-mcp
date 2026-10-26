namespace NetVsMcp.Contracts;

public sealed class DebugExpansionRequest
{
    public string Expression { get; set; } = string.Empty;
    public int[] MemberPath { get; set; } = [];
    public int ChildOffset { get; set; }
    public int MaxChildren { get; set; } = 20;
    public int MaxDepth { get; set; } = 1;
    public int MaxNodes { get; set; } = 100;
    public int MaxValueCharacters { get; set; } = 1024;
    public int BudgetMilliseconds { get; set; } = 1000;
    public bool AllowFunctionEvaluation { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Expression) || Expression.Length > 4096 || MemberPath is null || MemberPath.Length > 8 || MemberPath.Any(i => i < 1) ||
            ChildOffset < 0 || MaxChildren is < 1 or > 100 || MaxDepth is < 0 or > 4 || MaxNodes is < 1 or > 1000 ||
            MaxValueCharacters is < 64 or > 4096 || BudgetMilliseconds is < 1 or > 10000)
            throw new ArgumentException("Invalid expansion limits: expression 1–4096 characters, memberPath up to 8 positive indices, nonnegative offset, children 1–100, depth 0–4, nodes 1–1000, value characters 64–4096, budget 1–10000ms.");
    }
}

public sealed record DebugValueNode(string? Name, string? Type, string? Value, bool IsValid,
    IReadOnlyCollection<int> MemberPath, IReadOnlyCollection<DebugValueNode> Children,
    int? NextOffset, string? TruncationReason);

public sealed record DebugExpansionResult(DebugValueNode Root, int VisitedNodes, long ElapsedMilliseconds,
    bool AllowFunctionEvaluation, string BudgetSemantics = "Cooperative: a synchronous debugger-engine call may exceed the budget.");
