using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal interface IDebugValue
{
    string? Name { get; }
    string? Type { get; }
    string? Value { get; }
    bool IsValid { get; }
    int ChildCount { get; }
    IDebugValue Child(int index);
}

internal static class BoundedDebugExpansion
{
    public static DebugExpansionResult Expand(IDebugValue root, DebugExpansionRequest request, CancellationToken cancellationToken,
        Stopwatch? elapsed = null)
    {
        request.Validate();
        var clock = elapsed ?? Stopwatch.StartNew();
        var count = 0;
        bool OutOfTime() => clock.ElapsedMilliseconds >= request.BudgetMilliseconds;
        DebugValueNode Read(IDebugValue value, int depth, int[] path, int offset)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            var reasons = new List<string>();
            string? Clip(string? text)
            {
                if (text is null || text.Length <= request.MaxValueCharacters) return text;
                reasons.Add("valueLimit");
                return text.Substring(0, request.MaxValueCharacters);
            }
            string? name = null, type = null, text = null;
            var valid = false;
            var children = new List<DebugValueNode>();
            int? next = null;
            try
            {
                if (OutOfTime()) reasons.Add("timeBudget");
                else
                {
                    name = Clip(value.Name); type = Clip(value.Type); text = Clip(value.Value); valid = value.IsValid;
                    if (OutOfTime()) reasons.Add("timeBudget");
                    else if (valid)
                    {
                        var total = value.ChildCount;
                        if (depth >= request.MaxDepth && total > offset) { next = offset; reasons.Add("depthLimit"); }
                        else
                        {
                            var index = offset;
                            while (index < total)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (count >= request.MaxNodes) { reasons.Add("nodeLimit"); break; }
                                if (children.Count >= request.MaxChildren) { reasons.Add("childLimit"); break; }
                                if (OutOfTime()) { reasons.Add("timeBudget"); break; }
                                var child = value.Child(index + 1);
                                children.Add(Read(child, depth + 1, path.Concat(new[] { index + 1 }).ToArray(), 0));
                                index++;
                            }
                            if (index < total) next = index;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { valid = false; text = Clip(ex.Message); reasons.Add("engineError"); }
            return new(name, type, text, valid, path, children, next, reasons.Count == 0 ? null : string.Join(",", reasons.Distinct()));
        }
        var result = Read(root, 0, request.MemberPath, request.ChildOffset);
        return new(result, count, clock.ElapsedMilliseconds, request.AllowFunctionEvaluation);
    }
}
