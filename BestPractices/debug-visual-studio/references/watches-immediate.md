# Watches And Immediate Evaluation

```json
watch_add({ "expression": "order.Total", "sessionId": "..." })
watch_list({ "sessionId": "..." })
watch_remove({ "expression": "order.Total", "sessionId": "..." })
debug_snapshot({ "include": ["watch"], "watchExpressions": ["order.Total", "order.Items.Count"], "sessionId": "..." })
immediate_execute({ "statement": "someExpression", "sessionId": "..." })
```

`immediate_execute` uses Visual Studio expression evaluation rather than sending keystrokes. It only works while debugging.

`watch_add` evaluates immediately and therefore requires an active debug session. For one-off inspection, prefer snapshot `watchExpressions` so you do not modify the persistent watch list.

`watch_add`, `watch_list`, and `immediate_execute` accept optional `stackFrame` (1-based). Omit it to use the currently selected frame. When supplied, the debugger must be paused; the selected thread and frame remain unchanged:

```json
watch_add({ "expression": "order.Total", "stackFrame": 2, "sessionId": "..." })
watch_list({ "stackFrame": 2, "sessionId": "..." })
immediate_execute({ "statement": "order.Total", "stackFrame": 2, "sessionId": "..." })
```

Saved watch expressions are not bound to a particular frame. `watch_add.stackFrame` controls its initial evaluation; each later `watch_list` evaluates the expressions in that call's requested or current frame. `watch_remove` only removes the saved expression, so it does not take a frame.

Preserving the selected frame does not undo assignments, function calls, or other statement side effects. Use immediate execution that changes program state only when the user wants that change. Older VSIX versions return guidance when explicit frame context is unavailable; they do not silently evaluate in another frame.

The tools that accept `stackFrame` also accept optional `threadId`. With a thread ID, frame indices belong to that thread and default to 1. Inspection uses the target frame directly and leaves the debugger selection unchanged. Saved watches remain expression text, not bindings to a thread or frame; later queries use their own requested/current context. See `inspection.md` for examples and version requirements. Assignment and immediate-execution side effects persist.

Check `isValidValue` on returned watch expressions: a framework/native frame may have no evaluation context even when the call succeeds. Scoped `immediate_execute` reports `success: false` with the evaluation explanation in `output` in that case. Choose an explicit user-code frame from the target thread's call stack to retry; see `inspection.md`.
