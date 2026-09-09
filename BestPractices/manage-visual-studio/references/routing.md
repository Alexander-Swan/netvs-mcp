# Session Routing, Health, And Logs

Most NetVsMcp tools are routed. They accept optional `sessionId`, `processId`, `solutionPath`, `workspacePath`, `rootPath`, or `solutionName` and resolve them to one registered Visual Studio session before doing work.

Use solution-based routing first when the target is unclear. Prefer `solutionPath` when you know it, then `solutionName`; use `sessionId` only after selecting a specific running Visual Studio instance that must remain the target.

```json
vs_list_sessions()
vs_get_status()
vs_select_session({ "solutionPath": "D:\\Work\\App\\App.sln" })
vs_ping({ "solutionName": "App" })
vs_get_session({ "sessionId": "..." })
```

The broker resolves provided fields in this order: `sessionId`, `processId`, `solutionPath`, nearest `.sln` or `.slnx` from `workspacePath`/`rootPath`, `solutionName`, active Visual Studio window, then only registered session. Because `sessionId` is a runtime identity, prefer passing `solutionPath` or `solutionName` on normal follow-up calls. If no route is unique, retry with a more explicit solution field or the exact `sessionId` from `vs_list_sessions`.

`get_help` and `vs_get_capabilities` list tool metadata, including `McpEndpointPath`. Most tools are served from `/mcp`; `ui_*` and `web_*` tools are served only from `/mcp-wu`.

Use `netvs_doctor` for setup or routing problems. Use `vs_get_logs({ "maxFiles": 5, "maxCharsPerFile": 20000 })` only when troubleshooting needs broker-side evidence.
