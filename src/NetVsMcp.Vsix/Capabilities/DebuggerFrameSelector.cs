using System;
using System.Threading;

namespace NetVsMcp.Vsix;

internal static class DebuggerFrameSelector
{
    public static bool IsCurrentFrame<TFrame>(TFrame frame, TFrame? currentFrame,
        bool isCurrentThread, Func<TFrame, uint?> getDepth) where TFrame : class
    {
        if (!isCurrentThread || currentFrame is null)
            return false;
        if (ReferenceEquals(frame, currentFrame))
            return true;
        // DTE can return distinct automation objects for the same frame. Depth is
        // unique within a thread, even when recursive frames share a function name.
        var depth = getDepth(frame);
        return depth.HasValue && depth == getDepth(currentFrame);
    }

    public static TThread ResolveThread<TThread>(bool isPaused, int? threadId,
        Func<TThread?> getCurrentThread, Func<int, TThread?> findThread) where TThread : class
    {
        if (!isPaused)
            throw new InvalidOperationException("The debugger must be paused before inspecting a specific thread or frame.");
        if (threadId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(threadId), "threadId must be 1 or greater.");
        return (threadId.HasValue ? findThread(threadId.Value) : getCurrentThread())
            ?? throw new InvalidOperationException(threadId.HasValue ? "Thread was not found." : "No current debug thread is available.");
    }

    public static TFrame GetFrame<TFrame>(int frameIndex, Func<int> getFrameCount, Func<int, TFrame> getFrame)
    {
        if (frameIndex < 1 || frameIndex > getFrameCount())
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "stackFrame must identify an available frame in the requested thread.");
        return getFrame(frameIndex);
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
