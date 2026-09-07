# UI Automation

These tools drive real debuggee windows through UI Automation, mouse events, and SendKeys fallbacks.

```json
ui_capture_window({ "target": "MyApp", "sessionId": "..." })
ui_capture_region({ "x": 0, "y": 0, "width": 800, "height": 600, "sessionId": "..." })
ui_snapshot({ "target": "MyApp", "sessionId": "..." })
ui_get_tree({ "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
ui_find_elements({ "selector": "type=Button", "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
ui_click({ "selector": "id=SubmitButton", "target": "MyApp", "sessionId": "..." })
ui_set_value({ "selector": "id=UsernameBox", "text": "alice", "sessionId": "..." })
ui_send_keys({ "text": "{ENTER}", "target": "MyApp", "sessionId": "..." })
```

Selectors use a small `key=value` language: `id=`, `automationid=`, `name=`, `text=`, `class=`, `classname=`, `type=`, or `controltype=`. Bare selectors match Name, AutomationId, and ClassName.

Use generated element IDs from `ui_find_elements`, `ui_get_element`, or `ui_wait_for_element` in follow-up actions to avoid ambiguous text searches.

Screenshots return base64 PNG in `Text` with MIME/encoding metadata.
