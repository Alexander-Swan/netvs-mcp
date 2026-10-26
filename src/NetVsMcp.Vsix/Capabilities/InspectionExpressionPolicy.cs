using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetVsMcp.Vsix;

internal static class InspectionExpressionPolicy
{
    // AllowAutoFuncEval controls implicit getters. Explicit calls/statements need their
    // own guard: the debugger may otherwise execute a caller-supplied invocation.
    public static bool IsInspectionOnly(string expression)
    {
        if (expression.Trim() == "$exception") return true;
        var parsed = SyntaxFactory.ParseExpression(expression);
        return !parsed.ContainsDiagnostics && Allowed(parsed);
    }

    private static bool Allowed(ExpressionSyntax node) => node switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        BaseExpressionSyntax => true,
        ParenthesizedExpressionSyntax parenthesized => Allowed(parenthesized.Expression),
        MemberAccessExpressionSyntax member => member.Name is IdentifierNameSyntax && Allowed(member.Expression),
        ElementAccessExpressionSyntax element => Allowed(element.Expression) &&
            System.Linq.Enumerable.All(element.ArgumentList.Arguments, arg =>
                arg.Expression is LiteralExpressionSyntax literal && literal.RawKind == (int)SyntaxKind.NumericLiteralExpression),
        _ => false
    };
}
