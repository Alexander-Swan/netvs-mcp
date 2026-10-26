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

## Authentication boundary

The loopback HTTP endpoint does not require a broker authentication token. Loopback binding limits network exposure; it does not authenticate callers. Trust local processes and MCP clients that can reach the endpoint. Do not forward the port or expose it through a proxy.

The per-user named pipe connects the broker and extension within the same Windows user context. It is not a boundary between mutually untrusted applications running as that user.

## Audit logs and usage analytics

Tool calls are recorded in a local JSONL audit log with time, tool name, success/failure, routing fields, failure reason, and a short message. The logger does not serialize full tool arguments or results. Paths and messages can still contain sensitive context, so protect local logs accordingly.

Usage analytics are separate from audit logging and are disabled by default. When enabled, analytics are stored locally in SQLite as aggregate counters: daily buckets by broker version, tool, category, endpoint, and success/failure, plus size, token-estimate, and duration totals. They do not store tool arguments, responses, source text, debugger values, output panes, or messages.

## External services

IDE requests travel between the local MCP client, broker, and Visual Studio. Update checks use GitHub; package searches and restores use configured package feeds. These operations can require network access. An MCP client may independently send tool results to its configured model provider; NetVsMcp's local transport does not change that client's privacy settings.
