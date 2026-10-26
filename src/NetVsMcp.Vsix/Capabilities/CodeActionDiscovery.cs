using System.Collections.Generic;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace NetVsMcp.Vsix;

internal static class CodeActionDiscovery
{
    public static bool Intersects(TextSpan diagnostic, TextSpan selection) => selection.IsEmpty
        ? diagnostic.Contains(selection.Start) || diagnostic.IsEmpty && diagnostic.Start == selection.Start
        : diagnostic.IntersectsWith(selection);

    public static IReadOnlyList<CodeAction> Flatten(IEnumerable<CodeAction> actions, out bool truncated)
    {
        var leaves = new List<CodeAction>();
        var limited = false;
        void Visit(CodeAction action, int depth)
        {
            if (depth > 8 || leaves.Count >= 100) { limited = true; return; }
            if (action is CodeActionWithOptions) return; // Requires interactive options, not a prepared solution.
            if (action.NestedActions.IsDefaultOrEmpty) leaves.Add(action);
            else foreach (var child in action.NestedActions) Visit(child, depth + 1);
        }
        foreach (var action in actions) Visit(action, 0);
        truncated = limited;
        return leaves;
    }
}
