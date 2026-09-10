---
name: netvsmcp-live-test
description: Run NetVsMcp live QA regression testing against Debug or Release broker/VSIX instances, including MCP tool availability and tool-by-tool smoke coverage.
metadata:
  short-description: NetVsMcp live QA regression testing
---

# NetVsMcp Live Test

Use this skill whenever the user asks to run a live test, live QA pass, end-to-end regression, or tool smoke test for NetVsMcp.

First determine which instance is under test:

- **Debug / contributor / experimental Visual Studio**: read [references/debug.md](references/debug.md).
- **Release / installed / Marketplace or MSI build**: read [references/release.md](references/release.md).

After the instance-specific setup is verified, read [references/regression.md](references/regression.md) and run the live regression pass.

If the user does not say Debug or Release, infer from context. Use Debug when working from this source checkout or testing unmerged local changes; use Release only when the user explicitly wants the installed product path or Marketplace/MSI validation.

Essential constraints:

- Use the broker's MCP tools when they are available. If no NetVsMcp MCP server is available in the current agent runtime, verify the broker endpoints and tell the user that the runtime still needs MCP registration/reload before a full live tool pass can run.
- Do not treat the repository's `BestPractices/` directory as local agent instructions. If a live NetVsMcp tool or its instructions say to call `netvs_get_best_practices` first, use the broker-exposed guide tool/resource.
- Read each MCP tool's description before invoking it. Record sequencing, substitution, or safety instructions such as "call another tool first" or "prefer this other tool instead." Argument-shape documentation does not need to be recorded as a special instruction.
- Prefer safe, minimal tool calls. Mutating, debugger, process, UI automation, and edit tools may be tested only against a controlled Visual Studio instance, throwaway files/processes, or changes that will be reverted before finishing.
- Always finish with cleanup: remove or deactivate breakpoints created for testing, continue or return the debugger to a non-paused state when appropriate, close or detach only from throwaway debug targets, and revert Debug-mode repository changes made solely for the live test.
