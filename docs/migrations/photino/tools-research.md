# Windows Photino tools research

## Entry state
Branch photino-05-tools starts from initial UI checkpoint e1602a7. The checkpoint passed a Visual Studio solution build and 81 tests. Task 04 remains incomplete; this branch can carry follow-up UI workflows as well as task 05 utilities. No implementation in this document is claimed delivered.

## Existing implementation boundaries
- `TM/Services/SecurityToolsService.cs` orchestrates base64, file encryption with document/password/current-account keys, certificates, signatures, public-key encryption, purge, hashes and HMAC. It depends on WPF interaction and shell services, so it cannot be injected into the Photino host unchanged.
- `TM/Helpers/FileHelper.cs` combines file-dialog helpers with encryption overloads. Preserve existing formats and Windows-account semantics when separating these operations.
- `TM/Helpers/HashHelper.cs` supports MD5/SHA1/SHA256/SHA384/SHA512 file hashes and HMAC sidecars. `SecurityToolsService.CreateHmac` appends a Base64 salt; derivation uses context `HMAC` and length 64. Existing tests are `HashHelperTests.cs` and `FileHelperTests.cs`.
- `TM.Core/Services/ProjectCryptoService.cs` already owns secret encryption, password changes, private-key access, signing, certificate/key stores and encrypted document serialization. Retain its existing algorithms and DPAPI handling.
- `TM/Services/ProjectDocumentService.cs` implements the legacy `.sav` transfer format: exported XML is GCM-encrypted under a random key and Base64 encoded. Despite legacy 'unencrypted' naming, this is not a plaintext file. Import requires a transfer key and a new document password. Preserve format compatibility and add bounded parsing/atomic output rather than copying legacy file handling verbatim.
- `TM/Helpers/ClipBoardHelper.cs` uses WPF Clipboard and DispatcherTimer. Its unconditional delayed clear is unsuitable as the new adapter contract: expiry should only clear a still-owned clipboard value, not unrelated clipboard content copied later.
- `TM/Models/MainWindowViewModel.cs` has the existing generated-password command and protected-value editor flow.

## Proposed implementation slices
1. Generated protected passwords through the existing serialized workspace gate, bounded validated length, explicit replacement confirmation, dirty/revision updates, no plaintext in retained snapshots and HTTP tests. This is the smallest first slice and requires no new filesystem API.
2. Document password change with old/new/confirmation inputs, atomic failure behavior, empty/no-protected-item support, save/reopen compatibility and wrong-password tests. Current `ChangeMasterPassword` returns false for empty documents or documents without protected items; direct exposure would preserve that gap.
3. Native clipboard interface with Windows UI-thread dispatch, ownership-aware expiry, replacement/lock cleanup and fake-based tests before native acceptance.
4. Dialog-free file utilities plus native dialog contracts, starting with non-destructive hashes/base64. Add encryption/HMAC/signature/certificate adapters in individually tested slices. Never trust browser-supplied paths; guard overwrites, cancellation and partial output.
5. Legacy transfer import/export through bounded parsing, transient key presentation, native paths and frozen compatibility fixtures.

## Security and validation gates
All document/key operations must remain serialized with workspace lock/replacement. Razor handlers validate and delegate; they must not manipulate documents or native controls. Keep sensitive inputs out of persisted view models, logs and error messages. Zero temporary byte buffers in finally blocks. MD5/SHA1 are legacy compatibility options, not modern security recommendations. File purge needs explicit destructive confirmation and must not promise secure erasure on SSDs.

Each slice requires service and HTTP coverage, Visual Studio solution build/tests, and documented native/manual gaps before its checkpoint. Keep WPF runnable. Windows DPAPI and jnUtil stay in place; Linux/macOS and stable tagging are deferred.

## Generated-password slice design
- Reuse `SecurityHelper.GeneratePassword(length, complex)` as in `MainWindowViewModel.GeneratePassword`; match the WPF slider's 5–30 bounds and default length 12, with complex mode opt-in. These are compatibility settings, not a recommendation for short passwords.
- Append a GeneratePassword action to the command enum, plus validated length, complex-mode and dedicated replacement-confirmation fields. Keep existing enum values stable.
- Validate revision, unlocked document, target type and confirmation before generating. Encrypt into a local value before assigning the node; update selection, dirty state and revision only on success.
- Return no plaintext on generation. Use the existing explicit reveal flow to view the result. Dispose the generated SecureString and call the existing ZeroString helper on the temporary plaintext in finally; do not claim guaranteed managed-memory erasure.
- A separate form must state that generation replaces the protected password immediately in the document and still requires saving. It must not silently apply unrelated editor fields; existing unapplied-edit confirmation remains active.
- Cover minimum/default/maximum lengths and both modes, encrypted save/reopen, unchanged non-secret fields, stale revisions, missing confirmation, invalid lengths, wrong/missing targets, cancellation and locked/no-document states. HTTP tests must prove that generation and ordinary GETs do not reveal plaintext, malformed input is rejected without mutation, and lock removes reveal state.
