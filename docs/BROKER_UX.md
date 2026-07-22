# Broker UX

`NetVsMcp.Broker` is the local always-running WPF tray app that hosts the HTTP MCP endpoint and accepts Visual Studio VSIX registrations over the per-user named pipe.

## Status Window

The status window shows:

- broker running state and start time
- local MCP endpoint
- VSIX named pipe
- ready-to-copy MCP client JSON snippet
- registered Visual Studio sessions with solution name, solution path, session id, health, last seen time, debugger mode, active document, and advertised capabilities
- basic actions for copying configuration, refreshing status, toggling start at login, and opening the logs folder

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

The folder is created on demand. Structured broker file logging is still a follow-up; current runtime diagnostics primarily use trace output.
