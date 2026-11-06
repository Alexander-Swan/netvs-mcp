# Safe-Edit Preview Workflow

Safe edit computes a pending diff without touching the document until approval.

```json
edit_preview({ "operation": "replace", "path": "Project/File.cs", "text": "new code", "startLine": 10, "startColumn": 1, "endLine": 12, "endColumn": 1, "sessionId": "..." })
edit_list_pending({ "sessionId": "..." })
edit_approve({ "editId": "...", "saveAfterApply": true, "sessionId": "..." })
edit_reject({ "editId": "...", "sessionId": "..." })
```

`operation` is `write`, `insert`, or `replace`. Read the live buffer with `document_read` when needed. Approve the intended batch of edits before calling `build_and_get_errors` separately.

Read the returned identifier before the follow-up call: `edit_preview` returns it as `editId`. Preserve the same explicit route for the preview and decision calls. Approval returns true when saved and false when applied without saving. Rejection returns true.
