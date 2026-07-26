# NetVsMcp Visual Studio Bridge

Connects Visual Studio to the local **NetVsMcp Broker**, exposing this Visual Studio instance to MCP clients (like MCP clients) over a local, per-machine connection. No cloud services, no telemetry — everything stays on your machine.

## What it does

This extension registers the current Visual Studio instance with the NetVsMcp Broker tray app running on `127.0.0.1`, then executes the requests the broker routes to it:

- **Editor** — open/read/write documents, apply edits, navigate to symbols, search across the workspace
- **Navigation** — go to definition/implementation, find references, document/workspace symbols
- **Build** — build or rebuild projects/solutions, read build errors and status
- **Debugger** — start/stop/attach, breakpoints, stepping, call stacks, locals, watches, expression evaluation
- **Solution & project info** — solution overview, project references, NuGet packages
- **Tests** — discover and run tests, read results
- **Git** — read repository/branch context for the open solution

## Requirements

The **NetVsMcp Broker** must be installed and running locally for this extension to do anything — it's the process that MCP clients actually talk to.

Download the broker: https://github.com/Alexander-Swan/netvs-mcp/releases/latest

## Project

Source, documentation, and issue tracker: https://github.com/Alexander-Swan/netvs-mcp
