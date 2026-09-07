# Debug Session Control

```json
debug_status({ "sessionId": "..." })
debug_start({ "sessionId": "..." })
debug_start_without_debugging({ "sessionId": "..." })
debug_restart({ "sessionId": "..." })
debug_break({ "sessionId": "..." })
debug_continue({ "sessionId": "..." })
debug_step({ "stepKind": "Into", "sessionId": "..." })
debug_wait_for_break({ "timeoutSeconds": 30, "include": ["callStack"], "sessionId": "..." })
debug_stop({ "sessionId": "..." })
```

`dbgDesignMode` means no active debuggee. `dbgRunMode` means running. `dbgBreakMode` means paused.

Confirm before `debug_stop` unless the user explicitly asked to stop debugging.
