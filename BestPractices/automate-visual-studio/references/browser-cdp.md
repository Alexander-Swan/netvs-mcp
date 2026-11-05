# Browser CDP Automation

Use `web_connect` to connect to a Chromium CDP endpoint, such as a browser launched with `--remote-debugging-port=9222`.

```json
web_connect({ "target": "9222", "url": "https://localhost:5001/", "sessionId": "..." })
web_dom_query({ "selector": "#submit-button", "sessionId": "..." })
web_element_click({ "selector": "#submit-button", "sessionId": "..." })
web_disconnect({ "sessionId": "..." })
```

After connecting, check `Metadata.backend`, not just success. `backend: "cdp"` means real browser automation. `browser-shell-uia` and `http-fetch` are lower-fidelity fallbacks.

`web_dom_get` and `web_dom_query` operate on the most recent connected/navigated URL; they do not accept a URL parameter.

Specialized Blazor operations: `web_blazor_inspect`, `web_blazor_wait_ready`, `web_blazor_get_components`, `web_blazor_get_state`, `web_blazor_trigger_event`, `web_blazor_inspect_circuit`, and `web_blazor_inspect_wasm`.

`web_blazor_*` tools report observable browser-side Blazor state. They can detect readiness, component markers, DOM-visible state, Server circuit indicators, and WebAssembly runtime resources, but they do not expose private .NET component fields.

`web_js_execute`, `web_blazor_*`, real console events, and network events require CDP. Disconnect when finished.
