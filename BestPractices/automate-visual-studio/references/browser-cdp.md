# Browser CDP Automation

Use `web_connect` to connect to a Chromium CDP endpoint, such as a browser launched with `--remote-debugging-port=9222`.

```json
web_connect({ "target": "9222", "url": "https://localhost:5001/", "sessionId": "..." })
web_status({ "sessionId": "..." })
web_navigate({ "url": "https://localhost:5001/about", "sessionId": "..." })
web_screenshot({ "sessionId": "..." })
web_dom_get({ "sessionId": "..." })
web_dom_query({ "selector": "#submit-button", "sessionId": "..." })
web_console({ "sessionId": "..." })
web_js_execute({ "text": "document.title", "sessionId": "..." })
web_network({ "sessionId": "..." })
web_element_click({ "selector": "#submit-button", "sessionId": "..." })
web_element_set_value({ "selector": "#username", "text": "alice", "sessionId": "..." })
web_disconnect({ "sessionId": "..." })
```

After connecting, check `Metadata.backend`, not just success. `backend: "cdp"` means real browser automation. `browser-shell-uia` and `http-fetch` are lower-fidelity fallbacks.

`web_dom_get` and `web_dom_query` operate on the most recent connected/navigated URL; they do not accept a URL parameter.

`web_js_execute`, real console events, and network events require CDP. Disconnect when finished.
