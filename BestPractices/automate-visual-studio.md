# Automate Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Read debug references only for debugger control or state inspection.
- `console_*` is served from `/mcp`; `ui_*` and `web_*` are served only from `/mcp-wu`.
- These tools are real but best-effort. Check `Success`, `Message`, and `Metadata.backend`.
- UI and SendKeys fallbacks act on real windows, focus, mouse, and pixels.
- Prefer dedicated browser tooling when available. Use `web_*` as fallback or for browser/debuggee work routed through Visual Studio; prefer its CDP backend over lower-fidelity modes.
- Disconnect browser sessions when the task is done.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Understand endpoint split and backend reporting | `references/endpoint-backends.md` |
| Read or type into a debuggee console | `references/console.md` |
| Screenshot, inspect, click, type, or wait on debuggee windows | `references/ui-automation.md` |
| Connect to, navigate, inspect, script, or screenshot a browser | `references/browser-cdp.md` |

## Covered Tools

`console_*`, `ui_*`, and `web_*`.
