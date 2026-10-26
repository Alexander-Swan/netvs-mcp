# Debugging A Single Test

The examples below use the default CLI backend. For IDE-selected tests, see the [Test Explorer backend](../../manage-visual-studio/references/test-explorer.md); its debug response reports acceptance, so verify the actual pause before inspecting a test.

Use `test_debug` when the user wants to step through a specific test rather than merely run it:

```json
test_debug({
  "projectName": "App.Tests",
  "filter": "FullyQualifiedName~OrderTests.SubmitsOrder",
  "attachTimeoutSeconds": 30,
  "noBuild": true,
  "configuration": "Debug",
  "framework": "net10.0",
  "sessionId": "..."
})
```

`projectName`, `framework`, and a nonempty `filter` are required. A filter can match multiple cases; result case fingerprints are not execution selectors. Read `debug_status` first and select an idle session. The tool preserves an existing debugger target and rejects setup in an occupied session.

Set required source breakpoints before starting a fast test. The tool runs `dotnet test` with `VSTEST_HOST_DEBUG=1`, verifies a unique descendant testhost of its exact runner using Windows process ancestry, creation identity, and managed entry point, and attaches Visual Studio. Missing ownership evidence or multiple hosts fails closed. Older extensions without the ownership-capable RPC return unsupported; the broker does not fall back to guessing a process.

Check `value.supported` and the returned host/runner process IDs before assuming attachment succeeded, then use normal debugger tools and wait for the intended breakpoint. Failed or canceled setup cleans up verified owned waiting hosts and its runner. A client timeout does not prove setup stopped: inspect the routed session and returned operation/process evidence before retrying. Confirm before stopping or terminating a target unless the user already authorized cleanup. Detach only from the exact returned host if the process should continue.

VSTest can intentionally pause in `Microsoft.VisualStudio.TestPlatform.Execution.DebuggerBreakpoint.Break` / `WaitForDebugger` immediately after attachment. That is its entry handshake, even when `breakReason` says `breakpoint`; it is not evidence that the test-method breakpoint was reached. Read the returned call stack, continue once only after recognizing that shim, then call `debug_wait_for_break` again and verify the expected test method. Inspect unexpected exceptions or other stops rather than continuing blindly. Successful `test_debug` responses also include this handoff in `metadata.nextActions`.
