using System;
using System.Threading;

namespace NetVsMcp.Vsix;

internal static class DebuggerFrameSelector
{
    public static TResult Inspect<TFrame, TResult>(bool isPaused, int frameIndex, Func<TFrame> getCurrentFrame,
        Func<int> getFrameCount, Func<int, TFrame> getFrame, Action<TFrame> selectFrame,
        Func<TResult> inspect, CancellationToken cancellationToken) where TFrame : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frameIndex < 1)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "stackFrame must be 1 or greater.");
        if (!isPaused)
            throw new InvalidOperationException("The debugger must be paused before inspecting a specific stack frame.");
        var original = getCurrentFrame();
        var selectionAttempted = false;
        try
        {
            Select(isPaused, frameIndex, getFrameCount, getFrame, frame =>
            {
                selectionAttempted = true;
                selectFrame(frame);
            }, cancellationToken);
            return inspect();
        }
        finally
        {
            // Restoration must also run when evaluation throws or the request is canceled.
            if (selectionAttempted)
                selectFrame(original);
        }
    }

    public static T Select<T>(bool isPaused, int frameIndex, Func<int> getFrameCount,
        Func<int, T> getFrame, Action<T> selectFrame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (frameIndex < 1)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "frameIndex must be 1 or greater.");
        if (!isPaused)
            throw new InvalidOperationException("The debugger must be paused before selecting a stack frame.");
        var count = getFrameCount();
        if (frameIndex > count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), $"The current thread has {count} stack frame(s). Refresh debug_get_callstack and select a valid frameIndex.");
        var frame = getFrame(frameIndex);
        cancellationToken.ThrowIfCancellationRequested();
        selectFrame(frame);
        return frame;
    }
}
