# Launching Visual Studio

If `vs_list_sessions()` returns no sessions, infer the target solution instead of immediately asking the user to open Visual Studio.

Prefer a solution explicitly named by the user. Otherwise use the current workspace solution when exactly one `.sln` or `.slnx` is present. Ask only when multiple plausible solutions remain.

Prefer `vs_launch_instance`:

```json
vs_launch_instance({
  "solutionPath": "D:\\Work\\App\\App.sln",
  "experimental": false,
  "edition": null,
  "timeoutSeconds": 60
})
```

`experimental: true` passes `/RootSuffix Exp`. `edition` filters installed Visual Studio editions by path substring. If `vs_launch_instance` is unavailable, locate `devenv.exe` with `vswhere.exe`; do not assume a fixed install path.

After launch, call `vs_list_sessions()` again. If no session registers, ask the user to confirm the VSIX is installed in that Visual Studio profile and the broker is running.
