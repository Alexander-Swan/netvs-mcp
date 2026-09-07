# Snapshots And Waiting

Prefer `debug_snapshot` over separate step/continue/break plus inspection calls when you need state afterward:

```json
debug_snapshot({ "action": "stepOver", "include": ["callStack"], "sessionId": "..." })
debug_snapshot({ "action": "stepOver", "include": ["watch"], "watchExpressions": ["count", "request.Id"], "sessionId": "..." })
debug_snapshot({ "include": ["locals"], "sessionId": "..." })
```

`action` may be `stepInto`, `stepOver`, `stepOut`, `continue`, or `break`. Omit it for read-only inspection. Omit `include` to default to call stack only; pass `[]` to fetch no optional categories.

Locals are opt-in and can be large. Snapshot responses will not contain locals unless you pass `include: ["locals"]`. Prefer `include: ["watch"]` with `watchExpressions` for targeted values.

Wait for a breakpoint server-side instead of asking the user to tell you when it hits:

```json
debug_continue({ "sessionId": "..." })
debug_wait_for_break({ "timeoutSeconds": 30, "include": ["callStack"], "sessionId": "..." })
```

If it times out, the call succeeds with `timedOut: true`; call again when a longer wait is reasonable.
