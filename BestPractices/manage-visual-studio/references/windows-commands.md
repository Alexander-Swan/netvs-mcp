# Windows, Tool Windows, And Commands

Use window tools when the user needs a specific Visual Studio surface visible or active:

```json
window_list({ "sessionId": "..." })
window_activate({ "caption": "Solution Explorer", "sessionId": "..." })
toolwindow_show({ "objectKind": "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}", "sessionId": "..." })
toolwindow_hide({ "caption": "Output", "sessionId": "..." })
```

`window_activate`, `toolwindow_show`, and `toolwindow_hide` accept `caption` or `objectKind`; at least one is required. Use `objectKind` when captions are ambiguous or localized.

Use `execute_command` only when a dedicated NetVsMcp tool does not exist:

```json
execute_command({ "commandName": "Build.RebuildSolution", "arguments": null, "sessionId": "..." })
```
