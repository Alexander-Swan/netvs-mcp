# Navigate Visual Studio With NetVsMcp

This is the agent-neutral code navigation, search, and diagnostics workflow for this repository. Any AI agent can follow it when asked to find a definition, find references or implementations, walk a call hierarchy, list symbols, preview a rename, check diagnostics for a file, search text across the workspace, inspect git status, or open a batch of relevant files through NetVsMcp.

## Session Routing

Start with the NetVsMcp session tools when they are available:

```json
vs_list_sessions()
solution_info({ "sessionId": "..." })
```

Every routed navigation call accepts optional `sessionId`, `solutionName`, and `solutionPath`. Resolution order is `sessionId`, normalized `solutionPath`, exact `solutionName`, active Visual Studio window, only registered instance. Use explicit routing whenever multiple Visual Studio windows are open.

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

## Path And Position Conventions

- `documentPath` values are relative to the solution or absolute. Prefer forward slashes, for example `src/Project/File.cs`; if a value must contain Windows backslashes in JSON, escape them as double backslashes.
- `line` and `column` are always 1-based, matching the numbers shown in the Visual Studio editor. Any position-based tool below rejects `line < 1` or `column < 1` with `"Line must be greater than zero."` or `"Column must be greater than zero."`.
- Every position-based tool takes `documentPath`, `line`, and `column` as flat parameters, not a nested object; the broker packages them into a `CodePositionRequest` before dispatching to the routed session.

## Symbol Lookup At A Code Position

Use `code_go_to_definition`, `code_find_references`, `code_go_to_implementation`, `find_implementations`, and `symbol_context` when investigating a specific symbol at a known location:

```json
code_go_to_definition({
  "documentPath": "src/NetVsMcp.Broker/Services/BrokerToolService.cs",
  "line": 590,
  "column": 24,
  "sessionId": "..."
})
code_find_references({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
code_go_to_implementation({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
find_implementations({ "documentPath": "...", "line": 590, "column": 24, "sessionId": "..." })
symbol_context({ "documentPath": "...", "line": 590, "column": 24, "contextLines": 4, "sessionId": "..." })
```

Key behavior:

- `code_go_to_definition` returns `Symbol`, `Definitions` (candidate locations), and `Navigated` — it also moves the active Visual Studio editor to the definition as a side effect, so it is not a pure read-only lookup.
- `code_find_references` returns `Symbol` and `References` without navigating the editor.
- `code_go_to_implementation` and `find_implementations` both call the same underlying implementation lookup; `find_implementations` wraps the result as `{ Supported, Message, Position, Implementations }` so a language or symbol kind that does not support implementation lookup reports `Supported: false` instead of failing.
- `symbol_context` is a convenience call that combines a document read, `code_go_to_definition`, and `code_find_references`, plus a text snippet extracted around `line`. `contextLines` defaults to `4` and is clamped to zero or more; use it as a single-round-trip first look before deeper investigation.

## Document And Workspace Symbol Listing

Use `code_document_symbols` and `document_outline` for one file, and `code_workspace_symbols` to search across the live workspace:

```json
code_document_symbols({ "documentPath": "src/NetVsMcp.Broker/Services/BrokerToolService.cs", "sessionId": "..." })
document_outline({ "documentPath": "...", "sessionId": "..." })
code_workspace_symbols({ "query": "BrokerToolService", "maxResults": 100, "sessionId": "..." })
```

Key behavior:

- `code_document_symbols` and `document_outline` call the identical routed symbol listing; `code_document_symbols` returns the flat symbol list directly, while `document_outline` wraps the same list alongside the normalized `DocumentPath` as `{ DocumentPath, Symbols }`. Prefer whichever response shape is more convenient — they carry the same data.
- `documentPath` is required for both; an empty value fails with `"Document path is required."`.
- `code_workspace_symbols` requires a non-empty `query` and `maxResults > 0` (default `100`). It returns `{ Query, MatchCount, Truncated, Symbols }`; `Symbols` are richer `DocumentSymbolInfo` entries than the plain strings from `code_document_symbols`, and `Truncated: true` means more matches existed than `maxResults` allowed.

## Rename Preview

`rename_symbol_preview` is a preview-only tool — it never mutates source:

```json
rename_symbol_preview({
  "documentPath": "src/NetVsMcp.Broker/Services/BrokerToolService.cs",
  "line": 590,
  "column": 24,
  "newName": "CodeNavigateToDefinition",
  "sessionId": "..."
})
```

Key behavior:

- `newName` is required and trimmed; an empty value fails with `"New name is required."` before the request is dispatched.
- The result is `{ Supported, Message, Position, NewName, Symbol?, Changes? }`. Check `Supported` before trusting `Changes`; some symbol kinds or languages report `Supported: false` with an explanatory `Message` instead of a change list.
- There is no companion "apply rename" tool in this category — use this purely to inspect the blast radius of a rename before deciding whether to make the edit through the editor or safe-edit tools.

## Call Hierarchy

`call_hierarchy_get` returns who calls a symbol, what it calls, or both, as a tree:

```json
call_hierarchy_get({
  "documentPath": "src/NetVsMcp.Broker/Services/BrokerToolService.cs",
  "line": 590,
  "column": 24,
  "direction": "both",
  "maxDepth": 3,
  "sessionId": "..."
})
```

Key behavior:

- `direction` is `"incoming"` (callers, the default), `"outgoing"` (callees), or `"both"`.
- `maxDepth` defaults to `3` and is clamped to `1`-`6`. Incoming calls use Roslyn's `SymbolFinder.FindCallersAsync`; outgoing calls are found by walking the symbol's C#-only declaring syntax for invocations/object-creations and resolving each through the semantic model, since Roslyn has no direct "find callees" API.
- The tree is capped at roughly 500 total nodes across both directions to bound runaway trees; a node's `Truncated: true` means its children were cut off by the depth or node cap, and `IsRecursive: true` means expansion stopped because the symbol already appears earlier on the same path (a cycle).
- The result is `{ Supported, Message, Position, Direction, Symbol?, Incoming, Outgoing }`. Check `Supported` — a position with no resolvable symbol returns `Supported: true` with an empty `Incoming`/`Outgoing` and an explanatory `Message`.

## Diagnostics

Use `diagnostics_for_document` for compiler/analyzer diagnostics scoped to one file, and `diagnostics_binding_errors` for VSIX-surfaced binding diagnostics:

```json
diagnostics_for_document({
  "documentPath": "src/NetVsMcp.Broker/Services/BrokerToolService.cs",
  "includeWarnings": true,
  "maxItems": 200,
  "sessionId": "..."
})
diagnostics_binding_errors({ "target": "MainWindow.xaml", "timeoutMilliseconds": 5000, "sessionId": "..." })
```

Key behavior:

- `diagnostics_for_document` reads the full routed Error List and filters it to items whose file matches `documentPath` (path comparison tolerates absolute/relative and slash differences). `includeWarnings` defaults to `true`, `maxItems` defaults to `200` and must be greater than zero.
- The result is `{ DocumentPath, Items }`, where `Items` uses the same `ErrorListItemInfo` shape as the general error list tools.
- `diagnostics_binding_errors` only returns real data when the connected VSIX exposes a diagnostics automation backend (for example WPF/XAML binding error reporting); `target` is optional and `timeoutMilliseconds` must be greater than zero (default `5000`). The response is `{ Supported, Success, Message, Text?, Metadata? }` — check `Supported` before relying on `Text` or `Metadata`.

## Text And File Search

`workspace_search` and `find_in_files` both search text across files, but with different engines and capabilities:

```json
workspace_search({
  "query": "TODO",
  "filePattern": "*.cs",
  "rootPath": "src/NetVsMcp.Broker",
  "maxMatches": 100,
  "sessionId": "..."
})
find_in_files({
  "query": "class BrokerToolService",
  "rootPath": "src/NetVsMcp.Broker",
  "filePattern": "*.cs",
  "matchCase": false,
  "wholeWord": false,
  "useRegex": false,
  "maxResults": 100,
  "sessionId": "..."
})
```

Key behavior:

- `workspace_search` runs entirely in the broker process: it resolves `rootPath` (or, if omitted, the directory of the routed solution), enumerates files matching `filePattern` (default `*.*`), skips files that look binary, and does a plain case-insensitive substring match per line — there is no `matchCase`, `wholeWord`, or `useRegex` option. `query` is required and `maxMatches` must be greater than zero (default `100`).
- `find_in_files` instead delegates the search to the routed VSIX connection, so it can use richer Visual Studio search semantics: `matchCase`, `wholeWord`, and `useRegex` are all supported. `query` is required and `maxResults` must be greater than zero (default `100`); `rootPath` and `filePattern` are both optional.
- Both accept `rootPath` as relative or absolute; prefer forward slashes, for example `src/Project`.
- `editor_find` is the single-document analog of `find_in_files` for searching text inside one already-identified document rather than across the workspace.

## Git Context And Relevant Files

`git_context` and `open_relevant_files` are investigation helpers rather than code-intelligence lookups:

```json
git_context({ "rootPath": "src/NetVsMcp.Broker", "maxFiles": 100, "sessionId": "..." })
open_relevant_files({
  "paths": [
    "src/NetVsMcp.Broker/Services/BrokerToolService.cs",
    "src/NetVsMcp.Contracts/BrokerContracts.cs"
  ],
  "sessionId": "..."
})
```

Key behavior:

- `git_context` resolves its root the same way as `workspace_search` (explicit `rootPath`, else the routed solution's directory), then shells out to `git -C <root> status --short` and returns up to `maxFiles` changed paths. `maxFiles` must be greater than zero. Check `Supported` before trusting `ChangedFiles` — it is `false` with a `Message` when git is missing from PATH, the process fails to start, or the root is not a git working tree.
- `open_relevant_files` requires at least one path, deduplicates paths case-insensitively, and opens each one in turn through the same document-open call used by `document_open`. Treat it as a batch `document_open` for loading several files into the editor before reading or editing them. The result is `{ Documents }`, one entry per opened file.

## Reporting

Report findings with evidence:

- Active Visual Studio session used for routing.
- Resolved symbol, file, and line/column for any position-based lookup.
- Whether a tool reported `Supported: false` and why, so the caller knows a result is best-effort or unavailable.
- Relevant file paths, using forward slashes, for anything the caller may want to open or edit next.

## Troubleshooting

- Broker unavailable: ask the user to start the NetVsMcp Broker and check `http://127.0.0.1:5050/health`.
- No VS sessions: infer or confirm the target solution, launch Visual Studio with that solution (see the debugging guide for the launch command), then recheck `vs_list_sessions`.
- Ambiguous routing: call `vs_list_sessions` and retry with `sessionId`.
- Empty or missing symbol results: confirm `line`/`column` are 1-based and point at the symbol, not whitespace; retry with `symbol_context` to see the actual snippet Visual Studio resolved against.
- `Supported: false` from `find_implementations`, `rename_symbol_preview`, `diagnostics_binding_errors`, or `git_context`: report the reason from `Message` and fall back to `code_find_references`, manual inspection, `workspace_search`, or `find_in_files`.
- Path not found during search: prefer forward slashes and paths relative to the solution root; an absolute path also works.
