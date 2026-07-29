# Agent Instructions

## Visual Studio Skill Guides

This repository documents its NetVsMcp MCP tools as a set of agent-neutral skill guides under `.agents/skills/`, each with a matching Codex adapter under `.codex/skills/`. Read whichever guide matches the task at hand before using its tools:

- Session routing, launching Visual Studio, windows, solutions, projects, and tests: `.agents/skills/manage-visual-studio.md` (`.codex/skills/manage-visual-studio/SKILL.md`)
- Debugging (breakpoints, stepping, locals, watches, threads, processes): `.agents/skills/debug-visual-studio.md` (`.codex/skills/debug-visual-studio/SKILL.md`)
- Documents, direct edits, selection, and the safe-edit preview/approve workflow: `.agents/skills/edit-visual-studio.md` (`.codex/skills/edit-visual-studio/SKILL.md`)
- Code navigation, symbol/reference lookup, diagnostics, and search: `.agents/skills/navigate-visual-studio.md` (`.codex/skills/navigate-visual-studio/SKILL.md`)
- Build, output panes, and NuGet packages: `.agents/skills/build-visual-studio.md` (`.codex/skills/build-visual-studio/SKILL.md`)
- UI automation, browser/web debugging, and debuggee console I/O: `.agents/skills/automate-visual-studio.md` (`.codex/skills/automate-visual-studio/SKILL.md`)

## Visual Studio Debugging

Key behavior:

- Start by checking registered Visual Studio sessions with the NetVsMcp MCP tools when available.
- If no session exists, infer the intended solution from the current workspace or user request.
- Ask the user to confirm the project or solution only when multiple plausible candidates exist.
- Open a new Visual Studio instance with the selected `.sln` or `.slnx`, then recheck session registration.
- Prefer explicit `sessionId` or `solutionPath` routing when more than one Visual Studio instance is open.
- Confirm before stopping, terminating, or broadly removing breakpoints unless the user explicitly asked for that action.
- When finished, deactivate or remove breakpoints created for the investigation and continue execution if the debuggee is paused.
