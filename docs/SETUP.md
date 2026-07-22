# Local Setup

NetVsMcp is a local-only Visual Studio MCP broker plus VSIX. The broker runs on your Windows machine, exposes MCP over loopback HTTP, and routes requests to registered Visual Studio instances.

## Prerequisites

- Windows with local loopback networking available.
- Visual Studio 2026 with the Visual Studio extension development workload and VS SDK.
- .NET SDK compatible with `NetVsMcp.slnx` project target frameworks.
- PowerShell or another shell that can run `dotnet`.

## Build

From the repository root:

```powershell
dotnet restore .\NetVsMcp.slnx
dotnet build .\NetVsMcp.slnx
```

The solution contains the broker app, shared contracts, and the Visual Studio extension project.

## Run The Broker

Run the broker project locally:

```powershell
dotnet run --project .\src\NetVsMcp.Broker\NetVsMcp.Broker.csproj
```

By default the broker listens only on loopback:

- Status root: `http://127.0.0.1:5050/`
- Health check: `http://127.0.0.1:5050/health`
- MCP HTTP endpoint: `http://127.0.0.1:5050/mcp`

The broker also opens a per-user named pipe for VSIX registration. The tray/status UI is intended to show the running state, MCP registration snippet, and registered Visual Studio sessions.

## MCP Client Config

Configure your MCP client to use HTTP on localhost:

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

Use `127.0.0.1` or `localhost`; the broker rejects non-loopback hosts.

## VSIX Registration Model

The Visual Studio extension connects to the local broker through the per-user named pipe when a VS instance starts. It registers session information such as the VS process, opened solution, active document, and current debugger state. The broker keeps those registrations in memory and uses solution name/path routing to select the correct VS instance for MCP tool calls.

When more than one Visual Studio instance is open, MCP calls should include a solution name or solution path whenever the target is not obvious.

## Troubleshooting

- Broker not running: start `NetVsMcp.Broker` and check `http://127.0.0.1:5050/health`.
- Endpoint not reachable: confirm port `5050` is free, use `127.0.0.1` or `localhost`, and include `/mcp` for MCP clients.
- VS instance not registered: make sure the VSIX is installed or running in the experimental Visual Studio instance, then open a solution so the extension has session data to report.
- Ambiguous solution selection: specify the solution name or full solution path in the MCP request when multiple registered VS instances could match.
- Stale session: close/reopen the affected Visual Studio instance or restart the broker so registrations are rebuilt.
