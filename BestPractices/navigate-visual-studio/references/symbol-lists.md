# Symbol Lists

Use `code_document_symbols` or `document_outline` for one file, and `code_workspace_symbols` across the live workspace:

```json
code_document_symbols({ "documentPath": "Project/File.cs", "sessionId": "..." })
document_outline({ "documentPath": "Project/File.cs", "sessionId": "..." })
code_workspace_symbols({ "query": "BrokerToolService", "maxResults": 100, "sessionId": "..." })
```

`code_document_symbols` and `document_outline` call the same underlying symbol listing; choose the response shape that is easier to consume.

`code_workspace_symbols` requires a non-empty `query`; `Truncated: true` means more matches existed than `maxResults`.
