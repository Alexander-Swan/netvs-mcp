# Formatting And Cleanup

```json
document_cleanup({ "path": "Project/File.cs", "saveAfterCleanup": true, "sessionId": "..." })
format_and_organize({ "path": "Project/File.cs", "saveAfterCleanup": true, "sessionId": "..." })
```

`document_cleanup` runs Visual Studio format/cleanup. `format_and_organize` wraps cleanup and reports import-organization status when available. Both default `saveAfterCleanup` to `false`.
