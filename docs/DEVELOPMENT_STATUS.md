# Development Status

This file tracks agent orchestration so work can be resumed later.

## Repository Baseline

- Branch: `master`
- Baseline commit: `d0c55dd` - `Start local broker architecture`
- Fresh-start note: the previous single-executable prototype was removed before git initialization, so it is not present in repository history.

## Active Agents

| Agent | ID | Current Status | Current Task | Last Reported Commit |
| --- | --- | --- | --- | --- |
| Jason | `019f8874-afb5-7030-b0f4-7afd147b1c97` | Running | Broker routed tool dispatcher tests/follow-up | `cd0511d` from endpoint hardening |
| Locke | `019f88e1-5257-7683-b382-205bbf1c935e` | Running | VSIX build + diagnostics service skeletons | `c371042` from prior navigation task |
| Agent C | `client-new-thread:22291b11-a133-4af1-ba53-c26b78949be0` | Queued | Broker tray/status UX and autostart planning implementation | None yet |
| Agent D | `client-new-thread:3fac3be6-225a-4b5a-a1d8-c00300c7a745` | Queued | Shared broker-to-VSIX routed tool contracts | None yet |
| Agent D | `client-new-thread:d33e0211-d74f-432a-96f2-c666162f18f8` | Queued | Tool/RPC contract specification for broker-routed VSIX tools | None yet |
| Feynman | `019f89d6-0d09-7813-bfe1-457186641c73` | Running | Broker tray/status UX and autostart service | Pending |
| Darwin | `019f89d6-5179-76d0-8471-9f3cd6579f54` | Running | Tool/RPC contract specification | Pending |

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

### Jason: Normalize Solution Path Routing

- Status: Integrated on `master`
- Commit: `a9905ba` - `Normalize solution path routing`
- Reported build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 20 tests
- Review status: Reviewed
- Files:
  - `src/NetVsMcp.Broker/Services/SolutionPathNormalizer.cs`
  - `src/NetVsMcp.Broker/Services/SessionRegistry.cs`
  - `tests/NetVsMcp.Broker.Tests/SessionRegistryTests.cs`

### Jason: MCP HTTP Transport For Broker Tools

- Status: Integrated on `master`
- Commit: `ca4fcda` - `Use MCP HTTP transport for broker tools`
- Local build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors after Locke's navigation fix
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 21 tests
- Review status: Reviewed, follow-up issues tracked below
- Files:
  - `src/NetVsMcp.Broker/NetVsMcp.Broker.csproj`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `src/NetVsMcp.Broker/Services/LocalMcpHttpHost.cs`
  - `tests/NetVsMcp.Broker.Tests/LocalMcpHttpHostTests.cs`

### Jason: Harden Broker MCP Endpoint Validation

- Status: Integrated on `master`
- Commit: `cd0511d` - `Harden broker MCP endpoint validation`
- Review status: Pending
- Files:
  - `src/NetVsMcp.Broker/Services/LocalMcpHttpHost.cs`
  - `tests/NetVsMcp.Broker.Tests/LocalMcpHttpHostTests.cs`

### Orchestrator: Expanded Agent Workstreams

- Status: Integrated on `master`
- Commit: `24a5338` - `Track expanded agent workstreams`
- Review status: Tracking-only commit
- Notes:
  - Added follow-up orchestration for broker dispatcher, VSIX build/diagnostics, and upcoming UX/contracts lanes.

### Locke: VSIX Document Symbols Navigation Service

- Status: Integrated on `master`
- Commit: `85ba7db` - `Add VSIX document symbols service`
- Local build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors after later navigation fix
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationRpcTarget.cs`

### Locke: VSIX Definition And Reference Navigation

- Status: Integrated on `master`
- Commit: `c371042` - `Add VSIX definition and reference navigation`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 21 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/NavigationRpcTarget.cs`

## Current Agent Tasks

### Jason: Harden Broker HTTP Endpoint Validation

Write scope:

- `src/NetVsMcp.Broker/**`
- `tests/NetVsMcp.Broker.Tests/**`

Expected output:

- `localhost` and loopback IPs handled deliberately
- non-loopback hosts rejected
- endpoint validation tests
- optional MCP initialize/tools-list smoke test
- build/test result
- commit hash

Status:

- Completed by `cd0511d`.
- Jason is now continuing into broker routed tool dispatcher tests/follow-up work.

### Locke: VSIX Build + Diagnostics Service Skeletons

Write scope:

- `src/NetVsMcp.Vsix/**`
- `docs/VSIX.md`

Expected output:

- VSIX-side methods/RPC target methods for `build_solution`, `build_status`, `errors_list`, and `output_read`
- structured models for build status, errors, and output text
- docs update with expected RPC method names/inputs/outputs
- build result
- commit hash

### Agent C: Broker Tray/Status UX And Autostart

Write scope:

- `src/NetVsMcp.Broker/App.xaml*`
- `src/NetVsMcp.Broker/MainWindow.xaml*`
- `src/NetVsMcp.Broker/ViewModels/**`
- `src/NetVsMcp.Broker/Services/TrayIconController.cs`
- `src/NetVsMcp.Broker/Services/AutostartService.cs` if added
- `docs/BROKER_UX.md` if added

Expected output:

- richer broker status window with running status, endpoint, pipe name, MCP config, registered VS sessions, and health
- tray menu with status/open/copy config/refresh/autostart/logs/exit actions
- autostart service abstraction or documented placeholder
- build result
- commit hash

### Agent D: Shared Routed Tool Contracts

Write scope:

- `src/NetVsMcp.Contracts/**`
- `tests/NetVsMcp.Contracts.Tests/**` if added
- `NetVsMcp.slnx` if a test project is added
- `docs/RPC.md` if added

Expected output:

- shared DTOs for broker-routed VSIX tool requests/responses
- request/correlation IDs, routing target, status/error shape
- JSON-RPC method naming documentation
- build/test result
- commit hash

### Feynman: Broker Tray/Status UX And Autostart

Write scope:

- `src/NetVsMcp.Broker/App.xaml*`
- `src/NetVsMcp.Broker/MainWindow.xaml*`
- `src/NetVsMcp.Broker/ViewModels/**`
- `src/NetVsMcp.Broker/Services/TrayIconController.cs`
- `src/NetVsMcp.Broker/Services/AutostartService.cs` if added
- `docs/BROKER_UX.md` if added

Expected output:

- richer status window/tray UX
- MCP config copy/display
- registered VS sessions with health where available
- autostart service abstraction or documented placeholder
- build result
- commit hash

### Darwin: Tool/RPC Contract Specification

Write scope:

- `docs/TOOL_CONTRACTS.md`
- `docs/PLAN.md` if needed
- `docs/DEVELOPMENT_STATUS.md` only if needed

Expected output:

- broker MCP tool method names and routing fields
- VSIX RPC target method names and request/response shapes
- error behavior
- ownership by Broker vs VSIX
- open design questions
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
- Status: Resolved by `ca4fcda`, which maps MCP Streamable HTTP at `/mcp` and exposes broker tools with MCP tool attributes.

### HTTP Endpoint Host Validation

- File: `src/NetVsMcp.Broker/Services/LocalMcpHttpHost.cs`
- Commit: `ca4fcda`
- Issue: endpoint validation uses `IPAddress.Parse(uri.Host)`, so hostnames such as `localhost` fail with a parsing exception rather than a deliberate loopback validation result.
- Impact: default `127.0.0.1` works, but config robustness is weaker than intended.
- Follow-up: Jason is hardening endpoint validation and tests.

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
- Status: Resolved by `a9905ba`, with tests covering normalized path routing and ambiguity behavior.

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
