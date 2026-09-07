# Agent Instructions

## Product Best-Practices Guides

NetVsMcp exposes client-facing best-practices guides from `BestPractices/` through the broker's `netvs_get_best_practices` tool and `guide://netvsmcp/...` resources. These files are product assets, not local development-agent skills. Do not load them wholesale while developing this repository.

When editing the exposed guide corpus itself, preserve its progressive-disclosure shape: small entrypoints plus focused reference files. When testing NetVsMcp as a client would use it, access the guides through `netvs_get_best_practices` instead of treating `BestPractices/` as repo instructions.

## Visual Studio Debugging

Key behavior:

- Start by checking registered Visual Studio sessions with the NetVsMcp MCP tools when available.
- If no session exists, infer the intended solution from the current workspace or user request.
- Ask the user to confirm the project or solution only when multiple plausible candidates exist.
- Open a new Visual Studio instance with the selected `.sln` or `.slnx`, then recheck session registration.
- Prefer explicit `sessionId` or `solutionPath` routing when more than one Visual Studio instance is open.
- Confirm before stopping, terminating, or broadly removing breakpoints unless the user explicitly asked for that action.
- When finished, deactivate or remove breakpoints created for the investigation and continue execution if the debuggee is paused.
