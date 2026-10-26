# Current Exception Evidence

Read `debug_status` or a stopped snapshot, retain its `stopId` and `generation`, then call:

```json
debug_exception({
  "expectedStopId": "<current stopId>",
  "expectedGeneration": 1,
  "sessionId": "<selected session>"
})
```

The tool reads debugger event evidence without evaluating `$exception` properties or changing exception settings. `evidenceStatus` is `captured`, `noException` for an ordinary pause, or `unavailable` when the engine supplied no matching notification. A successful read with unavailable evidence does not prove there was no exception. Old extensions without this RPC return unsupported; no unguarded fallback is used.

Captured evidence includes the engine's category GUID, type name, description, optional raw message/code, throwing process/thread, notification kind, and raw processing flags. Text has explicit size limits and `evidenceTruncated`. A code is present only when the engine reports code support; zero is not substituted for missing information. A first-chance `thrown` notification does not establish future handling: `handled` stays null. `notHandled` reports an explicit second-chance notification; `userUnhandled` can still be handled by framework code, so final handling remains unknown.

An optional location comes from the throwing thread's top Concord frame in this pause, regardless of the selected inspection frame. It may be unavailable when the engine, symbols, or source query cannot supply it. A source query failure preserves the captured exception and returns a reason; the location does not establish source checksum matching.

After continue, step, restart, or another pause, discard the old identity and obtain fresh state. `stale_debugger_context` requires refreshing the pause; never inspect using an earlier exception's token. Observation started after a stop cannot reconstruct a notification that was not captured. Changing exception settings affects debugger behavior and must be scoped to the user's investigation and restored when temporary.
