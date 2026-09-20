# Main UI implementation research

## Boundary
Use a singleton application workspace service (serialized by SemaphoreSlim) over ProjectStore and ProjectCryptoService. Razor PageModel handlers only bind validated request models and delegate to this service. Return detached presentation snapshots; never expose mutable document instances or ciphertext/key stores through HTML. Keep WPF unchanged as fallback.

Use revision numbers on POST commands to reject stale edits after selection/mutation/lock. All mutations, save and dialog operations pass through the same gate. Require explicit discard confirmation before replacing/locking dirty state. Locked state clears the entire document, key/certificate state and UI snapshot; unlocking reopens the saved file. No silent saving or discarding on lock.

Native open/save dialogs supply filesystem paths through an injected interface; browser form fields do not supply paths. The live adapter binds a PhotinoWindow; tests use a deterministic fake. Passwords are posted once, converted to SecureString for the legacy API and not persisted in ViewModels. Secret reveal is a POST-only transient result, not stored in workspace state; small local JS removes it after 20 seconds. Existing encryption algorithms/DPAPI remain unchanged.

## Initial main workflows
New/open/save/save-as/lock/unlock; add/select/edit/delete projects, milestones, tasks, subtasks and protected entries; search, due-date/priority/progress editing and to-do navigation. Keep full HTML fallback and htmx partial refreshes with antiforgery tokens and local CSS/JS only. Validate names/enums/progress and node-parent relationships server-side. Serialize requests in the browser as well as the backend. Editing forms must clear secrets from the DOM on lock/replacement.

## Acceptance scope
Add application service tests for save/reopen, dirty guards, canceled native dialogs, stale revisions, invalid node relationships and lock-time clearing. Add HTTP/native workflow checks. Drag/drop, clipboard expiry, generated passwords, password changes, compatibility exports and security utilities remain explicit follow-up work until implemented and tested; do not mark the overall UI milestone complete prematurely.
