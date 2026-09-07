# Threads, Modules, Parallel State, And Exceptions

```json
debug_get_threads({ "sessionId": "..." })
thread_switch({ "threadId": 8, "sessionId": "..." })
thread_get_callstack({ "threadId": 8, "sessionId": "..." })
thread_set_frozen({ "threadId": 8, "frozen": true, "sessionId": "..." })
parallel_stacks({ "sessionId": "..." })
parallel_watch({ "sessionId": "..." })
module_list({ "sessionId": "..." })
exception_settings_get({ "exceptionName": "InvalidOperationException", "sessionId": "..." })
exception_settings_set({ "exceptionName": "System.InvalidOperationException", "breakOnThrown": true, "sessionId": "..." })
```

Some debug engines do not expose thread freeze/thaw, module data, or stack frames through EnvDTE. If a response says `supported: false`, report it and continue with available state.

When setting an exception that is not found and `breakOnThrown` is true, the VSIX attempts to create it under Common Language Runtime Exceptions.
