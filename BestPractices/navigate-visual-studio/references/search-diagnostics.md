# Search, Diagnostics, Git Context, And Relevant Files

Use `diagnostics_for_document` for compiler/analyzer diagnostics scoped to one file and `diagnostics_binding_errors` for VSIX-surfaced binding diagnostics. Check `Supported` when present.

For compiler diagnostics tied to an exact live buffer, use `document_diagnostics` with `path` (not `documentPath`). Pass `expectedTextVersion` from `document_read` or the first diagnostic page. It includes unsaved text and returns Roslyn compiler diagnostics, excluding analyzer and build Error List diagnostics. Use zero-based `offset`, `maxItems` from 1–200, and follow `nextOffset` with the same text version. Linked files require an unambiguous `projectName` or project path. `stale_document` means read the current buffer and restart the query; do not combine pages from different versions. This tool requires an updated extension.

```json
diagnostics_for_document({ "documentPath": "Project/File.cs", "includeWarnings": true, "maxItems": 200, "sessionId": "..." })
diagnostics_binding_errors({ "target": "MainWindow.xaml", "timeoutMilliseconds": 5000, "sessionId": "..." })
```

Prefer agent-native filesystem tools for ordinary repository text search. Use `find_in_files` when Visual Studio's loaded-solution search behavior matters, and `editor_find` for a single known document.

```json
editor_find({ "query": "TODO", "path": "Project/File.cs", "maxResults": 100, "sessionId": "..." })
find_in_files({ "query": "TODO", "rootPath": "src/Project", "filePattern": "*.cs", "maxResults": 100, "sessionId": "..." })
git_context({ "rootPath": "src/NetVsMcp.Broker", "maxFiles": 100, "sessionId": "..." })
open_relevant_files({ "paths": ["Project/File.cs"], "sessionId": "..." })
```

`open_relevant_files` is a batch `document_open`, useful before reading or editing several files in Visual Studio.
