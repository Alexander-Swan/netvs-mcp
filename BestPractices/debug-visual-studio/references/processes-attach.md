# Processes And Attach

```json
process_list_local({ "sessionId": "..." })
debug_attach({ "processId": 12345, "sessionId": "..." })
process_list_debugged({ "sessionId": "..." })
process_detach({ "processId": 12345, "sessionId": "..." })
process_terminate({ "processId": 12345, "sessionId": "..." })
```

Use `process_terminate` only when the user explicitly wants the debugged process killed or has approved it.

Remote transports are supported by setting `transport`, `transportQualifier`, and optionally `engine`:

```json
debug_attach({ "transport": "SSH", "transportQualifier": "dev-box:22", "processId": 4521, "sessionId": "..." })
```

Transport names are matched against Visual Studio's registered transport list. Availability depends on installed VS workloads.

Remote transport calls can block on native Visual Studio dialogs, especially unfamiliar SSH host-key or credential prompts. If a remote attach appears to hang or times out, ask the user to check that Visual Studio instance rather than retrying the same call repeatedly.
