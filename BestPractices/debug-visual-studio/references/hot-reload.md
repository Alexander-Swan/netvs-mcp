# Hot Reload

```json
debug_hot_reload_apply({ "sessionId": "..." })
```

Apply source edits first, then call Hot Reload. It requires an active debug session and fails fast in `dbgDesignMode`.

`Success` reflects whether the command ran without throwing and whether the routed Error List is free of high-severity errors afterward. On failure, inspect the returned errors.
