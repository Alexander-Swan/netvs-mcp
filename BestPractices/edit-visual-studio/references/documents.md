# Documents And Paths

Document/editor tools use `path`, not `documentPath`. Paths may be absolute or relative to the routed solution file's directory. Prefer forward slashes.

```json
document_active({ "sessionId": "..." })
document_list({ "sessionId": "..." })
document_open({ "path": "Project/File.cs", "sessionId": "..." })
document_read({ "path": "Project/File.cs", "sessionId": "..." })
document_close({ "path": "Project/File.cs", "policy": "Save", "allowDirtyDiscard": false, "sessionId": "..." })
```

`document_read` uses the live editor buffer when the file is open, including unsaved changes, otherwise disk. Omit range arguments for the full document, or pass a 1-based `startLine` and optional positive `lineCount`:

```json
document_read({ "path": "Project/File.cs", "startLine": 40, "lineCount": 25, "sessionId": "..." })
```

The result reports `startLine`, returned `lineCount`, `totalLines`, and `truncated` (true when lines were omitted before or after the range). Original line endings are preserved. An empty document has one line; a trailing newline starts an empty final line. Starting beyond EOF returns empty text and `lineCount: 0`.

`document_close.policy` is `NoSave`, `Save`, or `Discard`; dirty discard requires `allowDirtyDiscard: true`.
