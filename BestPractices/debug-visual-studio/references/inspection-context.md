# Keep Debugger Inspection On The Same Pause And Frame

## Identity

- `includeContext: true` adds identity to individual evaluations, watches, snapshots, and expansion; it defaults to false. Composite results return `context` once at the root. Direct expansion/watch-add values use `$value`/`$context` to separate identity from debuggee members.
- Context contains `stopId`, `generation`, process/thread IDs, frame index/depth, and available function/source position. Matching pause IDs inherit from enclosing state; missing optional engine fields are omitted. Unresolved selected-frame indices remain null.
- Identity describes the inspected frame, which may differ from IDE selection. Indices/depth apply only to that thread and pause; distinguish recursion by depth/index, never function name. See [inspection targeting](inspection.md).

## Pause Guards

- Retain `debug_status.stopId`/`generation` as `expectedStopId`/`expectedGeneration` on stack, locals, evaluations, watch add/list, snapshots, waits, and expansion. Both are required for `debug_expand`.
- Changed or resumed contexts are rejected. Read current state before retrying; stale inspection never justifies repeating execution.
- Updated extensions pin batches/watches to one frame and validate the pause after collection. A paused watch addition is saved only after validation. Unguarded `watch_add` outside a pause can save configuration without evaluation/context; `watch_list` reports unavailable.

## Compatibility And Side Effects

- Explicit selectors, guards, and `includeContext: true` require updated extensions and never silently fall back.
- Only plain current-frame `debug_eval_many` can fall back to legacy evaluation when the frame RPC is absent. Available pause IDs are checked between results; very old extensions without them cannot guarantee atomic collection.
- Legacy evaluation permits function evaluation and side effects. Shared identity does not prevent them; use `debug_expand` with its default function-evaluation policy for bounded inspection.
- Source positions do not prove loaded-build agreement; use `debug_source_evidence` for checksums. Parallel/thread-specific legacy summaries remain separate capabilities until explicitly qualified.
