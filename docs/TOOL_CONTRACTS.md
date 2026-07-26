# Tool And RPC Contracts

NetVsMcp has two public-ish contracts:

- MCP client to Broker: HTTP MCP tools on `http://127.0.0.1:5050/mcp`, plus a second endpoint at `http://127.0.0.1:5050/mcp-wu` scoped to the rarely used `ui_*`/`web_*` debuggee UI automation and web debugging tools (kept off the default endpoint to keep the advertised tool list smaller).
- Broker to VSIX: per-user named pipe with StreamJsonRpc.

The broker is the only MCP server. VSIX instances register with the broker and execute routed Visual Studio operations.

## Routing Fields

Every routed MCP tool accepts these optional fields:

```json
{
  "sessionId": null,
  "solutionName": null,
  "solutionPath": null
}
```

Resolution order:

1. `sessionId`
2. normalized `solutionPath`
3. exact `solutionName`
4. active Visual Studio window
5. only registered Visual Studio instance
6. fail with candidate session metadata

Routing failures return `success: false`, a message, and metadata such as `failureReason`, `candidateCount`, and `candidateSessionIds`.

## Broker-Only MCP Tools

| MCP tool | Owner | Notes |
| --- | --- | --- |
| `vs_list_sessions` | Broker | Returns registered `VsSessionInfo` records. |
| `vs_get_status` | Broker | Returns broker endpoint, pipe name, uptime, and session health. |
| `vs_get_capabilities` | Broker | Returns tool descriptors and VS capability categories. |
| `vs_get_session` | Broker | Resolves a target and returns session status. |
| `vs_select_session` | Broker | Resolver helper; does not persist global selection state. |
| `vs_ping` | Broker | Broker health, optionally with routed target status. |

## Broker-Routed MCP Tools

| MCP tool | VSIX RPC method |
| --- | --- |
| `document_active` | `GetActiveDocumentAsync` for the shared status path, `DocumentActiveAsync` for rich editor path |
| `document_read` | `DocumentReadAsync` |
| `document_open` | `DocumentOpenAsync` |
| `selection_get` | `SelectionGetAsync` |
| `document_write` | `DocumentWriteAsync` |
| `document_save` | `DocumentSaveAsync` |
| `editor_insert` | `EditorInsertAsync` |
| `editor_replace` | `EditorReplaceAsync` |
| `editor_goto_line` | `EditorGotoLineAsync` |
| `selection_set` | `SelectionSetAsync` |
| `document_cleanup` | `DocumentCleanupAsync` |
| `edit_preview` | `EditPreviewAsync` |
| `edit_approve` | `EditApproveAsync` |
| `edit_reject` | `EditRejectAsync` |
| `edit_list_pending` | `EditListPendingAsync` |
| `code_document_symbols` | `ListDocumentSymbolsAsync` for shared status path, `CodeDocumentSymbolsAsync` for rich navigation path |
| `code_go_to_definition` | `CodeGoToDefinitionAsync` |
| `code_find_references` | `CodeFindReferencesAsync` |
| `build_solution` | `BuildSolutionAsync` |
| `build_status` | `BuildStatusAsync` |
| `errors_list` | `ErrorsListAsync` |
| `output_read` | `OutputReadAsync` |
| `debug_status` | `DebugStatusAsync` |
| `debug_get_mode` | `DebugGetModeAsync` |
| `debug_start` | `DebugStartAsync` |
| `debug_stop` | `DebugStopAsync` |
| `debug_continue` | `DebugContinueAsync` |
| `debug_break` | `DebugBreakAsync` |
| `debug_step` | `DebugStepAsync` |
| `breakpoint_set` | `BreakpointSetAsync` |
| `breakpoint_list` | `BreakpointListAsync` |
| `breakpoint_remove` | `BreakpointRemoveAsync` |
| `breakpoint_enable` | `BreakpointEnableAsync` |
| `debug_get_callstack` | `DebugGetCallstackAsync` |
| `debug_get_locals` | `DebugGetLocalsAsync` |
| `debug_evaluate` | `DebugEvaluateAsync` |
| `solution_info` | `SolutionInfoAsync` |
| `project_list` | `ProjectListAsync` |
| `project_info` | `ProjectInfoAsync` |
| `startup_project_get` | `StartupProjectGetAsync` |
| `startup_project_set` | `StartupProjectSetAsync` |
| `test_discover` | `TestDiscoverAsync` |
| `test_run` | `TestRunAsync` |
| `test_results` | `TestResultsAsync` |

## Registration RPC

VSIX calls the broker over StreamJsonRpc:

```text
RegisterAsync(VsSessionRegistration registration, CancellationToken cancellationToken)
UpdateAsync(VsSessionUpdate update, CancellationToken cancellationToken)
HeartbeatAsync(string sessionId, CancellationToken cancellationToken)
UnregisterAsync(string sessionId, CancellationToken cancellationToken)
```

The broker stores session metadata and maps the same pipe connection to a reverse VSIX RPC proxy for later routed calls.

## Shared Status RPC

The broker creates a reverse proxy shaped like `IVisualStudioSessionRpc`. The VSIX exposes these exact method names:

```text
GetStatusAsync(CancellationToken cancellationToken)
GetActiveDocumentAsync(CancellationToken cancellationToken)
ListDocumentSymbolsAsync(string documentPath, CancellationToken cancellationToken)
```

These return `ToolResponse`-shaped values so the VSIX can report handled failures without turning every Visual Studio problem into a transport exception.

## Error Behavior

Use these layers consistently:

- Routing failure: broker returns `ToolResponse<T>` with `success: false`.
- Missing VSIX connection: broker returns a structured routed failure.
- VSIX handled failure: VSIX returns a response with `success: false` or `supported: false` where the specific tool model supports it.
- VSIX RPC exception: broker converts it to a structured `RpcFailure`.
- Invalid tool input: broker validates obvious fields before routing when possible.

High-impact tools, especially edits and debugger controls, should fail closed on ambiguous routing.

## Open Gaps

- End-to-end runtime validation inside an experimental Visual Studio instance is still required.
- VSIX mirrors shared wire DTOs locally because it targets `net472` while `NetVsMcp.Contracts` targets `net10.0`.
- Token authentication and audit logging are planned but not complete.
