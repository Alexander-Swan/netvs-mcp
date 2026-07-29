# Edit Visual Studio With NetVsMcp

This is the agent-neutral document/editor workflow for this repository. Any AI agent can follow it when asked to open, read, inspect, edit, format, or safely propose changes to files in the Visual Studio solution through NetVsMcp.

## Session Routing

Start with the NetVsMcp session tools when they are available:

```json
vs_list_sessions()
document_active({ "sessionId": "..." })
```

Every routed document/editor call accepts optional `sessionId`, `solutionName`, and `solutionPath`. Resolution order is `sessionId`, normalized `solutionPath`, exact `solutionName`, active Visual Studio window, only registered instance. Use explicit routing whenever multiple Visual Studio windows are open.

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

Every path parameter in this guide (`path`, `documentPath`, `rootPath`) should prefer forward slashes, for example `src/Project/File.cs`, even on Windows. If a Windows path with backslashes must be sent in JSON, escape each backslash as `\\`.

## Opening, Reading, Listing, And Closing Documents

```json
document_active({ "sessionId": "..." })
document_list({ "sessionId": "..." })
document_open({ "path": "src/Project/File.cs", "sessionId": "..." })
document_read({ "path": "src/Project/File.cs", "sessionId": "..." })
document_close({
  "path": "src/Project/File.cs",
  "policy": "Save",
  "allowDirtyDiscard": false,
  "sessionId": "..."
})
```

Key behavior:

- `document_active` returns the active document path for the routed session, or `null` if nothing is active.
- `document_list` returns `documents` (each an `EditorDocumentInfo` with `name`, `path`, `language`, `isOpen`, `isSaved`) plus `activeDocument`.
- `document_read` returns `document`, `text`, `source`, and `usedLiveBuffer` — reads from the live editor buffer when the file is open and dirty, otherwise from disk.
- `document_open` returns an `EditorDocumentInfo` for the opened document; use `open_relevant_files({ "paths": [...] })` to open several documents in one call.
- `document_close.policy` is `NoSave` (default), `Save`, or `Discard`. Set `allowDirtyDiscard: true` to permit discarding unsaved changes; otherwise a dirty document blocks a `Discard`/`NoSave` close.

## Making Direct Edits

Use these when you want the change applied immediately, with no separate approval step:

```json
editor_insert({
  "path": "src/Project/File.cs",
  "line": 42,
  "column": 1,
  "text": "        // TODO\n",
  "saveAfterEdit": true,
  "sessionId": "..."
})

editor_replace({
  "path": "src/Project/File.cs",
  "startLine": 10,
  "startColumn": 1,
  "endLine": 12,
  "endColumn": 1,
  "text": "replacement text\n",
  "saveAfterEdit": true,
  "sessionId": "..."
})

document_write({
  "path": "src/Project/File.cs",
  "text": "entire new file contents",
  "createIfMissing": false,
  "saveAfterWrite": true,
  "sessionId": "..."
})

document_save({ "path": "src/Project/File.cs", "sessionId": "..." })
```

Key behavior:

- Line and column values are 1-based, matching the Visual Studio editor.
- `editor_insert` inserts `text` at `line`/`column`; `editor_replace` replaces the `startLine`/`startColumn` to `endLine`/`endColumn` range.
- All direct mutation tools return a `DocumentMutationResult` with `success`, `message`, `document`, `saved`, and `charactersChanged`.
- `document_write.createIfMissing` creates the file when it does not exist; `document_save.path` is optional — omit it to save the currently active document.
- `saveAfterEdit`/`saveAfterWrite` default to `false`; without it the change lands in the open editor buffer but is not persisted to disk.

## Selection Get And Set

```json
selection_get({ "sessionId": "..." })
selection_set({
  "path": "src/Project/File.cs",
  "startLine": 5,
  "startColumn": 1,
  "endLine": 5,
  "endColumn": 20,
  "sessionId": "..."
})
```

Key behavior:

- `selection_get` returns `null` when there is no meaningful selection, otherwise a `SelectionInfo` with `document`, `text`, `anchorLine`/`anchorColumn`, `activeLine`/`activeColumn`, and `isEmpty`.
- `selection_set` opens/activates `path` if needed and reports the resulting `SelectionInfo`.

## Formatting And Cleanup

```json
document_cleanup({ "path": "src/Project/File.cs", "saveAfterCleanup": true, "sessionId": "..." })
format_and_organize({ "path": "src/Project/File.cs", "saveAfterCleanup": true, "sessionId": "..." })
```

Key behavior:

- `document_cleanup` runs the Visual Studio format/cleanup command and returns `success`, `supported`, `message`, `document`, `saved`, and the `command` that was invoked (`null` if the VSIX could not report which cleanup command ran).
- `format_and_organize` is a thin wrapper around `document_cleanup` that adds an explicit status `message` noting whether an organize-imports command was reported; use it when the caller specifically cares about import organization status rather than raw cleanup output.
- Both tools default `saveAfterCleanup` to `false`.

## The Safe-Edit Preview/Approve/Reject Workflow

"Safe edit" here means a pending-diff workflow: the tool computes a proposed change and holds it as a `PendingEditInfo` (with `editId`, `operation`, `path`, `summary`, `originalText`, `proposedText`, the affected range, and `createdUtc`) without touching the document, until the pending edit is explicitly approved or rejected.

```json
edit_preview({
  "operation": "replace",
  "path": "src/Project/File.cs",
  "text": "new code",
  "startLine": 10,
  "startColumn": 1,
  "endLine": 12,
  "endColumn": 1,
  "sessionId": "..."
})

prepare_safe_edit({
  "operation": "insert",
  "path": "src/Project/File.cs",
  "text": "new line\n",
  "line": 42,
  "column": 1,
  "sessionId": "..."
})

edit_list_pending({ "sessionId": "..." })

edit_approve({ "editId": "...", "saveAfterApply": true, "sessionId": "..." })

apply_safe_edit_and_build({
  "editId": "...",
  "saveAfterApply": true,
  "includeWarnings": true,
  "maxItems": 200,
  "sessionId": "..."
})

edit_reject({ "editId": "...", "sessionId": "..." })
```

Key behavior:

- `operation` must be `write`, `insert`, or `replace`. `insert` requires `line`/`column`; `replace` requires `startLine`/`startColumn`/`endLine`/`endColumn`; `write` only needs `path` and `text`.
- `edit_preview` computes the diff directly; `prepare_safe_edit` additionally reads the current document first and returns both the `original` (`DocumentReadResult`) and the `preview` (`EditPreviewResult`) in one round trip — prefer it when you have not already read the file, to avoid drifting between the text you inspected and the text the preview was computed against.
- Nothing is written to the document or disk until `edit_approve` (or `apply_safe_edit_and_build`) is called with the returned `editId`. `edit_reject` discards the pending edit without applying it.
- `edit_approve` returns an `EditDecisionResult` with `applied`, the original `pendingEdit`, and the resulting `mutation` once applied.
- `apply_safe_edit_and_build` approves the edit, then builds the routed solution (waiting for the build to finish) and returns diagnostics in one call — use it instead of a separate `edit_approve` + `build_solution` + `errors_list` sequence when you want to confirm the change compiles immediately. `includeWarnings` and `maxItems` control the returned error list.
- `edit_list_pending` lists all currently pending edits for the routed session; use it to recover an `editId` or audit outstanding proposals before approving/rejecting.
- Prefer the safe-edit workflow over `editor_insert`/`editor_replace`/`document_write` whenever the user should see or confirm a diff before it lands, when multiple edits need to be reviewed together, or when you want a build-verified apply step. Prefer the direct editor tools for small, unambiguous, already-agreed-upon changes where an extra preview/approve round trip adds no value.

## Searching Text: `editor_find` vs `find_in_files`

```json
editor_find({
  "query": "TODO",
  "path": "src/Project/File.cs",
  "matchCase": false,
  "wholeWord": false,
  "useRegex": false,
  "maxResults": 100,
  "sessionId": "..."
})

find_in_files({
  "query": "TODO",
  "rootPath": "src/Project",
  "filePattern": "*.cs",
  "maxResults": 100,
  "sessionId": "..."
})
```

Key behavior:

- `editor_find` searches a single document. `path` is optional; when omitted it searches the active document.
- `find_in_files` searches across files under `rootPath` (optional; defaults to the solution root when omitted), narrowed by optional `filePattern`.
- Both return a `TextSearchResult` with `query`, `matchCount`, `truncated`, and `matches` (`path`, `line`, `column`, `lineText`, `matchText`). `matchCase`, `wholeWord`, and `useRegex` default to `false`; `maxResults` defaults to `100` and must be greater than zero.
- Use `editor_find` when you already know which document to search; use `find_in_files` for solution- or folder-wide text search.

## Troubleshooting

- Broker unavailable: ask the user to start the NetVsMcp Broker and check `http://127.0.0.1:5050/health`.
- No VS sessions: infer or confirm the target solution, launch Visual Studio with that solution, then recheck `vs_list_sessions`.
- Ambiguous routing: call `vs_list_sessions` and retry with `sessionId`.
- Edit fails validation: confirm `operation` matches the fields supplied (`insert` needs `line`/`column`, `replace` needs the full range) and that `path` is not empty.
- Pending edit not found: call `edit_list_pending` to confirm the `editId` is still pending; it may have already been approved or rejected.
- Document path problems: prefer absolute or solution-relative paths with forward slashes; NetVsMcp resolves paths against the Visual Studio solution when possible.
