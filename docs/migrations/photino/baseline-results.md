# Baseline checkpoint results

## Changes
- Added nine regression tests plus immutable embedded synthetic encrypted/plaintext fixtures and an ECDH private-key digest.
- Verified all project fields by XML value comparison, password changes, re-save, wrong-password rejection, private-key/protected-value tampering, fresh encryption, master/ECDH key clearing and Unicode/XML-special text.
- Serialized FlaUI tests because they share global desktop input; a concurrent run previously failed with a COM Win32Exception.
- Preserved production application code, cryptographic parameters, jnUtil version and document format.

## Validation
- Initial baseline: 43 tests passed.
- Final baseline: **52 passed, 0 failed, 0 skipped** via Visual Studio Test Explorer (50 TM.Test, 2 TM.UITest).
- Visual Studio solution build succeeded. Terminal rebuild was canceled by the user; the user explicitly approved using Visual Studio build instead. Do not claim the canceled rebuild completed.
- Wrong passwords can produce CryptographicException or XmlException in legacy XML decryption. Tests accept only these known rejection paths, not arbitrary failures. Future readers must reject failures without publishing partial document state.
- Existing lock behavior leaves the certificate object; this is documented, not asserted to be secure clearing of all signing material.

## Manual acceptance remaining
Use disposable copies only. Verify new/open/save-as, cancelling dialogs, lock/reopen, password reveal/edit/copy expiry, search/to-do navigation, drag/drop, Unicode descriptions, certificate tools and encrypted-file utilities. Never test wipe/encryption against real originals.

The checkpoint may be committed and work may continue per the user's Windows-first instruction. **No stable tag or full manual acceptance is claimed.** Windows UI startup and create/save/decrypt workflows were automated; Linux/macOS and Photino runtime are not yet validated.
