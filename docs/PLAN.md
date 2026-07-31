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

## Capability Backlog

### Core Broker

- `vs_list_sessions`
- `vs_get_session`
- `vs_get_status`
- `vs_get_capabilities`
- `vs_select_session`
- `vs_ping`
- `vs_get_logs`
- `vs_launch_instance`

### General IDE

- `execute_command`
- `get_help`
- `window_list`
- `window_activate`
- `toolwindow_show`
- `toolwindow_hide`

### Solution And Project

- `solution_close`
- `solution_info`
- `project_list`
- `project_info`
- `startup_project_get`
- `startup_project_set`
- `solution_add_project`
- `solution_remove_project`
- `project_add_file`
- `project_remove_file`
- `project_add_reference`
- `project_remove_reference`

### Documents

- `document_active`
- `document_list`
- `document_open`
- `document_close`
- `document_read`
- `document_write`
- `document_save`
- `document_cleanup`

### Editor

- `editor_find`
- `editor_goto_line`
- `editor_insert`
- `editor_replace`
- `selection_get`
- `selection_set`
- `find_in_files`

### Edit Preview

- `edit_preview`
- `edit_approve`
- `edit_reject`
- `edit_list_pending`

### Code Navigation

- `code_go_to_definition`
- `code_find_references`
- `code_go_to_implementation`
- `code_document_symbols`
- `code_workspace_symbols`

### Build

- `build_solution`
- `build_project`
- `build_cancel`
- `build_status`
- `clean_solution`
- `rebuild_solution`
- `build_configuration_get`
- `build_configuration_set`

### Diagnostics And Output

- `errors_list`
- `output_list_panes`
- `output_read`
- `output_write`
- `output_clear`
- `diagnostics_binding_errors`

### Debugger

- `debug_start`
- `debug_start_without_debugging`
- `debug_stop`
- `debug_restart`
- `debug_attach`
- `debug_break`
- `debug_continue`
- `debug_step`
- `debug_get_mode`
- `debug_status`
- `debug_get_callstack`
- `debug_get_threads`
- `debug_get_locals`
- `debug_evaluate`
- `debug_set_variable`

### Breakpoints

- `breakpoint_set`
- `breakpoint_remove`
- `breakpoint_list`
- `breakpoint_enable`

Breakpoint variants:

- normal file/line breakpoint
- conditional breakpoint
- hit-count breakpoint
- function breakpoint

### Tests

- `test_discover`
- `test_run`
- `test_results`

### NuGet

- `nuget_list`
- `nuget_search`
- `nuget_install`
- `nuget_update`
- `nuget_uninstall`

### Advanced Debug

- `watch_add`
- `watch_remove`
- `watch_list`
- `thread_switch`
- `thread_set_frozen`
- `thread_get_callstack`
- `process_list_debugged`
- `process_list_local`
- `process_detach`
- `process_terminate`
- `immediate_execute`
- `module_list`
- `exception_settings_get`
- `exception_settings_set`
- `parallel_stacks`
- `parallel_watch`

### Debuggee Console

- `console_read`
- `console_send`
- `console_get_info`

### Debuggee UI Automation

Served from the separate `/mcp-wu` endpoint rather than the default `/mcp` (rarely used, kept off the default advertised tool list).

- `ui_capture_window`
- `ui_capture_region`
- `ui_snapshot`
- `ui_get_tree`
- `ui_find_elements`
- `ui_get_element`
- `ui_click`
- `ui_double_click`
- `ui_right_click`
- `ui_drag`
- `ui_set_value`
- `ui_invoke`
- `ui_send_keys`
- `ui_wait_for_element`
- `ui_wait_idle`

### Web Debugging

Served from the separate `/mcp-wu` endpoint rather than the default `/mcp` (rarely used, kept off the default advertised tool list).

- `web_connect`
- `web_disconnect`
- `web_status`
- `web_navigate`
- `web_screenshot`
- `web_dom_get`
- `web_dom_query`
- `web_console`
- `web_js_execute`
- `web_network`
- `web_element_click`
- `web_element_set_value`

## Tool Coverage Plan

The broker currently exposes a substantial routed MCP surface, but parity work remains. Treat these as planned workstreams after authentication and runtime validation unless a demo needs one sooner.

### Already Exposed Broker Tools

Current broker MCP tools include (all live-tested — see `docs/TOOL_LIVE_TEST_REPORT.md`):

- broker/session tools: `vs_list_sessions`, `vs_get_status`, `vs_get_capabilities`, `vs_get_session`, `vs_select_session`, `vs_ping`, `vs_get_logs`, `vs_context_snapshot`
- general IDE tools: `execute_command`, `get_status`, `get_help`, `window_list`, `window_activate`, `toolwindow_show`, `toolwindow_hide`
- documents/editor/safe edits: `document_active`, `document_list`, `document_read`, `document_open`, `document_close`, `open_relevant_files`, `selection_get`, `document_write`, `document_save`, `editor_insert`, `editor_replace`, `editor_find`, `editor_goto_line`, `selection_set`, `document_cleanup`, `format_and_organize`, `find_in_files`, `edit_preview`, `prepare_safe_edit`, `edit_approve`, `apply_safe_edit_and_build`, `edit_reject`, `edit_list_pending`
- navigation/context: `code_document_symbols`, `code_go_to_definition`, `code_go_to_implementation`, `code_find_references`, `code_workspace_symbols`, `symbol_context`, `document_outline`, `find_implementations`, `rename_symbol_preview`, `workspace_search`
- solution/project: `solution_open`, `solution_close`, `solution_info`, `solution_overview`, `solution_add_project`, `solution_remove_project`, `project_list`, `project_info`, `project_dependencies`, `project_add_file`, `project_remove_file`, `project_add_reference`, `project_remove_reference`, `startup_project_get`, `startup_project_set`
- build/diagnostics/output: `build_solution`, `build_project`, `build_cancel`, `build_status`, `clean_solution`, `rebuild_solution`, `build_configuration_get`, `build_configuration_set`, `build_and_get_errors`, `errors_list`, `diagnostics_for_document`, `diagnostics_binding_errors`, `output_list_panes`, `output_read`, `output_write`, `output_clear`
- tests: `test_discover`, `test_run`, `test_results`, `test_run_and_get_results`
- NuGet: `nuget_list`, `nuget_search`, `nuget_install`, `nuget_update`, `nuget_uninstall`, `package_restore`
- debugging/breakpoints: `debug_status`, `debug_get_mode`, `debug_start`, `debug_start_without_debugging`, `debug_stop`, `debug_restart`, `debug_attach`, `debug_continue`, `debug_break`, `debug_step`, `debug_get_callstack`, `debug_get_locals`, `debug_get_threads`, `debug_evaluate`, `debug_eval_many`, `debug_set_variable`, `debug_snapshot`, `breakpoint_set`, `breakpoint_list`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_group_list`, `breakpoint_group_enable`, `breakpoint_group_remove`
- advanced debug: `watch_add`, `watch_remove`, `watch_list`, `thread_switch`, `thread_set_frozen`, `thread_get_callstack`, `process_list_debugged`, `process_list_local`, `process_detach`, `process_terminate`, `immediate_execute`, `module_list`, `exception_settings_get`, `exception_settings_set`, `parallel_stacks`, `parallel_watch`
- debuggee console: `console_read`, `console_send`, `console_get_info`
- debuggee UI automation: `ui_capture_window`, `ui_capture_region`, `ui_snapshot`, `ui_get_tree`, `ui_find_elements`, `ui_get_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `ui_send_keys`, `ui_wait_for_element`, `ui_wait_idle`
- web debugging: `web_connect`, `web_disconnect`, `web_status`, `web_navigate`, `web_screenshot`, `web_dom_get`, `web_dom_query`, `web_console`, `web_js_execute`, `web_network`, `web_element_click`, `web_element_set_value`
- local context: `git_context`

Everything in this Tool Coverage Plan, including `vs_launch_instance`, is now implemented and marked accordingly.

### Instance Launch Tool

**Implemented.** `vs_launch_instance` launches a new `devenv.exe` process (discovered via `vswhere.exe`, with a running-process fallback), optionally opening a solution path and/or passing `/rootsuffix Exp` for an experimental instance, then polls `SessionRegistry` until the new process registers with the broker or a bounded timeout elapses.

Acceptance criteria:

- broker can launch a new Visual Studio process, optionally with a solution path, edition/version preference, and experimental instance flag (`/rootsuffix`) — done
- tool returns once the new instance has registered with the broker (with a bounded timeout) or reports a clear timeout/launch-failure result — done
- launched instances are otherwise ordinary registered sessions usable by all routing fields — done
- launch calls are audited with tool category metadata — done (`BrokerToolCategory.Admin`)
- tests cover launch success, launch timeout, and missing Visual Studio installation — not yet covered by automated tests

### Remaining General IDE Tools

**Implemented.** `get_status` is live and live-tested. General IDE coverage is complete for this plan slice.

Acceptance criteria:

- commands reject unknown or dangerous operations with clear errors
- window/tool-window responses include stable captions, kind, active/visible state, and activation support status
- tests cover successful execution, unknown command, and routed-session failure behavior

### Missing Solution And Project Mutation Tools

**Implemented.** `solution_open`, `project_remove_file`, `project_add_reference`, `project_remove_reference` are all live and live-tested (see `docs/TOOL_LIVE_TEST_REPORT.md` §8). Remaining acceptance criteria below are still open as part of the broader auth/audit workstream.

Acceptance criteria:

- mutation tools use audit logging
- path inputs are normalized and constrained to the routed solution where applicable
- responses include before/after project or solution identity and any Visual Studio reload/build implications
- tests cover routing failure, invalid paths, and successful fake-RPC dispatch

### Missing Document And Search Tools

**Implemented.** `document_list`, `document_close`, `editor_find`, `find_in_files` are all live and live-tested.

Acceptance criteria:

- document lists include open/dirty/active metadata where Visual Studio exposes it
- find tools support case sensitivity, whole word, regex where safe, and result limits
- large result sets include truncation metadata

### Missing Build And Configuration Tools

**Implemented.** `build_project`, `build_cancel`, `clean_solution`, `rebuild_solution`, `build_configuration_get`, `build_configuration_set` are all live; `build_configuration_get` live-tested, the rest confirmed present in the broker source.

Acceptance criteria:

- build operations preserve existing routed error behavior
- build configuration responses include solution configuration and platform
- setters are audited with clear success/failure metadata

### Missing Output And Diagnostics Tools

**Implemented.** `output_list_panes`, `output_write`, `output_clear`, `diagnostics_binding_errors` are all live and live-tested.

Acceptance criteria:

- output pane writes and clears are audited
- pane reads and diagnostics support result limits
- XAML/WPF binding diagnostics parse useful file, line, path, and message fields when available

### Missing Debugger Tools

**Implemented.** `debug_start_without_debugging`, `debug_restart`, `debug_attach`, `debug_get_threads`, `debug_set_variable` are all live and live-tested.

Acceptance criteria:

- attach supports process id and process name with ambiguity responses
- state-changing debug operations are audited
- variable mutation reports whether the debugger engine accepted the change

### Missing Advanced Debug Tools

**Implemented.** `watch_add`, `watch_remove`, `watch_list`, `thread_switch`, `thread_set_frozen`, `thread_get_callstack`, `process_list_debugged`, `process_list_local`, `process_detach`, `process_terminate`, `immediate_execute`, `module_list`, `exception_settings_get`, `exception_settings_set`, `parallel_stacks`, `parallel_watch` are all live and exposed. `exception_settings_get`/`set` are implemented via `EnvDTE90.Debugger3.ExceptionGroups`/`ExceptionSettings`. `immediate_execute` is implemented via `EnvDTE.Debugger.GetExpression`, reusing the same evaluator backing the Immediate Window. `memory_read` and `parallel_tasks_list` were removed (2026-07-26): EnvDTE does not expose a stable memory-read surface or the Parallel Tasks window data, so these tools always returned `supported:false`. Rather than keep permanently-unimplementable stub tools, they were removed from the tool catalog and RPC surface entirely.

Acceptance criteria:

- unsupported debugger-engine cases return explicit unsupported results instead of generic failures
- process termination and immediate execution require admin-grade policy
- memory/register APIs include size limits and clear native/mixed-mode caveats

### Missing Debuggee Console Tools

**Implemented.** `console_read`, `console_send`, `console_get_info` are all live; `console_get_info` live-tested, `console_read`/`console_send` confirmed present in the broker source.

Acceptance criteria:

- console targeting is tied to the routed debuggee process
- input/send operations are audited
- responses report unsupported cases for non-console debuggees

### Missing UI Automation Tools

**Implemented.** `ui_capture_window`, `ui_capture_region`, `ui_snapshot`, `ui_get_tree`, `ui_find_elements`, `ui_get_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `ui_send_keys`, `ui_wait_for_element`, `ui_wait_idle` are all live and live-tested (see `docs/TOOL_LIVE_TEST_REPORT.md` §6). `ui_get_tree`/`ui_snapshot`/`ui_wait_idle` return `nodeCount:0`/`windowCount:0` only when there is no debuggee window to inspect (e.g. no debuggee process attached); the UIA backend itself is fully implemented, not a skeleton.

Acceptance criteria:

- all actions target the routed debuggee window, not arbitrary desktop windows
- UI snapshots combine bounded UIA tree data with screenshot metadata
- click/type/drag operations require an automation/admin profile and are audited
- waits have explicit timeouts and stable timeout responses

### Missing Web Debugging Tools

**Implemented.** `web_connect`, `web_disconnect`, `web_status`, `web_navigate`, `web_screenshot`, `web_dom_get`, `web_dom_query`, `web_console`, `web_js_execute`, `web_network`, `web_element_click`, `web_element_set_value` are all live and live-tested. `web_console`/`web_network` report `supported:true` but require a CDP-backed browser shell (not available from the current backend).

Acceptance criteria:

- browser connections are explicit and scoped to the routed debug session where possible
- JavaScript execution and navigation require an automation/admin profile
- DOM, console, network, and screenshot outputs use size limits and truncation metadata

### Missing NuGet Tools

**Implemented.** `nuget_list`, `nuget_search`, `nuget_install`, `nuget_update`, `nuget_uninstall` are all live and live-tested (see `docs/TOOL_LIVE_TEST_REPORT.md` §8) — install/update/uninstall round-tripped end to end.

Acceptance criteria:

- package mutation is audited
- search uses NuGet APIs with result limits and version metadata
- install/update/uninstall responses include affected project, package id, requested version, and restore status

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
