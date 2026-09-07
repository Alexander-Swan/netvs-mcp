# Automate Visual Studio With NetVsMcp

Agent-neutral entrypoint for debuggee console I/O, UI automation, screenshots, browser control, DOM inspection, JavaScript execution, and web diagnostics through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling `console_*`, `ui_*`, or `web_*`, then only the matching reference.
- Do not automatically load the full debug guide. Load debug references only when the task also needs starting, stepping, breakpoints, or debug-state inspection.
- For learning or local skill creation, read references deliberately and keep generated skills as routers with references.

## First Rules

- `console_*` is served from `/mcp`; `ui_*` and `web_*` are served only from `/mcp-wu`.
- These tools are real but best-effort. Check `Success`, `Message`, and `Metadata.backend`.
- UI and SendKeys fallbacks act on real windows, focus, mouse, and pixels.
- Prefer CDP-backed browser control for web tasks; fallback modes are lower fidelity.
- Disconnect browser sessions when the task is done.

## Reference Files

| Task | Read |
| --- | --- |
| Understand endpoint split and backend reporting | `automate-visual-studio/references/endpoint-backends.md` |
| Read or type into a debuggee console | `automate-visual-studio/references/console.md` |
| Screenshot, inspect, click, type, or wait on debuggee windows | `automate-visual-studio/references/ui-automation.md` |
| Connect to, navigate, inspect, script, or screenshot a browser | `automate-visual-studio/references/browser-cdp.md` |

## Covered Tools

`console_*`, `ui_*`, and `web_*`.
