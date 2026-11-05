# Navigate Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Use explicit solution routing (`solutionPath`, then `solutionName`) when more than one Visual Studio window may match; use `sessionId` only to target a specific running instance.
- Navigation, `diagnostics_*`, and breakpoint tools use `documentPath`; document/editor tools, including `document_diagnostics`, use `path`.
- `line` and `column` are 1-based.
- Prefer agent-native filesystem search for ordinary repo text search; use Visual Studio search when its loaded-solution behavior matters.
- Prefer preview tools before broad renames or code-action applies.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Path, document, line, and column conventions | `references/path-position.md` |
| Definition, references, implementation, or symbol context | `references/symbol-position.md` |
| Document symbols, outline, or workspace symbols | `references/symbol-lists.md` |
| Rename, call hierarchy, code fixes, or refactorings | `references/code-intelligence.md` |
| Diagnostics, binding errors, text search, git context, or opening relevant files | `references/search-diagnostics.md` |

## Covered Tools

`document_active`, `code_*`, `symbol_context`, `document_outline`, `find_implementations`, `rename_symbol_*`, `call_hierarchy_get`, `code_actions_*`, `document_read`, `document_open`, `document_list`, `document_close`, `open_relevant_files`, `errors_list`, `diagnostics_*`, `editor_find`, `find_in_files`, and `git_context`.
