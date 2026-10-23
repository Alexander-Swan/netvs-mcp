# Paused-State Inspection

Use while `debug_status` reports `dbgBreakMode`:

```json
debug_get_callstack({ "sessionId": "..." })
debug_get_locals({ "sessionId": "..." })
debug_evaluate({ "expression": "customer.Id", "timeoutMilliseconds": 5000, "sessionId": "..." })
debug_eval_many({ "expressions": ["order.Total", "order.Items.Count"], "sessionId": "..." })
debug_set_variable({ "name": "retryCount", "value": "3", "sessionId": "..." })
```

`debug_get_callstack` defaults to two frames. Pass `maxFrames` when you need deeper callers, and check `hasMore` to decide whether another, larger request is useful.

Updated VSIX versions return `frameIndex` (1-based) and `isCurrent` for each frame. To inspect a caller, pass its returned index as optional `stackFrame` on the existing inspection tools:

```json
debug_get_locals({ "stackFrame": 2, "sessionId": "..." })
debug_evaluate({ "expression": "customer.Id", "stackFrame": 2, "sessionId": "..." })
debug_eval_many({ "expressions": ["order.Total", "customer.Id"], "stackFrame": 2, "sessionId": "..." })
debug_snapshot({ "include": ["locals", "watch"], "watchExpressions": ["customer.Id"], "stackFrame": 2, "sessionId": "..." })
debug_set_variable({ "name": "retryCount", "value": "3", "stackFrame": 2, "sessionId": "..." })
```

Omit both `threadId` and `stackFrame` to inspect Visual Studio's currently selected frame; this does not assume frame 1. An explicit `stackFrame` indexes the current thread, or the thread identified by `threadId`. With `threadId` alone, frame 1 is inspected. Updated extensions resolve frame objects directly without changing the selected thread or frame. Execution stays paused and the instruction pointer does not move.

```json
debug_get_locals({ "threadId": 42, "stackFrame": 2, "sessionId": "..." })
debug_eval_many({ "threadId": 42, "expressions": ["queue.Count", "isCompleted"], "sessionId": "..." })
debug_snapshot({ "threadId": 42, "stackFrame": 2, "include": ["callStack", "locals", "watch"], "watchExpressions": ["queue.Count"], "sessionId": "..." })
```

Get thread IDs from `debug_get_threads` and frame indices from `thread_get_callstack`. Thread IDs refer to the current debug program. Invalid or unavailable threads/frames fail explicitly without falling back to the current selection. In `debug_snapshot` and `debug_wait_for_break`, `threadId` targets call stack, locals, and watches; `stackFrame` targets locals and watches. Other sections retain their normal scope. An empty include list fetches no inspection sections.

`debug_set_variable` also accepts `stackFrame` to target a variable in a caller. The variable change persists while the selected thread and frame remain unchanged. Use it only when the user wants runtime state changed. Watches and immediate execution support the same optional context; see `watches-immediate.md` for their behavior.

Refresh the call stack after stepping, continuing, or switching threads; previous indices may refer to different frames. Explicit frame inspection requires `dbgBreakMode` and an updated VSIX. Explicit thread inspection also requires an updated VSIX. Older versions receive guidance to omit `threadId`/`stackFrame` or update; the broker never silently returns another frame's values.

Use `debug_select_frame` only when you deliberately want to change the selected inspection frame for subsequent calls. Unlike the optional `stackFrame` argument, that explicit selection persists until another debugger action changes it.

```json
debug_select_frame({ "frameIndex": 2, "sessionId": "..." })
```

A valid frame can still lack an evaluation context, particularly in framework/native code. Check each returned expression's `isValidValue`, including entries in batches and snapshot watches. When the debugger returns no evaluation result, NetVsMcp preserves the requested expression name and returns `isValidValue: false` with an explanation in `value`. The enclosing inspection call can succeed while the expression is invalid. Read that thread's call stack and retry with an explicit user-code `stackFrame`; the broker does not silently evaluate in a different frame.

Before evaluating variables manually, check whether stepping or moving the breakpoint can expose their live runtime values. If expected locals are missing, execution may be paused before their declaration or initialization.

Expression evaluation runs code in the debuggee context. Avoid side effects unless the user explicitly wants state changed.

For many targeted values, prefer `debug_eval_many` or snapshot watches over pulling all locals.

Snapshot-style tools do not return locals by default. To see locals in `debug_snapshot` or `debug_wait_for_break`, pass `include: ["locals"]`; otherwise only the requested non-local categories, or the default call stack, are returned.

For loaded binaries and debugger symbol status, use `module_list` in break mode or include `modules` in `debug_snapshot` / `debug_wait_for_break`. Use `moduleOptions` for snapshot filters and diagnostics; see `modules-symbols.md` for symbol interpretation and Concord engine limits.

The implementation uses [`Thread.StackFrames`](https://learn.microsoft.com/en-us/dotnet/api/envdte.thread.stackframes?view=visualstudiosdk-2022), [`StackFrame.Locals`](https://learn.microsoft.com/en-us/dotnet/api/envdte.stackframe.locals?view=visualstudiosdk-2022), and [`Debugger5.GetExpression3`](https://learn.microsoft.com/en-us/dotnet/api/envdte100.debugger5.getexpression3?view=visualstudiosdk-2022). The latter accepts a frame from any thread of a process in break mode. Engines that cannot expose a requested frame return an error; an available frame without an evaluation context returns an invalid expression result as described above. Inspection does not fall back to changing selection.
