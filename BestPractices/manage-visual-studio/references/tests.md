# Tests

Use test tools when the user wants Visual Studio's test context or a VS-routed test workflow.

Existing calls default to the CLI backend. For IDE discovery, execution, owned cancellation, and debugging, explicitly select `backend: "test-explorer"` and follow [Test Explorer backend](test-explorer.md).

```json
test_discover({ "projectName": "App.Tests", "sessionId": "..." })
test_run({ "projectName": "App.Tests", "filter": "FullyQualifiedName~OrderTests", "sessionId": "..." })
test_results_page({ "runId": "<from test_run>", "offset": 0, "maxItems": 25, "sessionId": "..." })
test_run_and_get_results({ "projectName": "App.Tests", "filter": null, "sessionId": "..." })
```

`projectName` is optional on CLI discover/run; omit it to target the solution. Test Explorer requires it. Prefer `test_run_and_get_results` when you need to run tests and immediately inspect results.

Use `test_debug` only for a specific test or subset. `filter` is required so the whole suite is not launched under the debugger by accident. After attach succeeds, use normal debug tools. When finished, prefer detaching from the returned test host process if you only need cleanup.

CLI/IDE test operation results include a `supported` flag; it does not mean tests passed. Inspect `outcome` and the runner exit code, then page retained cases using the exact `runId`. Never rerun a test solely to recover expired output.

Prefer `test_results_page` over the legacy whole-collection `test_results`. Supply the exact `runId`; no backend argument is needed. Offsets are zero-based and `maxItems` is 1–100 (default 25). Follow `nextOffset`, since rich failure evidence can produce shorter pages. Inspect total/retained counts and evidence truncation. History retains up to eight runs for 15 minutes in the VSIX lifetime, capped at 10000 cases and two million evidence characters per run. An expired run fails explicitly.

For line coverage, call `test_coverage_run` with an explicit `projectName` and route. It requires an updated VSIX and the project's installed `coverlet.collector`; it installs nothing. `coverage.outcome` reports `collected`, `unavailable`, or `invalidReport` independently of the test outcome. A missing report is not zero coverage. Use the returned `runId` with `test_coverage_results` for bounded file summaries, then `test_coverage_file` with an exact reported path for line hits. Follow `nextOffset`, and qualify `truncated` evidence. Coverage shares the eight-run, 15-minute VSIX test history. Branch coverage and Test Explorer coverage are not implemented.
