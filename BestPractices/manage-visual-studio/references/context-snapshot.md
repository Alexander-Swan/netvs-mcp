# Context Snapshot

Use `vs_context_snapshot` when you need a broad picture of a routed session before deciding what to do next:

```json
vs_context_snapshot({ "sessionId": "..." })
```

It combines session status, solution info, active document, editor selection, debugger status, build status, up to 50 errors/warnings, and pending safe edits.

Prefer it over separate calls to `get_status`, `solution_info`, `document_active`, `selection_get`, `debug_status`, `build_status`, `errors_list`, and `edit_list_pending` when those details are all useful. Do not use it when a narrow tool can answer directly.
