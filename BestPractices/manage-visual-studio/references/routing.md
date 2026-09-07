# Session Routing, Health, And Logs

Most NetVsMcp tools are routed. They accept optional `sessionId`, `processId`, `solutionPath`, `workspacePath`, `rootPath`, or `solutionName` and resolve them to one registered Visual Studio session before doing work.

Use these first when the target is unclear:

```json
vs_list_sessions()
vs_get_status()
vs_get_session({ "sessionId": "..." })
vs_select_session({ "solutionPath": "D:\\Work\\App\\App.sln" })
vs_ping({ "solutionName": "App" })
```

Routing resolution order is `sessionId`, `processId`, `solutionPath`, nearest `.sln` or `.slnx` from `workspacePath`/`rootPath`, `solutionName`, active Visual Studio window, then only registered session. If no route is unique, retry with a more explicit field.

`get_help` and `vs_get_capabilities` list tool metadata, including `McpEndpointPath`. Most tools are served from `/mcp`; `ui_*` and `web_*` tools are served only from `/mcp-wu`.

Use `netvs_doctor` for setup or routing problems. Use `vs_get_logs({ "maxFiles": 5, "maxCharsPerFile": 20000 })` only when troubleshooting needs broker-side evidence.
