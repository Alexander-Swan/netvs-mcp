# Solutions, Projects, Files, And References

Use `solution_info` for basic state and `solution_overview` when you need solution info, projects, startup project, and heuristic test-project detection in one call.

```json
solution_info({ "sessionId": "..." })
solution_overview({ "sessionId": "..." })
solution_open({ "path": "D:\\Work\\App\\App.sln", "sessionId": "..." })
solution_close({ "sessionId": "..." })
project_list({ "sessionId": "..." })
project_info({ "projectName": "App.Core", "sessionId": "..." })
startup_project_get({ "sessionId": "..." })
startup_project_set({ "projectName": "App.Web", "sessionId": "..." })
```

Use the project display name from Solution Explorer, or the unique name from `project_list` if matching is ambiguous.

`project_add_file` requires an existing file. `project_remove_file` removes the project item but does not delete the file from disk.

`project_add_reference` and `project_remove_reference` accept `referenceType: "assembly"` or `"project"`. Confirm before changing references unless the user explicitly requested it.
