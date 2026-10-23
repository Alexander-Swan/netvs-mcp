using System;
using System.Threading;

namespace NetVsMcp.Vsix.Tests;

public class DebuggerFrameSelectorTests
{
    [Theory]
    [InlineData(null, "selected")]
    [InlineData(42, "worker")]
    public void ResolveThread_ReturnsRequestedObjectWithoutSelectingIt(int? threadId, string expected)
    {
        var selected = "selected";
        var target = DebuggerFrameSelector.ResolveThread(true, threadId, () => selected,
            id => id == 42 ? "worker" : null);
        Assert.Equal(expected, target);
        Assert.Equal("selected", selected);
    }

    [Fact]
    public void ResolveThread_RejectsRunningDebuggerBeforeAccessingThreads()
    {
        Assert.Throws<InvalidOperationException>(() => DebuggerFrameSelector.ResolveThread<string>(false, 42,
            () => throw new Exception("Should not access current thread"),
            _ => throw new Exception("Should not access threads")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveThread_RejectsInvalidThreadId(int id) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DebuggerFrameSelector.ResolveThread(true, id,
            () => "selected", _ => "worker"));

    [Fact]
    public void ResolveThread_MissingThreadNeverFallsBackToSelectedThread() =>
        Assert.Throws<InvalidOperationException>(() => DebuggerFrameSelector.ResolveThread<string>(true, 42,
            () => "selected", _ => null));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GetFrame_RejectsInvalidFrameBeforeReadingIt(int index) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DebuggerFrameSelector.GetFrame<string>(index,
            () => 2, _ => throw new Exception("Should not access frame")));

    [Fact]
    public void GetFrame_ResolvesCallerOnRequestedThread()
    {
        var selected = new[] { "selected top", "selected caller" };
        var worker = new[] { "worker top", "worker caller" };
        var thread = DebuggerFrameSelector.ResolveThread(true, 42, () => selected, _ => worker);
        var frame = DebuggerFrameSelector.GetFrame(2, () => thread.Length, index => thread[index - 1]);
        Assert.Equal("worker caller", frame);
        Assert.Equal("selected caller", selected[1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Select_UsesOneBasedIndexAndChangesInspectionContext(int frameIndex)
    {
        var frames = new[] { new { Name = "Callee", Local = 1 }, new { Name = "Caller", Local = 42 } };
        var current = frames[0];
        var selected = DebuggerFrameSelector.Select(true, frameIndex, () => frames.Length,
            index => frames[index - 1], frame => current = frame, CancellationToken.None);

        Assert.Same(frames[frameIndex - 1], selected);
        Assert.Same(selected, current);
        Assert.Equal(frames[frameIndex - 1].Local, current.Local);
    }

    [Fact]
    public void Select_RejectsRunningDebuggerBeforeReadingFrames()
    {
        var countRead = false;
        var changed = false;
        Assert.Throws<InvalidOperationException>(() => DebuggerFrameSelector.Select(false, 1,
            () => { countRead = true; return 1; }, _ => "frame", _ => changed = true, CancellationToken.None));
        Assert.False(countRead);
        Assert.False(changed);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(3, 2)]
    [InlineData(1, 0)]
    public void Select_InvalidIndexNeverChangesTheCurrentFrame(int frameIndex, int frameCount)
    {
        var frameRead = false;
        var changed = false;
        Assert.Throws<ArgumentOutOfRangeException>(() => DebuggerFrameSelector.Select(true, frameIndex,
            () => frameCount, _ => { frameRead = true; return "frame"; }, _ => changed = true, CancellationToken.None));
        Assert.False(frameRead);
        Assert.False(changed);
    }

    [Fact]
    public void Select_CancellationAfterReadingDoesNotChangeContext()
    {
        using var cancellation = new CancellationTokenSource();
        var changed = false;
        Assert.Throws<OperationCanceledException>(() => DebuggerFrameSelector.Select(true, 1, () => 1,
            _ => { cancellation.Cancel(); return "frame"; }, _ => changed = true, cancellation.Token));
        Assert.False(changed);
    }

    [Fact]
    public void Select_UnavailableFrameDoesNotChangeContext()
    {
        var changed = false;
        Assert.Throws<InvalidOperationException>(() => DebuggerFrameSelector.Select<string>(true, 1, () => 1,
            _ => throw new InvalidOperationException("Frame disappeared."), _ => changed = true, CancellationToken.None));
        Assert.False(changed);
    }

    [Fact]
    public void FrameMetadata_PreservesSelectionIndexInWirePayload()
    {
        var frame = new CallStackFrameInfo("Caller", null, 0, 0, 2, true);
        var json = System.Text.Json.JsonSerializer.Serialize(frame, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("frameIndex").GetInt32());
        Assert.True(document.RootElement.GetProperty("isCurrent").GetBoolean());
    }
}
