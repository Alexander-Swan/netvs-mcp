# Context Snapshot

Use `vs_context_snapshot` when you need a broad picture of a routed session before deciding what to do next:

```json
vs_context_snapshot({ "sessionId": "..." })
```

It combines session status, solution info, active document, editor selection, debugger status, build status, up to 50 errors/warnings, and pending safe edits.

Its `debugger.eventCursor`, when present, is captured coherently with the debugger state. The broader IDE sections are collected separately and do not form one atomic snapshot. For state and an event cursor alone, prefer `debug_snapshot` with `include: []`; see [session events](events-control.md).

Prefer it over separate calls to `get_status`, `solution_info`, `document_active`, `selection_get`, `debug_status`, `build_status`, `errors_list`, and `edit_list_pending` when those details are all useful. Do not use it when a narrow tool can answer directly.
