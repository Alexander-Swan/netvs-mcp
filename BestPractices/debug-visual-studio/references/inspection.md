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

Omit `stackFrame` to inspect Visual Studio's currently selected frame, preserving existing behavior; this does not assume frame 1. An explicit `stackFrame` is used temporarily for the inspection, and the original selection is restored even if evaluation fails. It applies to locals and watches in `debug_snapshot` and `debug_wait_for_break`; other snapshot sections retain their normal scope. Execution stays paused and the instruction pointer does not move.

`debug_set_variable` also accepts `stackFrame` to target a variable in a caller. The variable change persists even though the inspection-frame selection is restored. Use it only when the user wants runtime state changed. Watches and immediate execution support the same optional context; see `watches-immediate.md` for their behavior.

Refresh the call stack after stepping, continuing, or switching threads; previous indices may refer to different frames. Explicit frame inspection requires `dbgBreakMode` and an updated VSIX. Older versions receive guidance to omit `stackFrame` or update; the broker never silently returns another frame's values.

Use `debug_select_frame` only when you deliberately want to change the selected inspection frame for subsequent calls. Unlike the optional `stackFrame` argument, that explicit selection persists until another debugger action changes it.

Before evaluating variables manually, check whether stepping or moving the breakpoint can expose their live runtime values. If expected locals are missing, execution may be paused before their declaration or initialization.

Expression evaluation runs code in the debuggee context. Avoid side effects unless the user explicitly wants state changed.

For many targeted values, prefer `debug_eval_many` or snapshot watches over pulling all locals.

Snapshot-style tools do not return locals by default. To see locals in `debug_snapshot` or `debug_wait_for_break`, pass `include: ["locals"]`; otherwise only the requested non-local categories, or the default call stack, are returned.

NetVsMcp no longer exposes a module-list tool. Do not call `module_list` or request `modules` from `debug_snapshot`; use the current debugger state, call stack, threads, exceptions, and targeted evaluations instead.
