# Errors And Output

```json
errors_list({ "includeWarnings": true, "maxItems": 200, "sessionId": "..." })
output_list_panes({ "sessionId": "..." })
output_read({ "paneName": "Build", "maxChars": 20000, "sessionId": "..." })
output_write({ "text": "Starting custom step...\n", "paneName": "MyPane", "activate": false, "sessionId": "..." })
output_clear({ "paneName": "Build", "sessionId": "..." })
```

Check `build_status` before trusting diagnostics immediately after a non-waiting build. `output_read.maxChars` is bounded and reports truncation.

When `paneName` is omitted, Visual Studio's Build pane is preferred if present; otherwise the first available pane is used.
