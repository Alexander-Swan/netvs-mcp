# Security And Local Runtime Model

NetVsMcp is designed as a local-only control plane for Visual Studio. Its security model assumes the broker, VSIX, MCP client, and Visual Studio instances all run on the same Windows user session.

## Local-Only Boundary

- The broker MCP endpoint must bind only to loopback addresses such as `127.0.0.1` or `localhost`.
- MCP clients should connect to the broker through the local HTTP endpoint, currently `http://127.0.0.1:5050/mcp`.
- The VSIX communicates with the broker through a per-user named pipe.
- The VSIX should not accept direct network requests; it should execute requests only after they are routed by the local broker.

The recommended default posture is to configure only trusted local MCP clients. Do not expose the broker port through port forwarding, public firewall rules, reverse proxies, or shared remote shells.

## Trust And Sensitive Data

MCP tools for Visual Studio can expose or modify high-value development data:

- Source code and unsaved editor buffers.
- File paths, project names, solution names, and build output.
- Debugger locals, watched expressions, call stacks, exception details, and evaluated values.
- Environment values visible through process state, output panes, build logs, or debuggee inspection.
- Breakpoints, conditional breakpoints, and execution flow.

Debugger and editing operations are high impact. Setting breakpoints, evaluating expressions, stepping, continuing, stopping, editing buffers, or starting builds can change program behavior, reveal secrets, or disrupt a debugging session.

## Routing Safety

The broker tracks registered Visual Studio instances and routes tool calls to a target session. Clients should provide an explicit session id, solution path, or solution name when more than one Visual Studio instance may match.

Expected routing behavior:

- Exact session id is preferred when available.
- Full solution path is safer than solution file name.
- Solution name can be convenient but may be ambiguous.
- Ambiguous target selection should fail with a clear error instead of guessing.
- Stale or disconnected sessions should not receive tool calls.

This is especially important for debugger and editing tools because routing a request to the wrong Visual Studio instance can modify the wrong codebase or control the wrong debuggee.

## Broker Token And Audit Expectations

Future broker hardening should include:

- A per-user broker token stored under `%LOCALAPPDATA%\NetVsMcp`.
- Token validation for broker-facing requests that need authentication.
- VSIX authentication to the broker before registration or command execution.
- A local audit log for MCP tool calls, including time, tool name, selected session, routing input, routing result, and success/failure.

Audit logging should avoid dumping full source code, full locals, full expression results, or large output panes by default. Log enough metadata to explain what happened without creating a second sensitive data store.

## Known MVP Gaps

- Real end-to-end validation with a running broker and an experimental Visual Studio instance is still required.
- Authentication token handling is not complete unless the current code proves otherwise.
- Local audit logging is not complete unless the current code proves otherwise.
- Debugger and editor tools should be reviewed carefully before being treated as safe for unattended client use.

Until those gaps are closed, use NetVsMcp only with trusted local clients and keep high-impact operations human-visible.
