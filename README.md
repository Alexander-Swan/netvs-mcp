# NetVsMcp

NetVsMcp connects MCP clients to live Visual Studio sessions on your Windows machine. It provides editor buffers, Roslyn navigation, build diagnostics, test execution, and debugger control through a local broker.

Install the Visual Studio extension once. It includes the broker, starts it when needed, and connects your open Visual Studio instances to one loopback HTTP endpoint. Register your MCP client once, then route calls by session ID, solution path, or solution name.

IDE operations run locally. NetVsMcp does not send source code or debugger values to a hosted service; your MCP client's own data handling still applies. Optional usage analytics remain local and are disabled by default. Package searches, restores, and update checks can contact their respective services.

## Install and connect

1. Install the VSIX from the [releases](https://github.com/Alexander-Swan/netvs-mcp/releases).
2. Open Visual Studio. The extension connects to an existing compatible broker or starts its bundled tray broker.
3. Open the broker's **Agents** tab and register your MCP client, or configure HTTP manually:

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

The separate `/mcp-wu` endpoint provides optional debuggee UI and browser automation. Add it as another MCP server if needed. A standalone broker MSI is available for deployments where the bundled broker cannot be used.

See [setup](docs/SETUP.md) for installation, client registration, and troubleshooting, and the [security model](docs/SECURITY.md) for the local trust boundary.

## Capabilities

- **Live editing and navigation:** read unsaved buffers, inspect symbols, find references, and apply edits. Safe-edit previews support developer approval.
- **Builds and diagnostics:** build solutions or projects, read output panes and errors, cancel builds.
- **Debugger control:** launch, attach, step, inspect chosen threads and frames, manage breakpoints and watches. Pause identities protect against stale inspection requests.
- **Tests:** discover and run tests with the CLI backend.
- **Multiple sessions:** discover open instances and route explicitly. Pause new automation mutations when needed. Follow session events and reconcile observed state.
- **Local records:** inspect tool-call audit metadata and optional aggregate usage analytics without recording full source or debugger values in those stores.

Use `get_help` for current tool descriptions.

## Tool coverage

| Area | Tools |
| --- | --- |
| Session management | `vs_list_sessions`, `vs_get_session`, `vs_select_session`, `vs_get_status`, `vs_ping`, `vs_launch_instance`, `vs_events`, `session_automation_control`, `vs_get_logs`, `vs_get_usage_summary`, `netvs_doctor`, `get_status`, `get_help`, `netvs_get_best_practices` |
| Documents & editor | `document_active`, `document_read`, `document_open`, `document_write`, `document_save`, `document_close`, `document_list`, `document_cleanup`, `document_outline` |
| Editor mutations | `editor_insert`, `editor_replace`, `editor_goto_line`, `selection_get`, `selection_set` |
| Safe-edit workflow | `edit_preview`, `edit_approve`, `edit_reject`, `edit_list_pending`, `prepare_safe_edit`, `apply_safe_edit_and_build` |
| Code navigation | `code_document_symbols`, `code_workspace_symbols`, `code_go_to_definition`, `code_go_to_implementation`, `code_find_references`, `find_implementations`, `rename_symbol_preview`, `rename_symbol_apply`, `call_hierarchy_get` |
| Code fixes & refactorings | `code_actions_list`, `code_actions_apply`,  |
| Search | `editor_find`, `find_in_files`, `open_relevant_files` |
| Build | `build_solution`, `build_project`, `build_and_get_errors`, `build_status`, `build_cancel`, `build_configuration_get`, `build_configuration_set`, `rebuild_solution`, `clean_solution`, `package_restore` |
| Diagnostics | `errors_list`, `output_read`, `output_write`, `output_clear`, `output_list_panes`, `diagnostics_for_document`, `diagnostics_binding_errors` |
| Task List | `task_list_get`, `task_list_add`, `task_list_remove`, `task_list_set_checked` |
| Debugger | `debug_start`, `debug_start_without_debugging`, `debug_stop`, `debug_restart`, `debug_continue`, `debug_break`, `debug_step`, `debug_attach`, `debug_status`, `debug_get_mode`, `debug_get_callstack`, `debug_select_frame`, `debug_get_locals`, `debug_evaluate`, `debug_eval_many`, `debug_set_variable`, `debug_snapshot`, `debug_wait_for_break`, `debug_hot_reload_apply`, `module_list` |
| Breakpoints | `breakpoint_set`, `breakpoint_list`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_group_list`, `breakpoint_group_enable`, `breakpoint_group_remove` |
| Threads & processes | `debug_get_threads`, `thread_switch`, `thread_get_callstack`, `thread_set_frozen`, `parallel_stacks`, `parallel_watch`, `process_list_debugged`, `process_list_local`, `process_detach`, `process_terminate` |
| Watches & immediate | `watch_add`, `watch_list`, `watch_remove`, `immediate_execute` |
| Exceptions | `exception_settings_get`, `exception_settings_set` |
| Solution & projects | `solution_info`, `solution_overview`, `solution_open`, `solution_close`, `solution_add_project`, `solution_remove_project`, `project_list`, `project_info`, `project_dependencies`, `project_add_file`, `project_remove_file`, `project_add_reference`, `project_remove_reference`, `startup_project_get`, `startup_project_set` |
| NuGet | `nuget_list`, `nuget_search`, `nuget_install`, `nuget_update`, `nuget_uninstall` |
| Tests | `test_discover`, `test_run`, `test_debug`, `test_run_and_get_results`, `test_results`,  |
| Git context | `git_context` |
| Snapshots | `vs_context_snapshot`, `symbol_context`, `debug_snapshot` |
| Console | `console_get_info`, `console_read`, `console_send` |
| Visual Studio modal dialogs | `dialog_list`, `dialog_respond` (also integrated with `debug_status`, `debug_get_mode`, `debug_snapshot`, and `debug_wait_for_break`) |
| Visual Studio UI | `window_activate`, `window_list`, `toolwindow_show`, `toolwindow_hide`, `execute_command`, `format_and_organize` |
| Debuggee UI automation (`/mcp-wu`) | `ui_capture_window`, `ui_capture_region`, `ui_snapshot`, `ui_get_tree`, `ui_find_elements`, `ui_get_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `ui_send_keys`, `ui_wait_for_element`, `ui_wait_idle` |
| Browser automation (`/mcp-wu`) | `web_connect`, `web_disconnect`, `web_status`, `web_navigate`, `web_screenshot`, `web_dom_get`, `web_dom_query`, `web_console`, `web_js_execute`, `web_blazor_inspect`, `web_blazor_wait_ready`, `web_blazor_get_components`, `web_blazor_get_state`, `web_blazor_trigger_event`, `web_blazor_inspect_circuit`, `web_blazor_inspect_wasm`, `web_network`, `web_element_click`, `web_element_set_value` |

Document/editor tools use `path`; navigation, diagnostics, and breakpoint tools generally use `documentPath`. Relative document paths resolve against the routed solution's directory. Call `vs_list_sessions` first when the target is uncertain, then pass the selected `sessionId` or `solutionPath`. Ambiguous routing fails instead of guessing.

## Best-practices guides

The broker exposes focused guides through `netvs_get_best_practices` and resources such as `guide://netvsmcp/manage-visual-studio.md`. List the guides, read the relevant entrypoint, then read only the references needed for the task. User and project instructions can refine these defaults.

- [Session routing, projects, and tests](BestPractices/manage-visual-studio.md)
- [Code navigation and diagnostics](BestPractices/navigate-visual-studio.md)
- [Editing and source transformations](BestPractices/edit-visual-studio.md)
- [Builds and output](BestPractices/build-visual-studio.md)
- [Debugger inspection and control](BestPractices/debug-visual-studio.md)
- [UI, browser, and console automation](BestPractices/automate-visual-studio.md)

Debugger command timeouts do not cancel execution. If a modal dialog interrupts a command, retain its operation ID, use `dialog_list` / `dialog_respond` as appropriate, and inspect `debug_status` instead of repeating the command. See the [event and operation contract](docs/event-operation-contract.md) for identity, ordering, cancellation, and reconciliation behavior.

## Architecture

```text
MCP client
  -> loopback HTTP -> NetVsMcp.Broker
    -> per-user named pipe -> Visual Studio instance A
    -> per-user named pipe -> Visual Studio instance B
```

The broker hosts MCP; each VSIX executes IDE operations in its own Visual Studio instance. Broker-side dialog inspection can operate while the IDE UI thread is blocked. The bundled broker exits after the last registered Visual Studio session is gone; standalone MSI runs remain user-managed.

See [broker behavior](docs/BROKER_UX.md) and [VSIX integration](docs/VSIX.md) for details.

## Build

Contributor builds require Visual Studio extension development tooling and a .NET SDK compatible with the solution's target frameworks.

```powershell
dotnet restore .\NetVsMcp.slnx
dotnet build .\NetVsMcp.slnx
dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj --no-build
dotnet test .\tests\NetVsMcp.Vsix.Tests\NetVsMcp.Vsix.Tests.csproj --no-build
```

Debug builds use separate broker defaults so they can run alongside an installed Release broker. See [contributor setup](docs/SETUP.md#run-the-broker).

Build the standalone broker MSI with:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release
```
