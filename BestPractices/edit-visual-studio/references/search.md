# Editor Search

```json
editor_find({ "query": "TODO", "path": "Project/File.cs", "matchCase": false, "wholeWord": false, "useRegex": false, "maxResults": 100, "sessionId": "..." })
find_in_files({ "query": "TODO", "rootPath": "src/Project", "filePattern": "*.cs", "maxResults": 100, "sessionId": "..." })
```

Use `editor_find` for one document and `find_in_files` for Visual Studio's solution/folder search. Prefer agent-native filesystem search when Visual Studio semantics are not needed.
