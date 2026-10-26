# Hot Reload

```json
debug_hot_reload_apply({ "sessionId": "..." })
```

Apply source edits first, then call Hot Reload. It requires an active debug session and fails fast in `dbgDesignMode`.

When apply-completion notifications are available, a new debug start waits for Hot Reload initialization before returning. If initialization cannot be confirmed, applying changes fails with restart guidance; restart before editing source. Older versions retain their command-based initialization behavior.

`Success` reflects whether the command ran without throwing and whether the routed Error List is free of high-severity errors afterward. On failure, inspect the returned errors.

This does not verify that the runtime is executing the updated code. When applying changes while paused, evaluations at that same pause can still return the old method's value. Step or continue to a fresh debugger stop before checking the expected value. Choose an advance that is safe for the target; the Hot Reload tool does not resume execution automatically.
