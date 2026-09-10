# Debug Live Test Setup

Use this path for contributor testing from a source checkout, experimental Visual Studio, or local Debug broker validation.

## Expected Debug Defaults

- Broker endpoint: `http://127.0.0.1:5051/mcp`
- Web/UI automation endpoint: `http://127.0.0.1:5051/mcp-wu`
- Health endpoint: `http://127.0.0.1:5051/health`
- MCP server names: `netvs-debug` and `netvs-debug-web-automation`
- Pipe prefix: `netvs-mcp-debug-`

Debug and Release brokers are intentionally isolated and may coexist. A Release broker on port `5050` is not a reason to stop the Debug broker or vice versa.

## Setup Checklist

1. Capture the initial repository state with `git status --short`.
2. Build the solution or at least the broker and VSIX projects in Debug configuration.
3. Ensure the Debug broker is running. Do not run it directly from `src/NetVsMcp.Broker/bin/Debug/...` when the regression will exercise Visual Studio build tools; that locks `NetVsMcp.Broker.exe` and causes VS/MSBuild copy failures. Instead, after the Debug build succeeds, copy the built broker output to a throwaway run folder such as `_live-test/debug-broker-run/` and start `NetVsMcp.Broker.exe` from that copy. If a broker is already listening on `5051`, verify it is the expected Debug broker before reusing it.
4. Verify `http://127.0.0.1:5051/health` responds.
5. Ensure the Debug VSIX from this checkout is installed in the Visual Studio Experimental instance. Install the built Debug `.vsix` into the `Exp` root suffix when needed.
6. Launch Visual Studio Experimental with this project loaded, normally `devenv.exe /RootSuffix Exp <repo>\NetVsMcp.slnx`.
7. Wait for the Visual Studio session to register with the Debug broker. Prefer MCP session/listing tools when available; otherwise use broker status/health evidence.
8. If the broker is healthy but no Visual Studio session registers, inspect the Experimental ActivityLog, normally under `%APPDATA%\Microsoft\VisualStudio\*\ActivityLog.xml`. If it shows `NetVsMcpPackage` loading from a missing or stale extension directory, close only the Experimental Visual Studio instance under test, run `devenv.exe /RootSuffix Exp /updateConfiguration`, relaunch the solution, and recheck `vs_list_sessions`.
9. Confirm the agent runtime has MCP tools from the Debug broker available. If the broker is running but tools are absent, the client likely needs MCP registration or reload.

## Debug Cleanup

Before finishing:

- Revert repository changes made only for the live test, including temporary files, test edits, generated previews, and debug-only scaffolding.
- Leave pre-existing user changes untouched.
- Stop any copied Debug broker process started for the test, then remove its throwaway run folder after confirming the resolved path is inside the repository workspace.
- Remove or deactivate test breakpoints, clear temporary watch expressions when supported, and resume/stop only test debuggees you started.
- Keep the Release broker and installed Release VSIX undisturbed unless the user explicitly included Release testing too.
