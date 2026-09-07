# Paused-State Inspection

Use while `debug_status` reports `dbgBreakMode`:

```json
debug_get_callstack({ "sessionId": "..." })
debug_get_locals({ "sessionId": "..." })
debug_evaluate({ "expression": "customer.Id", "timeoutMilliseconds": 5000, "sessionId": "..." })
debug_eval_many({ "expressions": ["order.Total", "order.Items.Count"], "sessionId": "..." })
debug_set_variable({ "name": "retryCount", "value": "3", "sessionId": "..." })
```

Expression evaluation runs code in the debuggee context. Avoid side effects unless the user explicitly wants state changed.

For many targeted values, prefer `debug_eval_many` or snapshot watches over pulling all locals.

Snapshot-style tools do not return locals by default. To see locals in `debug_snapshot` or `debug_wait_for_break`, pass `include: ["locals"]`; otherwise only the requested non-local categories, or the default call stack, are returned.
