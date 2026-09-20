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

## Password-based file encryption/decryption

### Delivered
- Native-selected password file encryption/decryption using the existing jnUtil format. No document key is required; document-key, HMAC and account-bound variants remain pending.
- Unlike WPF's destructive encryption helper, this adapter retains the source and refuses existing/same-path outputs. UI explicitly warns that original plaintext is retained and not secured by encrypting a copy.
- Operation-specific staging directory beside the destination with a nonexistent payload path, followed by no-overwrite publication on success. Cleanup deletes the known payload and empty staging directory, never unexpected files recursively.
- Legacy synchronous crypto runs off the request thread and is always awaited. Cancellation is checked before work and before publication; it cannot interrupt the legacy call or securely erase temporary plaintext.
- Separate password handler/form validates bounded inputs and encryption confirmation before native dialogs, shares the file-tools gate, clears request password/model state and returns generic status without paths/passwords.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, not a clean rebuild. Source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **133 passed, 0 failed, 0 skipped**.
- Six new cases cover empty/one-byte/multi-buffer bidirectional jnUtil interoperability, wrong-password and malformed-input rejection, no-overwrite/source preservation/staging cleanup, invalid/pre-canceled requests and HTTP validation/non-disclosure.
- HTTP tests check anonymous/antiforgery rejection, invalid/oversized/mismatched input before dialogs, ignored browser paths, encrypted/decrypted round trip, wrong-password cleanup and canceled selection.
- Initial validation found that jnUtil rejects precreated output files. Replaced the reserved temporary file with a nonexistent payload inside an operation-specific directory, then reran all tests successfully.

### Outstanding and next work
- Manual native file/password UX, permissions, large-file behavior and shutdown/cancellation during synchronous crypto remain acceptance items. Deterministic mid-call cancellation and destination-race tests are deferred.
- Existing format is retained; these tests do not establish comprehensive tamper authentication for arbitrary legacy ciphertext. Staging inherits destination-directory permissions; cleanup is deletion, not secure erasure.
- Next: document-key file encryption and HMAC under workspace locking, followed by signing/certificates and compatibility transfers. Tasks 04/05 remain incomplete; no stable tag, push, cutover or cross-platform claim.

## Document-key file encryption/decryption

### Delivered
- Native-selected document-key file operations matching WPF's `file encryption` HKDF context, 32-byte salt/key and existing jnUtil key-based file format.
- Requires a saved, unmodified, unlocked workspace before dialogs. The workspace gate protects document/key lifetime through native selection and the complete awaited crypto call; file tools also share their own serialization gate. No inverse gate dependency was introduced.
- Source files remain untouched; staged output publishes without overwrite only after success/cancellation checks. Temporary derived-key references are cleared on every exit, including both the original buffer and a potentially replaced jnUtil ref argument.
- Document-file commands leave document revision/dirty state unchanged and return no key, password, path or file contents. The UI explains retained plaintext, original-document/password recovery and password-change key rotation.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, not a clean rebuild. Edited source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **139 passed, 0 failed, 0 skipped**.
- Six new cases cover empty/multi-buffer bidirectional legacy interoperability, wrong-document/malformed-file rejection, source/output preservation, pre-cancellation/cleanup, saved/dirty/locked/stale workspace guards, native path selection/non-disclosure and deterministic lock serialization while a fake dialog is held open.
- Existing password-file, frozen document/key/certificate compatibility and native smoke tests also pass. Automated file-dialog tests use fakes and disposable synthetic files.

### Outstanding and next work
- HMAC remains next, followed by account-bound/public-key file operations, signing/certificates and compatibility transfers. Tasks 04/05 remain incomplete.
- Manual native dialogs, large-file behavior, request cancellation and shutdown while legacy crypto runs remain acceptance items. Synchronous legacy work must finish before cancellation can prevent publication; document locking waits for the operation. Staged plaintext deletion is not secure erasure.
- Retains the existing file format; wrong-document rejection tests are not proof of comprehensive ciphertext tamper authentication. Keep the old document/password when rotating keys if external encrypted files still depend on it.
- WPF, jnUtil and Windows DPAPI remain. No push, stable tag, cutover or Linux/macOS acceptance.

## Detached CMS file signing and verification

### Delivered
- Compatible detached `.p7c` CMS signatures using the existing document certificate, jnUtil SHA-256 signing and end-certificate inclusion. Signing stages output beside the selected destination and never overwrites sources or existing outputs.
- Certificate generation requires a saved, clean, unlocked document, marks it dirty and requires saving before signing. The workspace gate protects certificate lifetime through dialogs and signing; verification requires no document and uses native selection for data then signature.
- Verification distinguishes invalid signatures, valid signatures with an untrusted chain, and valid signatures with a trusted system chain. It disables revocation retrieval and does not claim identity, timestamp, revocation or long-term-signature validation.
- CMS input is limited to 64 MiB for compatible whole-file processing. Empty data files are explicitly rejected because the retained `SignedCms`/jnUtil implementation cannot create detached CMS signatures for empty content.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, zero failures (not a clean rebuild). Edited source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **166 passed, 0 failed, 0 skipped**.
- Added coverage includes legacy CMS interoperability, malformed/tampered signatures, source/output preservation, cancellation, no-certificate/empty/oversized limits, certificate generation/save/sign guards, lock serialization, authenticated native-path verification and non-disclosure.

### Outstanding and next work
- Manually validate native dialogs, external/cross-machine certificate-chain trust, certificate-store policy, permissions, large-file boundaries and shutdown behavior with synthetic files.
- Certificate import/export, signer identity/key distribution, revocation, timestamping and long-term trust remain pending. CMS validity is not a trusted identity assertion.
- Legacy transfer workflows remain pending. Tasks 04/05 and native acceptance remain incomplete; WPF, jnUtil and Windows DPAPI remain. No push, stable tag, cutover or Linux/macOS acceptance.

## Public-key file encryption and decryption

### Delivered
- Preserves WPF/jnUtil compatibility: active document ECDH private key plus peer Base64 SPKI public key derive a shared 32-byte key, and output uses the existing `nonce (12 bytes) || tag (16 bytes) || ciphertext` AES-GCM payload with no filename or key metadata.
- Requires a saved, clean, unlocked document with generated ECDH keys. Public-key operations and key generation hold the workspace gate through native selection and the awaited operation, so locking/replacement cannot clear the DPAPI-protected private key in use.
- Peer public-key text is bounded, decoded transiently, cleared after every request and never persisted in workspace snapshots, responses or messages. Invalid peer input returns a safe validation response rather than an HTTP error.
- Source files remain unchanged. New output is staged beside the selected destination and published without overwrite. Inputs over 64 MiB are refused before allocation because the compatible legacy GCM payload requires whole-file processing; malformed legacy payload parsing is normalized to cryptographic failure.
- UI explains that ECDH key agreement does not verify sender identity, signatures or trust. It warns that peer keys must be verified independently and that the compatible operation is intentionally limited to 64 MiB.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, zero failures (not a clean rebuild). Edited source diagnostics are empty and `git diff --check` passes.
- Final combined TM.Test/TM.UITest run: **161 passed, 0 failed, 0 skipped**.
- Six new cases cover empty/multi-buffer bidirectional jnUtil GCM compatibility, malformed/tampered/wrong-peer rejection, invalid peer data, same/existing/missing output preservation, cancellation, 64 MiB limit, saved/dirty/locked/no-key/stale workspace guards, ECDH key generation, lock serialization, session/origin/antiforgery validation, ignored browser paths and peer/path/content non-disclosure.
- Validation first exposed legacy malformed-payload `ArgumentException` and invalid Base64 peer-key HTTP 500 behavior. The core now reports malformed decrypt payloads as cryptographic failures, and peer input is validated before Razor response generation. Existing frozen compatibility and native startup/basic-editing tests pass.

### Outstanding and next work
- Manually validate native dialogs, cross-machine/cross-account exchange, independently verified peer-key workflows, Unicode/large-file boundaries, permissions and shutdown/cancellation using synthetic files. The 64 MiB limit is a Photino UI safety boundary, not a portable streaming replacement for the legacy payload.
- ECDH agreement alone does not authenticate a sender or recipient. Peer-key substitution, key lifecycle/distribution and signing trust remain caller responsibilities. Temporary plaintext and whole-file buffers are cleared where possible but managed-memory erasure is not guaranteed; staged cleanup is deletion, not secure erasure.
- Signing/certificates and compatibility transfers remain pending. Tasks 04/05 and native acceptance remain incomplete; release work is unstarted.
- WPF, jnUtil and Windows DPAPI remain. No push, stable tag, cutover or Linux/macOS acceptance.

## Document-key HMAC creation and verification

### Delivered
- Streaming HMAC using WPF's `HMAC` derivation context, 32-byte salt and 64-byte derived key. SHA-256 is the default; SHA-384/SHA-512 and explicitly labeled legacy MD5/SHA-1 preserve interoperability.
- Sidecars retain grouped uppercase hex plus Base64 salt. Verification bounds sidecar input to 4096 bytes, validates encoding/structure/MAC/salt lengths and uses fixed-time comparison. Derived key buffers are cleared on exit.
- Creation stages output beside the destination and publishes without overwrite; source files and existing outputs are retained. Verification does not modify either input.
- Native selection only; browser commands carry allowlisted operations/algorithms, not paths. Saved, clean, unlocked and revision checks precede dialogs. Workspace and file-tool gates protect document/key lifetime through completion; results omit paths and secrets.
- UI explains shared-key integrity, not public signatures, and warns that password changes affect document-derived keys. Keep the original document/password for dependent sidecars.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, zero failures (not a clean rebuild). Checked source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **148 passed, 0 failed, 0 skipped**.
- Nine added cases cover five-algorithm bidirectional WPF sidecar interoperability, altered data/MAC/salt/wrong-document mismatch, malformed/oversized sidecars, BOM/CRLF support, no-overwrite/source preservation, pre-canceled creation, workspace guards/state preservation and deterministic lock serialization, plus HTTP creation/verification/mismatch/malformed-input handling and path non-disclosure.
- Existing frozen document compatibility and native startup/basic-editing smoke tests also pass. Automated HMAC dialog coverage uses fakes and disposable synthetic files, not real user documents.

### Outstanding and next work
- Manually exercise HMAC controls/native selection, cancellation, Unicode paths, large files, permissions and shutdown during an operation. Native smoke does not cover the new HMAC controls; deterministic mid-stream cancellation and destination-race fault injection remain follow-up coverage.
- HMAC proves integrity only to holders of the shared key; it is not a public-key signature or encryption. Legacy algorithms are compatibility options, not security recommendations. Temporary-file cleanup is deletion, not secure erasure.
- Account-bound/public-key file operations, signing/certificates and compatibility transfers remain pending. Tasks 04/05 remain incomplete, with release work unstarted.
- WPF, jnUtil and Windows DPAPI remain. No push, stable tag, cutover or Linux/macOS acceptance.

## Windows-account file encryption (EFS)

### Delivered
- Inspection of installed jnUtil 2.0.2 confirmed account-file helpers call Windows `File.Encrypt`/`File.Decrypt` (EFS), not DPAPI file-blob encryption. The new native boundary retains these calls; document DPAPI and WPF remain unchanged.
- Creates a staged copy beside a new destination rather than modifying/renaming the source. Validates the resulting encrypted attribute before no-overwrite publication; decryption requires an EFS-encrypted source. Existing destinations and source files are retained.
- Serializes with other file tools through native selection, async copying and the complete awaited synchronous EFS call. Cancellation prevents publication after the native call returns; cleanup deletes only the known payload and empty staging directory.
- A separate acknowledged form explains Windows/NTFS/edition/policy requirements, EFS certificate/private-key backup, transparent authorized access, loss of protection on copying/export, retained plaintext and deletion versus secure erasure. The `.enc` suffix is not a portable ciphertext format.
- Commands contain only operation/acknowledgement; paths come from native dialogs. Invalid commands are rejected before dialogs, and generic results/errors do not echo paths or content. No document key/password is required.

### Validation
- Visual Studio solution build succeeds; latest Build pane reports five projects up-to-date, zero failures (not a clean rebuild). Edited source diagnostics are empty and `git diff --check` passes.
- Combined TM.Test/TM.UITest run: **155 passed, 0 failed, 0 skipped**.
- Seven added cases cover empty/multi-buffer copying, protection-boundary calls, source preservation, invalid/same/existing/missing inputs, plain-source decryption rejection, pre-cancellation, native failure/no-op rejection, destination-race preservation, cancellation during protection, staging cleanup, HTTP session/antiforgery/acknowledgement/enum validation, ignored browser paths, error non-disclosure and cross-tool serialization.
- All new EFS tests use an injected fake; no real EFS encryption/certificate operations are executed. These are orchestration/safety tests, not native encryption or account-isolation tests. Existing Windows startup/basic-editing smoke and frozen compatibility tests pass.

### Outstanding and next work
- Manual acceptance must verify EFS state after publication, native encrypt/decrypt round trips, Unicode/large files, unsupported volumes/Windows editions/policy, denied-account access, EFS certificate recovery, cancellation/shutdown and native-dialog UX using disposable synthetic files.
- Copying creates temporary plaintext; inherited directory permissions apply. The original plaintext remains, cleanup is not secure erasure, and EFS is transparent to authorized users/recovery agents. No portable encrypted-attachment or comprehensive account-isolation claim is made.
- Public-key file operations, signing/certificates and compatibility transfers remain pending. Tasks 04/05 and native acceptance remain incomplete; release work is unstarted.
- WPF, jnUtil and Windows DPAPI remain. No push, stable tag, cutover or Linux/macOS acceptance.
