# Photino Rewrite Assessment

## Baseline
- Source: `rewrite` at `cf0a98b154b06cb7529e6a0c2433d543e3875174`; baseline branch: `photino-01-baseline`.
- Solution: `TM.sln`; three projects: WPF `TM`, MSTest `TM.Test`, FlaUI/MSTest `TM.UITest`.
- All three target `net10.0-windows`, with `win-x64` runtime configuration.
- Initial IDE build succeeded (projects up to date). Initial Test Explorer run: **43 passed, 0 failed, 0 skipped**, including both UI tests. A rebuilding validation remains necessary before the baseline checkpoint.
- No installed migration scenario matches this rewrite. Documents in this directory are the authoritative migration record; there is no scenario.json workflow.

## Feature inventory and migration acceptance targets
| Feature group | Existing implementation | Acceptance target |
|---|---|---|
| Document lifecycle | `ProjectDocumentService`, `MainWindowViewModel` | New/open/save/save-as, cancel/error paths, title/path state, compatibility export/import |
| Hierarchical editing | `Entities/*`, `NodeModel`, `ProjectDocument` | Project/milestone/task/subtask/protected-item nesting; add/delete/move; identities and parent relationships |
| Details and scheduling | `NodeModel`, `MainWindow.xaml` | Names, multiline descriptions, Unicode/XML escaping, priorities, due dates, progress aggregation, created/changed/finished timestamps |
| Navigation | `ProjectDocument`, `MainWindow.xaml.cs` | Tree/to-do views, filter, sort, expand/collapse, focus, context menus, valid drag/drop, preserved selection |
| Secret editing | `MainWindowViewModel`, `ProjectCryptoService` | Lock, change password, reveal/edit/cancel/commit, generated passwords, guarded save, timed clipboard |
| Symmetric file tools | `SecurityToolsService`, `FileHelper` | Base64, password/document-key file encryption and decryption, hashes/HMAC, destructive-operation confirmations |
| Asymmetric tools | `ProjectCryptoService`, `SecurityToolsService` | ECDH keys and encrypted files; X509 import/export; CMS signing/verification |
| OS integration | `ShellService`, `UserInteractionService`, helpers | File/password dialogs, clipboard expiry, exit, keyboard conventions and stay-on-top behavior |
| Distribution | `.github/workflows/dotnet.yml`, README | Platform packaging, offline assets, launch/shutdown and dependency checks, signing/notarization decisions |

## Portability findings
- `TM.csproj` enables WPF; views and helpers depend on `System.Windows`, `Dispatcher`, `Microsoft.Win32` dialogs and WPF clipboard APIs. Preserve strict separation using application services and platform interfaces; do not put these operations in Razor markup or desktop window code.
- `ProjectDocumentService` combines document operations with concrete `UserInteractionService` and WPF dialog enums. Extract dialog-free application operations before introducing web request handlers.
- `ProjectDocument` and entities reference `NodeModel`; `NodeModel` mixes observable tree state with `Visibility`, derived scheduling state and editing behavior. Extraction is not a simple folder move.
- **jnUtil 2.0.2 is Windows-targeted:** inspected installed package metadata identifies `net10.0-windows7.0` assets/dependencies. Portable core must replace the used helpers or use a verified portable version/source port; do not assume retargeting will work. No package upgrade is approved implicitly.
- `ProjectCryptoService` stores in-memory master and ECDH private keys using Windows DPAPI. Its persisted document/key/certificate encryption is a separate password-derived mechanism. Account-file encryption in `FileHelper` delegates to `jnUtil` and must remain explicitly Windows-only or be replaced through an approved design with export guidance.
- `SecurityHelper.GetKeyFromPassword` is deterministic for identical inputs; compatibility tests must preserve derivation parameters and payload encodings.
- Published document envelope uses `project_store`, salts, pepper, encrypted projects, optional `key_store` and `cert_store`. PBKDF2 count is 1,000,000 in `ProjectCryptoService`; algorithm/layout details inside jnUtil require additional research before any replacement.
- Current `Lock/ClearAll` clears master/ECDH keys, but does **not** dispose/clear `CaCertificate`. Flag this for the security design; baseline tests must not pretend all signing material is cleared.
- Password change returns false for no projects/no protected items. Failed loads can leave derived key state until cleared. These are existing behaviors to review explicitly, not copy blindly into a security-sensitive web backend.
- Existing entities use hash-based equality; new compatibility coverage should compare serialized field values, not only hash codes.
- File tools can wipe originals. Use disposable synthetic files for all acceptance testing; never use user originals.

## Proposed architecture and security gates
Use Photino as a thin desktop host, a loopback-only ASP.NET Core host for Razor/htmx, a portable application/domain layer, and platform adapters. Keep JS limited to desktop interactions and htmx lifecycle behavior. Bundle all frontend assets locally.

Before accepting the shell, specify session authentication, host/origin checks, antiforgery validation, navigation restrictions, CSP/HTML encoding, no-store sensitive responses, lock-time DOM/state clearing, bounded file operations, serialized document mutations, and host shutdown. Do not equate loopback binding with authentication. Before replacing DPAPI, approve the threat model for process-memory keys, OS keystores and account-bound files.

## Validation limits
- Current UI tests automate a Windows desktop and contain Debug output-path assumptions. They are not portable browser/Photino tests; retain them while adding portable core tests and later browser/OS smoke coverage.
- Current CI runs on Windows and publishes win-x64 only. Its triggers do not run on these new milestone branches unless a suitable PR is opened or workflow changed later.
- Linux/macOS validation has not run. A Windows build, cross-publish, or browser test alone is not proof of native Photino runtime compatibility.
- Release packaging, OS/architecture matrix, webview dependencies and native UX must be verified in later milestones.

## Baseline regression implementation research
Before production changes, freeze synthetic output from the unchanged baseline application in `TM.Test/Fixtures`. Include a nested hierarchy, Unicode/XML-special characters, fixed IDs/dates, protected values, ECDH keys and a synthetic CA certificate; never include real account details or real secrets. Store a plaintext synthetic expectation and private-key digest for independent comparisons. Generate once using a disposable external harness, never regenerate automatically during tests.

Add `EncryptedDocumentCompatibilityTests.cs` covering frozen fixture loading, load/save/reload, password change, wrong passwords, tampered key/secret data, absence of plaintext in saves and master/ECDH key clearing on lock. Embed fixtures in the test assembly to avoid working-directory assumptions. Retain the existing production code, dependencies, and UI unchanged in this milestone. Record any failed characterization explicitly rather than silently changing the format or weakening assertions.
