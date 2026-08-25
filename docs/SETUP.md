# Setup

NetVsMcp is a local-only Visual Studio MCP broker plus VSIX. The broker runs on your Windows machine, exposes MCP over loopback HTTP, and routes requests to registered Visual Studio instances.

Two install paths exist:

- **Install (recommended)** -- get the VSIX from the Marketplace and the broker from the MSI installer. No source build required.
- **Build from source (contributors)** -- clone the repo and build/run the broker and VSIX yourself.

## Install (Recommended)

1. **Install the Visual Studio extension**: search for "NetVsMcp" in Visual Studio's Extensions > Manage Extensions, or install it directly from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/). Restart Visual Studio when prompted.
2. **Install the broker**: download the latest `NetVsMcp.Broker-*.msi` from the project's [GitHub Releases](https://github.com/Alexander-Swan/netvs-mcp/releases) page and run it. The installer sets up the broker as a Windows app that can start at login; launch it from the Start menu if it doesn't start automatically.
3. Continue with [MCP Client Config](#mcp-client-config) below to point your MCP client at the running broker.

The broker checks for updates on its own (see the tray/status window) and can install newer MSI releases without you repeating this process. The `NetVsMcp.Vsix` extension also has an independent version shown in the VSIX manifest -- update it the same way (Marketplace) when a new version ships.

## Build From Source (Contributors)

### Prerequisites

- Windows with local loopback networking available.
- Visual Studio 2026 with the Visual Studio extension development workload and VS SDK.
- .NET SDK compatible with `NetVsMcp.slnx` project target frameworks.
- PowerShell or another shell that can run `dotnet`.

### Build

From the repository root:

```powershell
dotnet restore .\NetVsMcp.slnx
dotnet build .\NetVsMcp.slnx
```

The solution contains the broker app, shared contracts, and the Visual Studio extension project.

### Run The Broker

Run the broker project locally:

```powershell
dotnet run --project .\src\NetVsMcp.Broker\NetVsMcp.Broker.csproj
```

By default the broker listens only on loopback:

- Status root: `http://127.0.0.1:5050/`
- Health check: `http://127.0.0.1:5050/health`
- MCP HTTP endpoint: `http://127.0.0.1:5050/mcp`
- MCP web/UI automation endpoint: `http://127.0.0.1:5050/mcp-wu` (rarely used `ui_*`/`web_*` tools only, kept off `/mcp` to keep the default tool list smaller)

The broker also opens a per-user named pipe for VSIX registration. The tray/status UI is intended to show the running state, MCP registration snippet, and registered Visual Studio sessions.

## MCP Client Config

The broker's status window has an **Agents** tab that can register NetVsMcp directly into a known client's own config file (Claude Desktop, Claude Code CLI, Codex CLI, GitHub Copilot CLI, Cursor, Windsurf, VS Code). It shows whether each client is detected on this machine and whether NetVsMcp is already registered. Clicking "Register" or "Update" writes the merged config immediately; by default, an existing file is backed up to `<path>.bak` first, and that backup can be disabled with the checkbox in the tab. Use "Open Config" there if you'd rather inspect or edit the file yourself.

To configure a client manually instead, or one the Agents tab doesn't know about yet, point it at HTTP on localhost:

```json
{
  "mcpServers": {
    "netvs": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp"
    },
    "netvs-web-automation": {
      "type": "http",
      "url": "http://127.0.0.1:5050/mcp-wu"
    }
  }
}
```

Use `127.0.0.1` or `localhost`; the broker rejects non-loopback hosts. The `netvs-web-automation` entry is optional if you do not need the `ui_*`/`web_*` debuggee automation tools; they are intentionally excluded from `/mcp`.

## Best-Practices Guides

After configuring the MCP client, load the included NetVsMcp best-practices guides if your agent supports MCP resources or instruction bundles. The broker exposes the guides as MCP resources such as `guide://netvsmcp/manage-visual-studio.md`; tool-only clients can call `netvs_get_best_practices` with no arguments to list guides, or with `guide` and optional `file` to read one.

The MCP server provides the tools; the guides provide the Visual Studio operating judgment. They are not required for the broker to run, but they help agents choose the right session, prefer native IDE operations, and use the build, edit, debug, navigation, and automation tools safely. They are agent-neutral defaults, not locked policy: users can layer their own project or user instructions over the bundled guides through their agent's normal instruction mechanism.

## VSIX Registration Model

The Visual Studio extension connects to the local broker through the per-user named pipe when a VS instance starts. It registers session information such as the VS process, opened solution, active document, and current debugger state. The broker keeps those registrations in memory and uses solution name/path routing to select the correct VS instance for MCP tool calls.

When more than one Visual Studio instance is open, MCP calls should include a solution name or solution path whenever the target is not obvious.

## Troubleshooting

- Broker not running: start `NetVsMcp.Broker` and check `http://127.0.0.1:5050/health`.
- Endpoint not reachable: confirm port `5050` is free, use `127.0.0.1` or `localhost`, and include `/mcp` for MCP clients.
- VS instance not registered: make sure the VSIX is installed or running in the experimental Visual Studio instance, then open a solution so the extension has session data to report.
- Ambiguous solution selection: specify the solution name or full solution path in the MCP request when multiple registered VS instances could match.
- Stale session: close/reopen the affected Visual Studio instance or restart the broker so registrations are rebuilt.
