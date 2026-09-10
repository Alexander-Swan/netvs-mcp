# Release Live Test Setup

Use this path for installed-product testing through the Release broker and Release Visual Studio extension.

## Expected Release Defaults

- Broker endpoint: `http://127.0.0.1:5050/mcp`
- Web/UI automation endpoint: `http://127.0.0.1:5050/mcp-wu`
- Health endpoint: `http://127.0.0.1:5050/health`
- MCP server names: `netvs` and `netvs-web-automation`
- Pipe prefix: `netvs-mcp-`

## Setup Checklist

1. Verify the Release broker is installed and running. Use the health endpoint and, when possible, the broker status UI or process information.
2. Verify the agent runtime has MCP tools from the Release broker available. If the broker is healthy but tools are absent, the MCP client registration/reload is incomplete.
3. Verify the NetVsMcp Visual Studio extension is installed in the normal Visual Studio instance.
4. Compare the Release extension identity/version with the broker or release artifact being tested. If the user asks to compare Debug and Release, verify both VSIX identities/versions explicitly and report whether they match.
5. Open the intended solution in the normal Visual Studio instance and wait for it to register with the broker.
6. Route tool calls explicitly by `sessionId`, `solutionPath`, or `solutionName` whenever more than one Visual Studio instance is registered.

## Release Safety

- Do not overwrite or uninstall an installed Release broker or extension unless the user explicitly asks for an installation/update test.
- Avoid persistent edits to the user's normal solution. Prefer read-only, status, navigation, discovery, and controlled temporary-file operations.
- For mutating or debugger tools, use a controlled sample project or a disposable file/process where possible, and revert any intentional changes before finishing.
