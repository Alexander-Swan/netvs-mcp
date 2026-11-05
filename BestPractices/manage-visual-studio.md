# Manage Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Start with `vs_list_sessions` when routing is uncertain.
- Prefer solution-based routing (`solutionPath`, then `solutionName`) for normal work; use `sessionId` only when you intentionally need a specific running Visual Studio instance.
- Repeat the chosen explicit routing field on later routed calls; NetVsMcp does not persist a global selection.
- If no Visual Studio session is registered, infer the target solution and launch Visual Studio with `vs_launch_instance` when available.
- Prefer focused combo tools such as `vs_context_snapshot`, `solution_overview`, and `test_run_and_get_results` when they match the task.
- Confirm before broad project/reference/test changes unless the user explicitly requested the mutation.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Resolve sessions, inspect broker health, or fetch logs | `references/routing.md` |
| Launch Visual Studio when no session exists | `references/launching.md` |
| Get a broad IDE snapshot before deciding what to do | `references/context-snapshot.md` |
| Observe session events or pause new automation mutations | `references/events-control.md` |
| Activate windows, show tool windows, or run DTE commands | `references/windows-commands.md` |
| Open/close/inspect solutions, projects, startup projects, files, or references | `references/solutions-projects.md` |
| Discover, run, debug, or inspect tests | `references/tests.md` |

## Covered Tools

`vs_*`, `session_automation_control`, `netvs_doctor`, `get_help`, `execute_command`, `window_*`, `toolwindow_*`, `solution_*`, `project_*`, `startup_project_*`, `test_*`, `task_list_*`, `git_context`, and `vs_context_snapshot`.
