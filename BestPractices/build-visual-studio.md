# Build Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Use explicit routing when multiple Visual Studio sessions may match.
- Prefer `build_and_get_errors` when you need post-build diagnostics.
- For non-waiting builds, use `build_status`, `errors_list`, or output tools to check progress and results.
- Confirm before package install/update/uninstall unless the user explicitly requested it.
- Report exit codes/messages from package operations instead of retrying blindly.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Build, rebuild, clean, cancel, status, or configuration | `references/building.md` |
| Read errors or output panes | `references/errors-output.md` |
| Inspect or mutate Visual Studio Task List items | `references/task-list.md` |
| Restore packages, inspect dependencies, or manage NuGet packages | `references/packages.md` |

## Covered Tools

`build_*`, `clean_solution`, `rebuild_solution`, `errors_list`, `output_*`, `task_list_*`, `package_restore`, `project_dependencies`, `project_add_reference`, `project_remove_reference`, and `nuget_*`.
