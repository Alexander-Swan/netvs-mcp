# Setup

NetVsMcp is a local-only Visual Studio MCP broker plus VSIX. The broker runs on your Windows machine, exposes MCP over loopback HTTP, and routes requests to registered Visual Studio instances.

Two setup paths exist:

- **Install (recommended)** -- install the VSIX. It includes the broker payload and can start it from Visual Studio. No separate broker installer is required for normal use.
- **Standalone broker fallback** -- if the bundled broker cannot be used on a machine, install the broker MSI from the GitHub release assets, then open Visual Studio. The extension will connect to the already-running standalone broker instead of starting its bundled broker.
- **Build from source (contributors)** -- clone the repo and build/run the broker and VSIX yourself.

## Install (Recommended)

1. **Install the Visual Studio extension**: search for "NetVsMcp" in Visual Studio's Extensions > Manage Extensions, or install it directly from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/). Restart Visual Studio when prompted.
2. **Open Visual Studio**: the first Visual Studio instance that loads the extension connects to an already-running broker if one exists. If no compatible broker is running, the extension starts the broker bundled inside the VSIX and the broker appears in the Windows tray.
3. **Open a solution**: each Visual Studio instance registers itself with the broker through the per-user named pipe.
4. Continue with [MCP Client Config](#mcp-client-config) below to point your MCP client at the running broker.

If you install the standalone broker MSI, launch the broker from the Start menu or enable its start-at-login option, then open Visual Studio. Visual Studio will connect to that running broker instead of starting the VSIX-bundled one. Standalone broker launches keep the broker's own update UI for now. MSI-installed brokers must not include the VSIX marker file. Only brokers running from the VSIX package hide broker self-update controls because the marker file beside the broker executable identifies that copy as part of the Visual Studio extension payload; that payload updates with the extension.

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

For normal installed usage, open Visual Studio and let the extension start the bundled broker. Contributors can also run the broker project directly:

```powershell
dotnet run --project .\src\NetVsMcp.Broker\NetVsMcp.Broker.csproj
```

Debug builds are isolated from an installed Release broker, so you do not need to close the Release tray app before running or debugging a local broker. The Debug broker uses its own default port (`5051`), named pipe (`netvs-mcp-debug-*`), MCP client server names (`netvs-debug` and `netvs-debug-web-automation`), analytics database location, and single-instance guard.

By default, the installed app listens only on loopback at:

- Status root: `http://127.0.0.1:5050/`
- Health check: `http://127.0.0.1:5050/health`
- MCP HTTP endpoint: `http://127.0.0.1:5050/mcp`
- MCP web/UI automation endpoint: `http://127.0.0.1:5050/mcp-wu` (rarely used `ui_*`/`web_*` tools only, kept off `/mcp` to keep the default tool list smaller)

Contributor Debug builds use the same paths on port `5051`.

The broker also opens a per-user named pipe for VSIX registration. The tray/status UI is intended to show the running state, MCP registration snippet, and registered Visual Studio sessions.

## MCP Client Config

The easiest path is the broker's **Agents** tab:

1. Open the broker status window from the Windows tray icon.
2. Select **Agents**.
3. Find your MCP client in the detected clients list.
4. Click **Register**. If the client already points at this broker endpoint, the button changes to **Update** and updates the existing entry.
5. Restart or reload your MCP client so it reads the updated config.

The Agents tab can register NetVsMcp directly into a known client's own config file (Claude Desktop, Claude Code CLI, Codex CLI, GitHub Copilot CLI, Cursor, Windsurf, VS Code). It shows only clients detected on this machine and whether NetVsMcp is already registered. Clicking "Register" or "Update" writes the merged config immediately; by default, an existing file is backed up to `<path>.bak` first, and that backup can be disabled with the checkbox in the tab. Use "Open Config" there if you'd rather inspect or edit the file yourself.

To configure a client manually instead, or one the Agents tab doesn't know about yet, add an HTTP MCP server that points at localhost:

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

Use `127.0.0.1` or `localhost`; the broker rejects non-loopback hosts. The `netvs-web-automation` entry is optional if you do not need the `ui_*`/`web_*` debuggee automation tools; they are intentionally excluded from `/mcp`. Some clients call this shape `mcpServers`, while others use a TOML or UI equivalent; the important values are HTTP transport plus `http://127.0.0.1:5050/mcp`.

## Best-Practices Guides

After configuring the MCP client, use the included NetVsMcp best-practices guides when your agent is about to use a matching tool family. The broker exposes the guide entrypoints as MCP resources such as `guide://netvsmcp/manage-visual-studio.md`; tool-only clients can call `netvs_get_best_practices` with no arguments to list guides and reference files, or with `guide` and optional `file` to read one file.

The guides are intentionally small entrypoints plus focused references. For normal tool use, read the matching entrypoint first and then only the reference files needed for the current operation. For learning NetVsMcp or creating local skills for an agent, it is reasonable to read more broadly, but those local skills should also use references so they do not load large tool manuals into context for small tasks.

The MCP server provides the tools; the guides provide the Visual Studio operating judgment. They are not required for the broker to run, but they help agents choose the right session, prefer native IDE operations, and use the build, edit, debug, navigation, and automation tools safely. They are agent-neutral defaults, not locked policy: users can layer their own project or user instructions over the bundled guides through their agent's normal instruction mechanism.

## VSIX Registration Model

The Visual Studio extension connects to the local broker through the per-user named pipe when a VS instance starts. If no compatible broker is already running, the first Visual Studio instance starts the broker bundled inside the VSIX; later Visual Studio instances connect to the same tray broker. Each instance registers session information such as the VS process, opened solution, active document, and current debugger state. The broker keeps those registrations in memory and uses solution name/path routing to select the correct VS instance for MCP tool calls.

When more than one Visual Studio instance is open, MCP calls should include a solution name or solution path whenever the target is not obvious.

## Troubleshooting

- Broker not running: open Visual Studio with the NetVsMcp extension enabled, then check the Windows tray and `http://127.0.0.1:5050/health`. If you use a standalone broker install, you can still start `NetVsMcp.Broker` directly.
- Bundled broker blocked by policy: install the standalone broker MSI from the same GitHub release assets, start it manually or at login, then reopen Visual Studio so the VSIX connects to that broker.
- Endpoint not reachable: confirm port `5050` is free, use `127.0.0.1` or `localhost`, and include `/mcp` for MCP clients.
- VS instance not registered: make sure the VSIX is installed or running in the experimental Visual Studio instance, then open a solution so the extension has session data to report.
- Ambiguous solution selection: specify the solution name or full solution path in the MCP request when multiple registered VS instances could match.
- Stale session: close/reopen the affected Visual Studio instance or restart the broker so registrations are rebuilt.
