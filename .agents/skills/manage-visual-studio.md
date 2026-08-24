# Manage Visual Studio With NetVsMcp

This is the agent-neutral guide for session routing, launching Visual Studio, window management, and solution/project/test workflows through NetVsMcp. Any AI agent can follow it when asked to discover Visual Studio sessions, launch a new instance, run a Visual Studio command, manage windows, open/close/inspect a solution, manage projects and references, or discover/run tests.

This guide also documents the shared session-routing model used by every routed NetVsMcp tool, including debugging and editing tools documented in other skill guides.

## Session Routing

Most NetVsMcp tools are "routed": they accept optional `sessionId`, `solutionName`, and `solutionPath` parameters (some also accept `processId`, `workspacePath`, and `rootPath`) and resolve them to one registered Visual Studio session before doing anything else.

Start with the session discovery tools when they are available:

```json
vs_list_sessions()
vs_get_status()
```

`vs_list_sessions()` returns every `VsSessionInfo` currently registered with the local broker: `sessionId`, `processId`, `visualStudioVersion`, `edition`, `solutionName`, `solutionPath`, `activeDocument`, `debuggerMode`, `isActiveWindow`, `lastSeenUtc`, and `capabilities`. `vs_get_status()` additionally reports the broker's own endpoint, pipe name, uptime, and version, plus the health (`Connected` or `Stale`) and age of each session.

### Routing resolution order

When a routed tool is called, the broker resolves the target session in this exact order, stopping at the first field that is set:

1. `sessionId` — exact, case-insensitive match against a registered session id. Fails with `SessionNotFound` if no session has that id.
2. `processId` — match against the Visual Studio process id. Fails with `ProcessIdNotFound` if none match, or `Ambiguous` if more than one session shares the process id.
3. `solutionPath` — normalized and compared case-insensitively against each session's registered solution path. Fails with `SolutionPathNotFound` or `Ambiguous`.
4. `workspacePath` / `rootPath` — the broker walks up from this path looking for a `.sln` or `.slnx` file (preferring a single match, then preferring `.slnx` when a directory has more than one solution file), then routes by that resolved solution path. Fails with `WorkspacePathNotFound` if no solution file is found at or above the path, or if no session has that resolved solution open.
5. `solutionName` — exact, case-insensitive match against each session's solution name (not the full path). Fails with `SolutionNameNotFound` or `Ambiguous`.
6. No routing fields set at all — if exactly one registered session has `isActiveWindow: true`, use it. Otherwise, if there is exactly one registered session total, use it.
7. Otherwise the call fails with `Ambiguous`, asking the caller to specify `sessionId`, `processId`, `solutionPath`, `workspacePath`, or `solutionName`.

If `vs_list_sessions()` returns no sessions at all, every routed call fails immediately with `NoRegisteredSessions` instead of attempting resolution.

Use explicit routing (most commonly `sessionId`) whenever more than one Visual Studio window is open, or whenever a prior call reported `Ambiguous`.

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

### Resolving and pinging a session without side effects

```json
vs_get_session({ "sessionId": "..." })
vs_select_session({ "solutionPath": "D:\\Work\\App\\App.sln" })
vs_ping({ "solutionName": "App" })
```

- `vs_get_session` and `vs_select_session` both accept the full routing parameter set (`sessionId`, `solutionName`, `solutionPath`, `processId`, `workspacePath`, `rootPath`) and resolve a session using the order above without dispatching any Visual Studio operation. `vs_get_session` returns a `VsSessionStatus` (session plus health and age); `vs_select_session` returns the raw `VsSessionInfo`. Neither persists the selection for later calls — routing parameters must be repeated on every subsequent tool call.
- `vs_ping` accepts the same routing fields, all optional. With no routing fields set, it returns broker-only health (`serverTimeUtc`, `isRunning`, `mcpEndpoint`, `pipeName`, `uptime`, `registeredSessionCount`) with no target session. With any routing field set, it resolves a session the same way and includes its status as `targetSession`.

### Capabilities and help

```json
vs_get_capabilities()
netvs_doctor()
get_help({ "requiresVisualStudioSession": true })
```

`vs_get_capabilities` and `get_help` return the same `BrokerCapabilities` shape: the broker's MCP endpoint, the full tool catalog (name, description, category, `McpEndpointPath`, and whether each tool requires a routed Visual Studio session), and the high-level Visual Studio capability categories (`Editor`, `Navigation`, `Build`, `Debugger`, `Diagnostics`, `Tests`, `ProjectSystem`). `get_help` additionally accepts an optional `requiresVisualStudioSession` filter to list only broker-only tools (`false`) or only session-routed tools (`true`). Check each tool's `McpEndpointPath` before assuming it's callable from the current connection — most tools are on `/mcp`, but `ui_*`/`web_*` tools are only served from the separate opt-in `/mcp-wu` endpoint (see the automate-visual-studio guide).

`netvs_doctor()` returns a structured broker health report with checks for the HTTP endpoint, VSIX registration pipe, registered/connected/stale sessions, pending restart settings, protocol compatibility, and split endpoint tools. Use it when setup or routing looks wrong before trying random routed calls.

### Broker and session logs

```json
vs_get_logs({ "maxFiles": 5, "maxCharsPerFile": 20000 })
```

Returns the broker's logs directory path plus the most recently modified log files (bounded by `maxFiles`), each truncated to `maxCharsPerFile` characters of tail text. Both parameters must be greater than zero. Useful when a session fails to register or a routed call behaves unexpectedly and the user wants broker-side evidence.

## Opening Visual Studio When No Session Exists

If `vs_list_sessions()` returns an empty list, actively open Visual Studio instead of stopping at a request for the user to do it manually.

1. Determine the target solution.
   - Prefer a solution explicitly named by the user.
   - Otherwise prefer the current workspace solution when exactly one `.sln` or `.slnx` is present.
   - If multiple candidate solutions exist, ask the user which project or solution to open.

2. Prefer `vs_launch_instance` over manually starting `devenv.exe`, since it locates an installed Visual Studio automatically and waits for broker registration:

```json
vs_launch_instance({
  "solutionPath": "D:\\Work\\App\\App.sln",
  "experimental": false,
  "edition": null,
  "timeoutSeconds": 60
})
```

Key behavior:

- `solutionPath` is optional; if given, it must exist on disk or the call fails immediately. Omit it to launch Visual Studio with no solution open.
- `experimental` passes `/rootsuffix Exp` to `devenv.exe`. Set it to `true` when the NetVsMcp VSIX (or another extension under test) is only installed in the experimental instance.
- `edition` optionally filters installed Visual Studio editions (matched as a substring of the `devenv.exe` path returned by `vswhere.exe`, for example `"Community"`, `"Enterprise"`).
- `timeoutSeconds` defaults to 60 and is clamped to a maximum of 300; the tool polls every 500ms for the new process to register.
- Visual Studio discovery order: `vswhere.exe` under `Program Files (x86)\Microsoft Visual Studio\Installer` is queried first for any installed `devenv.exe`. If `vswhere.exe` is unavailable or returns nothing, the launcher falls back to the executable path of any already-running `devenv.exe` process. If neither succeeds, the call fails with "No Visual Studio installation was found".
- The result (`VsLaunchInstanceResult`) reports `success`, `message`, the launched `processId`, and the registered `session` (null if the process started but never registered within the timeout).

3. If `vs_launch_instance` is not available in the current tool set, launch Visual Studio manually instead. Locate `devenv.exe` with `vswhere.exe` (see the discovery order below) rather than assuming a fixed path, and substitute the target solution's path:

```powershell
Start-Process -FilePath '<path to devenv.exe>' -ArgumentList '<path to the target .sln or .slnx>'
```

Use `/RootSuffix Exp` when the extension under test is only installed in the experimental Visual Studio instance:

```powershell
Start-Process -FilePath '<path to devenv.exe>' -ArgumentList '/RootSuffix Exp <path to the target .sln or .slnx>'
```

Then wait briefly and call `vs_list_sessions()` again.

4. If Visual Studio opens but no session registers (with either approach), ask the user to confirm that the NetVsMcp VSIX is installed in that Visual Studio profile and that the broker is running.

## Broad Context In One Call: `vs_context_snapshot`

```json
vs_context_snapshot({ "sessionId": "..." })
```

Returns a single compact snapshot combining session status, solution info, the active document, the current editor selection, debugger status, build status, up to 50 errors/warnings, and pending safe edits. Prefer this over separately calling `get_status`, `solution_info`, `document_active`, `selection_get`, `debug_status`, `build_status`, `errors_list`, and `edit_list_pending` when a broad picture of the routed session is needed before deciding what to do next.

## Windows And Tool Windows

```json
window_list({ "sessionId": "..." })
window_activate({ "caption": "Solution Explorer", "sessionId": "..." })
toolwindow_show({ "objectKind": "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}", "sessionId": "..." })
toolwindow_hide({ "caption": "Output", "sessionId": "..." })
```

Key behavior:

- `window_list` returns every window's caption, kind, object kind GUID, and whether it is active/visible.
- `window_activate`, `toolwindow_show`, and `toolwindow_hide` all accept `caption` and `objectKind`, and require at least one of the two to be set. `caption` matches by window title (for example `"Solution Explorer"`); `objectKind` matches by the tool window's GUID when the caption is ambiguous or localized.

## Running Arbitrary Visual Studio Commands

```json
execute_command({ "commandName": "Build.RebuildSolution", "arguments": null, "sessionId": "..." })
```

`commandName` is required (a DTE command name such as `File.SaveAll` or `Edit.Format`); `arguments` is an optional command argument string. Prefer a dedicated tool (`build_solution`, `format_and_organize`, etc.) when one exists; fall back to `execute_command` for commands NetVsMcp does not otherwise expose.

## Solution Management

```json
solution_open({ "path": "D:\\Work\\App\\App.sln" })
solution_info({ "sessionId": "..." })
solution_overview({ "sessionId": "..." })
solution_close({ "sessionId": "..." })
```

Key behavior:

- `solution_open` requires `path` (validated to be non-empty); it also accepts the usual routing fields to pick which existing session should open it.
- `solution_info` returns `name`, `path`, `isOpen`, `projectCount`, and `startupProject`.
- `solution_overview` is a convenience combo call: it internally calls `solution_info`, `project_list`, and `startup_project_get`, then also returns `testProjects` — the subset of projects whose name, unique name, or full name contains `"Test"` or `".Tests"` (a heuristic, not a guarantee every returned project is actually a test project, nor that every real test project is included).
- `solution_close` closes the currently open solution in the routed session and returns the resulting (now-closed) `SolutionInfoResult`.

```json
solution_add_project({ "projectPath": "D:\\Work\\App\\App.Tests\\App.Tests.csproj", "sessionId": "..." })
solution_remove_project({ "projectName": "App.Tests", "sessionId": "..." })
```

`solution_add_project` requires `projectPath` to be a non-empty path to an existing project file. `solution_remove_project` requires `projectName` (matched the same way as `project_info`'s `projectName`, see below) and only removes the project from the solution; it does not delete files from disk.

## Project Management

```json
project_list({ "sessionId": "..." })
project_info({ "projectName": "App.Core", "sessionId": "..." })
startup_project_get({ "sessionId": "..." })
startup_project_set({ "projectName": "App.Web", "sessionId": "..." })
```

- `project_list` returns every project's `name`, `uniqueName`, `fullName`, `kind`, `isLoaded`, `language`, and `outputFileName`.
- `project_info` and `startup_project_set` both require a non-empty `projectName`; project name matching is resolved on the VSIX side, so prefer the display name shown in Solution Explorer, falling back to the unique name from `project_list` if ambiguous.
- `startup_project_get` returns `projects` (the list of current startup project names) and `isMultiStartup` (true when the solution has multiple startup projects configured).

```json
project_add_file({ "projectName": "App.Core", "filePath": "D:\\Work\\App\\App.Core\\NewFile.cs", "sessionId": "..." })
project_remove_file({ "projectName": "App.Core", "filePath": "D:\\Work\\App\\App.Core\\OldFile.cs", "sessionId": "..." })
```

Both require non-empty `projectName` and `filePath`. `filePath` must be an existing file on disk for `project_add_file`. `project_remove_file` only removes the project item; it does not delete the file from disk.

```json
project_add_reference({
  "projectName": "App.Web",
  "reference": "D:\\Work\\App\\App.Core\\App.Core.csproj",
  "referenceType": "project",
  "sessionId": "..."
})
project_add_reference({
  "projectName": "App.Web",
  "reference": "System.Net.Http",
  "referenceType": "assembly",
  "hintPath": null,
  "sessionId": "..."
})
project_remove_reference({ "projectName": "App.Web", "reference": "System.Net.Http", "referenceType": "assembly", "sessionId": "..." })
```

Key behavior:

- `referenceType` must be `"assembly"` (the default) or `"project"`; any other value fails with "Reference type must be 'assembly' or 'project'." When `"project"`, `reference` should be a project path or name resolvable to a `ProjectReference`; when `"assembly"`, `reference` is the assembly name and `hintPath` is an optional path written into the project file's `HintPath` element.
- Both tools require non-empty `projectName` and `reference`.

## Tests

```json
test_discover({ "projectName": "App.Tests", "sessionId": "..." })
test_run({ "projectName": "App.Tests", "filter": "FullyQualifiedName~OrderTests", "sessionId": "..." })
test_debug({ "projectName": "App.Tests", "filter": "FullyQualifiedName~OrderTests", "attachTimeoutSeconds": 30, "noBuild": true, "configuration": "Debug", "framework": "net10.0", "sessionId": "..." })
test_results({ "runId": null, "sessionId": "..." })
test_run_and_get_results({ "projectName": "App.Tests", "filter": null, "runId": null, "sessionId": "..." })
```

Key behavior:

- `projectName` is optional on `test_discover` and `test_run`; omit it to target the whole solution.
- `filter` on `test_run` is an optional test-filter expression passed through to the VSIX test backend (for example a `FullyQualifiedName~` substring filter).
- `test_debug` requires a non-empty `filter` so it does not accidentally launch every test under the debugger. It starts `dotnet test` with `VSTEST_HOST_DEBUG=1`, waits for the test host, attaches Visual Studio to that process, and accepts optional `noBuild`, `configuration`, and `framework` values passed through to `dotnet test`. The result includes the attached test host (`TestHostProcessId`, `TestHostProcessName`), the launched runner (`TestRunnerProcessId`, `TestRunnerProcessName`), and launch diagnostics (`CommandLine`, `WorkingDirectory`, `TargetPath`, `AttachTimeoutSeconds`). After it attaches, use the normal debugger tools (`debug_status`, `debug_wait_for_break`, `process_list_debugged`, `process_detach`) to inspect or clean up.
- `test_results` accepts an optional `runId` to fetch a specific prior run; omit it for the most recent run.
- Every test tool returns a `TestOperationResult` with a `supported` flag, a `message`, `tests` (from discovery), and `results` (from a run). If `supported` is `false`, the active Visual Studio test backend does not support the requested operation; report that instead of retrying.
- `test_run_and_get_results` is a convenience combo tool: it calls `test_run` and then `test_results` in sequence and returns both (`run` and `results`) in one round trip. Prefer it over separately calling `test_run` then `test_results` unless the caller specifically needs to inspect the run before fetching results.

## Reporting

Report findings with evidence:

- Which session (`sessionId`, solution name/path) the routed calls resolved to, and how (explicit `sessionId`, active window, or the only session).
- Whether Visual Studio had to be launched, and with what solution/edition/experimental flag.
- Key solution/project/test state: startup project, project list, test discovery/run results, and whether the test backend reported `supported: false`.

## Troubleshooting

- Broker unavailable: ask the user to start the NetVsMcp Broker and check `http://127.0.0.1:5050/health`.
- No VS sessions (`NoRegisteredSessions`): infer or confirm the target solution, launch Visual Studio with `vs_launch_instance` (or manually), then recheck `vs_list_sessions`.
- Ambiguous routing (`Ambiguous`): call `vs_list_sessions` and retry with `sessionId`, `processId`, `solutionPath`, `workspacePath`, or `solutionName`.
- Session found but stale: `vs_get_session`/`vs_get_status` report health as `Stale` when a session has not been seen recently; treat it as unusable until it reconnects.
- Wrong session targeted: prefer `sessionId` explicitly; `solutionName` and `workspacePath`/`rootPath` matches can be ambiguous across multiple open windows of similarly named solutions.
- Test backend unsupported: report `supported: false` from `TestOperationResult` and fall back to `build_and_get_errors`/`execute_command` with a Test Explorer command, or ask the user how they prefer to run tests.
- Tool not found at all: check the tool's `McpEndpointPath` from `get_help`/`vs_get_capabilities` — `ui_*`/`web_*` tools are only served from `/mcp-wu`, a separate opt-in MCP connection (see the automate-visual-studio guide), not the default `/mcp` endpoint.
- `error_code: "operation_timed_out"`: every routed call has a broker-side ceiling (5 minutes by default) independent of any tool-specific `timeoutMilliseconds` parameter — this fires when the routed Visual Studio session itself never responded (e.g. a stuck COM call on the VSIX side), not when the requested operation legitimately finished slowly. Report it distinctly from `rpc_failure`; retrying immediately rarely helps if the same underlying VS state caused it.
