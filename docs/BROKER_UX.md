# Broker UX

`NetVsMcp.Broker` is the local always-running WPF tray app that hosts the HTTP MCP endpoint and accepts Visual Studio VSIX registrations over the per-user named pipe.

## Status Window

The status window shows:

- broker running state and start time
- local MCP endpoint
- VSIX named pipe
- active capability profile, editable through a dropdown that saves immediately
- ready-to-copy MCP client JSON snippet
- registered Visual Studio sessions with solution name, solution path, session id, health, last seen time, debugger mode, active document, and advertised capabilities
- basic actions for copying configuration, refreshing status, toggling start at login, and opening the logs folder

## Capability Profile

The status window's "Capability profile" dropdown selects one of `ReadOnly`, `EditPreview`, `EditDirect`, `Debug`, or `Admin`. Changing the selection takes effect immediately for all subsequent tool calls and is persisted to:

```text
%LOCALAPPDATA%\NetVsMcp\capability-profile.json
```

The persisted profile is loaded at broker startup and used until changed again. `BrokerToolAccessPolicy` still enforces which tool categories each profile allows; see `docs/PLAN.md` for the category-to-profile mapping.

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
