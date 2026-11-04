# Modules And Symbol Verification

Use `module_list` when a breakpoint does not bind, source locations look wrong, or you need to confirm which binary and symbols the debugger is using.

1. Check `debug_status` and explicitly route to the intended `sessionId` or `solutionPath` when multiple Visual Studio instances are open.
2. Module inspection requires `dbgBreakMode`. If running, pause with `debug_break` or wait for a breakpoint with `debug_wait_for_break`. Do not launch or attach a debuggee merely to inspect a session that has ended.
3. Filter by module name or path first. Add `processId` from `process_list_debugged` when multiple debugged processes could contain the same module.

```json
module_list({ "name": "App.dll", "sessionId": "..." })

module_list({ "processId": 1234, "name": "App.dll", "includeSymbolLoadInformation": true, "sessionId": "..." })
```

`name` is a case-insensitive substring of the module name or full path. Omit it to enumerate all modules exposed by Concord in the routed session. `module_list` reads debugger state without evaluating expressions, loading symbols, or changing the selected thread/frame.

## Interpret The Result

- Standalone unsupported queries return MCP errors. Unsupported sections inside snapshots retain `supported: false`; neither means a binary is missing. Mixed-engine sessions enumerate only Concord runtimes.
- Results group `modules` under `processes`, with `processId` once per group. Dump processes without an OS ID retain `processUniqueId`.
- Each module has a solution-relative `path` where possible; external paths remain absolute. Duplicate names, sizes, addresses, versions and module GUIDs are omitted.
- `symbols: ".pdb"` means the debugger loaded the matching adjacent PDB. A different PDB location is returned as a path. `"loaded"` without a path covers loaded/embedded symbols whose file path is unavailable.
- `symbols: "notLoaded"` means no current association; `"unavailable"` means symbols are unavailable. Symbol-status reasons remain explicit.
- Loaded symbols do not prove the local source matches the build or that a breakpoint binds. Finding a PDB on disk does not establish that it was loaded.
- An empty process/module result with a filter means no enumerated module matched. Unresolvable process filters fail explicitly.

When symbols are missing, retry the targeted query with `includeSymbolLoadInformation: true` and use `symbolLoadInformation` to inspect the debugger's search locations and outcomes. Report the returned evidence before proposing changes to build output, symbol paths, or symbol-loading settings. `module_list` does not load or reload symbols.

To verify symbols in the launch-and-wait workflow, request modules with the breakpoint result:

```json
debug_wait_for_break({ "include": ["callStack", "modules"], "moduleOptions": { "name": "App.dll" }, "sessionId": "..." })
```

`debug_snapshot` accepts the same `modules` section. Pass standalone `module_list` filters inside `moduleOptions` for these snapshot tools. Module collection happens once after reaching break mode; timeouts while running, ended sessions, and modal dialogs omit the section. If the extension lacks the module RPC, the module section returns `supported: false` with update guidance while other sections remain available. See `snapshots-waits.md`.

Older broker/VSIX versions may lack these options or the standalone tool; update both components before using them.

When finished, remove or disable breakpoints created for the investigation and continue execution if paused, unless the user asked to leave it paused or stop.
