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
code_actions_preview({ "documentPath": "...", "line": 590, "column": 24, "actionId": "<from list>", "sessionId": "..." })
code_actions_apply({ "documentPath": "...", "line": 590, "column": 24, "actionId": "<same ID>", "sessionId": "..." })
```

Call `code_actions_list` first, then preview and apply the returned `actionId` with the same route and position/span. IDs bind the original action to its workspace snapshot, expire after two minutes, and cannot be reused after apply. Preview prepares the exact transformation without applying it; any workspace change invalidates it. Structural/custom operations are explicitly unsupported. Actions with nested interactive choices are filtered out. Legacy `index`-only apply recomputes actions and has no stale-selection protection.

Inspect the returned `success`, `outcome`, changed document versions, and any `recoveryId`, including on partial or unknown outcomes. Never replay apply to recover a lost response. To revert verified owned changes, use `code_actions_revert` with that receipt and the same route. Receipts are single-use, expire after 15 minutes, are bounded to 32 entries, and disappear on VS restart. Later workspace source/project changes reject recovery; inspect current buffers instead of using broad IDE undo. Preview, verified IDs, and recovery require an updated extension.
