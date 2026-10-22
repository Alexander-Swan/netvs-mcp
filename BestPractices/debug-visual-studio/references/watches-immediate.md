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

`watch_add`, `watch_list`, and `immediate_execute` accept optional `stackFrame` (1-based). Omit it to use the currently selected frame. When supplied, the debugger must be paused; the original selection is restored after the operation:

```json
watch_add({ "expression": "order.Total", "stackFrame": 2, "sessionId": "..." })
watch_list({ "stackFrame": 2, "sessionId": "..." })
immediate_execute({ "statement": "order.Total", "stackFrame": 2, "sessionId": "..." })
```

Saved watch expressions are not bound to a particular frame. `watch_add.stackFrame` controls its initial evaluation; each later `watch_list` evaluates the expressions in that call's requested or current frame. `watch_remove` only removes the saved expression, so it does not take a frame.

Restoring the selected frame does not undo assignments, function calls, or other statement side effects. Use immediate execution that changes program state only when the user wants that change. Older VSIX versions return guidance when explicit frame context is unavailable; they do not silently evaluate in another frame.
