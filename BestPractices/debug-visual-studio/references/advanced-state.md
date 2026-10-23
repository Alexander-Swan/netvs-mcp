# Threads, Parallel State, And Exceptions

```json
debug_get_threads({ "sessionId": "..." })
thread_switch({ "threadId": 8, "sessionId": "..." })
thread_get_callstack({ "threadId": 8, "sessionId": "..." })
thread_set_frozen({ "threadId": 8, "frozen": true, "sessionId": "..." })
parallel_stacks({ "sessionId": "..." })
parallel_watch({ "sessionId": "..." })
exception_settings_get({ "exceptionName": "InvalidOperationException", "sessionId": "..." })
exception_settings_set({ "exceptionName": "System.InvalidOperationException", "breakOnThrown": true, "sessionId": "..." })
```

Some debug engines do not expose thread freeze/thaw or stack frames through EnvDTE. If a response says `supported: false`, report it and continue with available state.

When setting an exception that is not found and `breakOnThrown` is true, the VSIX attempts to create it under Common Language Runtime Exceptions.

For locals or expressions on another thread without changing debugger selection, pass optional `threadId` and `stackFrame` to inspection tools. `threadId` alone uses frame 1; both omitted use the selected frame. See `inspection.md` for targeting, snapshot scope, and version requirements. Use `thread_switch` only when you want the selection change to persist.
