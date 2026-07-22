# Development Status

This file tracks agent orchestration so work can be resumed later.

## Repository Baseline

- Branch: `master`
- Baseline commit: `d0c55dd` - `Start local broker architecture`
- Fresh-start note: the previous single-executable prototype was removed before git initialization, so it is not present in repository history.

## Active Agents

| Agent | ID | Current Status | Current Task | Last Reported Commit |
| --- | --- | --- | --- | --- |
| Jason | `019f8874-afb5-7030-b0f4-7afd147b1c97` | Running | Normalize solution path routing | `0364eda` observed on `master` |
| Locke | `019f88e1-5257-7683-b382-205bbf1c935e` | Running | VSIX document symbols navigation service | Pending |

## Completed Agent Tasks

### Lagrange: VSIX Skeleton

- Status: Integrated on `master`
- Commit: `0d7b9c0` - `Add Visual Studio extension skeleton`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Review status: Reviewed, follow-up issues tracked below
- Reported files:
  - `NetVsMcp.slnx`
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/**`

### Jason: Contracts + Broker Skeleton

- Status: Integrated on `master`
- Commit: `3555c6f` - `Add broker and contracts skeleton`
- Reported build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Review status: Reviewed, follow-up issues tracked below
- Reported files:
  - `src/NetVsMcp.Contracts/**`
  - `src/NetVsMcp.Broker/**`
  - `README.md`

### Jason: Broker HTTP Session Tools

- Status: Integrated on `master`
- Commit: `6f57956` - `Add broker HTTP session tools`
- Reported build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Reported tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 8 tests
- Review status: Reviewed, follow-up issues tracked below
- Reported files:
  - `src/NetVsMcp.Broker/Services/LocalMcpHttpHost.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `src/NetVsMcp.Broker/Services/BrokerRuntime.cs`
  - `src/NetVsMcp.Broker/NetVsMcp.Broker.csproj`
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `tests/NetVsMcp.Broker.Tests/**`
  - `NetVsMcp.slnx`

### Jason: Broker Named-Pipe VSIX Registration Endpoint

- Status: Integrated on `master`, but agent final message not received yet
- Commit: `0364eda` - `Add VSIX registration pipe listener`
- Local build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors on retry
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 15 tests
- Review status: Pending full review
- Observed files:
  - `src/NetVsMcp.Broker/Services/VsixRegistrationPipeListener.cs`
  - `src/NetVsMcp.Broker/Services/BrokerRegistrationRpcService.cs`
  - `src/NetVsMcp.Broker/Services/BrokerRuntime.cs`
  - `src/NetVsMcp.Broker/NetVsMcp.Broker.csproj`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`

### Lagrange: VSIX Registration + Heartbeat

- Status: Integrated on `master`
- Commit: `9f9ccd5` - `Add VSIX broker registration lifecycle`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors on retry
- Review status: Pending full review
- Reported files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/ActiveWindowTracker.cs`
  - `src/NetVsMcp.Vsix/BrokerConnection.cs`
  - `src/NetVsMcp.Vsix/BrokerPipeName.cs`
  - `src/NetVsMcp.Vsix/BrokerRegistrationLifecycle.cs`
  - `src/NetVsMcp.Vsix/VisualStudioStateChangeMonitor.cs`

### Lagrange: First VSIX Editor Tools

- Status: Integrated on `master`
- Commit: `3c1ec57` - `Add VSIX editor tool services`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors on retry
- Review status: Reviewed, follow-up issues tracked below
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/EditorCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorRpcTarget.cs`

## Current Agent Tasks

### Jason: Normalize Solution Path Routing

Write scope:

- `src/NetVsMcp.Broker/**`
- `src/NetVsMcp.Contracts/**` only if needed
- `tests/NetVsMcp.Broker.Tests/**`

Expected output:

- normalized solution paths at registration/update and target resolution
- expanded routing tests
- build/test result
- commit hash

### Locke: VSIX Document Symbols Navigation Service

Write scope:

- `src/NetVsMcp.Vsix/**`
- `docs/VSIX.md`

Expected output:

- VSIX-side method/interface for `code_document_symbols`
- symbol name, kind, file, line/column, containing type/namespace where practical
- docs update with expected RPC method names/inputs/outputs
- build result
- commit hash

## Review Checklist

- Confirm each agent committed only its intended write scope.
- Confirm no single-executable prototype files return.
- Confirm `.slnx` contains only the new planned projects.
- Build full solution after integration.
- Review public API shape in `NetVsMcp.Contracts` before wiring VSIX to broker.
- Keep references to external example projects out of public docs.

## Review Findings

### Broker Runtime Status

- File: `src/NetVsMcp.Broker/Services/BrokerRuntime.cs`
- Lines: 35-43 at commit `3555c6f`
- Issue: `StartAsync` sets `IsHttpEndpointRunning = true` even though no HTTP listener is started yet.
- Impact: tray/status window and MCP setup instructions can claim the broker is reachable when no MCP HTTP endpoint exists.
- Status: Resolved by `6f57956`, which starts a Kestrel listener on `127.0.0.1:5050`.

### HTTP MCP Protocol Placeholder

- File: `src/NetVsMcp.Broker/Services/LocalMcpHttpHost.cs`
- Commit: `6f57956`
- Issue: HTTP routes expose placeholder JSON endpoints under `/mcp/tools/*`, not the actual MCP HTTP transport yet.
- Impact: useful for broker status/dev smoke testing, but MCP clients cannot register this as a real HTTP MCP server yet.
- Follow-up: replace placeholder routes with proper MCP HTTP transport after broker/VSIX registration is wired.

### VSIX Heartbeat Lifecycle

- File: `src/NetVsMcp.Vsix/BrokerRegistrationLifecycle.cs`
- Lines: 22, 31-40, 52-64 at commit `0d7b9c0`
- Issue: the timer callback fire-and-forgets async heartbeat work without exception handling, and dispose does not unregister the session.
- Impact: broker can retain stale sessions after VS closes, and heartbeat failures may be swallowed or surface unpredictably.
- Status: Mostly resolved by `9f9ccd5`, which replaced the timer with a guarded connection loop, reconnect/backoff, and unregister/disconnect semantics. Needs end-to-end broker pipe validation.

### Solution Path Routing

- File: `src/NetVsMcp.Broker/Services/SessionRegistry.cs`
- Lines: 151-154 at commit `3555c6f`
- Issue: solution path routing compares raw strings without path normalization.
- Impact: equivalent paths with different slash style, casing, or relative segments may fail to route.
- Follow-up: normalize solution paths at registration/update and target resolution.

### VSIX Editor RPC Wiring

- File: `src/NetVsMcp.Vsix/Capabilities/EditorRpcTarget.cs`
- Commit: `3c1ec57`
- Issue: editor methods exist behind `EditorRpcTarget`, but that target is not yet attached to a broker-facing JSON-RPC server/client path.
- Impact: service code is ready for broker invocation but not reachable end-to-end.
- Follow-up: when bidirectional VSIX session RPC is added, expose `document_active`, `document_read`, `document_open`, and `selection_get` through the shared broker routing path.

## Next Tasks

After current tasks are integrated:

1. Wire broker named-pipe endpoint to VSIX registration contract.
2. Add broker HTTP MCP skeleton and `vs_list_sessions`.
3. Add broker tray status window session list.
4. Add VSIX `document_active` implementation.
5. Add `code_document_symbols` through Visual Studio workspace/language services.
