# Debug Visual Studio With NetVsMcp

This is the agent-neutral debugging workflow for this repository. Any AI agent can follow it when asked to debug, inspect, launch, attach to, pause, step through, or diagnose the Visual Studio solution through NetVsMcp.

## Session Routing

Start with the NetVsMcp session tools when they are available:

```json
vs_list_sessions()
debug_status({ "sessionId": "..." })
```

Every routed debug call accepts optional `sessionId`, `solutionName`, and `solutionPath`. Resolution order is `sessionId`, normalized `solutionPath`, exact `solutionName`, active Visual Studio window, only registered instance. Use explicit routing whenever multiple Visual Studio windows are open.

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

## Opening Visual Studio When No Session Exists

If `vs_list_sessions()` returns an empty list, actively open Visual Studio instead of stopping at a request for the user to do it manually.

1. Determine the target solution.
   - Prefer a solution explicitly named by the user.
   - Otherwise prefer the current workspace solution when exactly one `.sln` or `.slnx` is present.
   - If multiple candidate solutions exist, ask the user which project or solution to open.

2. Launch Visual Studio with the solution.

```powershell
Start-Process -FilePath 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe' -ArgumentList '.\NetVsMcp.slnx' -WorkingDirectory (Resolve-Path '.').Path
```

Use `/RootSuffix Exp` when the VSIX is only installed in the experimental Visual Studio instance:

```powershell
Start-Process -FilePath 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe' -ArgumentList '/RootSuffix Exp .\NetVsMcp.slnx' -WorkingDirectory (Resolve-Path '.').Path
```

3. Wait briefly, then call `vs_list_sessions()` again. If Visual Studio opens but no session registers, ask the user to confirm that the NetVsMcp VSIX is installed in that Visual Studio profile and that the broker is running.

## Debugger States

- `dbgDesignMode`: no active debuggee; use `debug_start` or `debug_attach`.
- `dbgRunMode`: debuggee is running; use `debug_break` before locals, call stack, step, or expression inspection if needed.
- `dbgBreakMode`: debuggee is paused; use inspection and stepping tools.

## Core Debug Tools

- `vs_list_sessions()`
- `debug_status({ sessionId?, solutionName?, solutionPath? })`
- `debug_start({ ...route })`
- `debug_start_without_debugging({ ...route })`
- `debug_restart({ ...route })`
- `debug_break({ ...route })`
- `debug_continue({ ...route })`
- `debug_step({ stepKind: "Into" | "Over" | "Out", ...route })`
- `debug_stop({ ...route })`

Confirm before `debug_stop` unless the user explicitly asked to stop debugging.

## Breakpoints And Tracepoints

Set a simple breakpoint:

```json
breakpoint_set({
  "documentPath": "D:\\Work\\App\\Program.cs",
  "line": 42,
  "sessionId": "..."
})
```

Useful optional fields:

- `condition`: expression that must evaluate true.
- `actionMessage`: tracepoint message.
- `continueAfterAction`: set true for a nonbreaking tracepoint.
- `hitCount`: positive integer hit target.
- `hitCountType`: `equals`, `multiple`, or `greaterThanOrEqual`.
- `groupName`: label for cleanup or scenario grouping.

Inspect and clean up:

```json
breakpoint_list({ "sessionId": "..." })
breakpoint_remove({ "documentPath": "...", "line": 42, "sessionId": "..." })
breakpoint_enable({ "documentPath": "...", "line": 42, "enabled": false, "sessionId": "..." })
breakpoint_group_list({ "sessionId": "..." })
breakpoint_group_enable({ "groupName": "scenario-name", "enabled": false, "sessionId": "..." })
breakpoint_group_remove({ "groupName": "scenario-name", "sessionId": "..." })
```

Prefer grouping breakpoints created for one investigation. Confirm before broad removal unless the user asked for cleanup.

When disabling (`enabled: false`), both `breakpoint_enable` and `breakpoint_group_enable` also return the current debugger `state` in the response (like `debug_snapshot`), and accept an optional `continueExecution: true` (plus `settleTimeoutMilliseconds`, default 300) to resume the debuggee in the same call if it's paused. Enabling breakpoints does not fetch or return state.

When the debugging task is finished, deactivate or remove breakpoints created for the investigation, then continue execution if the debuggee is paused and the user has not asked to stop debugging. Prefer disabling grouped breakpoints with `breakpoint_group_enable({ "groupName": "...", "enabled": false, "continueExecution": true })` in one call, or removing the investigation group with `breakpoint_group_remove`; avoid changing unrelated user breakpoints.

## Inspecting Paused State

Use while `debug_status` reports `dbgBreakMode`:

```json
debug_get_callstack({ "sessionId": "..." })
debug_get_locals({ "sessionId": "..." })
debug_evaluate({ "expression": "customer.Id", "timeoutMilliseconds": 5000, "sessionId": "..." })
debug_eval_many({ "expressions": ["order.Total", "order.Items.Count"], "sessionId": "..." })
debug_set_variable({ "name": "retryCount", "value": "3", "sessionId": "..." })
```

Treat expression evaluation as code execution in the debuggee context. Avoid expressions with side effects unless the user explicitly wants state changed.

## Advancing And Inspecting In One Call: `debug_snapshot`

Prefer `debug_snapshot` over separately calling `debug_step`/`debug_continue`/`debug_break` followed by `debug_get_callstack`/`debug_get_locals`. It optionally advances the debugger, waits for it to settle, and returns state plus locals (and anything else requested) in a single round trip:

```json
debug_snapshot({ "action": "stepOver", "include": ["callStack"], "sessionId": "..." })
debug_snapshot({ "action": "continue", "include": ["callStack", "threads"], "sessionId": "..." })
debug_snapshot({ "action": "break", "sessionId": "..." })
debug_snapshot({ "sessionId": "..." })
```

Key behavior:

- `action` is optional. Omit it for a pure, non-mutating inspection of the current state (equivalent to `debug_status` plus the requested `include`). Set it to `stepInto`, `stepOver`, `stepOut`, `continue`, or `break` to advance the debugger first.
- When `action` is set, `debug_snapshot` polls debugger status every 50ms until it leaves `dbgRunMode` or `settleTimeoutMilliseconds` (default 300) elapses, then reports the settled state.
- Locals are always fetched best-effort once the debugger is paused; there is no way to opt out.
- `include` accepts any of `callStack`, `breakpoints`, `watch`, `threads`, `modules`, `parallelStacks`, `parallelWatch`. Omit `include` entirely to default to `callStack` only; pass `[]` to fetch none of the optional categories. Unknown keys are echoed back in `unrecognizedInclude` so a typo can be corrected.
- If the debugger is still running after the settle timeout, or the program has exited (`dbgDesignMode`), only `state` is populated — increase `settleTimeoutMilliseconds` or call `debug_snapshot` again once the program is actually paused.

`debug_step`, `debug_continue`, and `debug_break` still exist for simple fire-and-forget use when no follow-up inspection is needed.

## Watches And Immediate Evaluation

```json
watch_add({ "expression": "order.Total", "sessionId": "..." })
watch_list({ "sessionId": "..." })
watch_remove({ "expression": "order.Total", "sessionId": "..." })
immediate_execute({ "statement": "someExpression", "sessionId": "..." })
```

`immediate_execute` uses Visual Studio expression evaluation rather than sending keystrokes to the Immediate window. It only works while debugging.

## Attach And Process Control

Attach by ID when possible:

```json
process_list_local({ "sessionId": "..." })
debug_attach({ "processId": 12345, "sessionId": "..." })
process_list_debugged({ "sessionId": "..." })
process_detach({ "processId": 12345, "sessionId": "..." })
```

Use `process_terminate` only when the user explicitly wants the debugged process killed or has approved it.

## Threads, Parallel Stacks, And Modules

```json
debug_get_threads({ "sessionId": "..." })
thread_switch({ "threadId": 8, "sessionId": "..." })
thread_get_callstack({ "threadId": 8, "sessionId": "..." })
thread_set_frozen({ "threadId": 8, "frozen": true, "sessionId": "..." })
parallel_stacks({ "sessionId": "..." })
parallel_watch({ "sessionId": "..." })
module_list({ "sessionId": "..." })
```

Some debug engines do not expose thread freeze/thaw, module data, or stack frames through EnvDTE. If the response says `supported: false`, continue with the available thread list, current call stack, or source-level inspection.

## Exception Settings

Search settings:

```json
exception_settings_get({ "exceptionName": "InvalidOperationException", "sessionId": "..." })
```

Break on thrown:

```json
exception_settings_set({
  "exceptionName": "System.InvalidOperationException",
  "breakOnThrown": true,
  "sessionId": "..."
})
```

When setting is not found and `breakOnThrown` is true, the VSIX attempts to create it under Common Language Runtime Exceptions.

## Reporting

Report findings with evidence:

- Active Visual Studio session and debugger mode.
- Key stack frame, locals, and expression results.
- Relevant source line or file path.
- Unsupported features reported by the active Visual Studio debug engine.
- Final debugger disposition, including whether investigation breakpoints were deactivated and execution was continued.

## Troubleshooting

- Broker unavailable: ask the user to start the NetVsMcp Broker and check `http://127.0.0.1:5050/health`.
- No VS sessions: infer or confirm the target solution, launch Visual Studio with that solution, then recheck `vs_list_sessions`.
- Ambiguous routing: call `vs_list_sessions` and retry with `sessionId`.
- Inspection returns empty while running: call `debug_break` if pausing is acceptable, then inspect again.
- Breakpoint path problems: prefer absolute paths; NetVsMcp resolves paths against the Visual Studio solution when possible.
- Unsupported debug feature: report the unsupported feature and switch to basic status, breakpoint, call stack, locals, or expression tools.
