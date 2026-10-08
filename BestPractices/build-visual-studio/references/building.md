# Building

```json
build_solution({ "waitForBuildToFinish": false, "sessionId": "..." })
build_project({ "projectName": "NetVsMcp.Broker", "waitForBuildToFinish": true, "sessionId": "..." })
rebuild_solution({ "waitForBuildToFinish": true, "sessionId": "..." })
clean_solution({ "sessionId": "..." })
build_cancel({ "sessionId": "..." })
build_status({ "sessionId": "..." })
events_wait({ "eventTypes": ["build_completed"], "timeoutSeconds": 120, "sessionId": "..." })
events_list({ "eventTypes": ["build_completed"], "sinceSequence": 0, "sessionId": "..." })
build_configuration_get({ "sessionId": "..." })
build_configuration_set({ "configuration": "Release", "platform": "Any CPU", "sessionId": "..." })
```

`build_solution` defaults to fire-and-forget. `build_project` and `rebuild_solution` default to waiting. Pass `waitForBuildToFinish` explicitly when the distinction matters.

After `build_solution({ "waitForBuildToFinish": false, ... })`, prefer `events_wait` for `build_completed` instead of polling `build_status`. Keep the returned `nextSequence` and pass it as `sinceSequence` on a later `events_wait` or `events_list` call to avoid re-reading old events.

`build_status.state` mirrors Visual Studio `SolutionBuild.BuildState`; `lastBuildInfo` is the error count from the most recently completed build.

Use `build_and_get_errors({ "includeWarnings": true, "maxItems": 200, "sessionId": "..." })` when you need a waiting build and Error List diagnostics in one response.
