# Automate Visual Studio With NetVsMcp

This is the agent-neutral guide for the UI automation, browser/web debugging, and debuggee console tools exposed through NetVsMcp. Any AI agent can follow it when asked to read or type into a debuggee's console, screenshot or click a debuggee's windows, or connect to and drive a browser during a Visual Studio debugging session.

Read `.agents/skills/debug-visual-studio.md` first for core debug-session workflow (starting, breakpoints, stepping, locals). This guide only covers the `console_*`, `ui_*`, and `web_*` tool families.

## Session Routing

Every tool in this guide accepts the same optional `sessionId`, `solutionName`, and `solutionPath` routing parameters used by the core debug tools. Resolution order is `sessionId`, normalized `solutionPath`, exact `solutionName`, active Visual Studio window, only registered instance. Use explicit routing whenever multiple Visual Studio windows are open.

```json
vs_list_sessions()
console_get_info({ "sessionId": "..." })
```

If the agent environment exposes namespaced MCP tools, use whichever NetVsMcp namespace is connected, such as `mcp__netvs` or `mcp__netvs_mcp`.

## Availability: This Is A Real But Best-Effort Backend, Not A Stub

Unlike the phrasing "when a VSIX ... backend is available" in each tool's catalog description might suggest, these tools are fully implemented in the VSIX (`AutomationCapabilityService` in `src/NetVsMcp.Vsix/Capabilities/AutomationCapabilityService.cs`), not placeholders. They use real Win32/UI Automation APIs, `SendKeys`, `System.Windows.Automation`, screen capture, and a small hand-written Chrome DevTools Protocol (CDP) client. That said, they degrade gracefully and can legitimately fail or return partial data, so treat every response as best-effort and check the fields below before trusting the result:

- Every call returns `ToolResponse<AutomationResult>`. `AutomationResult` has its own `Supported`, `Success`, `Message`, `Text`, and `Metadata` fields nested inside the outer `ToolResponse.Value`. In the current implementation `Supported` is always `true` once the VSIX responds at all (there is no per-tool "not supported" flag from this backend); a failed operation instead reports `Success: false` with an explanation in `Message` (for example "No target window was found to capture.", "No matching UI element was found.", or "JavaScript execution requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.").
- `Metadata` always includes a `backend` key describing which mechanism actually served the call (see the per-family notes below). Use it to tell a real capability (`uia`, `cdp`, `windows-console`) from a degraded fallback (`sendkeys`, `sendkeys-fallback`, `browser-shell-uia`, `http-fetch`, `visual-studio-output`).
- If the outer `ToolResponse.Success` is `false` with `error_code: "invalid_request"` and `failureReason: "CapabilityProfileDenied"`, the broker's active capability profile is blocking the tool category, not a VSIX limitation. `ui_*` and `web_*` tools require the `Admin` capability profile; `console_*` tools require `Debug` or higher. The broker's default profile is `Admin`, so this only occurs if someone lowered the profile in the broker tray UI.
- If the outer `ToolResponse.Success` is `false` with `error_code: "rpc_failure"` or `"session_not_connected"`, this is an ordinary session-routing/connectivity failure, identical to any other routed tool — report it the same way you would a failed `debug_status` call.
- There is no dedicated `VsCapability` entry for automation/browser/UI in `vs_get_capabilities`; you cannot pre-check availability that way. The reliable way to find out whether a given tool will work for the current target is to call it and inspect `backend`/`Success`/`Message`.

## Debuggee Console Interaction

For reading and writing the stdin/stdout of a debuggee console application:

```json
console_get_info({ "target": "MyApp", "sessionId": "..." })
console_read({ "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
console_send({ "text": "some input\n", "target": "MyApp", "sessionId": "..." })
```

Key behavior:

- `target` is optional on all three tools. It may be a process ID or a substring of a debugged process name; when omitted, the tool matches against Visual Studio's currently debugged processes.
- `console_read` first tries to read the debuggee's native console output buffer for the resolved process (`backend: "windows-console"`, with `processId` in metadata). If that is not available (for example, the app is not a console app, or the console buffer cannot be captured), it falls back to reading the Visual Studio Debug/Tests/Build output pane text instead (`backend: "visual-studio-output"`). These are very different data sources — check `backend` before assuming you read real console output.
- `console_send` similarly tries to write directly into the native console input buffer first (`backend: "windows-console"`). If that fails, it resolves a target window, brings it to the foreground, and sends the text as keystrokes via `SendKeys` (`backend: "sendkeys"`). The `SendKeys` fallback requires stealing window focus — expect it to disrupt whatever window currently has focus.
- `console_get_info`, despite its catalog description ("console metadata"), actually enumerates the visible windows owned by the resolved process(es) and returns that as text (`backend: "process-window-enumeration"`, `windowCount` in metadata). It does not return console buffer size/title metadata.

## UI Automation Of The Debuggee's Windows

These drive the debuggee's own Win32/WPF/WinForms windows via UI Automation (`System.Windows.Automation`), with `SendKeys`/mouse-event fallbacks. They act on real screen pixels and real window focus, so avoid interacting with the local machine's other windows while using them.

Capture and inspect:

```json
ui_capture_window({ "target": "MyApp", "sessionId": "..." })
ui_capture_region({ "x": 0, "y": 0, "width": 800, "height": 600, "sessionId": "..." })
ui_snapshot({ "target": "MyApp", "sessionId": "..." })
ui_get_tree({ "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
```

Find and act on elements:

```json
ui_find_elements({ "selector": "type=Button", "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
ui_get_element({ "selector": "id=SubmitButton", "target": "MyApp", "sessionId": "..." })
ui_click({ "selector": "id=SubmitButton", "target": "MyApp", "sessionId": "..." })
ui_double_click({ "selector": "name=Recent Files", "sessionId": "..." })
ui_right_click({ "selector": "id=TreeNode3", "sessionId": "..." })
ui_drag({ "selector": "id=Slider", "x": 400, "y": 120, "sessionId": "..." })
ui_set_value({ "selector": "id=UsernameBox", "text": "alice", "sessionId": "..." })
ui_invoke({ "selector": "id=SubmitButton", "sessionId": "..." })
ui_send_keys({ "text": "{ENTER}", "target": "MyApp", "sessionId": "..." })
ui_wait_for_element({ "selector": "id=ResultsGrid", "timeoutMilliseconds": 10000, "sessionId": "..." })
ui_wait_idle({ "target": "MyApp", "timeoutMilliseconds": 5000, "sessionId": "..." })
```

Key behavior:

- `target` resolves to a process ID or a substring of a debugged process name, same as the console tools; when omitted, all currently debugged processes' windows are used as search roots.
- `selector` is a small `key=value` mini-language, not CSS/XPath: `id=` / `automationid=` (AutomationId), `name=` / `text=` (Name property), `class=` / `classname=` (ClassName), `type=` / `controltype=` (a friendly control type name such as `button`, `edit`/`textbox`, `text`, `window`, `pane`, `document`, `hyperlink`/`link`, `menuitem`, `tabitem`, `listitem`, and similar). A bare selector with no `key=` prefix is matched against Name, AutomationId, and ClassName simultaneously (OR).
- `ui_find_elements`/`ui_get_element`/`ui_wait_for_element` register each match under a generated id like `ui-000123` in `SerializeElement`'s output. Reusing that id as the `selector` in a follow-up `ui_click`/`ui_invoke`/`ui_set_value` call avoids re-running an ambiguous text/name search.
- `ui_click`/`ui_double_click`/`ui_right_click` move the real mouse cursor to the element's bounding-rectangle center and synthesize mouse-down/up events; they do not accept a `timeoutMilliseconds` parameter (element resolution uses a fixed internal timeout).
- `ui_set_value` prefers the UI Automation `ValuePattern`; if the element does not expose it, it activates the element's window and falls back to `Ctrl+A` then `SendKeys` (`backend: "sendkeys-fallback"`).
- `ui_invoke` prefers the UI Automation `InvokePattern`; if unavailable, it falls back to a synthesized left click.
- `ui_capture_window`/`ui_capture_region` return a base64 PNG in `Text` with `mimeType`/`encoding` metadata; `ui_capture_region` takes explicit screen coordinates and does not accept `target` — it captures whatever is at those screen coordinates regardless of debuggee.
- `ui_wait_idle` calls `Process.WaitForInputIdle` per resolved window, which only works for classic Win32 message-pump processes; it is not meaningful for many WPF/modern UI shells.

## Browser/Web Debugging

`web_connect` connects the VSIX to a Chromium-based browser instance (Chrome, Edge, etc.) via its Chrome DevTools Protocol (CDP) endpoint — for example a browser launched with `--remote-debugging-port=9222`. It is not tied to a Visual Studio-managed browser launch; any locally reachable CDP endpoint works.

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

Key behavior:

- `web_connect`'s `target` is the CDP endpoint: a bare port number (interpreted as `http://127.0.0.1:<port>`), an absolute `http://`/`https://` URL, or a `host:port` string. `url` is an optional page URL used to pick the right tab/target on that endpoint.
- **Check the `backend` metadata after connecting, not just `Success`.** If the real CDP handshake fails (`WebException`, `WebSocketException`, `JsonException`, or `InvalidOperationException`), `web_connect` still returns `Success: true` but with `backend: "browser-shell-uia"` and a `cdpMessage` explaining the underlying failure — it has silently degraded to a much weaker fallback mode. If `target` is omitted entirely, no CDP attempt is made at all and, if `url` is set, the OS default browser is simply launched with `Process.Start(url)`.
- With a live CDP connection (`backend: "cdp"`): `web_navigate`, `web_screenshot`, `web_dom_get`/`web_dom_query` (live, JS-rendered DOM via `document.documentElement.outerHTML` / `querySelectorAll`), `web_console` (buffered `Runtime`/console events), `web_network` (buffered `Network` events), `web_js_execute` (arbitrary expression evaluation), and `web_element_click`/`web_element_set_value` (via `document.querySelector(...).click()` / value assignment plus `input`/`change` events) all work as real browser automation.
- Without a live CDP connection (`backend` other than `cdp`): `web_js_execute` fails outright (`Success: false`, asking the caller to `web_connect` with a real CDP endpoint first). `web_console`/`web_network` succeed but return empty text with a `message` noting that CDP is required for real data. `web_dom_get`/`web_dom_query` fall back to a plain unauthenticated HTTP GET of the last connected/navigated URL (`backend: "http-fetch"`) — this returns raw static HTML only, with no JavaScript execution, and fails if no URL was ever connected/navigated. `web_screenshot`/`web_element_click`/`web_element_set_value` fall back to the `ui_*` UI Automation tools against a window matched by `target`, defaulting to `"chrome"` if `target` was not supplied — pass an explicit `target` (e.g. `"msedge"`, `"firefox"`) if the browser under test is not Chrome.
- `web_dom_get`/`web_dom_query` do not accept an explicit `url` parameter from the broker tool signature; they always operate on the URL from the most recent `web_connect`/`web_navigate` call.
- `web_status` reports `connected=true/false; backend=...; target=...; url=...` (plus `websocket=...` when CDP is active) as plain text in `Text`, not structured JSON.

## Reporting

Report findings with evidence:

- The `backend` value actually used for each call, since it determines whether you got a real result or a degraded fallback.
- Screenshot/DOM/console/network payloads obtained, and their size/truncation (`Text` is truncated to roughly the last 20,000 characters for large captures).
- Any `CapabilityProfileDenied` or session-routing failures, quoting the `error_code`/`failureReason`.
- Whether a browser or window was left connected/foregrounded, since `console_send`/`ui_send_keys`/`ui_set_value` fallbacks steal focus and `web_connect` leaves a CDP socket open until `web_disconnect` is called.

## Troubleshooting

- All calls fail with `CapabilityProfileDenied`: the broker's capability profile was lowered below `Admin` (for `ui_*`/`web_*`) or below `Debug` (for `console_*`); ask the user to raise the profile in the broker tray UI, or use `debug_evaluate`/output-pane tools instead if elevation is not possible.
- `ui_*` calls report "No target window was found": confirm a process is actually being debugged (`process_list_debugged`) or pass an explicit `target` process id/name; the debuggee's window may not be visible yet.
- `ui_find_elements`/`ui_click`/etc. report "No matching UI element was found": broaden or fix the selector (bare text matches Name/AutomationId/ClassName; verify with `ui_get_tree` first to see actual property values).
- `web_js_execute`/real `web_console`/`web_network` return nothing useful: `web_connect` degraded to `browser-shell-uia`; retry `web_connect` with a browser actually launched with a remote-debugging port open, and check `cdpMessage` for why the CDP handshake failed.
- `console_read` returns Visual Studio output-pane text instead of real console output: the debuggee's native console buffer could not be captured (not a true console app, wrong `target`, or process exited); treat the output-pane text as a lower-fidelity fallback.
- Screenshots or `SendKeys` interactions seem to hit the wrong window: another window may have stolen focus concurrently; retry, or use a narrower `target`/`selector`.
