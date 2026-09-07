# Rename, Call Hierarchy, And Code Actions

Use preview before mutating broad source changes:

```json
rename_symbol_preview({ "documentPath": "...", "line": 590, "column": 24, "newName": "NewName", "sessionId": "..." })
rename_symbol_apply({ "documentPath": "...", "line": 590, "column": 24, "newName": "NewName", "sessionId": "..." })
```

Check `Supported` before trusting preview changes. Follow broad renames with a build.

Use call hierarchy to inspect callers, callees, or both:

```json
call_hierarchy_get({ "documentPath": "...", "line": 590, "column": 24, "direction": "both", "maxDepth": 3, "sessionId": "..." })
```

`maxDepth` is clamped to 1-6 and the total tree is bounded.

Code actions mirror the Visual Studio lightbulb:

```json
code_actions_list({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
code_actions_apply({ "documentPath": "...", "line": 590, "column": 24, "index": 0, "sessionId": "..." })
```

Call `code_actions_list` first and apply promptly with the same position/span. Actions with nested interactive choices are filtered out.
