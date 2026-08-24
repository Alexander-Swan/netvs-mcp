# NetVsMcp VSIX Design

## Role

`NetVsMcp.Vsix` runs inside each Visual Studio instance. It does not host MCP itself. The local broker remains the only MCP server and routes requests to the registered VSIX instance that owns the selected solution/session.

```text
MCP client
  -> NetVsMcp.Broker on 127.0.0.1
    -> per-user named pipe / StreamJsonRpc
      -> NetVsMcp.Vsix inside Visual Studio
```

## Current Skeleton

The project uses the SDK-style VSIX shape:

- target framework: `net472`
- VS SDK references: `Microsoft.VisualStudio.SDK`
- VSIX build tooling: `Microsoft.VSSDK.BuildTools`
- install range: Visual Studio `[17.0,19.0)` for Visual Studio 2022 and 2026
- product architecture: `amd64`
- package loading contexts: no solution and solution exists
- broker transport dependency: `StreamJsonRpc`

The local machine did not have `dotnet new` VSIX templates installed, so this skeleton is hand-authored from the SDK-style project shape. A full packaging/build requires the Visual Studio extension development workload and compatible VSSDK build tooling.

## Registration Lifecycle

On package initialization:

1. Switch to the Visual Studio UI thread.
2. Capture the current session snapshot.
3. Connect to the local broker over the per-user named pipe.
4. Register this Visual Studio instance.
5. Start heartbeat/state refresh.
6. Subscribe to Visual Studio events and push updates.
7. Unregister and disconnect when the package is disposed.

The current VSIX implementation has a reconnecting lifecycle:

- connect timeout: 2 seconds
- heartbeat interval: 10 seconds
- reconnect backoff: starts at 1 second and caps at 30 seconds
- state changes wake the heartbeat loop immediately
- failed unregisters are ignored during shutdown because the broker may already be gone

Registration payload shape:

```json
{
  "sessionId": "vs-12345",
  "processId": 12345,
  "visualStudioVersion": "18.0",
  "edition": "Enterprise",
  "solutionName": "NetVsMcp",
  "solutionPath": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\NetVsMcp.slnx",
  "activeDocument": "src\\NetVsMcp.Broker\\BrokerApp.cs",
  "debuggerMode": "Design",
  "isActiveWindow": true,
  "lastSeenUtc": "2026-07-22T14:16:00Z",
  "capabilities": ["editor", "navigation", "build", "debugger"]
}
```

## State Updates

The VSIX should notify the broker when these change:

- solution opened or closed
- active document changed
- debugger mode changed
- active Visual Studio window changed
- package reconnects after broker restart

The current event hooks are intentionally lightweight:

- `SolutionEvents.Opened`
- `SolutionEvents.AfterClosing`
- `WindowEvents.WindowActivated`
- `DebuggerEvents.OnEnterBreakMode`
- `DebuggerEvents.OnEnterDesignMode`
- `DebuggerEvents.OnEnterRunMode`

The heartbeat refreshes `lastSeenUtc`, solution, active document, debugger mode, active-window state, and capability list. Stopped heartbeats should make the broker mark the session as stale rather than immediately forgetting it.

Selection and build status events are still planned; they should be added beside the current state monitor once the editor/build command services are implemented.

## Broker RPC Contract Expectation

The VSIX uses internal DTOs with the expected wire shape and calls the shared broker registration method names over StreamJsonRpc.

The per-user pipe name is:

```text
netvs-mcp-{sanitized Windows user SID}
```

Example:

```text
netvs-mcp-S-1-5-21-...
```

The VSIX attaches a composed `VisualStudioCapabilityRpcTarget` to the same StreamJsonRpc connection used for registration and heartbeat. This lets the broker invoke VS-side capability methods over the existing named pipe after a session registers. Registration flows from VSIX to broker with the shared `IBrokerRegistrationRpc` method names:

```text
RegisterAsync
UpdateAsync
HeartbeatAsync
UnregisterAsync
```

The VSIX sends DTOs with the same JSON property shape as `VsSessionRegistration` and `VsSessionUpdate`. The VSIX currently keeps local wire DTOs instead of referencing `NetVsMcp.Contracts` directly because the VSIX targets `net472` and the contracts assembly targets `net10.0`.

The broker creates a reverse proxy for `IVisualStudioSessionRpc`, so the VSIX target exposes these exact broker-callable methods:

```text
GetStatusAsync(CancellationToken cancellationToken)
GetActiveDocumentAsync(CancellationToken cancellationToken)
ListDocumentSymbolsAsync(string documentPath, CancellationToken cancellationToken)
```

Those methods return `ToolResponse`-shaped values compatible with the shared contract:

- `GetStatusAsync` returns the current `VsSessionInfo` wire shape
- `GetActiveDocumentAsync` returns the active document path, falling back to document name
- `ListDocumentSymbolsAsync` returns conservative symbol label strings from the existing navigation service

Additional broker-to-VSIX capability calls use StreamJsonRpc method names such as:

```text
DocumentActiveAsync
CodeDocumentSymbolsAsync
BuildSolutionAsync
DebugStatusAsync
BreakpointEnableAsync
```

The broker-facing MCP tool names remain snake_case. The broker is responsible for mapping MCP names like `document_active`, `code_document_symbols`, `build_solution`, `debug_status`, and `breakpoint_enable` to the StreamJsonRpc method names documented below.

## Execution Surface

The skeleton defines service interfaces for the future VS-side command handlers:

- editor: active document, document read/open, selection
- navigation: go to definition, find references, document symbols
- build: solution/project build and cancel
- debugger: start/stop/continue/break/step/breakpoint set

Once `NetVsMcp.Contracts` lands, replace the placeholder registration models with shared DTOs and add the StreamJsonRpc command dispatcher here.

## Editor RPC Contract Expectation

The VSIX-side editor capability service now supports the first broker-routed editor tools. These method names are the expected StreamJsonRpc surface once the broker starts dispatching commands into the VSIX:

```text
DocumentActiveAsync(CancellationToken cancellationToken)
DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken)
DocumentOpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken)
SelectionGetAsync(CancellationToken cancellationToken)
DocumentWriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken)
DocumentSaveAsync(DocumentSaveRequest request, CancellationToken cancellationToken)
EditorInsertAsync(EditorInsertRequest request, CancellationToken cancellationToken)
EditorReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken)
EditorGotoLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken)
SelectionSetAsync(SelectionSetRequest request, CancellationToken cancellationToken)
DocumentCleanupAsync(DocumentCleanupRequest request, CancellationToken cancellationToken)
EditPreviewAsync(EditPreviewRequest request, CancellationToken cancellationToken)
EditApproveAsync(EditDecisionRequest request, CancellationToken cancellationToken)
EditRejectAsync(EditDecisionRequest request, CancellationToken cancellationToken)
EditListPendingAsync(CancellationToken cancellationToken)
```

Broker-facing MCP tool mapping:

```text
document_active -> DocumentActiveAsync
document_read   -> DocumentReadAsync
document_open   -> DocumentOpenAsync
selection_get   -> SelectionGetAsync
document_write  -> DocumentWriteAsync
document_save   -> DocumentSaveAsync
editor_insert   -> EditorInsertAsync
editor_replace  -> EditorReplaceAsync
editor_goto_line -> EditorGotoLineAsync
selection_set   -> SelectionSetAsync
document_cleanup -> DocumentCleanupAsync
edit_preview   -> EditPreviewAsync
edit_approve   -> EditApproveAsync
edit_reject    -> EditRejectAsync
edit_list_pending -> EditListPendingAsync
```

`document_read` request:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs"
}
```

`document_open` request:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs"
}
```

Relative paths are resolved against the active solution directory. Absolute paths are used as-is.

`document_active` and `document_open` return:

```json
{
  "name": "NetVsMcpPackage.cs",
  "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "language": "CSharp",
  "isOpen": true,
  "isSaved": true
}
```

`document_read` returns:

```json
{
  "document": {
    "name": "NetVsMcpPackage.cs",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "language": "CSharp",
    "isOpen": true,
    "isSaved": false
  },
  "text": "...",
  "source": "live",
  "usedLiveBuffer": true
}
```

`document_read` prefers the live Visual Studio text buffer for open documents, so unsaved edits are visible to the agent. If the document is not open, it falls back to disk.

`document_write` replaces the complete live text buffer for a file. It opens the document in Visual Studio first, so edits flow through the editor buffer. Missing files are rejected unless `createIfMissing` is true.

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "text": "new file contents",
  "createIfMissing": false,
  "saveAfterWrite": false
}
```

`document_save` saves an open document. If `path` is omitted, it saves the active document; if a path is supplied, the document must already be open.

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs"
}
```

`editor_insert` inserts text at a 1-based Visual Studio line/column position:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "line": 10,
  "column": 5,
  "text": "inserted text",
  "saveAfterEdit": false
}
```

`editor_replace` replaces a 1-based Visual Studio text range:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "startLine": 10,
  "startColumn": 5,
  "endLine": 10,
  "endColumn": 18,
  "text": "replacement text",
  "saveAfterEdit": false
}
```

Mutation methods return:

```json
{
  "success": true,
  "message": null,
  "document": {
    "name": "NetVsMcpPackage.cs",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "language": "CSharp",
    "isOpen": true,
    "isSaved": false
  },
  "saved": false,
  "charactersChanged": 16
}
```

`editor_goto_line` opens/activates a document and moves the caret:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "line": 42,
  "column": 1
}
```

`selection_set` opens/activates a document and selects a 1-based Visual Studio range:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "startLine": 10,
  "startColumn": 5,
  "endLine": 12,
  "endColumn": 18
}
```

`document_cleanup` currently invokes Visual Studio's `Edit.FormatDocument` command for text documents and can optionally save after formatting:

```json
{
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "saveAfterCleanup": false
}
```

`document_cleanup` returns:

```json
{
  "success": true,
  "supported": true,
  "message": null,
  "document": {
    "name": "NetVsMcpPackage.cs",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "language": "CSharp",
    "isOpen": true,
    "isSaved": false
  },
  "saved": false,
  "command": "Edit.FormatDocument"
}
```

Mutation tools are intentionally conservative: unsupported non-text documents, unresolved paths, and invalid ranges return structured errors instead of writing directly through ad hoc disk parsing.

Safe edit previews are kept in memory inside the running VSIX package. They do not persist across Visual Studio restarts, extension reloads, or process crashes.

`edit_preview` captures a pending `write`, `insert`, or `replace` operation without applying it:

```json
{
  "operation": "replace",
  "path": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "text": "replacement text",
  "startLine": 10,
  "startColumn": 5,
  "endLine": 10,
  "endColumn": 18,
  "saveAfterEdit": false
}
```

For `write`, `text` is the full proposed document content and `createIfMissing` may be set. For `insert`, use `line` and `column`. For `replace`, use `startLine`, `startColumn`, `endLine`, and `endColumn`. All line and column values follow Visual Studio DTE conventions and are 1-based.

`edit_preview` returns:

```json
{
  "success": true,
  "message": null,
  "pendingEdit": {
    "editId": "edit-000001",
    "operation": "replace",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "summary": "Replace range 10:5-10:18 in NetVsMcpPackage.cs (13 -> 16 chars).",
    "originalText": "old text",
    "proposedText": "replacement text",
    "startLine": 10,
    "startColumn": 5,
    "endLine": 10,
    "endColumn": 18,
    "originalLength": 13,
    "proposedLength": 16,
    "createdUtc": "2026-07-22T15:00:00Z"
  }
}
```

`edit_approve` applies the pending edit through the same VS editor-buffer mutation path used by `document_write`, `editor_insert`, and `editor_replace`, then removes it from the queue:

```json
{
  "editId": "edit-000001",
  "saveAfterApply": false
}
```

`edit_reject` removes a pending edit without applying it and uses the same request shape. `edit_approve` and `edit_reject` return:

```json
{
  "success": true,
  "message": null,
  "editId": "edit-000001",
  "applied": true,
  "pendingEdit": {
    "editId": "edit-000001",
    "operation": "replace",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "summary": "Replace range 10:5-10:18 in NetVsMcpPackage.cs (13 -> 16 chars).",
    "originalText": "old text",
    "proposedText": "replacement text",
    "startLine": 10,
    "startColumn": 5,
    "endLine": 10,
    "endColumn": 18,
    "originalLength": 13,
    "proposedLength": 16,
    "createdUtc": "2026-07-22T15:00:00Z"
  },
  "mutation": {
    "success": true,
    "message": null,
    "document": {
      "name": "NetVsMcpPackage.cs",
      "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "language": "CSharp",
      "isOpen": true,
      "isSaved": false
    },
    "saved": false,
    "charactersChanged": 16
  }
}
```

`edit_list_pending` returns:

```json
{
  "pendingEdits": [
    {
      "editId": "edit-000001",
      "operation": "replace",
      "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "summary": "Replace range 10:5-10:18 in NetVsMcpPackage.cs (13 -> 16 chars).",
      "originalText": "old text",
      "proposedText": "replacement text",
      "startLine": 10,
      "startColumn": 5,
      "endLine": 10,
      "endColumn": 18,
      "originalLength": 13,
      "proposedLength": 16,
      "createdUtc": "2026-07-22T15:00:00Z"
    }
  ]
}
```

Preview approval does not currently re-check that the live buffer still matches the captured `originalText`; that should be added before treating this as a conflict-safe edit protocol.

`selection_get` returns:

```json
{
  "document": {
    "name": "NetVsMcpPackage.cs",
    "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "language": "CSharp",
    "isOpen": true,
    "isSaved": true
  },
  "text": "selected text",
  "anchorLine": 10,
  "anchorColumn": 5,
  "activeLine": 12,
  "activeColumn": 18,
  "isEmpty": false
}
```

Line and column values follow Visual Studio DTE conventions and are 1-based.

## Code Navigation RPC Contract Expectation

The VSIX-side navigation capability service now supports the first broker-routed code navigation tool. It uses Visual Studio's live Roslyn workspace through `VisualStudioWorkspace`, so symbols are resolved from the solution state known to Visual Studio rather than from standalone disk parsing.

Expected StreamJsonRpc method:

```text
CodeDocumentSymbolsAsync(DocumentSymbolsRequest request, CancellationToken cancellationToken)
CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken)
CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken)
```

Broker-facing MCP tool mapping:

```text
code_document_symbols  -> CodeDocumentSymbolsAsync
code_go_to_definition  -> CodeGoToDefinitionAsync
code_find_references   -> CodeFindReferencesAsync
```

`code_document_symbols` request:

```json
{
  "documentPath": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs"
}
```

`documentPath` is optional. If omitted, the VSIX uses the active Visual Studio document. Relative paths are resolved against the active solution directory. Absolute paths are used as-is.

`code_document_symbols` returns:

```json
{
  "documentPath": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "symbols": [
    {
      "name": "NetVsMcpPackage",
      "kind": "NamedType",
      "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "line": 12,
      "column": 21,
      "containingType": null,
      "containingNamespace": "NetVsMcp.Vsix"
    },
    {
      "name": "InitializeAsync",
      "kind": "Method",
      "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "line": 18,
      "column": 35,
      "containingType": "NetVsMcp.Vsix.NetVsMcpPackage",
      "containingNamespace": "NetVsMcp.Vsix"
    }
  ]
}
```

Line and column values are 1-based. The initial implementation returns namespaces, types, methods, properties, fields, and events.

`code_go_to_definition` and `code_find_references` use the same 1-based source position request:

```json
{
  "documentPath": "src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
  "line": 18,
  "column": 35
}
```

`code_go_to_definition` returns the symbol at the requested position, all source definitions Roslyn finds for it, and whether the VSIX navigated Visual Studio to the first definition:

```json
{
  "symbol": {
    "name": "InitializeAsync",
    "kind": "Method",
    "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "line": 18,
    "column": 35,
    "containingType": "NetVsMcp.Vsix.NetVsMcpPackage",
    "containingNamespace": "NetVsMcp.Vsix"
  },
  "definitions": [
    {
      "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "line": 18,
      "column": 35,
      "symbol": {
        "name": "InitializeAsync",
        "kind": "Method",
        "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
        "line": 18,
        "column": 35,
        "containingType": "NetVsMcp.Vsix.NetVsMcpPackage",
        "containingNamespace": "NetVsMcp.Vsix"
      }
    }
  ],
  "navigated": true
}
```

`code_find_references` returns the resolved symbol and source reference locations:

```json
{
  "symbol": {
    "name": "InitializeAsync",
    "kind": "Method",
    "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
    "line": 18,
    "column": 35,
    "containingType": "NetVsMcp.Vsix.NetVsMcpPackage",
    "containingNamespace": "NetVsMcp.Vsix"
  },
  "references": [
    {
      "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
      "line": 18,
      "column": 35,
      "isImplicit": false,
      "symbol": {
        "name": "InitializeAsync",
        "kind": "Method",
        "file": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcpPackage.cs",
        "line": 18,
        "column": 35,
        "containingType": "NetVsMcp.Vsix.NetVsMcpPackage",
        "containingNamespace": "NetVsMcp.Vsix"
      }
    }
  ]
}
```

If Roslyn cannot resolve a symbol at the requested position, the VSIX returns a null `symbol` and an empty `definitions` or `references` collection.

## Build And Diagnostics RPC Contract Expectation

The VSIX-side build capability service now supports the first broker-routed build and diagnostics tools. It uses Visual Studio DTE on the UI thread for solution builds, build status, Error List extraction, and Output window pane reads.

Expected StreamJsonRpc methods:

```text
BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
BuildStatusAsync(CancellationToken cancellationToken)
ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken)
OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken)
```

Broker-facing MCP tool mapping:

```text
build_solution -> BuildSolutionAsync
build_status   -> BuildStatusAsync
errors_list    -> ErrorsListAsync
output_read    -> OutputReadAsync
```

`build_solution` request:

```json
{
  "waitForBuildToFinish": true
}
```

`build_solution` returns:

```json
{
  "status": {
    "state": "vsBuildStateDone",
    "lastBuildInfo": 0
  },
  "lastBuildInfo": 0
}
```

`lastBuildInfo` follows Visual Studio DTE conventions: after a completed build, `0` means no failed projects and positive values indicate failed project count.

`build_status` returns:

```json
{
  "state": "vsBuildStateInProgress",
  "lastBuildInfo": 0
}
```

`errors_list` request:

```json
{
  "includeWarnings": true,
  "maxItems": 200
}
```

`errors_list` returns:

```json
{
  "items": [
    {
      "description": "The name 'value' does not exist in the current context",
      "file": "D:\\Work\\App\\Program.cs",
      "line": 42,
      "column": 17,
      "level": "vsBuildErrorLevelHigh",
      "project": "App"
    }
  ]
}
```

`output_read` request:

```json
{
  "paneName": "Build",
  "maxChars": 20000
}
```

If `paneName` is omitted, the VSIX prefers the `Build` output pane and otherwise returns the first available output pane.

`output_read` returns:

```json
{
  "paneName": "Build",
  "text": "Build started...\r\nBuild succeeded.",
  "truncated": false
}
```

When `maxChars` is smaller than the pane content, the VSIX returns the trailing text and marks `truncated` as `true`.

## Debugger RPC Contract Expectation

The VSIX-side debugger capability service now supports the first broker-routed debugger tools. It uses Visual Studio DTE on the UI thread for execution control, breakpoints, call stack, locals, and expression evaluation.

Expected StreamJsonRpc methods:

```text
DebugStartAsync(CancellationToken cancellationToken)
DebugStopAsync(CancellationToken cancellationToken)
DebugContinueAsync(CancellationToken cancellationToken)
DebugBreakAsync(CancellationToken cancellationToken)
DebugStepAsync(DebugStepRequest request, CancellationToken cancellationToken)
DebugStatusAsync(CancellationToken cancellationToken)
DebugGetModeAsync(CancellationToken cancellationToken)
BreakpointSetAsync(BreakpointSetRequest request, CancellationToken cancellationToken)
BreakpointListAsync(CancellationToken cancellationToken)
BreakpointRemoveAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken)
BreakpointEnableAsync(BreakpointEnableRequest request, CancellationToken cancellationToken)
DebugGetCallstackAsync(CancellationToken cancellationToken)
DebugGetLocalsAsync(CancellationToken cancellationToken)
DebugEvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken)
WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken)
WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken)
WatchListAsync(CancellationToken cancellationToken)
DebugGetThreadsAsync(CancellationToken cancellationToken)
ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken)
ModuleListAsync(CancellationToken cancellationToken)
ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken)
ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
```

Broker-facing MCP tool mapping:

```text
debug_start         -> DebugStartAsync
debug_stop          -> DebugStopAsync
debug_continue      -> DebugContinueAsync
debug_break         -> DebugBreakAsync
debug_step          -> DebugStepAsync
debug_status        -> DebugStatusAsync
debug_get_mode      -> DebugGetModeAsync
breakpoint_set      -> BreakpointSetAsync
breakpoint_list     -> BreakpointListAsync
breakpoint_remove   -> BreakpointRemoveAsync
breakpoint_enable   -> BreakpointEnableAsync
debug_get_callstack -> DebugGetCallstackAsync
debug_get_locals    -> DebugGetLocalsAsync
debug_evaluate      -> DebugEvaluateAsync
watch_add           -> WatchAddAsync
watch_remove        -> WatchRemoveAsync
watch_list          -> WatchListAsync
debug_get_threads   -> DebugGetThreadsAsync
thread_switch       -> ThreadSwitchAsync
module_list         -> ModuleListAsync
immediate_execute   -> ImmediateExecuteAsync
exception_settings_get -> ExceptionSettingsGetAsync
exception_settings_set -> ExceptionSettingsSetAsync
```

Debugger state response:

```json
{
  "mode": "dbgBreakMode"
}
```

`debug_status` and `debug_get_mode` are no-op status tools. They return the same state payload without changing debugger execution.

`debug_step` request:

```json
{
  "stepKind": "Over"
}
```

Supported step kinds are `Into`, `Over`, and `Out`.

`breakpoint_set` request:

```json
{
  "documentPath": "src\\App\\Program.cs",
  "line": 42,
  "column": 1,
  "condition": "count > 3"
}
```

`breakpoint_set` and `breakpoint_list` return breakpoint records:

```json
{
  "name": "Program.cs, line 42",
  "file": "D:\\Work\\App\\Program.cs",
  "line": 42,
  "column": 1,
  "functionName": null,
  "condition": "count > 3",
  "enabled": true
}
```

`breakpoint_remove` request:

```json
{
  "name": "Program.cs, line 42",
  "documentPath": "src\\App\\Program.cs",
  "line": 42
}
```

`breakpoint_enable` request:

```json
{
  "name": "Program.cs, line 42",
  "documentPath": "src\\App\\Program.cs",
  "line": 42,
  "enabled": false
}
```

`breakpoint_enable` returns:

```json
{
  "updated": 1,
  "breakpoints": [
    {
      "name": "Program.cs, line 42",
      "file": "D:\\Work\\App\\Program.cs",
      "line": 42,
      "column": 1,
      "functionName": null,
      "condition": "count > 3",
      "enabled": false
    }
  ]
}
```

The VSIX removes and enables/disables breakpoints by exact name match or by exact file and line match. Relative breakpoint paths are resolved against the active solution directory, matching `breakpoint_set` behavior.

`debug_get_callstack` returns:

```json
{
  "state": {
    "mode": "dbgBreakMode"
  },
  "frames": [
    {
      "functionName": "App.Program.Main(string[])",
      "file": null,
      "line": 0,
      "column": 0
    }
  ]
}
```

The EnvDTE `StackFrame` API available to this project exposes function names and frame locals, but not source file, line, or column directly. Those fields are reserved in the contract and currently returned as `null` or `0` until a runtime-validated richer debugger frame API is wired.

`debug_get_locals` returns:

```json
{
  "state": {
    "mode": "dbgBreakMode"
  },
  "locals": [
    {
      "name": "count",
      "value": "4",
      "type": "int",
      "isValidValue": true
    }
  ]
}
```

`debug_evaluate` request:

```json
{
  "expression": "count + 1",
  "timeoutMilliseconds": 5000
}
```

`debug_evaluate` returns:

```json
{
  "state": {
    "mode": "dbgBreakMode"
  },
  "expression": {
    "name": "count + 1",
    "value": "5",
    "type": "int",
    "isValidValue": true
  }
}
```

`watch_add` request:

```json
{
  "expression": "count"
}
```

`watch_remove` uses the same request shape and matches by watch expression/name. In the current VSIX target, EnvDTE does not expose watch expression mutation, so watch operations return an unsupported result:

```json
{
  "supported": false,
  "success": false,
  "message": "Watch expressions are not exposed through this VSIX skeleton yet.",
  "watch": null
}
```

`watch_list` returns:

```json
{
  "supported": false,
  "message": "Watch expressions are not exposed through this VSIX skeleton yet.",
  "watches": []
}
```

`debug_get_threads` returns the current program's EnvDTE threads when a debug program is active:

```json
{
  "supported": true,
  "message": null,
  "threads": [
    {
      "id": 1234,
      "name": "Main Thread",
      "isCurrent": true
    }
  ]
}
```

`thread_switch` request:

```json
{
  "threadId": 1234
}
```

`thread_switch` returns:

```json
{
  "supported": true,
  "success": true,
  "message": null,
  "thread": {
    "id": 1234,
    "name": "Main Thread",
    "isCurrent": true
  }
}
```

`module_list` currently returns an unsupported result because the EnvDTE `Program` API available to this VSIX target does not expose module enumeration:

```json
{
  "supported": false,
  "message": "Module listing is not exposed through this VSIX skeleton yet.",
  "modules": []
}
```

`immediate_execute` request:

```json
{
  "statement": "? count"
}
```

`immediate_execute` currently returns `supported: false`; command routing to the Immediate window needs runtime validation so the VSIX does not send text to the wrong shell window.

`exception_settings_get` and `exception_settings_set` request:

```json
{
  "exceptionName": "System.InvalidOperationException",
  "breakOnThrown": true
}
```

Exception settings operations currently return `supported: false`; a later slice should wire the Visual Studio debugger exception settings APIs directly.

## Solution, Project, And Test RPC Contract Expectation

The VSIX-side solution capability service now supports broker-routed solution and project metadata operations through Visual Studio DTE. Test operations are intentionally conservative skeletons until Visual Studio Test Platform integration is wired.

Expected StreamJsonRpc methods:

```text
SolutionInfoAsync(CancellationToken cancellationToken)
ProjectListAsync(CancellationToken cancellationToken)
ProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken)
StartupProjectGetAsync(CancellationToken cancellationToken)
StartupProjectSetAsync(StartupProjectSetRequest request, CancellationToken cancellationToken)
TestDiscoverAsync(TestDiscoverRequest request, CancellationToken cancellationToken)
TestRunAsync(TestRunRequest request, CancellationToken cancellationToken)
TestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken)
```

Broker-facing MCP tool mapping:

```text
solution_info       -> SolutionInfoAsync
project_list        -> ProjectListAsync
project_info        -> ProjectInfoAsync
startup_project_get -> StartupProjectGetAsync
startup_project_set -> StartupProjectSetAsync
test_discover       -> TestDiscoverAsync
test_run            -> TestRunAsync
test_debug          -> TestDebugAsync
test_results        -> TestResultsAsync
```

`solution_info` returns:

```json
{
  "name": "NetVsMcp",
  "path": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\NetVsMcp.slnx",
  "isOpen": true,
  "projectCount": 4,
  "startupProject": "src\\NetVsMcp.Broker\\NetVsMcp.Broker.csproj"
}
```

`project_list` returns:

```json
{
  "projects": [
    {
      "name": "NetVsMcp.Vsix",
      "uniqueName": "src\\NetVsMcp.Vsix\\NetVsMcp.Vsix.csproj",
      "fullName": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\src\\NetVsMcp.Vsix\\NetVsMcp.Vsix.csproj",
      "kind": "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}",
      "isLoaded": true,
      "language": "CSharp",
      "outputFileName": "NetVsMcp.Vsix.dll"
    }
  ]
}
```

`project_info` request:

```json
{
  "projectName": "NetVsMcp.Vsix"
}
```

`project_info` returns a single project object or `null` when the project is not found or cannot be read through DTE.

`startup_project_get` returns:

```json
{
  "projects": [
    "src\\NetVsMcp.Broker\\NetVsMcp.Broker.csproj"
  ],
  "isMultiStartup": false
}
```

`startup_project_set` request:

```json
{
  "projectName": "NetVsMcp.Broker"
}
```

`startup_project_set` resolves by project name, unique name, or full path and sets a single startup project through DTE.

Test operation requests:

```json
{
  "projectName": "NetVsMcp.Broker.Tests",
  "filter": "FullyQualifiedName~SessionRegistry"
}
```

`test_discover`, `test_run`, and `test_results` return:

```json
{
  "supported": true,
  "message": "Ran 12 test result(s) from NetVsMcp.Broker.Tests. RunId: 4f2d...",
  "tests": [],
  "results": [
    {
      "name": "NetVsMcp.Broker.Tests.SessionRegistryTests.Register_AddsSession",
      "outcome": "Passed",
      "duration": "00:00:00.0123456",
      "message": null
    }
  ]
}
```

The VSIX resolves `projectName` against the active solution and runs `dotnet test` for either the selected project or the active solution. `filter` is passed through to `dotnet test --filter`, and `test_run` writes TRX results to a temporary NetVsMcp results directory so the VSIX can return structured result rows. `test_results` returns the last captured run in the current VSIX session, optionally constrained by `runId`.

`test_debug` requires a non-empty `filter`, starts `dotnet test` with `VSTEST_HOST_DEBUG=1`, waits for the spawned test host, and attaches the Visual Studio debugger to it. It also accepts `noBuild`, `configuration`, and `framework`, which are passed through to `dotnet test` as `--no-build`, `--configuration`, and `--framework`. The result is:

```json
{
  "supported": true,
  "message": "Started 'NetVsMcp.Broker.Tests' with VSTEST_HOST_DEBUG=1 and attached to testhost process '6404'.",
  "projectName": "NetVsMcp.Broker.Tests",
  "filter": "FullyQualifiedName~BrokerToolServiceTests.TestDebug_RequiresFilter",
  "testHostProcessId": 6404,
  "testHostProcessName": "dotnet",
  "testRunnerProcessId": 6200,
  "testRunnerProcessName": "dotnet",
  "commandLine": "dotnet test \"D:\\Work\\Learn\\dotnet\\netvs-mcp\\tests\\NetVsMcp.Broker.Tests\\NetVsMcp.Broker.Tests.csproj\" --filter \"FullyQualifiedName~BrokerToolServiceTests.TestDebug_RequiresFilter\" --no-build --configuration Debug --framework net10.0",
  "workingDirectory": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\tests\\NetVsMcp.Broker.Tests",
  "targetPath": "D:\\Work\\Learn\\dotnet\\netvs-mcp\\tests\\NetVsMcp.Broker.Tests\\NetVsMcp.Broker.Tests.csproj",
  "attachTimeoutSeconds": 30
}
```

Follow-up: startup project handling currently supports the common single-startup project path. Multi-startup project profiles need runtime validation and a richer request model before `startup_project_set` can safely edit them.

## Open Packaging Notes

- Verify the VSIX builds on a machine with the Visual Studio extension development workload.
- Confirm whether Visual Studio 2026 stable uses the same `[17.0,19.0)` install target range or requires a more specific manifest once final extension guidance is published.
- Add icons/license/release notes before publishing any VSIX artifact.
- Keep broker HTTP MCP local-only; the VSIX should only connect to the broker via local named pipe/RPC.
