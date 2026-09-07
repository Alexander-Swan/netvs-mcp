# Safe-Edit Preview Workflow

Safe edit computes a pending diff without touching the document until approval.

```json
edit_preview({ "operation": "replace", "path": "Project/File.cs", "text": "new code", "startLine": 10, "startColumn": 1, "endLine": 12, "endColumn": 1, "sessionId": "..." })
prepare_safe_edit({ "operation": "insert", "path": "Project/File.cs", "text": "new line\n", "line": 42, "column": 1, "sessionId": "..." })
edit_list_pending({ "sessionId": "..." })
edit_approve({ "editId": "...", "saveAfterApply": true, "sessionId": "..." })
apply_safe_edit_and_build({ "editId": "...", "saveAfterApply": true, "includeWarnings": true, "maxItems": 200, "sessionId": "..." })
edit_reject({ "editId": "...", "sessionId": "..." })
```

`operation` is `write`, `insert`, or `replace`. Use `prepare_safe_edit` when you have not already read the current document. Use `apply_safe_edit_and_build` when you want approval plus build diagnostics in one call.
