# Verify Source At A Pause

Read `debug_status` and `process_list_debugged`, retain the current `stopId`, `generation`, and process ID, then call:

```json
debug_source_evidence({
  "expectedStopId": "<current stopId>",
  "expectedGeneration": 1,
  "expectedProcessId": 1234,
  "sessionId": "<selected session>"
})
```

The tool reads the selected process/thread's **top instruction**, without changing selection or evaluating expressions. Supply `threadId` to inspect a different thread in that process. It compares engine-supplied SHA-256 document checksums with two separate sources: exact local file bytes (`disk`) and an immutable Roslyn snapshot including unsaved text (`editor`). Editor comparisons preserve encoding, BOM, and line endings and return a text version. A matching file name, loaded symbols, or an on-disk PDB alone does not prove matching source.

Interpret each status separately. `match` means one engine-supplied checksum matched that snapshot; `mismatch` means supported checksums were available and none matched. `unavailable` means evidence could not be obtained, including missing/legacy checksums, no local file, unknown encoding, an unsupported engine, or an ambiguous linked document. `tooLarge` enforces an 8 MiB byte / 4 Mi UTF-16 character bound. `stale` means the Roslyn workspace changed during collection; the returned checksum still describes its returned text version, but read again before acting. A changed pause is rejected rather than substituted.

For example, `disk: match` with `editor: mismatch` can explain why a paused instruction does not correspond to an unsaved edit. Both mismatching can indicate a loaded build older than the saved source. Check module identity, build configuration, symbols, and recent edits before deciding to rebuild or restart. The tool never downloads source, loads symbols, or changes execution. It currently supports engine-supplied SHA-256 checksums only; native and legacy checksum paths remain unqualified. `debug_run_to` continues to report its own source checksums as unverified; this is a separate inspection, not an atomic precondition for execution.
