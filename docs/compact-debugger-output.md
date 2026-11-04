# Debugging MCP response formats

The debugging workflow tools use reduced JSON by default. `debug_snapshot`,
`debug_wait_for_break`, and `debug_eval_many` no longer expose a `compact` input.
`get_help.compact` is a separate help-catalog setting and is unchanged.

Internal broker/VSIX RPC contracts retain their full evidence and pause guards.
Public tool descriptions in `tools/list` and `get_help` describe each projected result.

## Common rules

- Successful values have no `success`/`value` envelope. Scalars and arrays are returned
  directly in MCP text content; object results can also populate structured content.
  MCP structured content is object-only, so scalar/array results do not acquire an artificial wrapper.
- Failed calls set MCP `isError` and return an `error` message with available diagnostic
  and recovery metadata. Invalid individual expression values are not call failures.
- In-solution paths become solution-relative; external paths and exact operation identifiers
  remain intact. Coordinates use `file:line:column`; selections retain both endpoints.
- Default flags are omitted only where their absence has a defined meaning. Different state,
  unsupported capabilities, failed evaluations, stale-context checks, pagination, truncation,
  and modal operation evidence remain explicit.
- Frame indexes remain explicit. `mode`, `breakReason`, and `outcome` remain separate.
  Identical child state inherits from the enclosing state. Matching event-cursor session IDs
  inherit from state; event entries inherit matching session IDs and epochs from the batch.

## Values and inspection

Locals, watches and batch evaluations use expression names as object keys. Values remain
strings formatted by the debugger; numeric/boolean types are not inferred. Object summaries
use `{ "$preview": "Count = 3" }`; failed evaluation uses `{ "$error": "explanation" }`.
Duplicate or unnamed expressions retain arrays to avoid losing entries.

`debug_expand` returns expanded members directly, for example:

```json
{ "Id": "10", "Total": "100.01", "Items": { "$preview": "Count = 3" } }
```

Incomplete or ambiguous expansion returns `$value` plus `$expansion`, preserving member paths,
continuation offsets, truncation reasons, and budget evidence. An explicitly requested inspection
context on a direct expansion/watch value uses `$value`/`$context`.

`debug_select_frame` returns the selected frame without repeated state. Thread selection/freezing,
watch removal, detach/terminate and similar commands return true on success. `debug_set_variable`
returns the verified value. Watch addition returns its value or `$unavailable` when saved but not
yet evaluable; failure to save remains an MCP error.

## Breakpoints and modules

Breakpoints retain exact names accepted by follow-up mutations. Source breakpoints omit repeated
coordinates and function names. Function breakpoints retain function identity. Conditions,
tracepoint action/message/continuation, hit counts and disabled flags remain explicit.
Groups become object keys; the empty-string key holds ungrouped entries.

Breakpoint removal/enabling returns a boolean. Group enabling returns matched/updated counts;
group removal returns the number removed. A call that also continues execution retains observed
state rather than silently hiding pending or modal outcomes.

Modules group under `processes`, each with `processId` and `modules`. Dumps without OS IDs retain
`processUniqueId`. Modules retain path and symbol evidence, omitting redundant name, size, address,
version and module GUID. `symbols` is `.pdb` for a matching adjacent PDB, another path for symbols
elsewhere, `loaded` when symbols are associated but their path is unknown, or an explicit
`notLoaded`/`unavailable` status. Requested symbol-search diagnostics are preserved.

## Execution and supporting tools

Execution tools return observed debugger state directly. Run-to retains requested and hit
locations separately, process/thread identity when available, and owned-cleanup evidence.
Launch without debugging returns newly observed exact-startup-path process IDs (a scalar for
one, an array for several); unsupported/unobservable profiles return an explicit
`launched`/`processIdUnavailable` result, never an inferred PID.

Build tools return build identity/outcome; status reads retain state/error count. Compilation
failure is an observed build outcome. Diagnostics group as `errors`, `warnings`, and `messages`,
with `position` and `description`. Version/range/pagination evidence stays available.

Source reads return path/text/source/range information without duplicate document metadata or
textVersion. Version-sensitive diagnostic/edit workflows retain version identities. Symbol
context omits the duplicated full document and keeps its snippet and navigation information.
Navigation collections consolidate symbol identity; different related symbols remain explicit.

Test results retain run identity, outcomes, failure evidence and pagination. Combined run/results
responses consolidate matching metadata. Coverage uses covered/total counts and line-to-hit maps.
Session listings omit duplicate solution names and routine version/capability lists. Health reads
keep failed/warning checks; logs omit duplicate filename/directory fields.

UI/web automation, general edit transactions, help/guide catalogs and internal RPC are outside
this public debugging projection. No snapshot caching, delta protocol or execution replay is added.
