# Endpoint Split And Backends

The broker serves tools over two MCP endpoints:

- `console_*` tools are on the default `/mcp` endpoint.
- `ui_*` and `web_*` tools are only on the opt-in `/mcp-wu` endpoint.

If a client is connected only to `/mcp`, `ui_*` and `web_*` calls fail as missing tools. Confirm the second MCP server connection before troubleshooting individual automation calls.

Automation results are nested: the outer `ToolResponse` covers routing/RPC; the inner `AutomationResult` has `Supported`, `Success`, `Message`, `Text`, and `Metadata`.

Check `Metadata.backend`. High-fidelity backends include `uia`, `cdp`, and `windows-console`. Lower-fidelity fallbacks include `sendkeys`, `sendkeys-fallback`, `browser-shell-uia`, `http-fetch`, and `visual-studio-output`.
