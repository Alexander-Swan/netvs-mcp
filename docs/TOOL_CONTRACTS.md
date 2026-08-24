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
  "solutionPath": null,
  "processId": null,
  "workspacePath": null,
  "rootPath": null
}
```

Resolution order:

1. explicit `sessionId`
2. explicit `processId`
3. normalized `solutionPath`
4. normalized `workspacePath` or `rootPath` by walking upward to `.sln` or `.slnx`
5. exact `solutionName`
6. active Visual Studio window or configured default
7. only registered Visual Studio instance
8. otherwise fail with candidate session metadata

Routing failures return `success: false`, a message, and metadata such as `failureReason`, `candidateCount`, and `candidateSessionIds`.

## Broker-Only And Broker-Routed MCP Tools

The tool surface has grown to ~190 tools across session/solution/project/test management,
editing, navigation, build, debugging, and automation, and a hand-maintained table here has
already gone stale once (an earlier ~45-tool version of this table missed NuGet, advanced debug,
UI automation, web debugging, tests, task list, code actions, call hierarchy, and snapshot tools).
Rather than re-derive and re-drift another copy, the canonical, currently-accurate references are:

- the README's tool table
- the `.agents/skills/*.md` guides (one per tool category), which also document each tool's
  request/response shape in more depth than a flat table would

For the broker-side/VSIX-side RPC method naming convention: broker-only tools (session listing,
status, capabilities) are handled entirely inside the broker without a VSIX round trip; broker-routed
tools forward to a same-named-or-closely-named async method on the VSIX's `IVisualStudioSessionRpc`
implementation (e.g. `document_read` -> `DocumentReadAsync`) -- see
`src/NetVsMcp.Contracts/RpcInterfaces.cs` for the full interface.

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

- Token authentication for the broker's HTTP MCP endpoint is deliberately not implemented (see
  `docs/SECURITY.md`) -- the tool targets a single-developer local workstation, not shared/RDP/VDI
  machines, so this isn't planned unless the deployment model changes. Audit logging, by contrast,
  has shipped (`AuditLogService`) and is not a gap.
- `NetVsMcp.Contracts` now multi-targets `net10.0`/`netstandard2.0` and `NetVsMcp.Vsix` (net472)
  references it directly for the DTOs and RPC interfaces both sides share -- the earlier hand-mirrored
  "Wire" type duplication is gone.
