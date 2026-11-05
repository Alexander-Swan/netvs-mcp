# Paused-State Inspection

Inspect only in `dbgBreakMode`. Prefer stepping or moving a breakpoint to expose live values before manual evaluation; missing locals may not yet be declared or initialized. For targeted values, use `debug_eval_many` or snapshot watches rather than all locals.

## Target A Frame

Get thread IDs from `debug_get_threads` and 1-based frame indices from `thread_get_callstack` or `debug_get_callstack`. Call stacks default to two frames; use `maxFrames` and check `hasMore` for deeper callers. Returned frames expose `frameIndex` and `isCurrent` on updated extensions.

| Arguments | Inspection target |
| --- | --- |
| Neither `threadId` nor `stackFrame` | Selected frame, which need not be frame 1 |
| `threadId` only | Frame 1 of that thread |
| `stackFrame` only | Supplied frame of the current thread |
| Both | Supplied frame of that thread |

Inspection preserves selection and does not move the instruction pointer. Invalid or unavailable targets fail without fallback. Refresh indices after execution or switching threads. Explicit targeting requires an updated VSIX; older versions report update/omit-selector guidance rather than another frame's values.

```json
debug_get_locals({ "threadId": 42, "stackFrame": 2, "sessionId": "..." })
debug_eval_many({ "expressions": ["order.Total", "customer.Id"], "stackFrame": 2, "sessionId": "..." })
```

Locals, evaluations, variable writes, watches, and snapshots accept frame targeting. In snapshots/waits, `threadId` targets call stack, locals, and watches; `stackFrame` targets locals and watches. Other sections retain their scope. See [snapshots and waits](snapshots-waits.md) for inclusion defaults and [watches and immediate execution](watches-immediate.md) for those tools.

Use `debug_select_frame(frameIndex: ...)` only to deliberately change the selection for later calls; it persists until another debugger action changes it. Use `debug_set_variable(name: ..., value: ...)` only when the user wants runtime state changed. Expression evaluation can execute debuggee code; avoid side effects unless authorized.

A valid frame, especially in framework/native code, can lack an evaluation context. Named values use debugger-formatted strings, `$preview` summaries, or `{ "$error": "explanation" }`. An invalid value does not imply the enclosing inspection failed. Duplicate/unnamed expressions retain arrays with explicit validity flags. Read that thread's stack and retry in an explicit user-code frame; evaluation never silently switches frames.

## Bounded Object Expansion

1. Read `stopId` and `generation` from `debug_status` or a stopped snapshot. Pass both as required `expectedStopId` and `expectedGeneration` to `debug_expand`, along with `expression` and optional thread/frame targeting.
2. Bound work with `maxChildren` (default 20), `maxDepth` (1), `maxNodes` (100), `maxValueCharacters` (1024), and `budgetMilliseconds` (1000). The elapsed budget is cooperative; synchronous engine calls can exceed it.
3. Function evaluation defaults off and auto-expand rules are disabled. Set `allowFunctionEvaluation: true` only when executing debuggee code is authorized.
4. For more children, follow the returned node's `memberPath` and pass its `nextOffset` as `childOffset`, retaining the same pause guards and frame. Incomplete/duplicate-member results expose `$value` plus `$expansion` with paths, offsets, and truncation evidence. Scalars, unexpanded objects, and failures use strings, `$preview`, and `$error` respectively.

Expansion requires an updated VSIX and never falls back to unguarded inspection. A stale guard requires reading current state before a new inspection, not replaying execution. See [inspection context](inspection-context.md) for identity and compatibility.
