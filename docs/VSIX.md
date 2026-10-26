# Visual Studio Extension Integration

## Role and packaging

`NetVsMcp.Vsix` runs inside each Visual Studio instance and executes IDE operations using the Visual Studio SDK, Roslyn, and debugger APIs. It does not host MCP. The broker hosts the loopback HTTP MCP endpoints and routes requests over a per-user named pipe.

The extension targets `net472` and packages for amd64 Visual Studio installations in the manifest range `[17.0,19.0)` (Visual Studio 2022 and 2026). Individual features can require newer IDE APIs; optional Test Explorer integration requires the supported VS 18 API shape. A matching installation range does not guarantee every engine or backend supports every feature.

Building the VSIX requires Visual Studio extension development tooling. The build publishes the broker into the VSIX payload. The `.netvsmcp-vsix-bundled-broker` marker identifies this copy, which is updated with the extension. The standalone MSI broker is packaged separately and remains user-managed.

See [the manifest](../src/NetVsMcp.Vsix/source.extension.vsixmanifest), [the project](../src/NetVsMcp.Vsix/NetVsMcp.Vsix.csproj), and [setup](SETUP.md).

## Registration and reconnect

On initialization, the extension captures its process and solution state, connects to the broker, registers the Visual Studio session, and starts heartbeat and state publication. If no compatible broker is running, it starts the bundled broker. Disconnects trigger reconnect attempts; disposal unregisters the session when possible.

The pipe is scoped to the Windows user. Registration includes the Visual Studio process, solution, active document, and debugger state. The broker can route to several registered instances; callers should select an exact session or solution path when more than one instance is plausible.

Solution, active-window, debugger, build, and document-save observations are published through the existing connection. Publication is bounded and does not rely on MCP client notification support. Reconnects and event gaps require fresh authoritative state; commands are not replayed automatically. See the [event and operation contract](event-operation-contract.md).

## RPC surface

The connection carries registration calls from the extension to the broker and IDE capability calls from the broker to the extension. Public MCP tool names use snake_case; internal StreamJsonRpc methods use names such as `DocumentReadAsync`, `BuildSolutionAsync`, and `DebugStatusAsync`.

The current wire contracts and methods are defined in:

- [Registration interface](../src/NetVsMcp.Contracts/Interfaces/IBrokerRegistrationRpc.cs)
- [Visual Studio session interface](../src/NetVsMcp.Contracts/Interfaces/IVisualStudioSessionRpc.cs)
- [Shared request and result types](../src/NetVsMcp.Contracts)
- [Extension RPC target](../src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs)

Contracts target both `net10.0` and `netstandard2.0`. The extension also uses local capability models where needed. Refer to these definitions for exact method signatures and additive result fields, and use `get_help` for the public MCP tool surface.

## Capability behavior

- **Documents and navigation:** operations use live IDE buffers and workspace state. Relative paths resolve against the routed solution. Compiler diagnostics and guarded source transformations bind to document or workspace versions; concurrent edits can invalidate a request.
- **Builds:** results identify the requested build and configuration. A nonwaiting response does not establish completion. Canceling a caller's wait does not cancel the build.
- **Debugger:** execution generations and pause IDs reject stale requests. Thread/frame inspection preserves selection where supported. Snapshot sections are optional and bounded. `compact: true` opts into deduplicated child state; the default retains nested state fields.
- **Watches:** the extension retains its own expression list and evaluates it in debugger context. These tools do not edit Visual Studio's Watch window.
- **Immediate evaluation:** `immediate_execute` evaluates the supplied expression through the debugger API; it does not send keystrokes to the Immediate window. Evaluation can execute debuggee code and requires a paused session.
- **Exception settings:** the extension reads and updates settings through `EnvDTE90.Debugger3` when available. Unsupported debugger APIs return explicit evidence.
- **Tests:** CLI is the default backend. Explicit Test Explorer requests use the optional IDE adapter and never silently fall back to CLI. Native test IDs are selection evidence for the current discovery context; CLI results retain their own run identity.
- **Modal dialogs:** the broker inspects dialogs through Windows UI Automation so it can recover while the extension's UI thread is blocked. A modal response does not establish command completion.

Engine/backend prerequisites and older-extension compatibility are reported explicitly. Missing correctness-critical guards must not fall back to unguarded execution. Consult the focused [product guides](../README.md#best-practices-guides) before choosing a workflow.
