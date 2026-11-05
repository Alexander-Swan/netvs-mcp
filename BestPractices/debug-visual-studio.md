# Debug Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Check `debug_status`: `dbgDesignMode` has no debuggee (start or attach); `dbgRunMode` is running (break or wait before inspection); `dbgBreakMode` is paused (inspect or step).
- For `dbgModalBlock`, use `dialog_respond` with returned `dialogId`/`buttonId` and the same session routing, choosing an enabled button within existing authorization. Then check `debug_status` with any returned `operationId`; never replay the original command. Refresh IDs with `dialog_list` after a timeout or rejected response.
- Confirm before `debug_stop`, `process_terminate`, or broad breakpoint removal unless explicitly requested.
- Prefer `debug_snapshot` or `debug_wait_for_break` over repeated control and inspection calls.
- Prefer stepping or moving a breakpoint to expose live values before manual evaluation. Keep locals opt-in; use watches for targeted values.
- Call stacks default to two frames; request `maxFrames` only for deeper callers.
- Frame-dependent tools accept optional `stackFrame` (1-based) and `threadId`; omit both for the selected frame. Explicit inspection preserves selection. See `references/inspection.md` for targeting and version requirements.
- Use `debug_select_frame` to change selection; refresh frame indices after stepping or switching threads.
- Use `module_list` in break mode to verify binaries and symbols; see `references/modules-symbols.md` for filters and engine limits.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Start, stop, continue, break, step, wait, or understand debugger states | `references/session-control.md` |
| Run to a source location with owned temporary breakpoint cleanup | `references/run-to-location.md` |
| Debug a specific test | `references/test-debugging.md` |
| Apply hot reload | `references/hot-reload.md` |
| Set, disable, group, remove, or clean up breakpoints/tracepoints | `references/breakpoints.md` |
| Inspect breakpoint binding, bound locations, and engine warning history | `references/breakpoint-diagnostics.md` |
| Verify loaded modules, symbol status, or symbol-search diagnostics | `references/modules-symbols.md` |
| Compare loaded source checksums with disk and unsaved editor text | `references/source-evidence.md` |
| Inspect paused call stack, locals, variables, or expressions | `references/inspection.md` |
| Expand an object with bounded children and function evaluation off | `references/inspection.md` |
| Keep stack, locals, evaluations, and watches on one pause/thread/frame | `references/inspection-context.md` |
| Read exception details tied to the current pause | `references/exception-evidence.md` |
| Use `debug_snapshot` or `debug_wait_for_break` | `references/snapshots-waits.md` |
| Read or wait for session events and reconcile a cursor | Guide `manage-visual-studio`, file `references/events-control.md` |
| Use watches or immediate execution | `references/watches-immediate.md` |
| Attach, detach, list, or terminate processes, including remote transports | `references/processes-attach.md` |
| Threads, parallel stacks, or exception settings | `references/advanced-state.md` |

## Covered Tools

`debug_*`, `breakpoint_*`, `watch_*`, `thread_*`, `process_*`, `exception_settings_*`, `parallel_*`, `module_list`, `immediate_execute`, and `test_debug`.
