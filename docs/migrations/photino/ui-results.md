# Initial Windows Photino UI checkpoint

## Delivered
- Serialized workspace service with revision checks and detached presentation snapshots.
- New/open/save/save-as/lock/unlock, explicit dirty-discard guards, native file-dialog adapter, tree and to-do navigation, filtering, add/edit/delete, scheduling fields and transient protected-value reveal.
- Razor/htmx partial updates with antiforgery, encoded content, local styles/scripts and no client-supplied filesystem paths.
- WPF remains available; jnUtil, encrypted document format and Windows DPAPI are retained.

## Validation
- Visual Studio solution build: successful (`run_build`).
- Visual Studio Test Explorer: **81 passed, 0 failed, 0 skipped** across TM.Test and TM.UITest in the combined checkpoint run.
- Service coverage: save/lock/unlock round trip, transient reveal, dirty guards, confirmed discard, canceled dialogs, failed open, stale revisions, invalid relationships/edits, deletion and Unicode filtering.
- HTTP coverage: encoded names, save/lock/unlock, wrong password handling, transient secret removal, safe invalid/stale responses. Existing shell security and frozen encrypted-document compatibility tests also pass.
- Native Photino smoke coverage: startup/shutdown and creating a collection, adding a project, then renaming it through the embedded webview. Workspace smoke cleanup terminates the test process; it does **not** validate graceful dirty-close behavior.
- `git diff --check`: clean before checkpoint documentation.

## Outstanding work and acceptance
This is an initial UI checkpoint, not completion of task 04 or full WPF parity. No stable tag is warranted yet.

- Implement and validate drag/drop, sorting parity, focus/edit preservation and remaining keyboard accessibility.
- Add generated-password, password-change, clipboard-expiry and compatibility import/export workflows.
- Port security utilities and native integrations on the next tools branch, while retaining task 04 as incomplete until remaining UI work is validated.
- Manually validate native open/save dialogs, overwrite/cancel/error behavior, close-time dirty protection, high DPI, keyboard navigation and assistive technologies. Browser beforeunload alone is not proof of a native close guard.
- Broaden native acceptance to protected values, scheduling, deletion and document lifecycle; current native smoke only covers create/add/edit.
- Windows distribution/offline-package validation remains pending. Linux/macOS runtime and non-Windows key protection remain deferred.

## Checkpoint policy
Commit this tested slice on photino-04-ui, then create photino-05-tools from that commit. Do not retire WPF, create a stable tag, push branches or claim cross-platform acceptance.

## Tree sorting and timestamp parity checkpoint

### Delivered
- Ported the legacy WPF tree sort-by-name and sort-by-date commands to serialized Photino workspace actions. Each action preserves the selected node, marks the document dirty and participates in revision protection.
- Ported selected-item timestamp insertion using the legacy date/time shape (`yyyy-MM-dd` plus local short time), appending to an existing description without accepting browser-supplied replacement text.
- Added explicit toolbar controls for both sort modes and a selected-item timestamp command. All mutations continue through Razor/htmx antiforgery and the workspace gate.

### Validation
- Visual Studio solution build succeeds; edited-source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **176 passed, 0 failed, 0 skipped**.
- Added service and HTTP coverage for sorted ordering, timestamp insertion, dirty/revision updates, invalid selections and ignored browser-supplied description text.

### Remaining UI and release work
- Tree expand/collapse and focus restoration, drag/drop or an accessible equivalent, keyboard shortcuts/navigation and broader accessibility remain outstanding.
- Manually validate the Photino UI with native dialogs, dirty-close behavior, high DPI, screen readers, keyboard flow, touch/pointer behavior and existing WPF document compatibility.
- Task 05 code/test implementation is complete but its native Windows acceptance remains manual. Task 06 is blocked by UI completion, native acceptance, Windows distribution validation and a decision/implementation for non-Windows key protection and native substitutes.

## Tree navigation parity checkpoint

### Delivered
- Ported legacy tree expand, collapse and focus-selected behavior through serialized workspace commands. Focus clears filtering, expands selected ancestors and keeps the selected editor/tree item aligned.
- Tree expansion state is represented in immutable presentation snapshots and rendered as a server-owned HTML `details` boolean attribute after htmx updates. View-state actions do not mark document content dirty.
- Added accessible command buttons for expand, collapse and focus selected item. Existing tree item buttons remain keyboard reachable and identify the selected item with `aria-current`.

### Validation
- Visual Studio solution build succeeds; edited-source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **178 passed, 0 failed, 0 skipped**.
- Added service and HTTP coverage for collapsed/expanded snapshots, focus restoration through filters, selection state, invalid item rejection, document-neutral view state and minimized boolean-attribute rendering.

### Remaining UI and release work
- Drag/drop or an accessible move/reparenting equivalent, keyboard shortcuts/navigation and broader accessibility remain outstanding. Browser-side expansion changes before a server update are not persisted.
- Manually validate keyboard focus movement, screen readers, high-DPI layout, pointer/touch behavior, native dialogs and dirty-close behavior in the Photino window.
- Task 05 code/test implementation is complete but native Windows acceptance remains manual. Task 06 is blocked by UI completion, native acceptance, Windows distribution validation and a decision/implementation for non-Windows key protection and native substitutes.

## Keyboard navigation parity checkpoint

### Delivered
- Ported the legacy Ctrl/Cmd shortcuts for save, lock, expand tree, collapse tree, focus selected item, sort by name, sort by date and append timestamp. Shortcuts activate the existing antiforgery/revision-protected Razor/htmx forms rather than introducing a new client mutation API.
- The local handler ignores editable inputs, textareas, selects and content-editable elements so document and credential entry remain normal typing operations. Existing unapplied-editor guards still prevent shortcuts from discarding pending edits.

### Validation
- Visual Studio solution build succeeds; edited-source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **181 passed, 0 failed, 0 skipped**.
- Added authenticated static-asset coverage for the focus-safe allowlist, all expected commands and absence of a separate `fetch` mutation path. Existing workspace HTTP/service tests cover the mapped commands.

### Remaining UI and release work
- Manually validate shortcut behavior in the Photino WebView, including macOS modifier conventions when that target is introduced, keyboard focus, screen readers, browser-reserved combinations and high-DPI layout.
- Broader accessibility/native interaction acceptance, Windows distribution validation and non-Windows key-protection/native-service replacements remain before task 06 can begin.

## Accessible tree move research

- Legacy WPF drag/drop validates that source and target differ, rejects moves into a source descendant, and rejects non-protected nodes targeting protected nodes. A move into a node deep-copies non-protected source types into the valid child type, expands/recalculates the target and applies its due-date boundary; moving a non-protected node with no target promotes it to a root project.
- The Photino replacement will provide an explicit selected-item move target rather than pointer drag/drop. It must enforce the same type/cycle rules before changing the document, retain the selection, update tree/todo state and use a deliberate root-project option only for non-protected items.

## Accessible tree move parity checkpoint

### Delivered
- Added a serialized, keyboard-accessible move/reparent workflow for the selected tree item. Users choose an existing target from a form control, or explicitly promote a non-protected item to a new root project.
- Preserves legacy move semantics: non-protected nodes are converted to the valid type under their new parent, target expansion/progress and inherited due-date constraints are updated, and the moved item remains selected.
- Rejects self moves, descendant cycles, non-protected moves into protected targets and protected-item root promotion. The move is performed through the document model instead of pointer-specific UI behavior.

### Validation
- Visual Studio solution build succeeds; edited-source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **180 passed, 0 failed, 0 skipped**.
- Added service and HTTP coverage for reparenting, root promotion, type conversion, selection preservation, invalid target rejection, and ignored browser-supplied target text.

### Remaining UI and release work
- Pointer drag/drop is intentionally replaced by this accessible move workflow. Keyboard shortcuts/navigation, broader accessibility, and manual interaction acceptance remain outstanding.
- Manually validate target-list usability for large trees, keyboard/screen-reader flow, high-DPI layout, pointer/touch behavior, native dialogs and dirty-close behavior in the Photino window.
- Task 05 code/test implementation is complete but native Windows acceptance remains manual. Task 06 is blocked by UI completion, native acceptance, Windows distribution validation and a decision/implementation for non-Windows key protection and native substitutes.
