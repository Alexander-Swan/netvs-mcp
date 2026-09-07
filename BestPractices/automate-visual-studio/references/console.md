# Debuggee Console I/O

```json
console_get_info({ "target": "MyApp", "sessionId": "..." })
console_read({ "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
console_send({ "text": "some input\n", "target": "MyApp", "sessionId": "..." })
```

`target` may be a process ID or substring of a debugged process name. When omitted, the tools match Visual Studio's currently debugged processes.

`console_read` prefers the native console buffer (`backend: "windows-console"`) and falls back to Visual Studio output panes (`backend: "visual-studio-output"`). Treat those as different evidence sources.

`console_send` prefers native console input and can fall back to activating a window and sending keystrokes, which steals focus.
