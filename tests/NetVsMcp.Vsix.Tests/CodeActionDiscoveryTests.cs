using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace NetVsMcp.Vsix.Tests;

public class CodeActionDiscoveryTests
{
    [Fact]
    public void EmptyCaretSelectionFindsTheContainingDiagnostic()
    {
        Assert.True(CodeActionDiscovery.Intersects(new TextSpan(38, 3), new TextSpan(38, 0)));
        Assert.True(CodeActionDiscovery.Intersects(new TextSpan(38, 3), new TextSpan(40, 0)));
        Assert.False(CodeActionDiscovery.Intersects(new TextSpan(38, 3), new TextSpan(41, 0)));
    }

    [Fact]
    public void NestedNonInteractiveLeavesRetainTheirOriginalActionObjects()
    {
        var leaf = CodeAction.Create("original fix", _ => Task.FromResult<Solution>(null!));
        var group = CodeAction.Create("fix group", ImmutableArray.Create(leaf), isInlinable: false);
        var actions = CodeActionDiscovery.Flatten(new[] { group }, out var truncated);
        Assert.Same(leaf, Assert.Single(actions));
        Assert.False(truncated);
    }

    [Fact]
    public void ActionListsAreBoundedWithExplicitTruncation()
    {
        var input = Enumerable.Range(0, 150).Select(i => CodeAction.Create(i.ToString(), _ => Task.FromResult<Solution>(null!)));
        Assert.Equal(100, CodeActionDiscovery.Flatten(input, out var truncated).Count);
        Assert.True(truncated);
    }
}
