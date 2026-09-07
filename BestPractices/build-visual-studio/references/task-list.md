# Task List

```json
task_list_get({ "includeCommentTasks": true, "includeUserTasks": true, "maxItems": 200, "sessionId": "..." })
task_list_add({ "description": "Investigate flaky test", "priority": "High", "sessionId": "..." })
task_list_remove({ "index": 3, "sessionId": "..." })
task_list_set_checked({ "index": 3, "checked": true, "sessionId": "..." })
```

Task List indices are live 1-based positions and can shift. Call `task_list_get` again before mutating if time has passed.

`task_list_remove` and `task_list_set_checked` only operate on user tasks, not comment-token tasks derived from source.
