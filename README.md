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

See [docs/PLAN.md](docs/PLAN.md) for the saved development plan, [docs/SETUP.md](docs/SETUP.md) for local setup and usage, and [docs/BROKER_UX.md](docs/BROKER_UX.md) for the tray/status window behavior.

## Projects

```text
NetVsMcp.slnx
  src/NetVsMcp.Broker
    WPF tray/status app and local HTTP MCP broker skeleton

  src/NetVsMcp.Contracts
    shared DTOs and RPC contracts for broker/VSIX communication

  src/NetVsMcp.Installer
    WiX project that builds the broker tray app MSI

  src/NetVsMcp.Vsix
    planned Visual Studio extension
```

This repo is now structured for the local broker plus VSIX architecture. The old single-executable console prototype is intentionally not part of this fresh-start layout.

## MCP Registration

The broker status window will show the final ready-to-copy MCP configuration. The planned local HTTP shape is:

```json
{
  "mcpServers": {
    "netvs": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp"
    }
  }
}
```

The `ui_*`/`web_*` debuggee UI automation and web debugging tools are rarely needed, so they are kept off the default `/mcp` endpoint and served instead from `/mcp-wu`. Register a second `mcpServers` entry pointing at `http://127.0.0.1:5050/mcp-wu` only if you need those tools.

## Broker MSI

Build the broker tray app MSI with:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release
```

The MSI output is written to `src\NetVsMcp.Installer\bin\Release\NetVsMcp.Broker.msi`.
