using System;
using System.Threading;

namespace NetVsMcp.Vsix;

internal static class DebuggerFrameSelector
{
    public static int? FindIndex<TFrame>(TFrame selected, Func<int> getCount, Func<int, TFrame> getFrame,
        Func<TFrame, uint?> getDepth) where TFrame : class
    {
        try
        {
            var count = Math.Min(getCount(), 1024);
            for (var i = 1; i <= count; i++)
                if (IsCurrentFrame(getFrame(i), selected, true, getDepth)) return i;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or NotSupportedException or NotImplementedException) { }
        return null;
    }

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
        if (threadId.HasValue) return findThread(threadId.Value) ?? throw new InvalidOperationException("Thread was not found.");
        return RequireAvailable(getCurrentThread(), "current debug thread");
    }

    public static T RequireAvailable<T>(T? value, string description) where T : class => value ??
        throw new InspectionUnavailableException($"Visual Studio reports a pause but its {description} is unavailable. Inspect current state and wait for debugger frame readiness; do not repeat an execution command.");

    public static TFrame GetFrame<TFrame>(int frameIndex, Func<int> getFrameCount, Func<int, TFrame> getFrame) where TFrame : class
    {
        if (frameIndex < 1)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "stackFrame must identify an available frame in the requested thread.");
        var count = getFrameCount();
        if (count == 0) throw new InspectionUnavailableException("The paused debugger has not exposed stack frames. Wait for frame readiness without repeating execution.");
        if (frameIndex > count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "stackFrame must identify an available frame in the requested thread.");
        return RequireAvailable(getFrame(frameIndex), "requested stack frame");
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
