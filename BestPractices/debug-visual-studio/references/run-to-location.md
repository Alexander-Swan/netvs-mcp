# Run To A Source Location

Read `debug_status` and `process_list_debugged`. The current implementation requires one paused debugged process. Retain that process ID and the exact current `stopId`/`generation`:

```json
debug_run_to({
  "documentPath": "Program.cs",
  "line": 42,
  "expectedStopId": "<current stopId>",
  "expectedGeneration": 1,
  "expectedProcessId": 1234,
  "timeoutSeconds": 30,
  "sessionId": "<selected session>"
})
```

The tool creates process-filtered temporary breakpoints, continues once, waits for a new stop/exit/deadline, and cleans up only the exact breakpoint objects returned by its creation request. An existing breakpoint at the same file/line is not sufficient ownership evidence. If an enabled, unconditional stopping breakpoint already covers the target, the tool can reuse it without changing any properties (`breakpointMode: existingCallerBreakpoint`, zero owned creations). It then verifies the actual stopped thread's source position; a cached breakpoint name is insufficient. Existing disabled, conditional, counted, or differently filtered breakpoints are preserved and rejected instead of being reconfigured. A developer-changed ownership tag on a temporary breakpoint is preserved and cleanup is reported incomplete. Older extensions without this RPC fail explicitly; there is no broad-delete or unguarded-continue fallback.

Check both `outcome` and `cleanup.complete`. `reachedLocation` requires either a hit on this request's own breakpoint or a verified actual stopped source position when reusing the caller's breakpoint, always in the expected process at the requested path/line. When the current source position already matches, `alreadyAtLocation` returns without sending an execution command or creating artifacts. Column 1 selects a line; other columns require the exact bound column. `boundAtDifferentLocation` discloses engine relocation. Source checksums are not validated (`sourceMatchStatus: notVerified`); inspect module/source evidence when the loaded build may differ from the editor.

`stoppedElsewhere` preserves an earlier caller breakpoint or exception instead of continuing past it. Other outcomes include `exited`, `timedOut`, `waitCanceled`, `contextChanged`, `setupFailed`, and `outcomeUnknown`. Timeout/cancellation removes verified owned artifacts when possible and never stops the target. Read the returned current state; do not infer that execution stopped. Incomplete cleanup requires inspection of the returned evidence and breakpoint list, preserving developer changes. Repeating execution is not a cleanup operation.

If the client disconnects, inspect the exact VS session's debugger state, process, and breakpoints before issuing another execution command. The VSIX's `ownerId`, temporary group, and `runTo.accepted` / `runTo.completed` events identify its artifacts; they are not durable result receipts. Unknown outcomes require reconciliation, not automatic replay.
