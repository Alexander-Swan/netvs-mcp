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
