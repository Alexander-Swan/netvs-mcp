# Agent Instructions

## Visual Studio Debugging

Use the agent-neutral debug guide when asked to debug, inspect, launch, attach to, pause, step through, or diagnose this Visual Studio solution with NetVsMcp:

- Canonical workflow: `.agents/skills/debug-visual-studio.md`
- Codex adapter: `.codex/skills/debug-visual-studio/SKILL.md`

Key behavior:

- Start by checking registered Visual Studio sessions with the NetVsMcp MCP tools when available.
- If no session exists, infer the intended solution from the current workspace or user request.
- Ask the user to confirm the project or solution only when multiple plausible candidates exist.
- Open a new Visual Studio instance with the selected `.sln` or `.slnx`, then recheck session registration.
- Prefer explicit `sessionId` or `solutionPath` routing when more than one Visual Studio instance is open.
- Confirm before stopping, terminating, or broadly removing breakpoints unless the user explicitly asked for that action.
- When finished, deactivate or remove breakpoints created for the investigation and continue execution if the debuggee is paused.
