# Documents And Paths

Document/editor tools use `path`, not `documentPath`. Paths may be absolute or relative to the routed solution file's directory. Prefer forward slashes.

```json
document_active({ "sessionId": "..." })
document_list({ "sessionId": "..." })
document_open({ "path": "Project/File.cs", "sessionId": "..." })
document_read({ "path": "Project/File.cs", "sessionId": "..." })
document_close({ "path": "Project/File.cs", "policy": "Save", "allowDirtyDiscard": false, "sessionId": "..." })
```

`document_read` uses the live editor buffer when the file is open, including unsaved changes, and reads from disk otherwise. Omit range arguments to start at the beginning of the document, or pass a 1-based `startLine` and optional positive `lineCount`.

```json
document_read({ "path": "Project/File.cs", "startLine": 40, "lineCount": 25, "sessionId": "..." })
```

The result reports `startLine`, returned `lineCount`, `totalLines`, and `truncated` when some of the document was omitted. Each response includes up to 65,536 UTF-16 code units of text. When that limit cuts the requested range short, use `nextLine` and `nextColumn` as the next request's `startLine` and `startColumn`. If you specified `lineCount`, adjust it to keep the same end line. Each read captures the current buffer independently, so restart the read if the document changes between pages.

Original line endings are preserved. An empty document has one line. A trailing newline starts an empty final line. Starting beyond EOF returns empty text and `lineCount: 0`.

`document_close.policy` is `NoSave`, `Save`, or `Discard`; dirty discard requires `allowDirtyDiscard: true`.
