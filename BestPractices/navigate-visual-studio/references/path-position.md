# Path And Position Conventions

Code navigation, diagnostics, and breakpoint tools use `documentPath`. Document/editor tools such as `document_open` use `path`.

Paths may be relative to the routed solution file's directory or absolute. Prefer forward slashes. If Windows backslashes must appear in JSON, escape them as `\\`.

`line` and `column` are 1-based, matching Visual Studio editor numbers. Position-based tools reject values below 1.

Every position-based tool takes `documentPath`, `line`, and `column` as flat parameters, not a nested object.
