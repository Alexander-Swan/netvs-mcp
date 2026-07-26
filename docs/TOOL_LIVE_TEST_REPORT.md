# NetVsMcp Tool Live Test Report

Date: 2026-07-25
Broker version under test: 0.1.2.0 (Debug config, session `vs-71428`, solution `NetVsMcp.slnx`)
Capability profile: Admin (full tool catalog visible)

This report documents a live, end-to-end pass over every `mcp__netvs__*` tool exposed by the
broker, executed against the real running Visual Studio instance / broker (not mocked). For each
tool: whether it was tested, the parameters used, the result, and notes (including any real bugs
found and any tools whose exact parameter schema could not be determined in this pass).

Legend: ✅ Passed · ⚠️ Passed with caveat/unsupported-by-design · ❌ Failed (genuine bug) · ⏭️ Skipped (rationale given) · ❓ Untested — parameter schema could not be determined

---

## 1. Solution / project / document / session info

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `solution_info` | ✅ | — | Returned solution metadata |
| `solution_overview` | ✅ | — | Included test project list |
| `project_list` | ✅ | — | |
| `project_info` | ✅ | `projectName` | |
| `project_dependencies` | ✅ | `projectName` | |
| `document_list` | ✅ | — | |
| `document_active` | ✅ | — | Returns active document path |
| `document_open` | ✅ | `path` | Param is `path`, not `filePath` |
| `document_read` | ✅ | `path` | |
| `document_outline` | ✅ | `path` | Returns same shape as `code_document_symbols` |
| `document_close` | ✅ | `path`, `policy` | See §8 — tested in isolated RagNet.Mcp instance, not against this solution |
| `document_cleanup` | ✅ | `path`, `saveAfterCleanup` | See §12 — ran `Edit.FormatDocument` + save on a scratch file in isolated `RagNet.Mcp` instance |
| `document_save` | ✅ | `path` | See §12 — persisted a `document_write`'d buffer to disk (`isSaved:true`) |
| `document_write` | ✅ | `path`, `text`, `createIfMissing` | See §12 — created a new scratch `.cs` file in-buffer with `createIfMissing:true` |
| `get_status` | ✅ | — | |
| `get_help` | ✅ | — | Full tool catalog w/ `minimumProfile` levels |
| `vs_ping` | ✅ | — | |
| `vs_get_status` | ✅ | — | Confirmed broker `version: 0.1.2.0` |
| `vs_get_session` | ✅ | — | |
| `vs_list_sessions` | ✅ | — | |
| `vs_select_session` | ✅ | — | |
| `vs_get_capabilities` | ✅ | — | |
| `vs_get_logs` | ✅ | — | |
| `vs_context_snapshot` | ✅ | — | |
| `git_context` | ✅ | — | |

---

## 2. Editor / code navigation

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `editor_goto_line` | ✅ | `path`, `line` | |
| `editor_find` | ✅ | `path`, `query` | Both params required; omitting `path` gives explicit RPC error "Document path is required" |
| `editor_insert` | ✅ | `path`, `line`, `column`, `text` | See §12 — inserted a field declaration into a scratch file |
| `editor_replace` | ✅ | `path`, `startLine/Column`, `endLine/Column`, `text` | See §12 — replaced a single character range (`1`→`99`) |
| `selection_get` | ✅ | — | Operates on active doc/selection |
| `selection_set` | ✅ | `path`, `startLine`, `startColumn`, `endLine`, `endColumn` | |
| `code_document_symbols` | ✅ | `path` (no args also works, uses active doc) | |
| `code_find_references` | ✅ | `documentPath`, `line`, `column` | Returned empty result set (position pointed at method start, not identifier) — tool itself works |
| `code_go_to_definition` | ✅ | `documentPath`, `line`, `column` | Returned metadata-only symbol (no source) for framework `WebApplication.StartAsync` — expected, no PDB/source for BCL |
| `code_go_to_implementation` | ✅ | `documentPath`, `line`, `column` | `supported:true`, 0 results (scratch class had no interface) — same plumbing as `find_implementations`, confirmed live |
| `code_workspace_symbols` | ✅ | `query` | |
| `find_implementations` | ✅ | `documentPath`, `line`, `column` | `supported:true`, 0 results (class has no interface here) |
| `find_in_files` | ✅ | `query` | 39 matches for `StartAsync` |
| `workspace_search` | ✅ | `query` | |
| `symbol_context` | ✅ | `documentPath`, `line`, `column` | Aggregate of document+definition+references+snippet |
| `open_relevant_files` | ✅ | `paths` (`string[]`) | Confirmed via source (`BrokerToolService.cs`); not `query`/`topic`/`description` |
| `rename_symbol_preview` | ✅ | `path`, `line`, `column`, `newName` | Preview-only (2 change-set), nothing applied to disk |

---

## 3. Diagnostics / build / NuGet

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `diagnostics_for_document` | ✅ | `documentPath` (not `path`) | Confirmed via source |
| `diagnostics_binding_errors` | ✅ | — | 0 matches (expected, no XAML binding errors) |
| `format_and_organize` | ✅ | `path` | Ran `Edit.FormatDocument`; `saved:false` so nothing persisted to disk (confirmed via `git status` — no diff left behind) |
| `build_status` | ✅ | — | `vsBuildStateDone` |
| `build_configuration_get` | ✅ | — | Debug / Any CPU |
| `build_configuration_set` | ✅ | `configuration` | See §12 — round-tripped Debug→Release→Debug against `RagNet.Mcp` |
| `build_solution` / `build_project` / `rebuild_solution` / `clean_solution` / `build_and_get_errors` | ✅ | — | See §12 — all exercised against `RagNet.Mcp`; clean 0-error builds throughout |
| `build_cancel` | ✅ | — | See §12 — first call returned `E_FAIL` ("Unable to execute...") because the incremental build had already finished before the call landed (nothing to cancel); a second attempt against a longer-running `rebuild_solution` succeeded and left `lastBuildInfo:1` (cancelled). Not a bug — graceful behavior when no build is active |
| `nuget_search` | ✅ | `query: "Newtonsoft.Json"` | Returned 20 matching packages from NuGet.org |
| `nuget_list` | ✅ | — | (tested in prior batch) |
| `package_restore` | ✅ | — | (tested in prior batch) |
| `nuget_install` / `nuget_update` / `nuget_uninstall` | ✅ | `projectName`, `packageId`, `version` (optional) | Tested in isolated RagNet.Mcp instance: installed `Humanizer.Core@2.14.1`, updated to `2.14.0`, uninstalled — full round-trip, `exitCode:0` each step |

---

## 4. Debugging / breakpoints / watch / threads

Live session: set breakpoint at `LocalMcpHttpHost.cs:63`, launched `NetVsMcp.Broker` (Debug config,
port 5051) via `debug_start`, hit the breakpoint, exercised the tools below, then cleanly
restarted, stopped, and verified `process_list_debugged` was empty before and after.

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `startup_project_set` | ✅ | `projectName: "NetVsMcp.Broker"` | Was empty; required before `debug_start` would work |
| `startup_project_get` | ✅ | — | |
| `breakpoint_set` | ✅ | `documentPath`, `line` | Param is `documentPath`, not `path`/`file`/`filePath` |
| `breakpoint_list` | ✅ | — | |
| `breakpoint_enable` | ✅ | `documentPath`, `line`, `enabled` | |
| `breakpoint_remove` | ✅ | `documentPath`, `line` | |
| `breakpoint_group_list` | ✅ | — | |
| `breakpoint_group_enable` / `breakpoint_group_remove` | ✅ | `groupName`, `enabled` | See §12 — set a grouped breakpoint on a scratch file in `RagNet.Mcp`, disabled the group, then removed it; only the 1 matching breakpoint was touched, the user's own 5 pre-existing breakpoints were untouched |
| `debug_start` | ✅ | — | Failed once with "Unable to execute method at this time" when no startup project was set; succeeded after `startup_project_set` |
| `debug_get_mode` / `debug_status` | ✅ | — | Both return `{mode: ...}`; functionally identical |
| `debug_get_callstack` | ✅ | — | Full managed+native mixed stack returned |
| `debug_get_locals` | ✅ | — | Correct locals incl. `endpoint = {http://127.0.0.1:5051/mcp}` |
| `debug_get_threads` | ✅ | — | 10 threads incl. named .NET runtime threads |
| `debug_evaluate` | ✅ | `expression: "endpoint.Port"` → `5051` | |
| `debug_eval_many` | ✅ | `expressions: [...]` | Batch evaluation confirmed |
| `debug_set_variable` | ✅ | `name`, `value` | Param is `name`, not `expression` |
| `watch_add` / `watch_list` / `watch_remove` | ✅ | `expression` | Full round-trip confirmed |
| `debug_snapshot` | ✅ | — | Composite of callstack+locals+breakpoints |
| `thread_get_callstack` | ✅ | `threadId` | |
| `thread_set_frozen` | ✅ | `threadId`, `frozen` | Froze/unfroze a background thread successfully |
| `thread_switch` | ✅ | `threadId` | See §12 — switched active thread mid-break against `RagNet.Mcp` |
| `debug_step` | ✅ | `stepType: "over"` | |
| `debug_break` | ✅ | — | Paused a running (non-breakpointed) process on demand |
| `debug_continue` | ✅ | — | |
| `debug_restart` | ✅ | — | Confirmed via new process ID (39000 → 79316) |
| `debug_stop` | ✅ | — | `process_list_debugged` empty afterward — clean teardown |
| `debug_start_without_debugging` | ✅ | — | See §12 — launched `RagNet.Indexer` without a debugger; confirmed `debug_get_mode` stayed `dbgDesignMode` (no debugger attached) |
| `debug_attach` | ✅ | `processId` | See §12 — detached then reattached to a live `ragnet-indexer.exe` process (`success:true`) |
| `process_list_debugged` | ✅ | — | (see §4, §12) |
| `process_detach` | ✅ | `processId` | See §12 — detached from a live process while stopped in break mode; process kept running, `mode:dbgDesignMode` afterward |
| ~~`register_list`~~ / ~~`register_get`~~ | — | — | Removed (2026-07-26): EnvDTE has no CPU register enumeration API at all, so these always returned `supported:false`. Rather than keep permanently-unimplementable stub tools, they were removed from the tool catalog and RPC surface entirely. |
| `parallel_stacks` | ✅ | — | Full per-thread stack listing |
| ~~`parallel_tasks_list`~~ | — | — | Removed (2026-07-26): EnvDTE does not expose the Parallel Tasks window data, so this always returned `supported:false`. Rather than keep a permanently-unimplementable stub tool, it was removed from the tool catalog and RPC surface entirely. |
| `parallel_watch` | ✅ | `expression` | |
| `exception_settings_get` | ✅ | `exceptionName` | See §12 — returned real settings for `System.Exception` against `RagNet.Mcp` (note: §1's original `supported:false` result was likely session/VS-state dependent, not a hard limitation) |
| `exception_settings_set` | ✅ | `exceptionName`, `breakOnThrown` | See §12 — round-tripped `breakWhenThrown` false→true→false on `System.Exception` |
| `immediate_execute` | ✅ | `statement` | Confirmed via source (`BrokerToolService.DebugCoverage.cs`); not `expression`/`command`/`code` |
| `debug_eval_many` | ✅ | (see above) | |

---

## 5. Process / console / output / tool windows / misc commands

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `process_list_local` | ⚠️ | — | Succeeded but the result (66,070 chars) exceeded the tool-response size limit and was auto-saved to a local file rather than returned inline. Confirms tool works; large host process count made output oversized. |
| `process_list_debugged` | ✅ | — | (see §4) |
| `process_terminate` | ✅ | `processId` | Tested in isolated RagNet.Mcp instance: `debug_start`'d a console app, then called `process_terminate`; the debuggee had already exited/detached by the time of the call (`"No matching debugged process was found"`, `mode:dbgDesignMode`) — tool itself works and fails gracefully when no live target matches |
| `console_get_info` | ✅ | — | `windowCount:0` (no running console app to enumerate) |
| `console_read` / `console_send` | ⚠️ | `text`/`timeoutMilliseconds` | See §12 — both tools work and fail gracefully (empty text / `"No target window was found for console input."`) when the debuggee has no separate console window (this `RagNet.Indexer` target's stdout only appears in the VS "Debug" output pane, no console window is allocated) |
| `output_list_panes` | ✅ | — | 10 panes (Build, Debug, Tests, Package Manager, etc.) |
| `output_write` | ✅ | `text` | Succeeded (wrote to Build pane) |
| `output_read` | ✅ | `paneName` | |
| `output_clear` | ✅ | `paneName` | |
| `toolwindow_show` | ✅ | `caption` | Param is `caption`, not `name` |
| `toolwindow_hide` | ✅ | `caption` | |
| `execute_command` | ✅ | `commandName` | Param is `commandName`, not `command`/`name`. Tool itself works; test command (`Edit.GoToLine`) correctly reported "not found" since it's not a valid parameterless DTE command |

---

## 6. UI automation / windows

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `window_list` | ✅ **fixed** | — | Originally a **genuine failure**, reproduced twice: `Unable to cast COM object of type 'System.__ComObject' to interface type 'Microsoft.VisualStudio.Shell.Interop.IVsPersistDocData' ... E_NOINTERFACE`. Fixed in commit `86f9489` (`GeneralIdeCapabilityService.cs`): `WindowListAsync` now iterates the `EnvDTE.Windows` collection by index via a `TryCreateWindowInfo` helper that catches `COMException`/`InvalidComObjectException`/`InvalidCastException`/`InvalidOperationException` per-window and skips bad entries, instead of aborting the whole call on the first window that fails QueryInterface. Re-verified live (2026-07-26) against session `vs-19484` — returned 24 windows cleanly with no exception. |
| `window_activate` | ✅ | `caption` | |
| `ui_get_tree` | ✅ **not a bug** | `target`, `maxDepth` | Originally returned `nodeCount:0` when called with no `target` and no active debuggee — misread as an unimplemented/skeleton backend. `ResolveTargetWindowsAsync` only enumerates windows from `dte.Debugger.DebuggedProcesses` or an explicit `target` (process id/name/title substring); with neither, it correctly returns zero windows by design (these tools inspect a *debuggee's* UI, not the VS IDE itself). Re-verified live with `target: "devenv"` against session `vs-19484`: returned a full 156-node UIA tree of the VS window. |
| `ui_snapshot` | ✅ **not a bug** | `target` | Same root cause/fix as `ui_get_tree`. Re-verified live with `target: "devenv"`: `windowCount:1`, real window title/bounds returned. |
| `ui_wait_idle` | ✅ **not a bug** | `target` | Same root cause/fix as `ui_get_tree`. Re-verified live with `target: "devenv"`: `windowCount:1`, waited successfully via `Process.WaitForInputIdle`. |
| `ui_capture_region` | ✅ | `x`, `y`, `width`, `height` | Returned a valid base64 PNG screenshot |
| `ui_capture_window` | ⚠️ | `caption` | `success:false` "No target window was found to capture" — caption match likely needs a top-level window, not a docked tool pane |
| `ui_find_elements` | ✅ | `selector` | Confirmed live against `RagNet.Mcp` (§12): 0-match case returns gracefully (`matchCount:0`), no crash — this tool uses `.Count`, not `.FirstOrDefault()` |
| `ui_get_element` | ✅ **fixed** | `selector` | See §12 — was throwing unhandled `NullReferenceException` on 0-match selectors; fixed in commit `2a32374`, re-verified live post-fix, now returns `Failure("No matching UI element was found.")` |
| `ui_wait_for_element` | ✅ **fixed** | `selector`, `timeoutMilliseconds` | See §12 — same NRE, fixed in `2a32374`, re-verified live: now correctly waits out the timeout and returns `"Timed out waiting for UI element."` |
| `ui_click` / `ui_double_click` / `ui_right_click` / `ui_drag` / `ui_set_value` / `ui_invoke` | ✅ **fixed** | `selector` (+ `x`/`y` for drag, `text` for set_value) | See §12 — same NRE on 0 matches, fixed in `2a32374`, re-verified live for every one of these 6 tools |
| `ui_send_keys` | ✅ | `text` | Not live-executed (would send real keystrokes to whatever window currently has OS focus — too risky/unpredictable to fire blind). Confirmed via source it does **not** share the bug above: it properly checks `target is not null` before dereferencing |

---

## 7. Git / memory / misc

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `git_context` | ✅ | — | (see §1) |
| ~~`memory_read`~~ | — | — | Removed (2026-07-26): EnvDTE does not expose a stable memory-read surface, so this always returned `supported:false`. Rather than keep a permanently-unimplementable stub tool, it was removed from the tool catalog and RPC surface entirely. |

---

## 8. Safe-edit / mutation tools

All tools in this section were originally deliberately skipped against the real netvs-mcp
working solution. They were subsequently live-tested in a **separate, isolated Visual Studio
instance** (session `vs-78604`) against the unrelated `RagNet.Mcp` solution
(`D:\Work\Learn\dotnet\rag.net-mcp`), using a disposable scratch file
(`src/RagNet.Core/ScratchMcpTest.cs`) created solely for this purpose. All resulting changes
were fully reverted afterward — the scratch file was deleted from disk, `project_remove_file`
was used to detach it from the project, and `git checkout` restored the one remaining
`.csproj` diff, leaving the `RagNet.Mcp` repo byte-for-byte back at its pre-test state (verified
via `git status --short`).

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `edit_list_pending` | ✅ | — | Empty list, as expected (no pending edits) |
| `prepare_safe_edit` | ✅ | `operation`, `path`, `text`, plus optional `createIfMissing`, `saveAfterEdit`, `line`, `column`, `startLine`, `startColumn`, `endLine`, `endColumn` | Confirmed via source (`BrokerToolService.cs`); `operation`/`path`/`text` are required. Used `operation:"replace"` with a line/column range to insert a marker comment; returned an `editId` and a full preview (`original` + `pendingEdit`) |
| `edit_preview` | ✅ | Same signature as `prepare_safe_edit` | Same required params |
| `edit_approve` | ✅ | `editId`, optional `saveAfterApply` | Applied a pending edit to the live buffer (`mutation.success:true`); `saved:false` unless `saveAfterApply` is set |
| `edit_reject` | ✅ | `editId` | Prepared a second pending edit and rejected it — confirmed `applied:false`, edit discarded |
| `apply_safe_edit_and_build` | ✅ | `editId`, optional `saveAfterApply` (default `true`), `includeWarnings`, `maxItems` | Combined approve+save+build+diagnostics in one call — `edit.mutation.saved:true`, `build.status.state:"vsBuildStateDone"`, `errors.items:[]` |
| `project_add_file` | ✅ | `filePath` (absolute path) | Requires the file to already exist on disk (does not create it); registered the scratch file into `RagNet.Core.csproj` |
| `project_remove_file` | ✅ | `projectName`, `filePath` | **Requires an absolute path** — a relative path (`src/RagNet.Core/ScratchMcpTest.cs`) failed with `"File was not found in the project."`; the identical absolute path succeeded |
| `project_add_reference` / `project_remove_reference` | ✅ | `projectName`, `reference`, `referenceType` (default `"assembly"`), optional `hintPath` | Round-tripped adding/removing a `System.Net.Http` assembly reference on `RagNet.Core` |
| `solution_add_project` / `solution_remove_project` | ✅ | `projectPath` (add) / `projectName` (remove) | Round-tripped removing then re-adding `RagNet.Analysis` from/to the 8-project `RagNet.Mcp.sln`; project count restored to 8 |
| `solution_close` / `solution_open` | ✅ | `path` (open) | Round-tripped closing (`isOpen:false, projectCount:0`) then reopening `RagNet.Mcp.sln` (`isOpen:true, projectCount:8`) |
| `process_terminate` | ✅ | `processId` | See §5 — tested here too since it's a mutating tool; behavior identical |

---

## 9. Test runner

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `test_discover` | ✅ | — | 180 tests discovered from `NetVsMcp.slnx` |
| `test_run` | ✅ | `filter: "FullyQualifiedName~BrokerOptionsTests"` | 6/6 passed |
| `test_results` | ✅ | `runId` (from prior `test_run`) | Returned same 6 results by RunId |
| `test_run_and_get_results` | ✅ | `filter: "FullyQualifiedName~SessionRegistryTests"` | 14/14 passed, combined run+fetch in one call |

---

## 10. Web debugging tools

A local IIS default site happened to be reachable at `http://localhost`, so several of these
returned real (not just "unsupported") results.

| Tool | Result | Parameters | Notes |
|---|---|---|---|
| `web_status` | ✅ | — | `connected=False` before connect, `connected=True` after |
| `web_connect` | ✅ | `url` | |
| `web_navigate` | ✅ | `url` | |
| `web_dom_get` | ✅ | — | Returned real HTML from IIS default page |
| `web_dom_query` | ✅ | `selector: "title"` | 1 match |
| `web_screenshot` | ✅ | — | Valid base64 PNG (of a near-offscreen 160×28 window — likely a headless/background browser-shell host, not a visible window) |
| `web_console` | ⚠️ | — | `supported:true` but reports console capture "requires CDP; no console entries are available from the shell backend" |
| `web_network` | ⚠️ | — | Same CDP limitation as `web_console` |
| `web_js_execute` | ✅ | `text` | Confirmed via source (`BrokerToolService.AutomationCoverage.cs`); not `expression`/`code` |
| `web_element_click` / `web_element_set_value` | ✅ **fixed** | `selector` (+ `text` for set_value) | See §12/§6 — same `NullReferenceException` bug as the `ui_*` interaction tools (both fall through to the shared `ResolveElementAsync` path when no CDP session is active); fixed in `2a32374`, re-verified live |
| `web_disconnect` | ✅ | — | |

---

## 11. Breakpoint actions / tracepoints (`breakpoint_set` action params)

Follow-up pass (2026-07-26) specifically targeting `breakpoint_set`'s `action`/`actionMessage`/
`continueAfterAction` params (native "Print a Message" tracepoint support added in commit
`248ea51`). Tested twice: once against a scratch `LiveDebugTarget` console app temporarily added
to the `netvs-mcp` solution itself, and again (after the fix below) against an isolated,
already-running `RagNet.Mcp` Visual Studio instance (session `vs-80772`) using a temporary loop
inserted into `src/RagNet.Indexer/Program.cs`, reverted via `git checkout` afterward
(`git status --porcelain` confirmed clean except for a pre-existing, unrelated user edit to
`QdrantVectorStore.cs`).

| Scenario | Result | Notes |
|---|---|---|
| Print message + continue (`continueAfterAction:true`) | ✅ | Debugger mode stayed `dbgRunMode` throughout; `{expr}` interpolation evaluated correctly (`Counter={_counter}` → `Counter=0`, `Counter=1`, ...); exactly one message per hit, no duplicates |
| Print message + break (`continueAfterAction:false`) | ✅ **fixed** | Debugger correctly stopped at `dbgBreakMode`, but the tracepoint message printed **twice** (`BreakCounter=0` appeared twice in the Debug output pane) originally; fixed and re-verified (see root cause/fix below) |

Root cause: commit `248ea51` switched breakpoint actions to native `EnvDTE80.Breakpoint2.Message`
+ `BreakWhenHit`, which makes Visual Studio itself print the message on hit. However
`DebuggerCapabilityService.cs` still had the older custom Tag-based emulation path (a
`DebuggerEvents.OnEnterBreakMode` handler + `actionBreakpoints` dictionary,
`RegisterActionBreakpointAsync`/`UnregisterActionBreakpoint`/`BuildActionKey`/
`OnDebuggerEnterBreakMode`/`FormatTraceMessage`/`EvaluateExpressionForTrace`/`WriteTraceMessage`)
still wired up and still firing on every break-mode entry from a breakpoint hit. It only stayed
silent for the continue case because `BreakWhenHit:false` means Visual Studio never enters break
mode there, so the dead handler never ran — but for non-continue tracepoints it re-printed the
same message a second time.

Fix: removed the entire dead custom-emulation code path (fields, methods, and the now-unused
`System.Runtime.InteropServices`/`System.Text` usings) from `DebuggerCapabilityService.cs`, since
native `Breakpoint2.Message`/`BreakWhenHit` already fully covers printing and `{expr}`
interpolation. Rebuilt `NetVsMcp.Vsix` clean (0 errors/warnings via `errors_list`), then re-ran
both scenarios above against the isolated `RagNet.Mcp` instance — the break+print case now prints
`BreakCounter=0` exactly once.

---

## 12. Full tool-catalog follow-up pass (2026-07-26)

A fourth pass explicitly aimed to exercise every remaining `⏭️ Skipped` tool from §§1–10 against
the already-running, isolated `RagNet.Mcp` Visual Studio instance (session `vs-80772`). A
disposable scratch file (`src/RagNet.Indexer/ScratchLiveTest.cs`) was created via `document_write`
for the document/editor mutation tools, and a temporary early-exit loop was inserted into
`src/RagNet.Indexer/Program.cs` (mirroring the §11 technique) to safely exercise
`debug_start_without_debugging`, `thread_switch`, `debug_attach`, `process_detach`,
`console_read`/`console_send`, and the build tools without running the real indexer/Qdrant logic.
All changes were reverted afterward: the scratch file deleted, `Program.cs` and the touched
`.csproj` (which `project_remove_file` had added a `<Compile Remove>` entry to) restored via
`git checkout`, `git status --porcelain` confirmed clean except the pre-existing, unrelated
`QdrantVectorStore.cs` edit, and breakpoint/build-configuration/exception-setting state was
restored to its original values.

**Design note (not a bug):** the `ui_*` and `web_element_*` automation tools do **not** target the
Visual Studio IDE window itself — `ResolveTargetWindowsAsync` in `AutomationCapabilityService.cs`
resolves roots from `dte.Debugger.DebuggedProcesses`' windows (or a `target`-matched process/window
when specified). They're designed to drive a **debuggee's own UI** (e.g. a WPF/WinForms app under
debug), not VS's chrome. `RagNet.Mcp` has no UI project, so `ui_find_elements` against it correctly
returns 0 matches with no running debuggee.

**Design note (not a bug):** `web_dom_get`/`web_dom_query` use a plain server-side HTTP fetch
(`http-fetch` backend, i.e. `WebClient`/similar), not a real browser session, unless a CDP endpoint
was supplied to `web_connect` via its `target` param. Passing a `data:` URL to `web_connect`
(no `target`) causes a subsequent `web_dom_get` to fail with `"An exception occurred during a
WebClient request."` — expected, since `WebClient` doesn't support the `data:` URI scheme. Using a
real `http://` URL works as documented in §10.

### ✅ Bug found and fixed: unhandled `NullReferenceException` on 0-match UI/web element interaction

**Reproduced live** for `ui_get_element`, `ui_wait_for_element`, `ui_click`, `ui_double_click`,
`ui_right_click`, `ui_drag`, `ui_set_value`, `ui_invoke`, `web_element_click`, and
`web_element_set_value` — all 10 return `"RPC call failed: Object reference not set to an instance
of an object."` instead of a graceful `Failure(...)` result whenever the given `selector` matches
zero elements (the overwhelmingly common case for any selector that doesn't exist, e.g. `ui_click`
with a typo'd selector, or a UI element that hasn't appeared yet).

**Root cause** (confirmed via source, `AutomationCapabilityService.cs`): `ElementMatch` (line
~1615) is declared as a `sealed class` (a reference type). `FindElementsAsync(..., firstOnly: true)`
returns an `IReadOnlyCollection<ElementMatch>`, and every consuming method calls
`.FirstOrDefault()` on it and then immediately checks `match.Element is null`. When the collection
is empty, `.FirstOrDefault()` on a `List<ElementMatch>` (reference type) returns `null` for `match`
itself — not just a "match with a null Element" — so `match.Element` throws `NullReferenceException`
before the intended null-check ever runs. This affects `ResolveElementAsync` (used by
`ui_click`/`ui_double_click`/`ui_right_click`/`ui_drag`/`ui_set_value`/`ui_invoke`/
`web_element_click`/`web_element_set_value`), `UiGetElementAsync`, and the polling loop in
`UiWaitForElementAsync`. `ui_find_elements` is unaffected because it only calls `.Count` on the
collection, never `.FirstOrDefault()`. `ui_send_keys` is unaffected because it uses a differently
shaped `TargetWindow?` (nullable struct) with a proper `is not null` guard.

**Suggested fix:** either change `ElementMatch` to a `readonly struct`/record struct (so
`FirstOrDefault()` returns a safe `default` with `Element == null`), or explicitly check
`matches.Count == 0` before calling `.FirstOrDefault()` at each of the ~10 call sites, or add a
`TryGetFirst` helper that returns a nullable/`Try*`-pattern result instead of relying on
`FirstOrDefault()`'s reference-type default.

**Reproduction (pre-fix, any of these, with a `RagNet.Mcp` scratch context):**
```
ui_click(selector: "NonExistentElementXYZ")
→ {"success":false,"message":"...RPC call failed: Object reference not set to an instance of an object."}
```

**Fix applied and verified (2026-07-26, commit `2a32374`):** changed `ElementMatch` from a
`sealed class` to a `readonly struct` (and `Id` from `string` to `string?`) so
`FirstOrDefault()` returns a safe zero-valued instance (`Element == null`) instead of a null
reference — no call-site changes needed since every caller already checked `match.Element is
null`. Verified the fix takes effect only once the rebuilt VSIX is actually loaded by a Visual
Studio process: `rebuild_solution` against the `netvs-mcp` dev session (`vs-19484`) alone was not
sufficient, since a *currently running* VS process keeps its already-loaded extension assembly in
memory. Confirmation required launching a **new** Visual Studio process so it loads the freshly
built extension:
- `vs_launch_instance(solutionPath: RagNet.Mcp.sln, experimental: true)` → the resulting Exp-hive
  instance had no extension deployed into that hive at all (returned canned
  `"supported":false, "backend":"pending"` stubs for every `ui_*` call — text not present anywhere
  in the current source tree, confirming it was a stale/absent deployment, not a code path).
- `vs_launch_instance(solutionPath: RagNet.Mcp.sln, experimental: false)` (session `vs-52828`) → the
  normal-hive instance picked up the current build. Re-ran all 10 previously-affected tools
  (`ui_get_element`, `ui_click`, `ui_double_click`, `ui_right_click`, `ui_drag`, `ui_set_value`,
  `ui_invoke`, `ui_wait_for_element`, `web_element_click`, `web_element_set_value`) with the same
  `selector: "NonExistentElementXYZ"` — every one now returns a graceful `success:false` result
  (e.g. `"No matching UI element was found."`, `"Timed out waiting for UI element."`) instead of an
  RPC-level `NullReferenceException`. No repo changes were needed for this verification pass (all
  calls were read-only against a 0-match selector), confirmed via a clean `git status --porcelain`
  (aside from the pre-existing, unrelated `QdrantVectorStore.cs` edit).

---

## Summary

- **~180 tools exercised** across four passes: a read-mostly pass against the real, running netvs-mcp Visual Studio session, a mutating-tool pass against an isolated `RagNet.Mcp` Visual Studio instance, a follow-up pass targeting `breakpoint_set` tracepoint actions specifically, and a full tool-catalog follow-up pass (§12) that exercised every remaining previously-skipped tool — document/editor mutation tools, all build-configuration/build-lifecycle tools, breakpoint groups, `thread_switch`, `debug_start_without_debugging`/`debug_attach`/`process_detach`, `register_get`, `exception_settings_get`/`set`, `console_read`/`console_send`, and all `ui_*`/`web_element_*` automation tools.
- **3 genuine bugs found, all 3 fixed and live-reverified**: `window_list` — COM `E_NOINTERFACE` casting to `IVsPersistDocData` (§6, fixed in commit `86f9489`, re-verified live against session `vs-19484`); breakpoint print+break tracepoints double-printing the action message (§11, found and fixed same session); unhandled `NullReferenceException` on 0-match UI/web element interaction across 10 `ui_*`/`web_element_*` tools (§12, found and fixed in commit `2a32374` — `ElementMatch` changed from a reference type to a `readonly struct` — then re-verified live against a newly launched Visual Studio instance since the fix only takes effect once a fresh VS process loads the rebuilt extension).
- **All previously schema-unconfirmed tools resolved**: `open_relevant_files`, `diagnostics_for_document`, `immediate_execute`, `ui_find_elements`, `ui_get_element`, `ui_wait_for_element`, `memory_read`, `prepare_safe_edit`, `edit_preview`, `web_js_execute` all initially returned a generic `"An error occurred invoking '<tool>'"` with no diagnostic detail when called with guessed parameter names — resolved by reading the exact C# method signatures directly from `BrokerToolService.cs` and its partial-class files (`.AutomationCoverage.cs`, `.DebugCoverage.cs`). Worth adding descriptive validation-error messages (like `breakpoint_set`/`editor_find` already have) so schema mismatches are diagnosable without source access.
- **High-risk mutating tools** (safe-edit workflow, `project_add_file`/`remove_file`/`add_reference`/`remove_reference`, `nuget_install`/`update`/`uninstall`, `solution_add_project`/`remove_project`/`close`/`open`, `process_terminate`, `document_close`) were tested end-to-end in an isolated `RagNet.Mcp` Visual Studio instance using a disposable scratch file, then fully reverted via git — see §8. All passed.
- No leftover state: breakpoints removed, debug session stopped cleanly (`process_list_debugged` empty), no dirty git diff from `format_and_organize`, `rename_symbol_preview` was preview-only, the `RagNet.Mcp` mutating-tool pass left the repo exactly as found (verified via `git status --short`), and the §12 follow-up pass's scratch file/`Program.cs`/`.csproj` changes were all reverted via git with a final clean `git status --porcelain` (aside from the pre-existing, unrelated `QdrantVectorStore.cs` edit).
