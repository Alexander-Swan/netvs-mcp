# Symbol Lookup At A Position

Use these when investigating a known symbol location:

```json
code_go_to_definition({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
code_find_references({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
code_go_to_implementation({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
find_implementations({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
symbol_context({ "documentPath": "...", "line": 590, "column": 24, "contextLines": 4, "sessionId": "..." })
```

`code_go_to_definition` moves the active Visual Studio editor as a side effect. `code_find_references` does not navigate.

`find_implementations` reports `Supported: false` when implementation lookup is unavailable for the language or symbol kind.

`symbol_context` combines a document read, definition lookup, references lookup, and a small snippet. Use it as a first look before deeper investigation.
