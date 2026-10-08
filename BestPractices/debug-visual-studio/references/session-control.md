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
events_wait({ "eventTypes": ["debugger_break"], "timeoutSeconds": 30, "sessionId": "..." })
events_wait({ "eventTypes": ["debugger_stopped", "debugger_mode_changed"], "timeoutSeconds": 30, "sessionId": "..." })
debug_stop({ "sessionId": "..." })
```

`dbgDesignMode` means no active debuggee. `dbgRunMode` means running. `dbgBreakMode` means paused.

Use `events_wait` when you only need to know that the debugger broke, stopped, or changed mode. Use `debug_wait_for_break` when you also need an immediate debugger snapshot such as call stack, locals, or watches.

Confirm before `debug_stop` unless the user explicitly asked to stop debugging.
