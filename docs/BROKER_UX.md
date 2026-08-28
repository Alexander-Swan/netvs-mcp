# Broker UX

`NetVsMcp.Broker` is the local always-running WPF tray app that hosts the HTTP MCP endpoint and accepts Visual Studio VSIX registrations over the per-user named pipe.

## Status Window

The status window is split into tabs:

- **Status**: a "Connection" card listing MCP endpoint, named pipe, start-at-login state, and logs folder, each with its copy/open/toggle action inline right next to the value (no separate footer to hunt for the matching button). Client registration itself - manual or automatic - lives entirely in the Agents tab now, so this card stays focused on the broker's own connection info.
- **Sessions**: a "Registered Visual Studio sessions" table (solution name, PID, debugger mode, active document, last seen, age, solution path, session id, VSIX version) that shows a contextual "No sessions yet" message in place of the table when nothing is registered.
- **Agents**: the single place for getting a client talking to NetVsMcp. A table of the known local MCP clients (Claude Desktop, Claude Code CLI, Codex CLI, GitHub Copilot CLI, Cursor, Windsurf, VS Code) actually **detected** on this machine, showing whether NetVsMcp is already registered in each one's config file; clients that aren't installed are left off the list entirely - there's nothing useful to do for an app that isn't there. Registration is matched by **URL, not by entry name** - if a server entry already points at the running broker's endpoint under some other name (e.g. a hand-registered `netvs-mcp`), it's recognized as already registered and gets updated in place on "Register", instead of adding a second, confusingly-named duplicate entry. Only when no existing entry matches the endpoint does a fresh entry get created, named `netvs`/`netvs-web-automation`. "Register"/"Update" writes the merged config directly; an existing file is backed up to `<path>.bak` first by default, with a checkbox to disable that backup. "Open Config" opens the file, or its containing folder if the file doesn't exist yet, so users can inspect, register, or edit by hand instead. JSON clients (Claude Desktop, Claude Code, GitHub Copilot CLI, Cursor, Windsurf, VS Code) merge via `System.Text.Json`; Codex CLI's `config.toml` merges via Tomlyn, only touching the matched `[mcp_servers.*]` tables (existing per-tool `[mcp_servers.<name>.tools.*]` approval settings and unrelated tables/secrets are preserved). Below the table, a "Manual configuration" section keeps the raw `/mcp` + `/mcp-wu` JSON snippet with a Copy button, for any client not in the known list or anyone who prefers to edit by hand.
- **Settings**: editable startup settings (port, logs folder, sessions folder) that save immediately but require a broker restart to take effect, plus logging level controls that apply immediately.

The header (running state badge, refresh) stays visible above the tabs regardless of which tab is selected.

## Persisted Settings

All broker settings are configured from the status window and persisted to a single file:

```text
%LOCALAPPDATA%\NetVsMcp\settings.json
```

- **Port, logs folder, sessions folder**: edited in the "Startup Settings" group and saved with the "Save Settings" button. These are read once at broker startup (the HTTP listener and log/session directories are fixed for the life of a run), so changes only apply after restarting NetVsMcp Broker (exit via the tray icon, then relaunch).
- **Minimum log level**: edited in the "Logging" group and applied immediately. It controls which audit entries are written in the first place.
- **Named pipe**: not user-configurable. It is always derived from the current Windows user SID (and Debug/Release build) so the VSIX, which computes the same value independently, can always find the broker without any coordination step.

There are no environment variables for broker configuration; `--mcp-port`, `--mcp-endpoint`, `--pipe-name`, `--logs-dir`, `--sessions-dir`, and `--settings-file` command-line arguments remain available for advanced/one-off overrides (e.g. automated testing) and take precedence over the persisted settings file for that single run only — they are not saved.

## Tray Menu

The tray icon menu includes:

- Open Status Window
- Copy MCP Config
- Refresh
- Start at Login
- Open Logs Folder
- Exit

The tray tooltip summarizes how many Visual Studio sessions are currently registered.

## Autostart

The current autostart implementation uses the per-user Windows `Run` registry key:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\NetVsMcp.Broker
```

This avoids elevation and keeps the slice conservative. A later installer-focused slice can replace this with Task Scheduler or MSI-managed startup if needed.

## Logs

The UI opens this folder for logs:

```text
%LOCALAPPDATA%\NetVsMcp\Logs
```

The folder is created on demand. Broker tool calls are written as newline-delimited JSON files named like:

```text
audit-20260722.jsonl
```

Audit entries keep metadata such as timestamp, level, tool name, success/failure, selected session, routing fields, failure reason, and a short message. Logs roll daily and the default retention keeps only today's `audit-yyyyMMdd.jsonl` file. The broker also exposes the same audit logs at:

```text
GET /logs?maxFiles=5&maxCharsPerFile=20000&minLevel=warning
```

`minLevel` is optional and accepts `debug`, `info`, `warning`, or `error`. Runtime diagnostics outside tool calls still primarily use trace output.
