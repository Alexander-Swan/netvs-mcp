# Edit Visual Studio With NetVsMcp

Agent-neutral entrypoint for opening, reading, editing, formatting, selecting, saving, and previewing document changes through NetVsMcp.

## How To Read This Guide

- For tool use, read this entrypoint before calling a matching edit/document tool, then only the reference files needed for the operation.
- For learning or local skill creation, read references deliberately. Do not embed this whole guide in generated skills; route to references there too.

## First Rules

- Document/editor tools use `path`, not `documentPath`.
- Paths may be absolute or relative to the routed solution file's directory; prefer forward slashes.
- Line and column values are 1-based.
- Use safe-edit previews when the user should review a diff, when the blast radius is uncertain, or when build-verified apply is useful.
- Use direct editor tools for small, unambiguous, already-agreed changes.

## Reference Files

| Task | Read |
| --- | --- |
| Open, read, list, close, or understand path conventions | `edit-visual-studio/references/documents.md` |
| Insert, replace, write, or save immediately | `edit-visual-studio/references/direct-edits.md` |
| Get or set editor selection | `edit-visual-studio/references/selection.md` |
| Format, cleanup, or organize imports | `edit-visual-studio/references/formatting.md` |
| Preview, approve, reject, or build-verify pending edits | `edit-visual-studio/references/safe-edit.md` |
| Search text through the editor or Visual Studio Find in Files | `edit-visual-studio/references/search.md` |

## Covered Tools

`document_*`, `editor_*`, `selection_*`, `document_cleanup`, `format_and_organize`, `edit_*`, `prepare_safe_edit`, `apply_safe_edit_and_build`, `find_in_files`, and `open_relevant_files`.
