# Windows Photino tools checkpoints

## Generated protected passwords

### Delivered
- Protected-item generation form with explicit replacement confirmation and separate reveal action.
- Existing jnUtil generator, length 5–30 (default 12) and optional complex mode, matching WPF settings. Short lengths are retained for compatibility, not recommended as strong passwords.
- Serialized generation with revision/unlocked-document/target checks. Encrypt before changing the protected item, preserve other metadata, and set dirty state on success.
- Generation returns no plaintext. Temporary SecureString is disposed and temporary plaintext is cleared with the existing helper; managed-memory erasure is not guaranteed.
- Existing encrypted document format, WPF fallback and Windows DPAPI remain unchanged.

### Validation
- Visual Studio `run_build`: successful. Latest Build pane reports five projects up-to-date, no failures; this was not a clean rebuild.
- Visual Studio Test Explorer combined TM.Test/TM.UITest run: **91 passed, 0 failed, 0 skipped**.
- Ten added test cases: six service round trips for both modes at minimum/default/maximum lengths; one service rejection test; two HTTP default/complex round trips; one HTTP rejection test.
- Service tests verify unchanged metadata, dirty/revision updates, save/lock/reopen and no mutation for invalid lengths, missing confirmation, missing/wrong targets, stale revisions and canceled requests. Locked/no-document generation is rejected.
- HTTP tests verify malformed/overflowing values are rejected, requests use existing antiforgery/session protection, generation and ordinary GETs omit plaintext, explicit reveal is transient, file contents omit generated plaintext, and lock removes reveal/form state.
- Existing frozen compatibility, security and native smoke tests pass. Native smoke tests cover startup and basic editing, not the new generation controls.
- No diagnostics reported for edited source files; `git diff --check` passes.

### Outstanding
- Manually exercise generation controls, complex mode, reveal expiry and unapplied-edit confirmation in the native WebView; verify keyboard/focus behavior.
- This is a tested tools slice, not completion of task 05 or UI parity. Password change, clipboard expiry, file/security utilities and legacy transfer workflows remain pending.
- Task 04 gaps remain listed in ui-results.md. Keep WPF runnable; no stable tag, release cutover, Linux/macOS acceptance or automatic push.

### Next slice
Document password change: investigate empty/no-protected-item documents, authenticate the old password independently of protected entries, and ensure failure leaves the active document untouched before exposing it through the workspace UI.
