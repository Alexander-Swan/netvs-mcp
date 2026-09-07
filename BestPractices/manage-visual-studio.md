# Manage Visual Studio With NetVsMcp

Agent-neutral entrypoint for session routing, launching Visual Studio, windows, solutions, projects, references, and tests through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling a matching tool, then read only the reference files needed for the current operation.
- For learning NetVsMcp or creating local agent skills, read entrypoints broadly and then relevant references deliberately.
- Do not load every reference file by default. These guides are product knowledge exposed by NetVsMcp, not Codex-specific policy.

## First Rules

- Start with `vs_list_sessions` or `vs_get_session` when routing is uncertain.
- Repeat explicit routing fields such as `sessionId` on later routed calls; NetVsMcp does not persist a global selection.
- If no Visual Studio session is registered, infer the target solution and launch Visual Studio with `vs_launch_instance` when available.
- Prefer focused combo tools such as `vs_context_snapshot`, `solution_overview`, and `test_run_and_get_results` when they match the task.
- Confirm before broad project/reference/test changes unless the user explicitly requested the mutation.

## Reference Files

Read only the rows that match the task.

| Task | Read |
| --- | --- |
| Resolve sessions, inspect broker health, or fetch logs | `manage-visual-studio/references/routing.md` |
| Launch Visual Studio when no session exists | `manage-visual-studio/references/launching.md` |
| Get a broad IDE snapshot before deciding what to do | `manage-visual-studio/references/context-snapshot.md` |
| Activate windows, show tool windows, or run DTE commands | `manage-visual-studio/references/windows-commands.md` |
| Open/close/inspect solutions, projects, startup projects, files, or references | `manage-visual-studio/references/solutions-projects.md` |
| Discover, run, debug, or inspect tests | `manage-visual-studio/references/tests.md` |

## Covered Tools

`vs_*`, `netvs_doctor`, `get_help`, `execute_command`, `window_*`, `toolwindow_*`, `solution_*`, `project_*`, `startup_project_*`, `test_*`, `task_list_*`, `git_context`, and `vs_context_snapshot`.
