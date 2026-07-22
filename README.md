# NetVsMcp

NetVsMcp is a planned local-only Visual Studio MCP integration.

The target architecture is an always-running local broker with a tray/status UI. The broker hosts MCP over HTTP on `127.0.0.1`, tracks registered Visual Studio extension instances, and routes tool calls to the correct Visual Studio session.

```text
Codex / MCP Client
  -> HTTP MCP on 127.0.0.1:5050
    -> NetVsMcp.Broker tray app
      -> NetVsMcp.Vsix instance A
      -> NetVsMcp.Vsix instance B
      -> NetVsMcp.Vsix instance C
```

See [docs/PLAN.md](docs/PLAN.md) for the saved development plan.

## Planned Projects

```text
NetVsMcp.slnx
  src/NetVsMcp.Broker
  src/NetVsMcp.Vsix
  src/NetVsMcp.Contracts
  tests/NetVsMcp.Tests
```

## MCP Registration

The broker status window will show the final ready-to-copy MCP configuration. The planned local HTTP shape is:

```json
{
  "mcpServers": {
    "netvs": {
      "type": "http",
      "url": "http://127.0.0.1:5050"
    }
  }
}
```
