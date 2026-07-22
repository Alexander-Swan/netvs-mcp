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

Until `NetVsMcp.Contracts` is integrated, the VSIX uses internal DTOs with the expected wire shape and calls these StreamJsonRpc methods:

```text
RegisterVisualStudioSessionAsync(VsRegistrationRequest request)
HeartbeatVisualStudioSessionAsync(VsHeartbeatRequest request)
UnregisterVisualStudioSessionAsync(string sessionId)
```

The per-user pipe name is:

```text
netvs-mcp-{sanitized Windows user SID}
```

Example:

```text
netvs-mcp-S-1-5-21-...
```

The broker should expose a matching named-pipe server and accept exactly one request object for registration/heartbeat. Once shared contracts land, replace `VsSessionSnapshot`, `VsRegistrationRequest`, and `VsHeartbeatRequest` in the VSIX with the shared DTOs rather than maintaining parallel types.

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
```

Broker-facing MCP tool mapping:

```text
document_active -> DocumentActiveAsync
document_read   -> DocumentReadAsync
document_open   -> DocumentOpenAsync
selection_get   -> SelectionGetAsync
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
```

Broker-facing MCP tool mapping:

```text
code_document_symbols -> CodeDocumentSymbolsAsync
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

## Open Packaging Notes

- Verify the VSIX builds on a machine with the Visual Studio extension development workload.
- Confirm whether Visual Studio 2026 stable uses the same `[17.0,19.0)` install target range or requires a more specific manifest once final extension guidance is published.
- Add icons/license/release notes before publishing any VSIX artifact.
- Keep broker HTTP MCP local-only; the VSIX should only connect to the broker via local named pipe/RPC.
