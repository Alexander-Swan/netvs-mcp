using System;
using System.Threading;

namespace NetVsMcp.Vsix.Tests;

public class DebuggerFrameSelectorTests
{
    [Fact]
    public void Inspect_UsesRequestedFrameAndRestoresThePreviouslySelectedCaller()
    {
        var frames = new[] { "top", "caller", "previous selection" };
        var current = frames[2];
        var result = DebuggerFrameSelector.Inspect(true, 2, () => current, () => frames.Length,
            index => frames[index - 1], frame => current = frame, () => current, CancellationToken.None);
        Assert.Equal("caller", result);
        Assert.Equal("previous selection", current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inspect_RestoresSelectionAfterFailureOrCancellation(bool cancel)
    {
        var frames = new[] { "top", "caller" };
        var current = frames[0];
        using var cancellation = new CancellationTokenSource();
        var failure = Assert.ThrowsAny<Exception>(() => DebuggerFrameSelector.Inspect<string, string>(true, 2,
            () => current, () => frames.Length, index => frames[index - 1], frame => current = frame,
            () =>
            {
                Assert.Equal("caller", current);
                if (cancel) { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
                throw new InvalidOperationException("Inspection failed.");
            }, cancellation.Token));
        if (cancel) Assert.IsType<OperationCanceledException>(failure);
        else Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("top", current);
    }

    [Fact]
    public void Inspect_OutOfRangeDoesNotAttemptSelectionOrInspection()
    {
        var changed = false;
        var inspected = false;
        Assert.Throws<ArgumentOutOfRangeException>(() => DebuggerFrameSelector.Inspect(true, 3,
            () => "current", () => 1, _ => "frame", _ => changed = true,
            () => { inspected = true; return "value"; }, CancellationToken.None));
        Assert.False(changed);
        Assert.False(inspected);
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
