# Debug Visual Studio With NetVsMcp

Agent-neutral entrypoint for launching, attaching, pausing, stepping, inspecting, hot reload, breakpoints, tracepoints, watches, threads, modules, processes, and test debugging through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling a matching debug tool, then only the references needed for the current operation.
- For learning or local skill creation, read references deliberately and keep generated skills as routers with their own references.
- Do not load every debug reference before a small operation such as setting one breakpoint.

## First Rules

- Check `debug_status` before inspection or stepping.
- `dbgDesignMode` means no active debuggee; use `debug_start` or `debug_attach`.
- `dbgRunMode` means the debuggee is running; break or wait before inspecting locals/call stack.
- `dbgBreakMode` means paused; inspection and stepping tools apply.
- Confirm before `debug_stop`, `process_terminate`, or broad breakpoint removal unless explicitly requested.
- Prefer `debug_snapshot` or `debug_wait_for_break` when they avoid manual continue/step plus repeated inspection calls.
- Keep locals opt-in; use watch expressions for targeted values.

## Reference Files

| Task | Read |
| --- | --- |
| Start, stop, continue, break, step, wait, or understand debugger states | `debug-visual-studio/references/session-control.md` |
| Debug a specific test | `debug-visual-studio/references/test-debugging.md` |
| Apply hot reload | `debug-visual-studio/references/hot-reload.md` |
| Set, disable, group, remove, or clean up breakpoints/tracepoints | `debug-visual-studio/references/breakpoints.md` |
| Inspect paused call stack, locals, variables, or expressions | `debug-visual-studio/references/inspection.md` |
| Use `debug_snapshot` or `debug_wait_for_break` | `debug-visual-studio/references/snapshots-waits.md` |
| Use watches or immediate execution | `debug-visual-studio/references/watches-immediate.md` |
| Attach, detach, list, or terminate processes, including remote transports | `debug-visual-studio/references/processes-attach.md` |
| Threads, parallel stacks, modules, or exception settings | `debug-visual-studio/references/advanced-state.md` |

## Covered Tools

`debug_*`, `breakpoint_*`, `watch_*`, `thread_*`, `process_*`, `module_list`, `exception_settings_*`, `parallel_*`, `immediate_execute`, and `test_debug`.
