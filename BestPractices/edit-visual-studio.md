# Edit Visual Studio With NetVsMcp

Read this entrypoint, then only relevant references. User and project instructions override these defaults.

## First Rules

- Document/editor tools use `path`, not `documentPath`.
- Paths may be absolute or relative to the routed solution file's directory; prefer forward slashes.
- Line and column values are 1-based.
- Use safe-edit previews when the user should review a diff, when the blast radius is uncertain, or when build-verified apply is useful.
- Use direct editor tools for small, unambiguous, already-agreed changes.

## Reference Files

Pass reference paths as `file` to `netvs_get_best_practices` with this guide's name (or the guide named in the row).

| Task | Read |
| --- | --- |
| Open, read, list, close, or understand path conventions | `references/documents.md` |
| Insert, replace, write, or save immediately | `references/direct-edits.md` |
| Get or set editor selection | `references/selection.md` |
| Format, cleanup, or organize imports | `references/formatting.md` |
| Preview, approve, reject, or build-verify pending edits | `references/safe-edit.md` |
| Search text through the editor or Visual Studio Find in Files | `references/search.md` |
| Read compiler diagnostics tied to the live document version | Guide `navigate-visual-studio`, file `references/search-diagnostics.md` |

## Covered Tools

`document_*`, `editor_*`, `selection_*`, `document_cleanup`, `format_and_organize`, `edit_*`, `find_in_files`, and `open_relevant_files`.
