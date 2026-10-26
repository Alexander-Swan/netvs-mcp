# Event and Operation Contract

This contract describes session event identity, command observation, and recovery behavior.

## Identity and Ordering

An event belongs to one registered Visual Studio session and one publisher epoch. Each publisher increments its sequence monotonically. An epoch changes when the publisher restarts; sequence values from different epochs are never comparable. Debugger generation and stop ID retain their existing meanings and travel with debugger observations.

A subscriber captures the event watermark before issuing a command. Events published before that watermark cannot complete the command. A fast stop published before the caller starts waiting must still be available. A current-state snapshot alone is not proof that a requested command completed.

## Delivery and Reconciliation

Publication must not block the Visual Studio UI thread. Buffers are bounded. Overflow, unknown epochs, and cursors older than retained history return an explicit reconciliation requirement. A reconnect obtains a fresh authoritative snapshot and watermark before trusting further events. Missing notifications never cause mutations to be replayed.

The internal VS-to-broker channel is independent of whether an external MCP client supports notifications. Request/response tools remain available; broker wait operations consume internal events and return structured results.

`debug_snapshot(include: [])` exposes the authoritative debugger state with `state.eventCursor` (session ID, epoch, watermark). `debug_status` and the debugger section of `vs_context_snapshot` use the same capture. Follow that cursor with `vs_events`; reconcile gaps with a fresh read-only snapshot. Legacy extensions and modal or stale observations omit the cursor rather than combining independently read state and events.

## Domain-specific operations

Builds expose their own start/status/cancel controls. Tests expose run IDs and bounded retained
results. Debugger commands keep execution identity, pause guards, bounded waits, and modal
recovery through `debug_status(operationId: ...)`. There is no generic background-job API,
request-ID deduplication service, or durable operation-history file.

Canceling a wait or reaching a timeout does not prove the IDE command stopped. Preserve the
original modal-command ID when one is returned; inspect its outcome rather than replaying it.
These command records belong to the current broker lifetime. After restart, reconcile current
IDE state; a missing record does not establish whether work executed.

## Compatibility

New wire members are additive. Features requiring correctness-critical guards or event delivery are negotiated explicitly. Missing support must produce a structured unsupported result instead of silently ignoring a guard. Legacy clients keep existing request/response behavior while newer clients can opt into stronger contracts.

Default debugger MCP responses retain nested state fields. Only `compact: true` opts into inherited-state deduplication.
