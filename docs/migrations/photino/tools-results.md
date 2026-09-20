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

### Follow-up
Document password change is now implemented in the following slice; remaining tools are still pending.

## Document password changes

### Delivered
- Separate current/new/confirmation password form with explicit acknowledgement, bounded inputs and no echoed passwords. Saving remains explicit; discarding restores the previous saved password and contents.
- Fixed-time comparison of the supplied old-password key against the DPAPI-unprotected active key, including empty and no-protected-item documents.
- Re-encrypted detached entities, replacement node construction and new DPAPI key protection complete before live state is changed. Wrong passwords and damaged entries leave the active key/salt/nodes untouched. Temporary plaintext bytes and derived keys are cleared in finally blocks.
- Preserves ECDH/certificate state and frozen document compatibility. The UI warns that document-derived external-file/HMAC keys change, external files are not updated, and an old document/password copy may be needed.
- Loading requires a decrypted projects element with no remaining XML encrypted payload; missing/unsuccessful payload decryption cannot be silently treated as an empty collection. This adds validation, not a new file format or authenticated-encryption scheme for the legacy CBC document envelope.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, not a clean rebuild. Edited source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **99 passed, 0 failed, 0 skipped**, including frozen WPF key/certificate and document-format tests.
- Eight added cases cover empty/no-protected documents, authentication, damaged-later-entry failure without partial rekey, missing payload rejection, empty/populated save/reopen and discard, service guards and HTTP validation/non-disclosure.
- Initial validation found an old-password acceptance on empty-document reopening; added post-decryption structural validation and deterministic missing-payload coverage. The damaged-entry test was corrected to account for sorted node order. An existing WPF UI test hit a process-exit cleanup race on the first run and passed on the final full rerun; no native-test production change was made.

### Outstanding and next slice
- Manually exercise the password-change form in the native WebView, including unapplied-edit confirmation, keyboard/focus, save/discard and error UX. Current native smoke covers startup/basic editing, not these controls.
- Continue with ownership-aware clipboard expiry and native integration. File utilities, hashes/HMAC, signatures/certificates and legacy transfer workflows remain pending.
- Tasks 04/05 remain incomplete. WPF, Windows DPAPI and jnUtil remain; no stable tag, push, cutover or cross-platform acceptance.

## Ownership-aware clipboard copying

### Delivered
- Protected-only Copy password command sends decrypted text directly to a native clipboard boundary, never to the HTTP response. Temporary plaintext is cleared after the native write completes; snapshots and expiry metadata retain no copied password.
- Windows backend uses a dedicated STA thread, a hidden message-only owner window, message pumping and native clipboard allocations. Browser clipboard permission remains disabled; no WPF dependency was added to TM.Desktop.
- Default 15-second expiry, checked once per second. Copy/expiry are serialized; repeated copies replace the deadline. Ownership and sequence checks occur under the native clipboard lock, preserving subsequent external copies even if text is identical.
- Lock and successful New/Open/Unlock replacement request cleanup. Failed opens do not clear the existing copy. Busy cleanup stays pending for the hosted expiry loop instead of preventing document locking. Graceful host shutdown attempts owned cleanup.
- Best-effort Windows history/cloud exclusion flags and a visible warning about other applications/clipboard managers. Embedded NUL text is rejected to avoid truncated password copies.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date (not a clean rebuild). Source diagnostics are empty; `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **106 passed, 0 failed, 0 skipped**.
- Seven new fake-backed tests cover deadline replacement, external copies including identical text, busy retries, failed/canceled/NUL copies, shutdown cleanup, workspace lock/replacement guards and HTTP non-disclosure.
- Initial workflow test failures identified a fake retaining the caller's string rather than copying native data. Corrected the fake to copy its input; production temporary-string cleanup is retained.
- No automated test writes to the real Windows clipboard. Existing native smoke tests exercise host startup/shutdown and basic editing, not native clipboard writes.

### Limits and next work
- Manually validate Unicode/empty passwords, paste into a disposable editor, 15-second expiry, quick repeated copies, a later copy from another application, lock/replacement, graceful shutdown and clipboard contention/history behavior. Do not use real secrets for acceptance.
- Expiry is best effort: contention, crashes and forced termination can delay or prevent clearing. Cleanup cannot remove copies already captured by another process, clipboard manager or history/sync provider. Windows history/cloud hints are not a security guarantee.
- Continue with dialog-free file hashing/base64 and native file selection, followed by encryption/HMAC/signature/certificate and legacy transfer workflows. Tasks 04/05 and native UX acceptance remain incomplete; no stable tag, push or cross-platform claim.

## File hashing and Base64 tools

### Delivered
- File-tool UI embedded independently of the document editor and available at `/FileTools`. Native dialogs select both files; the browser command contains only an allowlisted operation, never paths or file contents.
- Streaming SHA-256 (default), SHA-384, SHA-512, MD5 and SHA-1 hash sidecars preserving WPF grouped-uppercase text. MD5/SHA-1 are explicitly labeled legacy, not security recommendations.
- Streaming Base64 encode/decode with bounded buffers, cancellation, UTF-8 BOM/ASCII whitespace decoding and rejection of malformed/truncated/trailing-after-padding data. Base64 is clearly labeled as encoding, not encryption.
- Temporary output in the destination directory, published without overwrite after successful completion. Same-path and existing destinations are refused, with no-overwrite publication also guarding destination races. Source files are never overwritten. Only operation-created temporary files are cleaned up on failure.
- Safe generic results/errors omit paths and contents. Native tools serialize operations and wait for an open dialog to resolve before releasing the gate, even when a request has been canceled.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, not a clean rebuild. Source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **127 passed, 0 failed, 0 skipped**.
- Twenty-one added cases: five legacy hash comparisons, six Base64 sizes (empty through multi-buffer) with bidirectional jnUtil compatibility, six malformed-input cleanup cases, BOM/whitespace decoding, file-preservation/pre-canceled input checks, native-dialog cancellation and HTTP integration.
- HTTP coverage verifies anonymous rejection, antiforgery, invalid operation rejection before dialogs, ignored browser path injection, safe success/error responses, canceled selection and unchanged existing destinations.
- Initial six Base64 compatibility failures were test calls not awaiting jnUtil's asynchronous encode/decode operations. Both calls are now awaited and all compatibility cases pass. Production WPF code was not changed.

### Outstanding and next work
- Manually validate native input/output dialogs, cancellation, existing-output refusal, Unicode paths, large files, permissions and app shutdown during an operation. Native smoke coverage still covers startup/basic editing, not interactive file-tool dialogs.
- Request-abort cancellation is implemented; dedicated progress/cancel controls, deterministic mid-stream cancellation and destination-race fault injection remain follow-up coverage. Cleanup deletes temporary files but is not secure erasure; decoding can create sensitive plaintext on disk.
- Continue with file encryption/HMAC, signing/certificates and compatibility transfer workflows. Tasks 04/05 remain incomplete; retain WPF, Windows DPAPI and jnUtil. No stable tag, push or Linux/macOS acceptance.
