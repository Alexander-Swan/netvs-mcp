# NetVsMcp

NetVsMcp is a local MCP server for Visual Studio. The Visual Studio extension ships the broker payload, starts it locally when needed, exposes a standard MCP HTTP endpoint on loopback, and routes tool calls into whichever Visual Studio instance holds the solution you care about — including when several are open at once.

No cloud services. No telemetry. No repo-local or per-project configuration files. Everything runs on your machine and stays there.

## What makes it different

**One broker, all your instances.** Install the VSIX once; the first Visual Studio instance connects to an already-running broker or starts the bundled broker in the Windows tray, and that broker manages every Visual Studio session. If no detected MCP client is already registered for the broker endpoint and the broker has not already shown setup, the status window opens on **Agents** so you can register or copy configuration right away. Open three solutions simultaneously, and MCP clients can target any of them by session ID, process ID, solution path, workspace path, or solution name — without a separate MCP config for each Visual Studio instance.

**Deep IDE integration, not just file access.** NetVsMcp routes tool calls through the Visual Studio SDK, so it works with what Visual Studio actually knows: live error lists, the active debugger state, in-memory editor buffers, Roslyn's symbol index, and the real build system. Reading files from disk is the floor, not the ceiling.

**Safe-edit workflow.** Edits can be queued as previews before they touch the buffer. The agent calls `edit_preview`, the developer reviews the diff in Visual Studio, then approves or rejects it. Nothing lands in the editor without an explicit accept, and multiple pending edits can be queued and managed independently.

Approval checks that the full live document still matches the preview snapshot, including whether the target existed. Conflicts and failed approvals stay pending; reject a stale edit and create a fresh preview before applying it to changed text.

**Full debugger control.** Start, stop, attach, and step through code. Set conditional breakpoints, read locals and the call stack, evaluate arbitrary expressions in the current frame, manage watch expressions, switch threads, and take structured snapshots of debugger state — all from MCP.

**Audit log.** Every routed tool call is appended to a local JSONL audit log — tool name, target session, routing fields, success/failure, and failure reason. Enough to reconstruct what happened, without capturing full source text or secret values.

**Optional local usage analytics.** Usage analytics are disabled by default and are not telemetry. When enabled, the broker writes aggregate-only usage counters to a local SQLite database on your machine. It tracks call counts, success/failure totals, durations, broker version, endpoint, tool category, and estimated request/response tokens by daily bucket; it does not store tool arguments, responses, source text, debugger values, output panes, or messages.

## Tool coverage

Document/editor tools such as `document_open` and `document_read` use the parameter name `path`. Code navigation, diagnostics, and breakpoint tools use `documentPath`. Relative document paths resolve against the routed `.sln`/`.slnx` file's directory; absolute paths are used as-is.

| Area | Tools |
| --- | --- |
| Session management | `vs_list_sessions`, `vs_get_session`, `vs_select_session`, `vs_get_status`, `vs_ping`, `vs_launch_instance`, `vs_get_logs`, `vs_get_usage_summary`, `netvs_doctor`, `get_status`, `get_help`, `netvs_get_best_practices` |
| Documents & editor | `document_active`, `document_read`, `document_open`, `document_write`, `document_save`, `document_close`, `document_list`, `document_cleanup`, `document_outline` |
| Editor mutations | `editor_insert`, `editor_replace`, `editor_goto_line`, `selection_get`, `selection_set` |
| Safe-edit workflow | `edit_preview`, `edit_approve`, `edit_reject`, `edit_list_pending`, `prepare_safe_edit`, `apply_safe_edit_and_build` |
| Code navigation | `code_document_symbols`, `code_workspace_symbols`, `code_go_to_definition`, `code_go_to_implementation`, `code_find_references`, `find_implementations`, `rename_symbol_preview`, `rename_symbol_apply`, `call_hierarchy_get` |
| Code fixes & refactorings | `code_actions_list`, `code_actions_apply` |
| Search | `editor_find`, `find_in_files`, `open_relevant_files` |
| Build | `build_solution`, `build_project`, `build_and_get_errors`, `build_status`, `build_cancel`, `build_configuration_get`, `build_configuration_set`, `rebuild_solution`, `clean_solution`, `package_restore` |
| Diagnostics | `errors_list`, `output_read`, `output_write`, `output_clear`, `output_list_panes`, `diagnostics_for_document`, `diagnostics_binding_errors` |
| Task List | `task_list_get`, `task_list_add`, `task_list_remove`, `task_list_set_checked` |
| Debugger | `debug_start`, `debug_start_without_debugging`, `debug_stop`, `debug_restart`, `debug_continue`, `debug_break`, `debug_step`, `debug_attach`, `debug_status`, `debug_get_mode`, `debug_get_callstack`, `debug_get_locals`, `debug_evaluate`, `debug_eval_many`, `debug_set_variable`, `debug_snapshot`, `debug_wait_for_break`, `debug_hot_reload_apply` |
| Breakpoints | `breakpoint_set`, `breakpoint_list`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_group_list`, `breakpoint_group_enable`, `breakpoint_group_remove` |
| Threads & processes | `debug_get_threads`, `thread_switch`, `thread_get_callstack`, `thread_set_frozen`, `parallel_stacks`, `parallel_watch`, `process_list_debugged`, `process_list_local`, `process_detach`, `process_terminate` |
| Watches & immediate | `watch_add`, `watch_list`, `watch_remove`, `immediate_execute` |
| Exceptions | `exception_settings_get`, `exception_settings_set` |
| Solution & projects | `solution_info`, `solution_overview`, `solution_open`, `solution_close`, `solution_add_project`, `solution_remove_project`, `project_list`, `project_info`, `project_dependencies`, `project_add_file`, `project_remove_file`, `project_add_reference`, `project_remove_reference`, `startup_project_get`, `startup_project_set` |
| NuGet | `nuget_list`, `nuget_search`, `nuget_install`, `nuget_update`, `nuget_uninstall` |
| Tests | `test_discover`, `test_run`, `test_debug`, `test_run_and_get_results`, `test_results` |
| Git context | `git_context` |
| Snapshots | `vs_context_snapshot`, `symbol_context`, `debug_snapshot` |
| Console | `console_get_info`, `console_read`, `console_send` |
| Visual Studio modal dialogs | `dialog_list`, `dialog_respond` (also integrated with `debug_status`, `debug_get_mode`, `debug_snapshot`, and `debug_wait_for_break`) |
| Visual Studio UI | `window_activate`, `window_list`, `toolwindow_show`, `toolwindow_hide`, `execute_command`, `format_and_organize` |
| Debuggee UI automation (`/mcp-wu`) | `ui_capture_window`, `ui_capture_region`, `ui_snapshot`, `ui_get_tree`, `ui_find_elements`, `ui_get_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `ui_send_keys`, `ui_wait_for_element`, `ui_wait_idle` |
| Browser automation (`/mcp-wu`) | `web_connect`, `web_disconnect`, `web_status`, `web_navigate`, `web_screenshot`, `web_dom_get`, `web_dom_query`, `web_console`, `web_js_execute`, `web_blazor_inspect`, `web_blazor_wait_ready`, `web_blazor_get_components`, `web_blazor_get_state`, `web_blazor_trigger_event`, `web_blazor_inspect_circuit`, `web_blazor_inspect_wasm`, `web_network`, `web_element_click`, `web_element_set_value` |

Debug tools report the broker-defined effective mode `dbgModalBlock` when the selected VS process has a modal dialog. The state includes `dialogs` with accessible messages and button IDs, `sessionId`, `underlyingMode`, and `underlyingModeIsStale`. The underlying mode is the last known native debugger state, or `Unknown` if unavailable; modal blocking does not establish whether the debuggee is running or paused. `debug_status` and `debug_get_mode` inspect dialogs directly from the broker, including dialogs that appear after `debug_continue` has returned. `debug_snapshot` and `debug_wait_for_break` return modal state promptly without fetching locals, stacks, or watch expressions while a known modal is open.

While an IDE command is pending, the broker also checks for modal dialogs about once per second. Interrupted commands returning debugger state or a debug snapshot expose `dbgModalBlock` as a successful state observation, with `operation.operationId` identifying the original command, which is still running. Other response types report `error_code: dialog_blocked` with `metadata.dialogs` (a JSON array), `sessionId`, `operationId`, and recovery guidance. Use the returned message and button names to choose a response consistent with the user's task and existing authorization. Pass the exact `dialogId`, `buttonId`, and `sessionId` to `dialog_respond`, then call `debug_status` with that `sessionId` and optional `operationId`. Do not repeat the original command.

`debug_status` reports current effective mode and, when applicable, an `operation` whose status is `running`, `completed`, or `failed`. With no operation ID, it includes the latest retained operation for that VS session. With an operation ID and no routing fields, it selects the original session; explicit routing must agree. `operation.result` contains the original session RPC payload, including a nested success/message envelope when that RPC returns one. While a retained command is still pending, mode readings without a detected dialog are marked stale so another status RPC is not queued behind the command. A completed status check does not itself mean the original command succeeded: inspect `operation.status` and `operation.success`. The original command keeps its original timeout. Completed outcomes remain available for 10 minutes after completion and are lost if the broker restarts. Canceling a status check does not cancel the retained command.

To inspect dialogs independently, call `dialog_list` with the intended `sessionId` (or solution routing fields). Every listing or automatic probe replaces previous IDs for that process, and each dialog ID permits one response attempt. A successful response means the button was invoked; it does not guarantee that the dialog closed. If a response times out, list dialogs again before retrying because the invocation may already have occurred. Responses can discard changes or authorize other actions.

The broker uses Windows UI Automation directly so dialog inspection and responses can run while the VS UI thread is waiting for input. Message text comes from accessible text controls; custom controls may expose less information, and `truncated` indicates a bounded scan did not read the entire control tree. Only dialogs owned by the selected VS process are included; use `ui_*` tools for debuggee/application dialogs. Detection runs through debug-state checks and while IDE calls are pending, rather than as idle background notifications.

## Architecture

```text
MCP client (Claude, Copilot, any agent)
  -> HTTP MCP  http://127.0.0.1:5050/mcp
    -> NetVsMcp.Broker  (bundled WPF tray broker)
      -> NetVsMcp.Vsix  Visual Studio instance A  (named pipe)
      -> NetVsMcp.Vsix  Visual Studio instance B
      -> NetVsMcp.Vsix  Visual Studio instance C
```

The broker is the only MCP server. The VSIX includes the same WPF tray/status broker app and starts it when Visual Studio opens if no compatible broker is already running; otherwise Visual Studio connects to the already-running broker. A VSIX-bundled broker exits after all registered Visual Studio sessions are gone, and Visual Studio starts a replacement if the bundled broker is killed while Visual Studio is still running. Release assets also include a standalone broker MSI for fallback deployments where the bundled broker cannot be used or where an administrator wants to install the broker separately. Visual Studio extensions connect to the broker over a per-user named pipe using StreamJsonRpc. For IDE operations, the broker resolves the target session and forwards the call through the existing pipe connection to the correct VSIX instance. The VSIX executes it using the Visual Studio SDK and returns a structured result. Dialog tools resolve the same session and operate directly from the broker against that session's process.

## Session routing

Every routed tool accepts optional routing fields:

```json
{
  "sessionId": null,
  "processId": null,
  "solutionPath": null,
  "workspacePath": null,
  "rootPath": null,
  "solutionName": null
}
```

Resolution order: explicit session ID → process ID → normalized solution path → nearest solution found from `workspacePath`/`rootPath` → solution name → active Visual Studio window → only registered instance → fail with candidate metadata.

Routing failures return a structured error with `failureReason`, `candidateCount`, and `candidateSessionIds` so the client can recover without guessing.

## MCP registration

If no detected MCP client is already registered for the broker endpoint and the broker has not already shown setup, the status window opens directly on **Agents**. You can also open the broker from the Windows tray later, select **Agents**, and click **Register** for your detected MCP client. If a detected client already points at the broker endpoint under any server name, the button shows **Update** instead and the broker records setup as complete so later starts stay tray-first. The broker writes or updates the client's config file and backs it up to `<path>.bak` by default, overwriting that single backup on later updates. Restart or reload the MCP client after registration.

For manual registration, add an HTTP MCP server that points at the local broker:

```json
{
  "mcpServers": {
    "netvs": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp"
    },
    "netvs-web-automation": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp-wu"
    }
  }
}
```

The broker status window shows this snippet ready to copy. The `netvs-web-automation` endpoint serves the rarely used `ui_*`/`web_*` debuggee UI automation tools, kept off the default endpoint to keep the advertised tool list smaller.

## Better results with best-practices guides

NetVsMcp gives your AI agent tools to control Visual Studio. For better results, pair it with the included best-practices guides: domain knowledge that teaches the agent how to route to the right Visual Studio session and use the management, navigation, editing, build, debug, and automation tools safely.

The broker exposes the bundled guide entrypoints as MCP resources such as `guide://netvsmcp/manage-visual-studio.md`. Tool-only clients can call `netvs_get_best_practices` with no arguments to list guides and reference files, or with `guide` and optional `file` to read one file.

The guides are designed for progressive disclosure. When an agent is about to use a NetVsMcp tool family, it should read the matching guide's small entrypoint first, then only the reference files needed for the current operation. When the user asks to learn NetVsMcp or create local agent skills for it, the agent can read more broadly, but generated skills should also stay small and route to references instead of embedding the full guide text.

The bundled guides are agent-neutral defaults, not locked policy; users and projects can layer their own agent or repository instructions on top.

The source files for the exposed guide corpus live under `BestPractices/`. They are product assets bundled into the broker and exposed to NetVsMcp clients; they are intentionally not stored under `.agents` or `.codex` so agents developing this repository do not load the client-facing tool manuals as local development skills.

## Projects

```text
NetVsMcp.slnx
  src/NetVsMcp.Broker        WPF tray/status app and local HTTP MCP broker
  src/NetVsMcp.Broker.Analytics  Local SQLite aggregate usage analytics
  src/NetVsMcp.Contracts     Shared DTOs and RPC contracts
  src/NetVsMcp.Installer     Fallback WiX MSI installer for standalone broker deployments
  src/NetVsMcp.Vsix          Visual Studio extension
  tests/NetVsMcp.Broker.Tests
```

## Build

```powershell
dotnet restore .\NetVsMcp.slnx
dotnet build .\NetVsMcp.slnx
```

Contributor Debug builds are isolated from the installed Release broker, so a locally debugged broker can run side by side with the Release tray app. See [docs/SETUP.md](docs/SETUP.md#run-the-broker) for the Debug defaults.

Build the standalone broker installer MSI, mainly for fallback/manual broker deployments:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release
```

See [docs/SETUP.md](docs/SETUP.md) for local setup and usage, [docs/BROKER_UX.md](docs/BROKER_UX.md) for tray and status window behavior, and [docs/SECURITY.md](docs/SECURITY.md) for the local security model.
