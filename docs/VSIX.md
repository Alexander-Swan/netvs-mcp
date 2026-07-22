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

The local machine did not have `dotnet new` VSIX templates installed, so this skeleton is hand-authored from the SDK-style project shape. A full packaging/build requires the Visual Studio extension development workload and compatible VSSDK build tooling.

## Registration Lifecycle

On package initialization:

1. Switch to the Visual Studio UI thread.
2. Capture the current session snapshot.
3. Connect to the local broker over the per-user named pipe.
4. Register this Visual Studio instance.
5. Start heartbeat/state refresh.
6. Subscribe to Visual Studio events and push updates.

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
- selection changed
- debugger mode changed
- build started, completed, canceled, or failed
- package reconnects after broker restart

The heartbeat should refresh `lastSeenUtc`, solution, active document, debugger mode, and capability list. Stopped heartbeats should make the broker mark the session as stale rather than immediately forgetting it.

## Execution Surface

The skeleton defines service interfaces for the future VS-side command handlers:

- editor: active document, document read/open, selection
- navigation: go to definition, find references, document symbols
- build: solution/project build and cancel
- debugger: start/stop/continue/break/step/breakpoint set

Once `NetVsMcp.Contracts` lands, replace the placeholder registration models with shared DTOs and add the StreamJsonRpc command dispatcher here.

## Open Packaging Notes

- Verify the VSIX builds on a machine with the Visual Studio extension development workload.
- Confirm whether Visual Studio 2026 stable uses the same `[17.0,19.0)` install target range or requires a more specific manifest once final extension guidance is published.
- Add icons/license/release notes before publishing any VSIX artifact.
- Keep broker HTTP MCP local-only; the VSIX should only connect to the broker via local named pipe/RPC.
