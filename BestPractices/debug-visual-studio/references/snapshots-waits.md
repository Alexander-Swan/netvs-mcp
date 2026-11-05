# Snapshots And Waiting

Prefer `debug_snapshot` over separate step/continue/break plus inspection calls when you need state afterward:

```json
debug_snapshot({ "action": "stepOver", "include": ["callStack"], "sessionId": "..." })
debug_snapshot({ "action": "stepOver", "include": ["watch"], "watchExpressions": ["count", "request.Id"], "sessionId": "..." })
debug_snapshot({ "include": ["locals"], "sessionId": "..." })
```

## Shared Defaults

- Omit `action` for read-only inspection; otherwise choose `stepInto`, `stepOver`, `stepOut`, `continue`, or `break` (`debug_snapshot` only).
- Omit `include` for call stack only; `[]` requests state only. Sections are `callStack`, `locals`, `breakpoints`, `watch`, `threads`, `parallelStacks`, `parallelWatch`, and `modules`. Locals/modules are opt-in; prefer `watch` with `watchExpressions` for targeted values.
- Call stacks default to two frames. Use `maxFrames` for deeper callers; `callStack.hasMore` reports omitted frames.
- Optional `threadId` targets stack, locals, and watches; `stackFrame` targets locals/watches. Other sections retain their scope. See [inspection](inspection.md) for targeting and [context](inspection-context.md) for `includeContext` and pause guards. Inspection preserves selection; an explicit action still advances execution.
- `moduleOptions` (`processId`, `name`, `includeSymbolLoadInformation`) applies only when modules are included, independently of thread/frame. See [modules and symbols](modules-symbols.md) for interpretation and support limits.

For event observation, request `include: []`. Updated extensions return a coherent `state.eventCursor`; pass its `epoch` and `watermark` (as `afterSequence`) to `vs_events`. On a gap or changed epoch, obtain a new read-only snapshot; never replay execution. `debug_status` also captures a coherent cursor. Legacy, modal, or stale observations omit it; do not infer one from a separate event read. See [session events](../../manage-visual-studio/references/events-control.md) for paging/timeouts. Break events do not guarantee frame readiness; bounded read-only probes may still be needed.

MCP responses use reduced JSON by default, without a `compact` argument or `success`/`value` envelope. Identical child states inherit the nearest enclosing state; different observations remain explicit. Source locations use solution-relative `file:line:column`; external paths stay absolute. Locals and watches use expression-keyed objects, with debugger strings, `$preview` for object summaries and `$error` for invalid values. Duplicate/unnamed expressions retain arrays. Matching event-cursor session IDs inherit from state. Pause guards, diagnostic evidence and pagination remain available. Request `includeContext` only when process/thread/frame identity is needed.

While paused, `state.breakReason` reports `breakpoint`, `stepCompleted`, `exception`, `manualPause`, `unknown`, or `other`. It is null outside break mode or when an older extension does not supply it.

Wait for a breakpoint server-side instead of asking the user to tell you when it hits:

```json
debug_continue({ "sessionId": "..." })
debug_wait_for_break({ "timeoutSeconds": 30, "include": ["callStack"], "sessionId": "..." })
```

If it times out, the call succeeds with `timedOut: true`; wait again when reasonable. A wait does not start, resume, or cancel execution. For `dbgModalBlock`, respond using returned dialog/button IDs within existing authorization, then check `debug_status` with any `operationId`; never replay the action.

Break mode can precede usable thread/frame objects during attachment or an engine transition. Updated extensions report `inspection_unavailable` for missing frame objects instead of a null-reference failure. `debug_wait_for_break` retries only read-only inspection within its original deadline. At the deadline, it preserves current state and returns `timedOut: true`, null inspection sections, and an explicit `inspectionStatus`/`inspectionMessage`. This does not prove the target is running or that frames will become available for this engine. Use `include: []` for state-only observation. Never repeat Continue, Step, launch, or attach merely to obtain a frame.

Without a caller-supplied pause guard, a wait can reconcile a newer pause if the observed context changes during collection. An explicit `expectedStopId` or `expectedGeneration` is preserved: a changed pause fails with `stale_debugger_context`, rather than silently refreshing the caller's guard.

## Verify Modules When A Breakpoint Is Reached

After `debug_start` or `debug_attach`, include `modules` in the wait request when verifying the loaded application and its symbols:

```json
debug_wait_for_break({ "include": ["callStack", "modules"], "moduleOptions": { "name": "App.dll" }, "sessionId": "..." })
```

The module query runs once after the debugger reaches break mode, not on each status poll. Inspect `state.mode`, `state.breakReason`, and `timedOut` first. A timeout while running, an ended session, or a modal dialog returns no module section. A tracepoint that continues execution will not by itself produce a paused module snapshot. Set a stopping breakpoint when a paused inspection is needed.

Missing module support in an older extension or unsupported Concord engine returns `modules.supported: false`, preserving other sections; this does not mean binaries or symbols are missing. Use `module_list` for a separate query.
