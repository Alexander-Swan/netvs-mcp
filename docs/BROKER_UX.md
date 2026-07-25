# Broker UX

`NetVsMcp.Broker` is the local always-running WPF tray app that hosts the HTTP MCP endpoint and accepts Visual Studio VSIX registrations over the per-user named pipe.

## Status Window

The status window is split into two tabs:

- **Status**: broker running state and start time, local MCP endpoint, VSIX named pipe, start-at-login state, logs folder, a ready-to-copy MCP client JSON snippet, registered Visual Studio sessions (solution name, solution path, session id, health, last seen time, debugger mode, active document, advertised capabilities), and basic actions text.
- **Settings**: active capability profile, editable through a dropdown that saves immediately, and editable startup settings (port, logs folder, sessions folder) that save immediately but require a broker restart to take effect.

The header (running state, MCP config/refresh buttons) and footer (copy endpoint/pipe, toggle autostart, open logs) stay visible above and below the tabs regardless of which tab is selected.

## Persisted Settings

All broker settings are configured from the status window and persisted to a single file:

```text
%LOCALAPPDATA%\NetVsMcp\settings.json
```

- **Capability profile**: the "Capability profile" dropdown selects one of `ReadOnly`, `EditPreview`, `EditDirect`, `Debug`, or `Admin`. Changing the selection takes effect immediately for all subsequent tool calls. `BrokerToolAccessPolicy` still enforces which tool categories each profile allows; see `docs/PLAN.md` for the category-to-profile mapping.
- **Port, logs folder, sessions folder**: edited in the "Startup Settings" group and saved with the "Save Settings" button. These are read once at broker startup (the HTTP listener and log/session directories are fixed for the life of a run), so changes only apply after restarting NetVsMcp Broker (exit via the tray icon, then relaunch).
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

Audit entries keep metadata such as timestamp, tool name, success/failure, selected session, routing fields, failure reason, and a short message. Runtime diagnostics outside tool calls still primarily use trace output.
