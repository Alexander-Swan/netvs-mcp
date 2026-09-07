# Selection

```json
selection_get({ "sessionId": "..." })
selection_set({ "path": "Project/File.cs", "startLine": 5, "startColumn": 1, "endLine": 5, "endColumn": 20, "sessionId": "..." })
```

`selection_get` returns `null` when there is no meaningful selection. `selection_set` opens and activates `path` if needed.
