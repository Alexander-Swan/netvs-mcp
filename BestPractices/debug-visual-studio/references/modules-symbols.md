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

- Check `supported` before drawing conclusions. `supported: false` means the engine exposes no Concord processes; it does not establish that the module or symbols are missing. Mixed-engine sessions enumerate only Concord runtimes.
- Check the module's `path`, `version`, and `processId` to confirm the expected binary is loaded. `processUniqueId` and `moduleId` distinguish instances with identical names or recycled process IDs. Dump processes have no OS `processId`.
- `symbolsLoaded: true` and `symbolStatus: "loaded"` mean the debugger has associated symbols with that module. This does not prove the local source matches or that a breakpoint will bind at a particular line.
- `symbolStatus: "notLoaded"` means there is no current symbol association. `"unavailable"` means the debugger marks symbols permanently unavailable for that module instance.
- `symbolFile` is the debugger-reported symbol-file path when available. It can be null even when symbols are loaded, including embedded-symbol cases. Finding a PDB on disk is not evidence that the debugger loaded it.
- An empty supported result with a name filter means no enumerated module matched. A process filter that cannot be resolved through Concord fails explicitly. Neither result proves a symbol-loading failure.

When symbols are missing, retry the targeted query with `includeSymbolLoadInformation: true` and use `symbolLoadInformation` to inspect the debugger's search locations and outcomes. Report the returned evidence before proposing changes to build output, symbol paths, or symbol-loading settings. `module_list` does not load or reload symbols.

To verify symbols in the launch-and-wait workflow, request modules with the breakpoint result:

```json
debug_wait_for_break({ "include": ["callStack", "modules"], "moduleOptions": { "name": "App.dll" }, "sessionId": "..." })
```

`debug_snapshot` accepts the same `modules` section. Pass standalone `module_list` filters inside `moduleOptions` for these snapshot tools. Module collection happens once after reaching break mode; timeouts while running, ended sessions, and modal dialogs omit the section. If the extension lacks the module RPC, the module section returns `supported: false` with update guidance while other sections remain available. See `snapshots-waits.md`.

Older broker/VSIX versions may lack these options or the standalone tool; update both components before using them.

When finished, remove or disable breakpoints created for the investigation and continue execution if paused, unless the user asked to leave it paused or stop.
