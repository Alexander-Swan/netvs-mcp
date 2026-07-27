# NetVsMcp Visual Studio Bridge

Give your AI agent a full window into Visual Studio — not just files on disk, but the live IDE: editor buffers, Roslyn's symbol index, the real build system, and the active debugger.

This extension connects Visual Studio to the **NetVsMcp Broker**, a lightweight tray app that exposes a standard MCP endpoint on loopback (`http://127.0.0.1:5050/mcp`). Your MCP client (Claude, Copilot, or any agent) talks to the broker; the broker routes tool calls through to the right Visual Studio instance. No cloud services, no telemetry — everything runs on your machine.

> **Requires the NetVsMcp Broker** — install it before using this extension.
> **Download:** https://github.com/Alexander-Swan/netvs-mcp/releases/latest

## What your agent can do

### Edit & navigate
Read and write documents, apply edits to the live in-memory buffer, go to definition or implementation, find all references, search symbols across the workspace. A **safe-edit workflow** lets the agent queue a preview diff that you review and approve inside Visual Studio before anything lands in the editor.

### Build & diagnose
Trigger solution or project builds, read the live error list and output panes, cancel in-progress builds, switch build configurations, and restore NuGet packages — all from the agent.

### Debug
Full debugger control: start, stop, attach, step (into/over/out), set and manage **conditional breakpoints**, read locals and the call stack, evaluate expressions in the current frame, manage watches, switch threads, freeze/thaw threads, inspect parallel stacks, and take structured snapshots of debugger state.

### Solution, projects & tests
Query solution structure, project dependencies, and NuGet packages. Discover and run tests and read results.

## Tool highlights

| Area | Example tools |
| --- | --- |
| Documents | `document_read`, `document_write`, `document_outline`, `editor_goto_line` |
| Safe edits | `edit_preview`, `edit_approve`, `edit_reject`, `apply_safe_edit_and_build` |
| Navigation | `code_go_to_definition`, `code_find_references`, `code_workspace_symbols` |
| Build | `build_solution`, `build_and_get_errors`, `errors_list`, `output_read` |
| Debugger | `debug_start`, `debug_step`, `debug_evaluate`, `debug_get_locals`, `debug_snapshot` |
| Breakpoints | `breakpoint_set`, `breakpoint_enable`, `breakpoint_group_list` |
| Threads | `thread_switch`, `thread_set_frozen`, `parallel_stacks` |
| Tests | `test_discover`, `test_run_and_get_results` |
| NuGet | `nuget_search`, `nuget_install`, `nuget_update` |

150+ tools in total — see the full list in the project README.

## Project

Source, documentation, and issue tracker: https://github.com/Alexander-Swan/netvs-mcp
