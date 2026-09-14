# Task List

```json
task_list_get({ "includeCommentTasks": true, "includeUserTasks": true, "maxItems": 200, "sessionId": "..." })
task_list_add({ "description": "Investigate flaky test", "priority": "High", "sessionId": "..." })
task_list_get({ "includeUserTasks": true, "maxItems": 200, "sessionId": "..." }) // retain the added task's returned index
task_list_set_checked({ "index": 3, "checked": true, "sessionId": "..." })
task_list_remove({ "index": 3, "sessionId": "..." })
```

Create a temporary user task with `task_list_add` before testing `task_list_set_checked` or `task_list_remove`. Task List indices are live 1-based positions and can shift, so call `task_list_get` immediately after adding the task and retain its returned index; do not assume index `0`.

`task_list_remove` and `task_list_set_checked` only operate on user tasks, not comment-token tasks derived from source.
