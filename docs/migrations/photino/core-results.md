# Shared-core checkpoint

## Implemented
- Extracted existing domain entities, observable/tree/document/security models, crypto service and session record into `TM.Core` without changing namespaces or encryption parameters.
- Removed WPF visibility types from the model; WPF uses boolean-to-visibility bindings and references the core assembly for entity types.
- Added dialog-free `ProjectStore`: bounded XML parsing with DTDs prohibited, async/cancellable I/O, fresh document loading with key/certificate cleanup on failure, same-directory temporary-file saves and explicit close cleanup.
- `TM.Core` now targets portable .NET 10 with jnUtil 2.0.0. Windows retains DPAPI while non-Windows hosts use authenticated process-local protection for session-only keys; native Linux/macOS runtime validation remains pending.
- Added eight persistence/filtering/assembly-boundary regression tests.

## Evidence
- Visual Studio solution build successful.
- Visual Studio Test Explorer: **60 passed, 0 failed, 0 skipped**, including frozen encrypted-document fixtures and two WPF desktop acceptance tests.
- Core assembly reference test confirms no direct dependency on PresentationCore, PresentationFramework, WindowsBase or TM.
- No production crypto/file-format changes; no warning suppressions added.

## Remaining
- Native Linux/macOS runtime validation and an optional OS-keyring-backed key protector remain pending.
- Manual UX acceptance/stable tag remain pending; this is a tested local checkpoint.
- WPF dialog orchestration remains in the WPF project; the new client will use the dialog-free store.
- Caller must serialize mutations with saving. A later UI/session service owns this concurrency boundary.
- The user's `.github/copilot-instructions.md` changes are preserved and excluded from agent commits.
