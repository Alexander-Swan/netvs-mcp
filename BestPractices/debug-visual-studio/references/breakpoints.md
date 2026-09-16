# Breakpoints And Tracepoints

```json
breakpoint_set({ "documentPath": "D:\\Work\\App\\Program.cs", "line": 42, "sessionId": "..." })
breakpoint_list({ "sessionId": "..." })
breakpoint_remove({ "documentPath": "...", "line": 42, "sessionId": "..." })
breakpoint_enable({ "documentPath": "...", "line": 42, "enabled": false, "sessionId": "..." })
breakpoint_group_list({ "sessionId": "..." })
breakpoint_group_enable({ "groupName": "scenario-name", "enabled": false, "continueExecution": true, "sessionId": "..." })
breakpoint_group_remove({ "groupName": "scenario-name", "sessionId": "..." })
```

Useful `breakpoint_set` options include `condition`, `actionMessage`, `continueAfterAction`, `hitCount`, `hitCountType`, and `groupName`.

Pass conditions as literal code expressions, not HTML-encoded text: use `count > 3`, `a && b`, and `x => x.Id == id`.

For inspection, prefer a breakpoint on or after the line where the needed values are available. Do not stop before variables are declared and then manually reconstruct their values when stepping or moving the breakpoint would expose the live runtime values.

Group breakpoints created for one investigation. When finished, disable or remove only the breakpoints you created, then continue execution if the debuggee is paused and the user did not ask to stop.
