# NetVsMcp Plan

## Goal

Build a local-only Visual Studio MCP integration with one always-running local broker and one Visual Studio extension instance per running Visual Studio process.

The broker is the MCP server. Visual Studio extensions register themselves with the broker, and the broker routes MCP requests to the correct Visual Studio instance.

## Architecture

```text
Codex / MCP Client
  -> HTTP MCP on 127.0.0.1:5050/mcp
    -> NetVsMcp.Broker tray app
      -> NetVsMcp.Vsix instance A
      -> NetVsMcp.Vsix instance B
      -> NetVsMcp.Vsix instance C
```

No remote central server, Docker, Rancher, or stdio bridge is required.

## Solution Layout

```text
NetVsMcp.slnx
  src/NetVsMcp.Broker
    WPF tray app
    hosts HTTP MCP server on 127.0.0.1
    shows tray/status window/MCP config
    routes requests to VSIX instances

  src/NetVsMcp.Vsix
    Visual Studio extension
    registers with broker
    executes Visual Studio operations

  src/NetVsMcp.Contracts
    shared RPC contracts and DTOs

  tests/NetVsMcp.Broker.Tests
    routing, contracts, and broker tests
```

## Broker UX

The broker should be a Windows user app with a tray icon and status window.

Tray menu:

```text
NetVsMcp
Status: Running
-------------------------
Open Status Window
Copy MCP Config
Restart Broker
Reconnect Visual Studio Instances
-------------------------
Start at Login: On/Off
Open Logs Folder
Exit
```

Status window sections:

- broker status
- MCP endpoint details
- ready-to-copy MCP registration config
- active registered Visual Studio instances
- recent logs
- autostart controls

Example MCP config:

```json
{
  "mcpServers": {
    "netvs": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp"
    }
  }
}
```

## VS Session Registration

Each VSIX instance registers with the broker and updates state continuously.

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

## Transport

```text
MCP client -> Broker: HTTP MCP on 127.0.0.1
VSIX -> Broker: named pipe + StreamJsonRpc
```

The broker must bind only to localhost. VSIX communication should use a per-user named pipe.

## Routing

Every MCP tool should accept optional routing fields:

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

Broker target selection order:

1. explicit `sessionId`
2. explicit `processId`
3. normalized `solutionPath`
4. normalized `workspacePath` or `rootPath` by walking upward to `.sln` or `.slnx`
5. exact `solutionName`
6. active Visual Studio window or configured default
7. only registered Visual Studio instance
8. otherwise return candidate sessions and ask for a more specific target

## Tool Surface

The original "Capability Backlog" and "Tool Coverage Plan" sections that lived here are gone
(DOC-1, 2026-08-23): both were bootstrap-phase planning docs that drifted out of date as the tool
surface grew (missing roughly 20 tools shipped since, e.g. `rename_symbol_preview`,
`call_hierarchy_get`, `code_actions_list`/`apply`, `find_implementations`, `symbol_context`,
`document_outline`, `open_relevant_files`, `workspace_search`, `debug_hot_reload_apply`,
`debug_wait_for_break`, `debug_snapshot`, `debug_eval_many`, `test_run_and_get_results`, the Task
List family, `git_context`, `vs_context_snapshot`, `solution_overview`, `prepare_safe_edit`,
`apply_safe_edit_and_build`, `build_and_get_errors`), and one of the two ("Tool Coverage Plan")
had every section header claiming tools were "Missing" while the body said "**Implemented**" for
all of them — a leftover from before the initial tool sweep finished.

For the current, accurate tool surface, see:

- the README's tool table
- the `.agents/skills/*.md` guides (one per tool category: session/solution/project/test
  management, debugging, editing, navigation, build, and automation)

Both are kept in sync with the actual `[McpServerTool]`-attributed methods and are the canonical
reference going forward.

## Recommended MVP

Start with the smallest useful vertical slice:

```text
Broker starts
VSIX connects
VSIX registers session
MCP client calls vs_list_sessions
MCP client calls document_active
MCP client calls code_document_symbols
```

MVP tools:

- `vs_list_sessions`
- `vs_get_status`
- `document_active`
- `document_read`
- `document_open`
- `selection_get`
- `code_document_symbols`
- `code_go_to_definition`
- `code_find_references`
- `build_solution`
- `errors_list`
- `output_read`
- `debug_start`
- `debug_stop`
- `debug_continue`
- `debug_break`
- `debug_step`
- `breakpoint_set`
- `breakpoint_list`
- `breakpoint_remove`
- `debug_get_callstack`
- `debug_get_locals`
- `debug_evaluate`

## Standout Direction

The project should compete on reliability, trust, and Visual Studio-native intelligence rather than only tool count.

Positioning:

```text
One local Visual Studio agent control plane:
one broker, many VS instances, Roslyn-native navigation,
safe edits, debugger snapshots, and visible user control.
```

Primary differentiators:

- one local broker endpoint configured once by the MCP client
- dynamic Visual Studio instance registration through the VSIX
- best-in-class session selection by session id, solution path, solution name, process id, active VS instance, workspace/root path, or explicit default
- Roslyn-first code intelligence for navigation, symbol search, references, semantic diagnostics, and refactoring previews
- safe editing workflow with preview, approve/reject, pending edit visibility, and audit logging
- debugger workflows shaped around snapshots, break reasons, locals, call stack, breakpoints, and current source location
- tray/status app that makes the invisible broker visible and trustworthy
- local-only security with loopback binding, per-user authentication, approval gates where needed, and audit logs
- agent-friendly high-level tools that return useful context in one call

High-level tools to add after the current routed surface is stable:

- `vs_context_snapshot`
- `solution_overview`
- `debug_snapshot`
- `symbol_context`
- `prepare_safe_edit`
- `build_and_get_errors`
- `open_relevant_files`

Demo scenarios that should drive polish:

- agent fixes a compile error in the active Visual Studio solution
- agent finds references and explains symbol context using Roslyn
- agent previews an edit and the user approves it from Visual Studio or broker UI
- agent sets a conditional breakpoint, starts debugging, and inspects locals
- multiple Visual Studio instances are open and the broker routes to the intended solution without confusion

## Patterns To Borrow From Analog Tools

Keep the central local broker as the primary architecture, but borrow practical usability patterns seen in other Visual Studio MCP approaches.

Patterns to adopt:

- workspace/root-path based auto-selection, including walking upward from a client-provided path to find a solution
- optional per-session manifest files under `%LOCALAPPDATA%\NetVsMcp\Sessions` for debugging, recovery, and manual inspection
- stale-session cleanup for both broker registry entries and optional manifest files
- tool category metadata so users can inspect read, edit, debug, or admin-grade tools
- clear ambiguous-session responses with candidate sessions and selection hints

Patterns not to adopt:

- making every Visual Studio extension instance its own MCP server
- requiring a stdio bridge
- using port files as the primary routing or discovery transport

## Near-Term Follow-Ups

These items were identified during the first broker/VSIX skeleton review and should be handled before expanding the tool surface too far.

### Replace Placeholder HTTP Routes With MCP

The broker currently has local HTTP JSON routes for early status/tool smoke testing. Replace these with the actual MCP HTTP transport so MCP clients can register the broker directly at:

```text
http://127.0.0.1:5050/mcp
```

Acceptance criteria:

- broker exposes real MCP initialize/tools/call behavior over local HTTP
- `vs_list_sessions`, `vs_get_status`, and `vs_get_capabilities` are available as MCP tools
- status window MCP config works against the running broker
- endpoint remains bound to loopback only

### Validate Broker/VSIX Named Pipe End To End

The broker has a named-pipe registration listener and the VSIX has a registration lifecycle. Validate the full path with a running broker and experimental Visual Studio instance.

Acceptance criteria:

- VSIX connects to broker pipe
- VSIX registers session
- broker status window shows the Visual Studio instance and solution name
- heartbeat updates `LastSeenUtc`
- VS close/unload unregisters or eventually marks session stale
- reconnect works if broker starts after Visual Studio

### Normalize Solution Path Routing

Session routing should normalize solution paths at registration/update and target resolution.

Acceptance criteria:

- equivalent paths match despite slash style, casing, and relative segments
- solution-name routing still reports ambiguity when multiple sessions share a solution file name
- tests cover exact session id, normalized solution path, solution name, active instance, only instance, and ambiguity

### Align VSIX RPC Methods With Shared Contracts

The VSIX lifecycle uses broker method names documented during the skeleton phase. Align the VSIX client calls with `NetVsMcp.Contracts` once the broker registration endpoint is stable.

Acceptance criteria:

- VSIX uses shared contract DTOs where compatible with VSIX target framework constraints
- method names match broker registration service
- broker and VSIX can exchange register/update/heartbeat/unregister calls without adapter-only placeholders

### Add Local Authentication

The broker already tracks the token file path shape, but token generation and enforcement still need to be completed.

Acceptance criteria:

- broker creates or loads a per-user token under `%LOCALAPPDATA%\NetVsMcp`
- HTTP MCP endpoint requires the token for tool calls
- VSIX named-pipe registration authenticates to the broker
- tray/status window shows token/auth status and copyable MCP config
- tests cover missing, invalid, and valid token behavior

### Improve Session Selection

Make session routing a product strength.

Acceptance criteria:

- tools can route by `sessionId`, `solutionPath`, `solutionName`, `processId`, and client-provided `workspacePath` or `rootPath`
- workspace path matching walks upward to find `.sln` or `.slnx`
- ambiguous routing returns candidate sessions with process id, solution path, active-window state, and last-seen time
- broker UI shows the currently selected/default session
- tests cover process id, workspace path, ambiguity, stale sessions, and active/default fallbacks

### Add Optional Session Manifests

The broker registry remains authoritative, but manifest files can make troubleshooting and recovery easier.

Acceptance criteria:

- VSIX or broker writes one manifest per registered Visual Studio instance under `%LOCALAPPDATA%\NetVsMcp\Sessions`
- manifest includes process id, session id, solution path/name, broker pipe, capabilities, last seen, and extension version
- stale manifests are cleaned when the process exits or last-seen exceeds the configured threshold
- broker status window can open the session manifest folder

### Add Agent-Friendly Snapshot Tools

Reduce the number of calls an agent needs for common workflows.

Acceptance criteria:

- `vs_context_snapshot` returns selected session, active document, selection, build/debug status, and recent diagnostics
- `solution_overview` returns solution/project structure and startup project
- `debug_snapshot` returns debugger mode, stopped location, reason when available, call stack, thread summary, locals summary, and active exception when available
- `symbol_context` returns symbol declaration, containing type/member, references summary, and diagnostics near the symbol
- snapshot tools use size limits and truncation metadata to avoid oversized responses

### Polish Runtime Demos

Before treating the project as public-ready, validate real workflows inside Visual Studio.

Acceptance criteria:

- documented demo for fixing a compile error
- documented demo for Roslyn symbol/reference navigation
- documented demo for safe edit preview approval
- documented demo for conditional breakpoint plus locals inspection
- documented demo for multiple Visual Studio instances and correct solution routing

## Implementation Phases

1. Broker and registration
2. Editor and navigation
3. Build and diagnostics
4. Safe editing
5. Debugging
6. Tests and project operations
7. Authentication and audit polish
8. Session selection, optional manifests, and stale cleanup
9. Snapshot tools and product demos
10. Advanced debug, UI automation, and web debugging

## Security

- local-only HTTP endpoint on `127.0.0.1`
- per-user named pipe for VSIX connections
- broker token stored under `%LOCALAPPDATA%\NetVsMcp`
- VSIX must authenticate to broker
- reject non-local connections
- local audit log for tool calls
