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
