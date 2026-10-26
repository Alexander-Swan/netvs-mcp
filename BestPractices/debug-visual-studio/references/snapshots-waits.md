# Snapshots And Waiting

Prefer `debug_snapshot` over separate step/continue/break plus inspection calls when you need state afterward:

```json
debug_snapshot({ "action": "stepOver", "include": ["callStack"], "sessionId": "..." })
debug_snapshot({ "action": "stepOver", "include": ["watch"], "watchExpressions": ["count", "request.Id"], "sessionId": "..." })
debug_snapshot({ "include": ["locals"], "sessionId": "..." })
```

`action` may be `stepInto`, `stepOver`, `stepOut`, `continue`, or `break`. Omit it for read-only inspection. Omit `include` to default to call stack only; pass `[]` to fetch no optional categories. Supported include values are `callStack`, `locals`, `breakpoints`, `watch`, `threads`, `parallelStacks`, `parallelWatch`, and `modules`. Modules are opt-in; use `moduleOptions` to filter the process or module name and optionally request symbol-search diagnostics. See `modules-symbols.md`.

Call-stack responses include up to two frames by default. Pass `maxFrames` when deeper callers are needed. When `callStack.hasMore` is `true`, additional callers were omitted to keep the snapshot small.

For event observation, call `debug_snapshot` with `include: []`. On updated extensions, `state.eventCursor` contains the session ID, epoch, and watermark captured coherently with that state. Pass its epoch and watermark to `vs_events` to read subsequent events. If replay reports a gap or changed epoch, obtain a new snapshot and resume from its cursor; never repeat an execution action to reconcile events. `debug_status` uses the same coherent capture, and debugger waits consume session events. Legacy extensions and modal or stale observations omit the cursor; do not infer one from a separate event read.

The event call's argument is `afterSequence`: pass the cursor's `watermark` there. Follow [session events](../../manage-visual-studio/references/events-control.md) for paging and timeout rules. Frame-readiness retries and independent modal-window detection can still use bounded probes; an event indicating break mode does not guarantee usable frames.

Locals are opt-in and can be large. Snapshot responses will not contain locals unless you pass `include: ["locals"]`. Prefer `include: ["watch"]` with `watchExpressions` for targeted values.

Default MCP responses preserve nested debugger states for existing clients. When the broker advertises `compact`, pass `compact: true` on `debug_snapshot`, `debug_wait_for_break`, or `debug_eval_many` to omit child states identical to the enclosing state. Read an omitted child state from its nearest enclosing state. Different child states are preserved in full. Compact snapshots also use solution-relative stack files. Stack files within the routed solution directory become solution-relative paths; external files remain absolute, and unknown files stay unknown. Pause guards, failure evidence, frame locations, values, and pagination remain available. Request `includeContext` only when process/thread/frame identity is needed.

Both snapshot tools accept optional `threadId` and `stackFrame`. With `threadId`, call stack, locals, and watches use that thread; `stackFrame` selects the frame for locals and watches and defaults to 1 on an explicit thread. Omit both to use the current selection. Other sections retain their usual scope. Inspection leaves thread/frame selection unchanged; an explicit `action` still advances execution. See `inspection.md` for discovery, version requirements, and invalid evaluation results.

While paused, `state.breakReason` reports `breakpoint`, `stepCompleted`, `exception`, `manualPause`, `unknown`, or `other`. It is null outside break mode or when an older extension does not supply it.

Wait for a breakpoint server-side instead of asking the user to tell you when it hits:

```json
debug_continue({ "sessionId": "..." })
debug_wait_for_break({ "timeoutSeconds": 30, "include": ["callStack"], "sessionId": "..." })
```

If it times out, the call succeeds with `timedOut: true`; call again when a longer wait is reasonable.

Break mode can precede usable thread/frame objects during attachment or an engine transition. Updated extensions report `inspection_unavailable` for missing frame objects instead of a null-reference failure. `debug_wait_for_break` retries only read-only inspection within its original deadline. At the deadline, it preserves current state and returns `timedOut: true`, null inspection sections, and an explicit `inspectionStatus`/`inspectionMessage`. This does not prove the target is running or that frames will become available for this engine. Use `include: []` for state-only observation. Never repeat Continue, Step, launch, or attach merely to obtain a frame.

Without a caller-supplied pause guard, a wait can reconcile a newer pause if the observed context changes during collection. An explicit `expectedStopId` or `expectedGeneration` is preserved: a changed pause fails with `stale_debugger_context`, rather than silently refreshing the caller's guard.

## Verify Modules When A Breakpoint Is Reached

After `debug_start` or `debug_attach`, include `modules` in the wait request when verifying the loaded application and its symbols:

```json
debug_wait_for_break({ "include": ["callStack", "modules"], "moduleOptions": { "name": "App.dll" }, "sessionId": "..." })
```

The module query runs once after the debugger reaches break mode, not on each status poll. Inspect `state.mode`, `state.breakReason`, and `timedOut` first. A timeout while running, an ended session, or a modal dialog returns no module section. A tracepoint that continues execution will not by itself produce a paused module snapshot. Set a stopping breakpoint when a paused inspection is needed.

`debug_snapshot` supports the same opt-in section and `moduleOptions` (`processId`, `name`, `includeSymbolLoadInformation`). These options only apply when `modules` is included. Module scope is independent of `threadId` and `stackFrame`. Missing support in an older extension yields `modules.supported: false` with update guidance, preserving other requested sections. Unsupported Concord engines also return `supported: false`; do not interpret that as missing binaries or symbols. Use `module_list` for a separate follow-up query.
