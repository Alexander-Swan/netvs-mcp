# Session Events And Automation Control

Use `debug_snapshot({ "include": [], "sessionId": "..." })` for debugger state with a coherent `state.eventCursor`. `debug_status` exposes the cursor directly on its returned state. Pass the cursor's `epoch` and `watermark` as `epoch` and `afterSequence` to `vs_events`, preserving the same session route:

```json
vs_events({ "epoch": "<cursor epoch>", "afterSequence": 42, "timeoutSeconds": 30, "maxItems": 100, "sessionId": "..." })
```

`timeoutSeconds: 0` reads immediately; 1–60 waits for events. `maxItems` is 1–1000. Omitting the cursor obtains the current watermark without replaying history; capture it before a command when subsequent events must include fast completion. Advance paged reads using the last returned event's `sequence`, not the batch watermark while unread events remain. An empty page can advance to its watermark. Events are observations; a current-state snapshot alone does not prove a particular command completed.

When `requiresReconciliation` is true, obtain a fresh read-only `debug_snapshot` and resume from its cursor; reconcile build/test outcomes through their own status or retained run tools. Never repeat a mutation to repair lost events. Legacy extensions can lack events; modal or stale state can lack a cursor. Do not fabricate one by pairing unrelated reads. MCP notification support is unnecessary: events flow internally from VS to the broker. For debugger stops, prefer `debug_wait_for_break` over a client polling loop.

`session_automation_control` requires an explicit `sessionId`. Omit `paused` to inspect, use `paused: true` to block new automation mutations, and `paused: false` to resume them. Reads and recovery controls such as `build_cancel` and `debug_break` remain available. Already-running commands continue; inspect `activeMethod` and their domain-specific outcomes. Pausing does not suspend the developer or debuggee and does not cancel existing work. The pause state lasts only for this broker lifetime.
