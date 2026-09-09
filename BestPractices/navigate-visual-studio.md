# Navigate Visual Studio With NetVsMcp

Agent-neutral entrypoint for definitions, references, symbols, diagnostics, code actions, renames, call hierarchy, workspace search, and relevant file opening through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling a matching tool, then only the reference files needed for the operation.
- For learning or skill creation, read the entrypoints and relevant references deliberately. Do not load every reference by default.

## First Rules

- Use explicit solution routing (`solutionPath`, then `solutionName`) when more than one Visual Studio window may match; use `sessionId` only to target a specific running instance.
- Navigation, diagnostics, and breakpoint tools use `documentPath`; document/editor tools use `path`.
- `line` and `column` are 1-based.
- Prefer agent-native filesystem search for ordinary repo text search; use Visual Studio search when its loaded-solution behavior matters.
- Prefer preview tools before broad renames or code-action applies.

## Reference Files

| Task | Read |
| --- | --- |
| Path, document, line, and column conventions | `navigate-visual-studio/references/path-position.md` |
| Definition, references, implementation, or symbol context | `navigate-visual-studio/references/symbol-position.md` |
| Document symbols, outline, or workspace symbols | `navigate-visual-studio/references/symbol-lists.md` |
| Rename, call hierarchy, code fixes, or refactorings | `navigate-visual-studio/references/code-intelligence.md` |
| Diagnostics, binding errors, text search, git context, or opening relevant files | `navigate-visual-studio/references/search-diagnostics.md` |

## Covered Tools

`document_active`, `code_*`, `symbol_context`, `document_outline`, `find_implementations`, `rename_symbol_*`, `call_hierarchy_get`, `code_actions_*`, `document_read`, `document_open`, `document_list`, `document_close`, `open_relevant_files`, `errors_list`, `diagnostics_*`, `editor_find`, `find_in_files`, and `git_context`.
