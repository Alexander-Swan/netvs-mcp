# NetVsMcp

NetVsMcp is a local MCP server for Visual Studio. It runs as a lightweight tray app on your Windows machine, exposes a standard MCP HTTP endpoint on loopback, and routes tool calls into whichever Visual Studio instance holds the solution you care about — including when several are open at once.

No cloud services. No telemetry. No per-project configuration files. Everything runs on your machine and stays there.

## What makes it different

**One broker, all your instances.** Install once; the broker tray app auto-starts on login and manages every Visual Studio session. Open three solutions simultaneously, and MCP clients can target any of them by solution path, solution name, or session ID — without reconfiguring anything.

**Deep IDE integration, not just file access.** NetVsMcp routes tool calls through the Visual Studio SDK, so it works with what Visual Studio actually knows: live error lists, the active debugger state, in-memory editor buffers, Roslyn's symbol index, and the real build system. Reading files from disk is the floor, not the ceiling.

**Safe-edit workflow.** Edits can be queued as previews before they touch the buffer. The agent calls `edit_preview`, the developer reviews the diff in Visual Studio, then approves or rejects it. Nothing lands in the editor without an explicit accept, and multiple pending edits can be queued and managed independently.

**Full debugger control.** Start, stop, attach, and step through code. Set conditional breakpoints, read locals and the call stack, evaluate arbitrary expressions in the current frame, manage watch expressions, switch threads, and take structured snapshots of debugger state — all from MCP.

**Audit log.** Every routed tool call is appended to a local JSONL audit log — tool name, target session, routing fields, success/failure, and failure reason. Enough to reconstruct what happened, without capturing full source text or secret values.

## Tool coverage

| Area | Tools |
| --- | --- |
| Session management | `vs_list_sessions`, `vs_get_session`, `vs_select_session`, `vs_get_status`, `vs_get_capabilities`, `vs_ping` |
| Documents & editor | `document_active`, `document_read`, `document_open`, `document_write`, `document_save`, `document_close`, `document_list`, `document_cleanup`, `document_outline` |
| Editor mutations | `editor_insert`, `editor_replace`, `editor_goto_line`, `selection_get`, `selection_set` |
| Safe-edit workflow | `edit_preview`, `edit_approve`, `edit_reject`, `edit_list_pending`, `prepare_safe_edit`, `apply_safe_edit_and_build` |
| Code navigation | `code_document_symbols`, `code_workspace_symbols`, `code_go_to_definition`, `code_go_to_implementation`, `code_find_references`, `find_implementations` |
| Search | `editor_find`, `find_in_files`, `workspace_search`, `open_relevant_files` |
| Build | `build_solution`, `build_project`, `build_and_get_errors`, `build_status`, `build_cancel`, `build_configuration_get`, `build_configuration_set`, `rebuild_solution`, `clean_solution`, `package_restore` |
| Diagnostics | `errors_list`, `output_read`, `output_list_panes`, `diagnostics_for_document`, `diagnostics_binding_errors` |
| Debugger | `debug_start`, `debug_stop`, `debug_restart`, `debug_continue`, `debug_break`, `debug_step`, `debug_attach`, `debug_status`, `debug_get_mode`, `debug_get_callstack`, `debug_get_locals`, `debug_evaluate`, `debug_eval_many`, `debug_set_variable`, `debug_snapshot` |
| Breakpoints | `breakpoint_set`, `breakpoint_list`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_group_list`, `breakpoint_group_enable`, `breakpoint_group_remove` |
| Threads & processes | `debug_get_threads`, `thread_switch`, `thread_get_callstack`, `thread_set_frozen`, `parallel_stacks`, `parallel_watch`, `process_list_debugged`, `process_list_local`, `process_detach`, `process_terminate` |
| Watches & immediate | `watch_add`, `watch_list`, `watch_remove`, `immediate_execute` |
| Exceptions | `exception_settings_get`, `exception_settings_set` |
| Modules | `module_list` |
| Solution & projects | `solution_info`, `solution_overview`, `solution_open`, `solution_close`, `solution_add_project`, `solution_remove_project`, `project_list`, `project_info`, `project_dependencies`, `project_add_file`, `project_remove_file`, `project_add_reference`, `project_remove_reference`, `startup_project_get`, `startup_project_set` |
| NuGet | `nuget_list`, `nuget_search`, `nuget_install`, `nuget_update`, `nuget_uninstall` |
| Tests | `test_discover`, `test_run`, `test_run_and_get_results`, `test_results` |
| Git context | `git_context` |
| Snapshots | `vs_context_snapshot`, `symbol_context`, `debug_snapshot` |
| Console | `console_get_info`, `console_read`, `console_send` |
| Visual Studio UI | `window_activate`, `window_list`, `toolwindow_show`, `toolwindow_hide`, `execute_command`, `format_and_organize` |

## Architecture

```text
MCP client (Claude, Copilot, any agent)
  -> HTTP MCP  http://127.0.0.1:5050/mcp
    -> NetVsMcp.Broker  (WPF tray app, always running)
      -> NetVsMcp.Vsix  Visual Studio instance A  (named pipe)
      -> NetVsMcp.Vsix  Visual Studio instance B
      -> NetVsMcp.Vsix  Visual Studio instance C
```

The broker is the only MCP server. Visual Studio extensions connect to it over a per-user named pipe using StreamJsonRpc. When a tool call arrives, the broker resolves the target session and forwards the call through the existing pipe connection to the correct VSIX instance. The VSIX executes it using the Visual Studio SDK and returns a structured result.

## Session routing

Every routed tool accepts optional routing fields:

```json
{
  "sessionId": null,
  "solutionName": null,
  "solutionPath": null
}
```

Resolution order: explicit session ID → normalized solution path → solution name → active Visual Studio window → only registered instance → fail with candidate metadata.

Routing failures return a structured error with `failureReason`, `candidateCount`, and `candidateSessionIds` so the client can recover without guessing.

## MCP registration

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

The broker status window shows this snippet ready to copy. A second endpoint at `http://127.0.0.1:5050/mcp-wu` serves the rarely used `ui_*`/`web_*` debuggee UI automation tools, kept off the default endpoint to keep the advertised tool list smaller.

## Projects

```text
NetVsMcp.slnx
  src/NetVsMcp.Broker        WPF tray/status app and local HTTP MCP broker
  src/NetVsMcp.Contracts     Shared DTOs and RPC contracts
  src/NetVsMcp.Installer     WiX MSI installer for the broker tray app
  src/NetVsMcp.Vsix          Visual Studio extension
  tests/NetVsMcp.Broker.Tests
```

## Build

```powershell
dotnet restore .\NetVsMcp.slnx
dotnet build .\NetVsMcp.slnx
```

Build the installer MSI:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release
```

See [docs/SETUP.md](docs/SETUP.md) for local setup and usage, [docs/BROKER_UX.md](docs/BROKER_UX.md) for tray and status window behavior, and [docs/SECURITY.md](docs/SECURITY.md) for the local security model.
