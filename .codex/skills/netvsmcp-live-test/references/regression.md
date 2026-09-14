# Live Regression Procedure

Run this after the selected Debug or Release instance is installed, running, connected, and visible through MCP.

## Tool Inventory

1. Enumerate every available NetVsMcp MCP tool in the selected server, including optional web/UI automation tools if that endpoint is part of the test.
2. For each tool, read its description before calling it.
3. Record non-argument instructions from the description, especially:
   - required prerequisite tools or guide calls;
   - warnings that another tool should be preferred instead;
   - routing requirements for multiple Visual Studio sessions;
   - state requirements such as "debuggee must be paused" or "preview must be created first";
   - cleanup requirements.
4. Ignore purely argument-shape guidance for the purpose of this instruction log, though still pass valid arguments when invoking the tool.

## Coverage Strategy

Call every tool at least once unless doing so would require unsafe external side effects that cannot be contained. Use the safest meaningful invocation for each tool:

- Management/session tools: list sessions, inspect context, and verify explicit routing.
- Best-practices tools/resources: call the entrypoint or focused reference required by the tool family being tested.
- Document/editor tools: use a disposable document or a reversible edit preview, then undo/revert.
- Navigation and symbol tools: use stable symbols in the loaded solution.
- Build and diagnostics tools: prefer read-only status first; run builds only when the user requested full live testing and the solution is expected to build.
- Test tools: discover first, then run a small filtered test when a safe filter is available.
- Debugger tools: start or attach only to a controlled debug target, pause at an intentional breakpoint, exercise stack/locals/evaluate/step/status tools, then remove breakpoints and restore execution state.
- Process tools: inspect broadly if safe, but terminate or detach only from throwaway processes started for the test.
- UI/web automation tools: use an app or page created for the test; do not drive unrelated user apps or browsers.
- Analytics/log tools: verify data shape and recent entries without exposing sensitive payloads in the final report.

If a tool cannot currently be called, do not skip it merely because a prerequisite is missing. Read its description and the matching best-practices guide, identify the required earlier tool call or state, establish that prerequisite, and retry the tool. Use the dependency plan below as the initial call-order map. Record a tool as blocked only after its prerequisite cannot be established within the controlled test environment; record the exact missing prerequisite and the evidence from the tool response.

## Full-Coverage Dependency Plan

Use this plan when the requested regression pass is intended to call every exposed tool. It is a state-machine plan, not a mandate to operate on a user's working solution. In this repository, use the local regression solution at `.regressionTarget/RegressionTarget.slnx` as the default controlled fixture. Use a different controlled solution only when the user explicitly provides or requests one. Pass the same explicit `sessionId` on every routed call.

### Fixture contract

Open `.regressionTarget/RegressionTarget.slnx` for default coverage. It contains `RegressionTarget.Console`, `RegressionTarget.WinForms`, and `RegressionTarget.Web`; use the corresponding app for console, desktop UI, and web coverage. The directory is intentionally Git-ignored but persistent: restore its baseline after reversible test mutations rather than deleting it. Prepare separate throwaway targets only for coverage the solution does not supply, such as a test project, class library, intentional warning/error, code-action and rename targets, a browser with a CDP endpoint, or helper processes for attach, detach, and terminate coverage. Do not claim full coverage when the console, UI-automation, or CDP backend is unavailable; record the affected tools as blocked with that concrete capability gap.

Retain returned values needed by later calls: `sessionId`, `solutionPath`, `projectName`, `documentPath`, `editId`, `breakpointName`, `threadId`, `watchId`, `processId`, `testFilter`, `webTarget`, and UI selectors.

### Ordered phases

1. **Broker and routing.** Call `netvs_doctor`, `vs_get_status`, `vs_ping`, `vs_list_sessions`, `get_help`, `netvs_get_best_practices`, `vs_get_usage_summary`, and `vs_get_logs`. If no registered session exists, call `vs_launch_instance(solutionPath)`, then repeat `vs_list_sessions`. Resolve and retain an explicit route with `vs_get_session` and `vs_select_session`. Then call `get_status`, `vs_context_snapshot`, `window_list`, `window_activate`, `toolwindow_show`, and `toolwindow_hide`. `vs_select_session` does not persist a global selection.
2. **Solution and project state.** Call `solution_open`, `solution_info`, `solution_overview`, `project_list`, `project_info`, `project_dependencies`, `startup_project_get`, and `git_context`. Exercise only reversible fixture mutations: `solution_add_project` -> `project_add_file` -> `project_remove_file` -> `solution_remove_project`; then `startup_project_set` -> `startup_project_get`; and a harmless fixture-only `execute_command`.
3. **Documents, symbols, and direct edits.** Call `document_active`, `document_list`, `document_open`, `document_read`, `selection_get`, `document_outline`, `code_document_symbols`, `code_workspace_symbols`, `editor_find`, `find_in_files`, `code_go_to_definition`, `code_go_to_implementation`, `code_find_references`, `find_implementations`, `call_hierarchy_get`, `symbol_context`, `open_relevant_files`, `selection_set`, `editor_goto_line`, `document_write`, `editor_insert`, `editor_replace`, `document_save`, `document_cleanup`, `format_and_organize`, `errors_list`, `diagnostics_for_document`, `diagnostics_binding_errors`, and `document_close`. For transformations, use `code_actions_list` -> returned action index -> `code_actions_apply`, and `rename_symbol_preview` -> `rename_symbol_apply` -> reverse the fixture rename.
4. **Safe edits.** Run all three chains with the same explicit route: `edit_preview` -> `value.pendingEdit.editId` -> `edit_approve`; `prepare_safe_edit` -> `value.preview.pendingEdit.editId` -> `edit_reject`; `edit_preview` -> `edit_list_pending` -> `value.pendingEdit.editId` -> `apply_safe_edit_and_build`.
5. **Packages, builds, tests, and task list.** Call `build_configuration_get` -> `build_configuration_set` -> `build_configuration_get`; `package_restore`, `nuget_search`, and `nuget_list`; then `nuget_install` -> `nuget_update` -> `nuget_uninstall` and `project_add_reference` -> `project_remove_reference`. Run `build_solution`, `build_status`, `build_and_get_errors`, `build_project`, `output_list_panes`, `output_read`, `output_write`, `output_clear`, `clean_solution`, `rebuild_solution`, and `build_cancel`. `build_status` is not reliable in a cold session until `build_solution` or `build_and_get_errors`; a Build output pane may not exist until `output_list_panes` or a build/debug action. Run `test_discover` -> `test_run` -> `test_results` -> `test_run_and_get_results`; then create a temporary user task with `task_list_add`, call `task_list_get` and retain that task's returned live index, call `task_list_set_checked` with the retained index, and call `task_list_remove` with that same index. Do not assume index `0`: Task List indices are 1-based and may shift.
6. **Debugger.** Set and manage a fixture breakpoint with `breakpoint_set`, `breakpoint_list`, `breakpoint_group_list`, `breakpoint_enable`, `breakpoint_group_enable`, `breakpoint_remove`, and `breakpoint_group_remove`. Start the fixture with `debug_start`, inspect `debug_status`, then wait for its intentional pause using `debug_wait_for_break`. While paused, call `debug_get_mode`, `debug_snapshot`, `debug_get_callstack`, `debug_get_locals`, `debug_evaluate`, `debug_eval_many`, `debug_set_variable`, `immediate_execute`, `module_list`, `parallel_stacks`, and `parallel_watch`. Call `watch_add` -> `watch_list` -> `watch_remove`; `debug_get_threads` -> `thread_switch` -> `thread_set_frozen` -> `thread_get_callstack`; and `exception_settings_get` -> `exception_settings_set`. Exercise `debug_step`, wait for a stable pause, then `debug_continue`; use a hot-reloadable fixture change before `debug_hot_reload_apply`; call `debug_restart`, `debug_break`, and `debug_start_without_debugging` when their expected state applies. Use `process_list_local` -> `debug_attach`, then `process_list_debugged` -> `process_detach`; attach separately to a disposable process before `process_terminate`. Run `test_debug` with a non-empty `testFilter`; finally call `debug_stop`.
7. **Console, UI, and web.** Start a disposable console debuggee before console coverage; call `console_get_info` to discover its returned target, then pass that target to `console_read` and `console_send`. A desktop GUI debuggee without a console is not a valid `console_send` fixture. Then, with the desktop debuggee and optional backends active, call `ui_capture_window`, `ui_capture_region`, `ui_snapshot`, `ui_get_tree`, `ui_find_elements`, `ui_get_element`, `ui_wait_for_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `ui_send_keys`, and `ui_wait_idle`. For web coverage call `web_connect(CDP endpoint)` -> `web_status` -> `web_navigate` -> `web_screenshot` -> `web_dom_get` -> `web_dom_query` -> `web_console` -> `web_js_execute` -> `web_network` -> `web_element_click` -> `web_element_set_value` -> `web_disconnect`. `web_js_execute` specifically requires a connected CDP backend; DOM calls can use their URL/HTTP fallback.
8. **Verification and cleanup.** Call `build_and_get_errors` and `test_run_and_get_results`. Remove test-created breakpoints, watches, tasks, packages, references, temporary projects and files. Stop debuggees, close the disposable solution with `solution_close`, and confirm the final broker/session state with `vs_get_session` and `vs_list_sessions`.

## Result Tracking

Maintain a table or checklist during the run with:

- tool name;
- setup or prerequisite instruction noticed;
- invocation summary;
- pass/fail/skip status;
- evidence or returned high-level result;
- cleanup performed or still needed.

Do not paste large MCP responses into the final report. Summarize failures and notable results, and keep raw logs in a local artifact only when the user asks for one.

## Final Cleanup And Report

Before reporting completion:

1. Remove test breakpoints and temporary debugger state.
2. Revert all test edits, generated temporary files, and repository changes made by the live test.
3. Confirm `git status --short` is back to its initial state except for user-owned changes that existed before the run.
4. Call the broker analytics or usage-summary tool after the live pass, normally `vs_get_usage_summary`, and include the returned high-level analytics result in the report. Summarize counts, retention/configuration, and notable recent-tool outcomes without pasting sensitive raw payloads.
5. Report the tested mode, broker endpoint, Visual Studio instance, extension/broker version evidence when available, tool coverage counts, failures, skips, analytics summary, and cleanup status.
