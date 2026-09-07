# Direct Edits

Use direct mutations when the change should apply immediately:

```json
editor_insert({ "path": "Project/File.cs", "line": 42, "column": 1, "text": "text", "saveAfterEdit": true, "sessionId": "..." })
editor_replace({ "path": "Project/File.cs", "startLine": 10, "startColumn": 1, "endLine": 12, "endColumn": 1, "text": "replacement", "saveAfterEdit": true, "sessionId": "..." })
document_write({ "path": "Project/File.cs", "text": "entire file", "createIfMissing": false, "saveAfterWrite": true, "sessionId": "..." })
document_save({ "path": "Project/File.cs", "sessionId": "..." })
```

`saveAfterEdit` and `saveAfterWrite` default to `false`; without them the change can remain in the open editor buffer.
