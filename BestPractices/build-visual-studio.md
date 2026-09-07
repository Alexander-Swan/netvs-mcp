# Build Visual Studio With NetVsMcp

Agent-neutral entrypoint for build, rebuild, clean, build status, diagnostics, output panes, Task List, package restore, dependencies, and NuGet operations through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling a matching build/package tool, then only the relevant reference files.
- For learning or local skill creation, read references deliberately and keep generated skills as routers with references.

## First Rules

- Use explicit routing when multiple Visual Studio sessions may match.
- Prefer `build_and_get_errors` when you need post-build diagnostics.
- Poll `build_status` after a non-waiting build.
- Confirm before package install/update/uninstall unless the user explicitly requested it.
- Report exit codes/messages from package operations instead of retrying blindly.

## Reference Files

| Task | Read |
| --- | --- |
| Build, rebuild, clean, cancel, status, or configuration | `build-visual-studio/references/building.md` |
| Read errors or output panes | `build-visual-studio/references/errors-output.md` |
| Inspect or mutate Visual Studio Task List items | `build-visual-studio/references/task-list.md` |
| Restore packages, inspect dependencies, or manage NuGet packages | `build-visual-studio/references/packages.md` |

## Covered Tools

`build_*`, `clean_solution`, `rebuild_solution`, `errors_list`, `output_*`, `task_list_*`, `package_restore`, `project_dependencies`, `project_add_reference`, `project_remove_reference`, and `nuget_*`.
