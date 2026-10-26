using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix.Tests;

public class BoundedDebugExpansionTests
{
    [Theory]
    [InlineData("root.Items[0].Name", true)]
    [InlineData("this.field", true)]
    [InlineData("$exception", true)]
    [InlineData("root.Mutate()", false)]
    [InlineData("root.Items[GetIndex()]", false)]
    [InlineData("root.Field = 1", false)]
    [InlineData("root.Count++", false)]
    [InlineData("new Thing()", false)]
    public void ExplicitEvaluationRequiresOptIn(string expression, bool allowed) =>
        Assert.Equal(allowed, InspectionExpressionPolicy.IsInspectionOnly(expression));

    private sealed class FakeValue(string name, params IDebugValue[] children) : IDebugValue
    {
        public string Name => name;
        public string Type => "fixture";
        public string Value => new string('x', 100);
        public bool IsValid => true;
        public int ChildCount => children.Length;
        public IDebugValue Child(int index) => children[index - 1];
    }

    [Fact]
    public void ChildPagesHaveStablePathsWithinTheSamePause()
    {
        var root = new FakeValue("root", new FakeValue("one"), new FakeValue("two"), new FakeValue("three"));
        var result = BoundedDebugExpansion.Expand(root, new() { Expression = "root", MaxChildren = 1, ChildOffset = 1 }, default);
        Assert.Equal("two", Assert.Single(result.Root.Children).Name);
        Assert.Equal(new[] { 2 }, result.Root.Children.Single().MemberPath);
        Assert.Equal(2, result.Root.NextOffset);
        Assert.Contains("childLimit", result.Root.TruncationReason);
    }

    [Fact]
    public void CyclesCannotExceedDepthOrNodeBudgets()
    {
        var root = new FakeValue("root", new FakeValue("one", new FakeValue("nested")), new FakeValue("two"));
        var result = BoundedDebugExpansion.Expand(root, new() { Expression = "root", MaxNodes = 2, MaxDepth = 4 }, default);
        Assert.Equal(2, result.VisitedNodes);
        Assert.Single(result.Root.Children);
        Assert.Contains("nodeLimit", result.Root.TruncationReason);
        var shallow = BoundedDebugExpansion.Expand(root, new() { Expression = "root", MaxDepth = 0 }, default);
        Assert.Empty(shallow.Root.Children);
        Assert.Equal(0, shallow.Root.NextOffset);
        Assert.Contains("depthLimit", shallow.Root.TruncationReason);
    }

    [Fact]
    public void ValuesAreBoundedAndEvaluationPolicyIsReturned()
    {
        var result = BoundedDebugExpansion.Expand(new FakeValue("root"), new() { Expression = "root", MaxValueCharacters = 64 }, default);
        Assert.Equal(64, result.Root.Value!.Length);
        Assert.Contains("valueLimit", result.Root.TruncationReason);
        Assert.False(result.AllowFunctionEvaluation);
    }

    [Fact]
    public void ElapsedBudgetPreventsMoreEngineCalls()
    {
        var elapsed = Stopwatch.StartNew();
        Thread.Sleep(10);
        var result = BoundedDebugExpansion.Expand(new FakeValue("root"), new() { Expression = "root", BudgetMilliseconds = 1 }, default, elapsed);
        Assert.Null(result.Root.Name);
        Assert.Equal("timeBudget", result.Root.TruncationReason);
    }

    [Fact]
    public void CancellationAndInvalidLimitsAreExplicit()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => BoundedDebugExpansion.Expand(new FakeValue("root"), new() { Expression = "root" }, cancel.Token));
        Assert.Throws<ArgumentException>(() => BoundedDebugExpansion.Expand(new FakeValue("root"), new() { Expression = "root", MaxDepth = 100 }, default));
    }
}
