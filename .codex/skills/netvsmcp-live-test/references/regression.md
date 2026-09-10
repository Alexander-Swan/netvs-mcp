# Live Regression Procedure

Run this after the selected Debug or Release instance is installed, running, connected, and visible through MCP.

## Tool Inventory

1. Enumerate every available NetVsMcp MCP tool in the selected server, including optional web/UI automation tools if that endpoint is part of the test.
2. For each tool, read its description before calling it.
3. Record non-argument instructions from the description, especially:
   - required prerequisite tools or guide calls;
   - warnings that another tool should be preferred instead;
   - routing requirements for multiple Visual Studio sessions;
   - state requirements such as "debuggee must be paused" or "preview must be created first";
   - cleanup requirements.
4. Ignore purely argument-shape guidance for the purpose of this instruction log, though still pass valid arguments when invoking the tool.

## Coverage Strategy

Call every tool at least once unless doing so would require unsafe external side effects that cannot be contained. Use the safest meaningful invocation for each tool:

- Management/session tools: list sessions, inspect context, and verify explicit routing.
- Best-practices tools/resources: call the entrypoint or focused reference required by the tool family being tested.
- Document/editor tools: use a disposable document or a reversible edit preview, then undo/revert.
- Navigation and symbol tools: use stable symbols in the loaded solution.
- Build and diagnostics tools: prefer read-only status first; run builds only when the user requested full live testing and the solution is expected to build.
- Test tools: discover first, then run a small filtered test when a safe filter is available.
- Debugger tools: start or attach only to a controlled debug target, pause at an intentional breakpoint, exercise stack/locals/evaluate/step/status tools, then remove breakpoints and restore execution state.
- Process tools: inspect broadly if safe, but terminate or detach only from throwaway processes started for the test.
- UI/web automation tools: use an app or page created for the test; do not drive unrelated user apps or browsers.
- Analytics/log tools: verify data shape and recent entries without exposing sensitive payloads in the final report.

If a tool cannot be safely called, record it as skipped with the concrete reason and what prerequisite would make it testable.

## Result Tracking

Maintain a table or checklist during the run with:

- tool name;
- setup or prerequisite instruction noticed;
- invocation summary;
- pass/fail/skip status;
- evidence or returned high-level result;
- cleanup performed or still needed.

Do not paste large MCP responses into the final report. Summarize failures and notable results, and keep raw logs in a local artifact only when the user asks for one.

## Final Cleanup And Report

Before reporting completion:

1. Remove test breakpoints and temporary debugger state.
2. Revert all test edits, generated temporary files, and repository changes made by the live test.
3. Confirm `git status --short` is back to its initial state except for user-owned changes that existed before the run.
4. Call the broker analytics or usage-summary tool after the live pass, normally `vs_get_usage_summary`, and include the returned high-level analytics result in the report. Summarize counts, retention/configuration, and notable recent-tool outcomes without pasting sensitive raw payloads.
5. Report the tested mode, broker endpoint, Visual Studio instance, extension/broker version evidence when available, tool coverage counts, failures, skips, analytics summary, and cleanup status.
