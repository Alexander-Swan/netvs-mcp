# Building

```json
build_solution({ "waitForBuildToFinish": false, "sessionId": "..." })
build_project({ "projectName": "NetVsMcp.Broker", "waitForBuildToFinish": true, "sessionId": "..." })
rebuild_solution({ "waitForBuildToFinish": true, "sessionId": "..." })
clean_solution({ "sessionId": "..." })
build_cancel({ "sessionId": "..." })
build_status({ "sessionId": "..." })
build_configuration_get({ "sessionId": "..." })
build_configuration_set({ "configuration": "Release", "platform": "Any CPU", "sessionId": "..." })
```

`build_solution` defaults to fire-and-forget. `build_project` and `rebuild_solution` default to waiting. Pass `waitForBuildToFinish` explicitly when the distinction matters.

After `build_solution({ "waitForBuildToFinish": false, "sessionId": "..." })`, use `build_status`, `errors_list`, or output tools to check progress and results. Waiting builds use IDE build-completion events; canceling a wait alone does not prove that the build stopped. Use `build_cancel` when cancellation is intended, then inspect status.

`build_status.state` mirrors Visual Studio `SolutionBuild.BuildState`; `lastBuildInfo` is the error count from the most recently completed build.

Use `build_and_get_errors({ "includeWarnings": true, "maxItems": 200, "sessionId": "..." })` when you need a waiting build and Error List diagnostics in one response.
