# Debugging A Single Test

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

`filter` is required so a broad test run is not accidentally launched under the debugger. The tool runs `dotnet test` with `VSTEST_HOST_DEBUG=1`, waits for the test host, and attaches Visual Studio to it.

After attach succeeds, set breakpoints if needed, then use normal debugger tools. When done, prefer `process_detach` for the returned test host process if cleanup should not kill the process.
