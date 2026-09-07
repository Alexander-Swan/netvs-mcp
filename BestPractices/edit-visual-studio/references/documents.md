# Documents And Paths

Document/editor tools use `path`, not `documentPath`. Paths may be absolute or relative to the routed solution file's directory. Prefer forward slashes.

```json
document_active({ "sessionId": "..." })
document_list({ "sessionId": "..." })
document_open({ "path": "Project/File.cs", "sessionId": "..." })
document_read({ "path": "Project/File.cs", "sessionId": "..." })
document_close({ "path": "Project/File.cs", "policy": "Save", "allowDirtyDiscard": false, "sessionId": "..." })
```

`document_read` uses the live editor buffer when the file is open and dirty, otherwise disk. `document_close.policy` is `NoSave`, `Save`, or `Discard`; dirty discard requires `allowDirtyDiscard: true`.
