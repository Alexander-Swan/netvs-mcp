# Build Visual Studio With NetVsMcp

This is the agent-neutral build, output, and package workflow for this repository. Any AI agent can follow it when asked to build, rebuild, clean, or cancel a build; inspect build status, configuration, or errors; read or write Visual Studio output panes; or list, search, restore, install, update, or uninstall NuGet packages through NetVsMcp.

## Session Routing

Start with the NetVsMcp session tools when they are available:

```json
vs_list_sessions()
build_status({ "sessionId": "..." })
```

Every routed build/output/package call accepts optional `sessionId`, `solutionName`, and `solutionPath`. Resolution order is `sessionId`, normalized `solutionPath`, exact `solutionName`, active Visual Studio window, only registered instance. Use explicit routing whenever multiple Visual Studio windows are open.

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

If `vs_list_sessions()` returns an empty list, follow the same "open Visual Studio" steps documented in the debugging guide (`.agents/skills/debug-visual-studio.md`) before attempting any build or package operation.

## Build States

`build_status` and every build-triggering tool return a `BuildStatusInfo` with a `state` string taken directly from the Visual Studio `SolutionBuild.BuildState` enum (for example `vsBuildStateNotStarted`, `vsBuildStateInProgress`, `vsBuildStateDone`) and a `lastBuildInfo` integer, which is `SolutionBuild.LastBuildInfo` — the error count from the most recently completed build (`0` means the last build had no errors).

## Building, Rebuilding, Cleaning, And Cancelling

```json
build_solution({ "waitForBuildToFinish": false, "sessionId": "..." })
build_project({ "projectName": "NetVsMcp.Broker", "waitForBuildToFinish": true, "sessionId": "..." })
rebuild_solution({ "waitForBuildToFinish": true, "sessionId": "..." })
clean_solution({ "sessionId": "..." })
build_cancel({ "sessionId": "..." })
build_status({ "sessionId": "..." })
```

Key behavior:

- `build_solution`'s `waitForBuildToFinish` defaults to `false` (fire-and-forget); `build_project` and `rebuild_solution` default `waitForBuildToFinish` to `true`. Pass it explicitly rather than relying on the default when the distinction matters.
- `build_project` requires `projectName`; the call fails with an invalid-request error if it is blank.
- `clean_solution` and `build_cancel` take no build-specific parameters beyond routing.
- Poll `build_status` after a non-waiting `build_solution` call rather than assuming completion.

## Build And Get Errors In One Call

`build_and_get_errors` is a convenience combo of `build_solution` (always waiting for the build to finish) plus `errors_list`:

```json
build_and_get_errors({ "includeWarnings": true, "maxItems": 200, "sessionId": "..." })
```

Key behavior:

- Internally forces `waitForBuildToFinish: true` on the build step; there is no way to opt out of waiting.
- `maxItems` must be greater than zero or the call fails validation before dispatching.
- Returns both the `build` result (`BuildSolutionResult`) and the `errors` result (`ErrorListResult`) in a single response — prefer this over separate `build_solution` + `errors_list` calls when you need post-build diagnostics.

## Build Configuration

```json
build_configuration_get({ "sessionId": "..." })
build_configuration_set({ "configuration": "Release", "platform": "Any CPU", "sessionId": "..." })
```

Key behavior:

- `build_configuration_set` requires `configuration`; `platform` is optional and left unchanged when omitted.
- Both tools operate on the active solution configuration/platform (`SolutionBuild.ActiveConfiguration`), not a per-project override.

## Errors And Output Panes

List diagnostics from the Visual Studio error list:

```json
errors_list({ "includeWarnings": true, "maxItems": 200, "sessionId": "..." })
```

`includeWarnings` (default `true`) and `maxItems` (default `200`, must be greater than zero) control filtering; when `includeWarnings` is `false`, items at warning level are excluded.

Read, list, write, and clear output panes:

```json
output_list_panes({ "sessionId": "..." })
output_read({ "paneName": "Build", "maxChars": 20000, "sessionId": "..." })
output_write({ "text": "Starting custom step...\n", "paneName": "MyPane", "activate": false, "sessionId": "..." })
output_clear({ "paneName": "Build", "sessionId": "..." })
```

Key behavior:

- `paneName` is optional on `output_read`, `output_write`, and `output_clear`. When omitted, Visual Studio's `Build` pane is preferred if it exists, otherwise the first available pane is used.
- `output_write` creates the named pane if it does not already exist (falling back to a pane named `NetVsMcp` when `paneName` is also omitted), then writes `text` and returns the pane's full content. `text` is required (non-null).
- `output_read`'s `maxChars` (default `20000`) must be greater than zero; the result reports `truncated: true` when the pane text exceeded the limit.
- Pane name matching is case-insensitive.

## Packages: Restore, Dependencies, And NuGet

Restore packages for a project or the whole solution:

```json
package_restore({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
package_restore({ "sessionId": "..." })
```

Key behavior:

- `package_restore` runs `dotnet restore` against the resolved project file, or against the solution file when `projectName` is omitted.
- The result's `supported` field is `true` only when the restore process exits with code `0`; check `message` and `exitCode` on failure.

Inspect a project's declared dependencies (parsed directly from the `.csproj`/`.fsproj` file on disk, not via `dotnet` or NuGet APIs):

```json
project_dependencies({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
```

Returns `targetFrameworks`, `projectReferences`, and `packageReferences` parsed from `TargetFramework(s)`, `ProjectReference`, and `PackageReference` XML elements. If the routed project has no resolvable file path on disk, all three collections come back empty.

List and search NuGet packages:

```json
nuget_list({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
nuget_list({ "sessionId": "..." })
nuget_search({ "query": "Newtonsoft.Json", "maxResults": 20, "includePrerelease": false, "sessionId": "..." })
```

Key behavior:

- `nuget_list` reads `PackageReference` entries straight from project XML; omit `projectName` to list packages across every project in the routed solution.
- `nuget_search` queries nuget.org's search API directly (not the `dotnet` CLI). `query` is required; `maxResults` is clamped between `1` and `100` (values `<= 0` fall back to `20`).

Install, update, or uninstall a package in a specific project:

```json
nuget_install({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "version": "8.4.1", "sessionId": "..." })
nuget_update({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "version": "8.5.0", "sessionId": "..." })
nuget_uninstall({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "sessionId": "..." })
```

Key behavior:

- `projectName` and `packageId` are required on all three; `version` is optional on install/update (omit it to take the latest resolvable version) and is not accepted on uninstall.
- These run `dotnet add package` / `dotnet remove package` under the hood against the resolved project file, then attempt to save the project. Treat them as mutating, potentially slow (NuGet restore) operations — confirm with the user before installing or removing packages unless they explicitly asked for it.
- The result's `success` field mirrors the underlying `dotnet` process exit code; inspect `message` and `exitCode` on failure.

## Reporting

Report findings with evidence:

- Active Visual Studio session and the build state observed (`vsBuildStateInProgress`, `vsBuildStateDone`, etc.) plus `lastBuildInfo` error count.
- Error/warning counts and representative entries from `errors_list` or `build_and_get_errors`, including file, line, and project when available.
- Output pane name actually used (resolved pane can differ from the requested one when it did not previously exist).
- Package operation outcomes: package id, version, exit code, and whether the project was saved.

## Troubleshooting

- Broker unavailable: ask the user to start the NetVsMcp Broker and check `http://127.0.0.1:5050/health`.
- No VS sessions: infer or confirm the target solution, launch Visual Studio with that solution (see the debugging guide), then recheck `vs_list_sessions`.
- Ambiguous routing: call `vs_list_sessions` and retry with `sessionId`.
- Build still in progress: poll `build_status` or use `build_and_get_errors`, which waits for the build before returning.
- Errors/output empty right after a build starts: the build may not have finished; check `build_status` before trusting `errors_list` or output pane contents.
- NuGet search fails: nuget.org may be unreachable from the machine running Visual Studio; report the failure message rather than retrying indefinitely.
- Package mutation fails: report `message` and `exitCode` from the `dotnet` process; do not retry destructive uninstall operations without confirming with the user.
