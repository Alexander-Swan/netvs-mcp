# Tests

Use test tools when the user wants Visual Studio's test context or a VS-routed test workflow.

```json
test_discover({ "projectName": "App.Tests", "sessionId": "..." })
test_run({ "projectName": "App.Tests", "filter": "FullyQualifiedName~OrderTests", "sessionId": "..." })
test_results({ "runId": null, "sessionId": "..." })
test_run_and_get_results({ "projectName": "App.Tests", "filter": null, "runId": null, "sessionId": "..." })
```

`projectName` is optional on discover/run; omit it to target the solution. Prefer `test_run_and_get_results` when you need to run tests and immediately inspect results.

Use `test_debug` only for a specific test or subset. `filter` is required so the whole suite is not launched under the debugger by accident. After attach succeeds, use normal debug tools. When finished, prefer detaching from the returned test host process if you only need cleanup.

Every test tool returns a `supported` flag. If it is `false`, report that and use build/output evidence or ask how the user prefers to run tests.
