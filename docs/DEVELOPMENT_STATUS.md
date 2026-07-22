# Development Status

This file tracks agent orchestration so work can be resumed later.

## Repository Baseline

- Branch: `master`
- Baseline commit: `d0c55dd` - `Start local broker architecture`
- Fresh-start note: the previous single-executable prototype was removed before git initialization, so it is not present in repository history.

## Active Agents

| Agent | ID | Current Status | Current Task | Last Reported Commit |
| --- | --- | --- | --- | --- |
| Jason | `019f8874-afb5-7030-b0f4-7afd147b1c97` | Idle | Completed broker rich navigation tools | `92a080c` |
| Agent E | `client-new-thread:f9ff1f98-3c7d-44a2-8244-b9948c548e7b` | Completed | Broker-routed build/diagnostics MCP tools | `121e892` |
| Agent F | `client-new-thread:22291b11-a133-4af1-ba53-c26b78949be0` | Completed | Broker-routed editor/safe-editing tools | `37262ec` |
| Agent G | `client-new-thread:3fac3be6-225a-4b5a-a1d8-c00300c7a745` | Completed | Broker-routed solution/project/test tools | `260c708` |
| Locke | `019f88e1-5257-7683-b382-205bbf1c935e` | Idle | Completed safe edit preview and advanced debugger skeletons | `c517104` |
| Feynman | `019f89d6-0d09-7813-bfe1-457186641c73` | Completed | Orchestration-only; broker UX completed by Locke | `4ebec1f` |
| Darwin | `019f89d6-5179-76d0-8471-9f3cd6579f54` | Completed | Tracker/orchestration update; contract spec completed by orchestrator | `32bb4c4` |

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

### Jason: Broker VS Session Dispatcher

- Status: Integrated on `master`
- Commit: `c69105a` - `Add broker VS session dispatcher`
- Local build: `dotnet build .\src\NetVsMcp.Broker\NetVsMcp.Broker.csproj` passed with 0 warnings and 0 errors
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 29 tests
- Review status: Reviewed, follow-up issue tracked below
- Files:
  - `src/NetVsMcp.Broker/Services/BrokerRuntime.cs`
  - `src/NetVsMcp.Broker/Services/SessionRegistry.cs`
  - `src/NetVsMcp.Broker/Services/VsSessionConnectionMap.cs`
  - `src/NetVsMcp.Broker/Services/VsSessionDispatchResult.cs`
  - `src/NetVsMcp.Broker/Services/VsSessionDispatcher.cs`
  - `tests/NetVsMcp.Broker.Tests/VsSessionDispatcherTests.cs`

### Jason: VSIX Pipe Registration Connections

- Status: Integrated on `master`
- Commit: `ec19017` - `Wire VSIX pipe registrations to connections`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 35 tests
- Review status: Reviewed
- Files:
  - `src/NetVsMcp.Broker/Services/BrokerRegistrationRpcService.cs`
  - `src/NetVsMcp.Broker/Services/BrokerRuntime.cs`
  - `src/NetVsMcp.Broker/Services/VsixRegistrationPipeListener.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsixRegistrationPipeListenerTests.cs`

### Jason: Broker-Routed Document Tools

- Status: Integrated on `master`
- Commit: `5110c05` - `Expose routed broker document tools`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 40 tests
- Review status: Reviewed
- Files:
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `src/NetVsMcp.Broker/Services/VsSessionDispatchResult.cs`
  - `src/NetVsMcp.Broker/Services/VsSessionDispatcher.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`

### Jason: Broker Session Utility Tools

- Status: Integrated on `master`
- Commit: `01ebf25` - `Add broker session utility tools`
- Reported build: `dotnet build .\src\NetVsMcp.Broker\NetVsMcp.Broker.csproj` passed with 0 warnings and 0 errors
- Reported tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 47 tests
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Review status: Pending full review
- Files:
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`

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

### Locke: VSIX Build Diagnostics Tools

- Status: Integrated on `master`
- Commit: `78c4fa3` - `Add VSIX build diagnostics tools`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: blocked by later uncommitted debugger-slice changes, not by this commit
- Review status: Pending full review
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/BuildCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/BuildModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/BuildRpcTarget.cs`

### Locke: VSIX Debugger Tool Skeletons

- Status: Integrated on `master`
- Commit: `3338ea5` - `Add VSIX debugger tool skeletons`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 34 tests
- Review status: Pending full review
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerRpcTarget.cs`

### Locke: VSIX Debugger Status And Breakpoint Polish

- Status: Integrated on `master`
- Commit: `e47378d` - `Polish VSIX debugger status and breakpoints`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local VSIX build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerRpcTarget.cs`

### Locke: VSIX Capability RPC Target Wiring

- Status: Integrated on `master`
- Commit: `1a28a51` - `Wire VSIX capability RPC target`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 40 tests
- Review status: Reviewed, follow-up issue tracked below
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/BrokerConnection.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`
  - `src/NetVsMcp.Vsix/NetVsMcpPackage.cs`

### Locke: VSIX Broker Registration RPC Alignment

- Status: Integrated on `master`
- Commit: `f7ca112` - `Align VSIX broker RPC contracts`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 47 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/BrokerConnection.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`
  - `src/NetVsMcp.Vsix/NetVsMcpPackage.cs`
  - `src/NetVsMcp.Vsix/RegistrationModels.cs`

### Locke: VSIX Solution Project Test Tools

- Status: Integrated on `master`
- Commit: `41362da` - `Add VSIX solution project test tools`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 47 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/SolutionCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/SolutionModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/SolutionRpcTarget.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityCatalog.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`
  - `src/NetVsMcp.Vsix/NetVsMcpPackage.cs`

### Agent E: Broker Build Diagnostics Tools

- Status: Integrated on `master`
- Commit: `121e892` - `Add broker build diagnostics tools`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 52 tests
- Review status: Reviewed
- Files:
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsSessionDispatcherTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsixRegistrationPipeListenerTests.cs`

### Orchestrator: Tool Contract Specification

- Status: Integrated on `master`
- Commit: `32bb4c4` - `Add tool contract specification`
- Review status: Documentation-only
- Files:
  - `docs/TOOL_CONTRACTS.md`

### Jason: Broker Debugger Tools

- Status: Integrated on `master`
- Commit: `b8352c2` - `Add broker debugger tools`
- Follow-up build fix: `9f2f8ef` - `Fix VSIX debugger skeleton build`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 65 tests
- Review status: Pending full review
- Files:
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/**`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerModels.cs`

### Jason: Broker Editor Mutation Tools

- Status: Integrated on `master`
- Commit: `37262ec` - `Add broker editor mutation tools`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 82 tests
- Review status: Pending full review
- Files:
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsSessionDispatcherTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsixRegistrationPipeListenerTests.cs`

### Jason: Broker Solution Project Test Tools

- Status: Integrated on `master`
- Commit: `260c708` - `Add broker solution project test tools`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 92 tests
- Review status: Pending full review
- Files:
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsSessionDispatcherTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsixRegistrationPipeListenerTests.cs`

### Jason: Broker Rich Navigation Tools

- Status: Integrated on `master`
- Commit: `92a080c` - `Add broker rich navigation tools`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 96 tests
- Review status: Pending full review
- Files:
  - `src/NetVsMcp.Contracts/BrokerContracts.cs`
  - `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerToolServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/BrokerRegistrationRpcServiceTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsSessionDispatcherTests.cs`
  - `tests/NetVsMcp.Broker.Tests/VsixRegistrationPipeListenerTests.cs`

### Locke: Broker Tray Status UX

- Status: Integrated on `master`
- Commit: `7a89bb6` - `Improve broker tray status UX`
- Review fix: `36d5698` - `Marshal broker status refresh to UI thread`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 96 tests
- Review status: Reviewed with UI-thread fix
- Files:
  - `README.md`
  - `docs/BROKER_UX.md`
  - `src/NetVsMcp.Broker/App.xaml.cs`
  - `src/NetVsMcp.Broker/MainWindow.xaml`
  - `src/NetVsMcp.Broker/MainWindow.xaml.cs`
  - `src/NetVsMcp.Broker/Services/AutostartService.cs`
  - `src/NetVsMcp.Broker/Services/TrayIconController.cs`
  - `src/NetVsMcp.Broker/ViewModels/MainWindowViewModel.cs`

### Locke: VSIX Safe Editor Mutation Tools

- Status: Integrated on `master`
- Commit: `0cdef6b` - `Add VSIX safe editor mutation tools`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 52 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/EditorCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorRpcTarget.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityCatalog.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`

### Locke: VSIX Safe Edit Preview Queue

- Status: Integrated on `master`
- Commit: `feba4a1` - `Add VSIX safe edit preview queue`
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 65 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/EditorCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/EditorRpcTarget.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`

### Locke: VSIX Advanced Debugger Skeletons

- Status: Integrated on `master`
- Commits:
  - `9f2f8ef` - `Fix VSIX debugger skeleton build`
  - `c517104` - `Add VSIX advanced debugger skeleton tools`
- Reported build: `dotnet build .\src\NetVsMcp.Vsix\NetVsMcp.Vsix.csproj` passed with 0 warnings and 0 errors
- Local solution build: `dotnet build .\NetVsMcp.slnx` passed with 0 warnings and 0 errors
- Local broker tests: `dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj` passed with 65 tests
- Review status: Reviewed
- Files:
  - `docs/VSIX.md`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerCapabilityService.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerModels.cs`
  - `src/NetVsMcp.Vsix/Capabilities/DebuggerRpcTarget.cs`
  - `src/NetVsMcp.Vsix/Capabilities/VisualStudioCapabilityRpcTarget.cs`

## Current Agent Tasks

### Agent E: Broker-Routed Build/Diagnostics Tools

Write scope:

- `src/NetVsMcp.Contracts/**`
- `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
- `src/NetVsMcp.Broker/Services/VsSessionDispatcher.cs` if needed
- `tests/NetVsMcp.Broker.Tests/**`

Expected output:

- extend broker-side RPC contracts/models for `BuildSolutionAsync`, `BuildStatusAsync`, `ErrorsListAsync`, and `OutputReadAsync`
- add MCP tools `build_solution`, `build_status`, `errors_list`, and `output_read`
- reuse existing routed-tool error behavior for no session, ambiguity, stale session, missing connection, and VSIX RPC failure
- add fake-RPC success tests and at least one routing failure test
- build/test result
- commit hash

Status:

- Completed by `cd0511d`.
- First dispatcher abstraction completed by `c69105a`.
- Pipe registration-to-connection wiring completed by `ec19017`.
- Routed document tools completed by `5110c05`.
- Session utility tools completed by `01ebf25`.
- Build diagnostics tools completed by `121e892`.
- Debugger tools completed by `b8352c2`.
- Editor mutation and safe-editing tools completed by `37262ec`.

### Locke: VSIX Advanced Debugger Skeletons

Write scope:

- `src/NetVsMcp.Vsix/**`
- `docs/VSIX.md`

Expected output:

- VSIX-side advanced debugger skeletons from the plan, such as watches, thread/process helpers, modules, exception settings, or immediate-window execution
- prefer real EnvDTE behavior where straightforward
- return explicit unsupported results where native/debugger-specific APIs are not safe in this slice
- update `VisualStudioCapabilityRpcTarget`
- document method names and response shapes
- build result
- commit hash

Status:

- Build diagnostics completed by `78c4fa3`.
- Debugger tools completed by `3338ea5`.
- Debugger polish completed by `e47378d`.
- Capability RPC target wiring completed by `1a28a51`.
- Broker registration RPC alignment completed by `f7ca112`.
- Solution/project/test operations completed by `41362da`.
- Safe editor mutation tools completed by `0cdef6b`.
- Safe edit preview queue completed by `feba4a1`.
- Advanced debugger skeletons completed by `9f2f8ef` and `c517104`.

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

Status:

- Original Feynman task returned orchestration/status updates instead of code.
- Implemented by Locke in `7a89bb6`; UI-thread review fix committed as `36d5698`.

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

### Agent F: Broker-Routed Editor/Navigation/Safe-Editing Tools

Write scope:

- `src/NetVsMcp.Contracts/**`
- `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
- `tests/NetVsMcp.Broker.Tests/**`

Expected output:

- broker-routed MCP tools for document read/open, selection, document mutation, edit preview queue, and definition/reference navigation
- shared DTOs aligned by property name with VSIX models
- fake-RPC broker tests
- build/test result
- commit hash

Status:

- Editor read/open, mutation, cleanup, and safe-edit preview tools completed by `37262ec`.
- Definition/reference broker routing completed by `92a080c`.

### Agent G: Broker-Routed Solution/Project/Test Tools

Write scope:

- `src/NetVsMcp.Contracts/**`
- `src/NetVsMcp.Broker/Services/BrokerToolService.cs`
- `tests/NetVsMcp.Broker.Tests/**`

Expected output:

- broker-routed MCP tools for solution info, projects, startup project, and test skeleton responses
- shared DTOs aligned by property name with VSIX models
- fake-RPC broker tests
- build/test result
- commit hash

Status:

- Completed by `260c708`.

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
- Status: Mostly resolved by `1a28a51`, which attaches a combined VSIX capability RPC target to the broker pipe connection. Runtime validation still pending.

### VSIX Broker Registration RPC Method Names

- File: `src/NetVsMcp.Vsix/BrokerConnection.cs`
- Commit: `1a28a51`
- Issue: VSIX registration still invokes `RegisterVisualStudioSessionAsync`, `HeartbeatVisualStudioSessionAsync`, and `UnregisterVisualStudioSessionAsync`, while the broker exposes `RegisterAsync`, `HeartbeatAsync`, and `UnregisterAsync`.
- Impact: the pipe can connect, but VSIX registration calls may fail to resolve at runtime.
- Follow-up: Locke is aligning method names and DTOs with `IBrokerRegistrationRpc`.

### VSIX Breakpoint Removal Path Resolution

- File: `src/NetVsMcp.Vsix/Capabilities/DebuggerCapabilityService.cs`
- Commit: `3338ea5`
- Issue: breakpoint setting resolves relative document paths against the solution directory, but breakpoint removal compares `request.DocumentPath` with `Path.GetFullPath(request.DocumentPath)` using the process working directory.
- Impact: removing a breakpoint by relative file path may fail even when setting the same relative path succeeded.
- Follow-up: Locke is fixing this in the debugger polish slice.

## Next Tasks

After current tasks are integrated:

1. Wire broker named-pipe endpoint to VSIX registration contract.
2. Add broker HTTP MCP skeleton and `vs_list_sessions`.
3. Add broker tray status window session list.
4. Add VSIX `document_active` implementation.
5. Add `code_document_symbols` through Visual Studio workspace/language services.
