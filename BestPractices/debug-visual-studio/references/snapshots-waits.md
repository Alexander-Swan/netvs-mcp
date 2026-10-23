# Snapshots And Waiting

Prefer `debug_snapshot` over separate step/continue/break plus inspection calls when you need state afterward:

```json
debug_snapshot({ "action": "stepOver", "include": ["callStack"], "sessionId": "..." })
debug_snapshot({ "action": "stepOver", "include": ["watch"], "watchExpressions": ["count", "request.Id"], "sessionId": "..." })
debug_snapshot({ "include": ["locals"], "sessionId": "..." })
```

`action` may be `stepInto`, `stepOver`, `stepOut`, `continue`, or `break`. Omit it for read-only inspection. Omit `include` to default to call stack only; pass `[]` to fetch no optional categories. Supported include values are `callStack`, `locals`, `breakpoints`, `watch`, `threads`, `parallelStacks`, and `parallelWatch`; do not request `modules`.

Call-stack responses include up to two frames by default. Pass `maxFrames` when deeper callers are needed. When `callStack.hasMore` is `true`, additional callers were omitted to keep the snapshot small.

Locals are opt-in and can be large. Snapshot responses will not contain locals unless you pass `include: ["locals"]`. Prefer `include: ["watch"]` with `watchExpressions` for targeted values.

Both snapshot tools accept optional `threadId` and `stackFrame`. With `threadId`, call stack, locals, and watches use that thread; `stackFrame` selects the frame for locals and watches and defaults to 1 on an explicit thread. Omit both to use the current selection. Other sections retain their usual scope. Inspection leaves thread/frame selection unchanged; an explicit `action` still advances execution. See `inspection.md` for discovery, version requirements, and invalid evaluation results.

While paused, `state.breakReason` reports `breakpoint`, `stepCompleted`, `exception`, `manualPause`, `unknown`, or `other`. It is null outside break mode or when an older extension does not supply it.

Wait for a breakpoint server-side instead of asking the user to tell you when it hits:

```json
debug_continue({ "sessionId": "..." })
debug_wait_for_break({ "timeoutSeconds": 30, "include": ["callStack"], "sessionId": "..." })
```

If it times out, the call succeeds with `timedOut: true`; call again when a longer wait is reasonable.
