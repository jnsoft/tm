# Photino and htmx Rewrite Plan

## Overview

**Target**: Replace WPF with a portable .NET 10 Photino desktop host and local ASP.NET Core/Razor/htmx UI, without a heavy JavaScript framework.
**Scope**: Windows-first implementation using the existing jnUtil dependency and DPAPI. Linux/macOS builds, runtime validation and non-Windows key protection are deferred. Preserve WPF until replacement parity is accepted; continue automatically between tested checkpoint branches, reserving stable tags for manual acceptance.

## Tasks

### 01-baseline: Establish behavioral and encrypted-document regression coverage

On `photino-01-baseline`, document current features and dependencies, verify existing tests, and freeze synthetic encrypted-document compatibility fixtures. Expand regression checks without changing production behavior. Record build/test evidence and manual acceptance checks before a local checkpoint commit. Apply the stable tag only after manual acceptance.

**Done when**: The solution rebuilds without warnings, existing and new tests pass, and a local checkpoint is committed with outstanding manual checks documented. Stable tagging awaits manual acceptance.

---

### 02-portable-core: Separate portable domain, persistence and security services

Create `photino-02-core` from the tested baseline. Extract UI-independent operations and platform contracts and adapt WPF to consume the shared core. Keep jnUtil and Windows DPAPI for now; do not weaken key protection or claim that the Windows-targeted core is already portable. Preserve encrypted-file compatibility using frozen fixtures.

**Done when**: Shared core code no longer depends on WPF UI types, Windows compatibility tests pass, WPF remains functional, and a checkpoint is committed. Non-Windows dependency/key protection work remains explicitly deferred.

---

### 03-desktop-shell: Introduce the secured Photino and htmx desktop foundation

Create `photino-03-shell` from the accepted core milestone. Add a thin Photino host, local ASP.NET Core application, Razor HTML partial rendering and offline htmx assets. Establish local session protection, request validation, navigation constraints, configuration, DI, cancellation and deterministic lifecycle handling.

**Done when**: The shell launches and closes on Windows, functions offline, rejects unauthorized/cross-origin state changes, and passes security/lifecycle tests before a checkpoint commit. Native Linux/macOS validation is deferred.

---

### 04-application-ui: Replace the main WPF interaction surface

Create `photino-04-ui` from the accepted shell. Implement tree/to-do navigation, detail editing, document lifecycle, search/sorting, keyboard accessibility, valid drag/drop and protected-field workflows through application services. Preserve focus and editing state across htmx updates without introducing a heavy JS framework.

**Done when**: Windows automated tests verify main workflows, compatibility, dirty-state handling and lock-time removal of sensitive UI state. Commit a tested checkpoint and record outstanding manual keyboard/UX checks rather than claiming acceptance.

---

### 05-platform-tools: Port security utilities and native integrations

Create `photino-05-tools` from the accepted UI milestone. Complete file utilities, password/document/public-key encryption, signing/certificates, hashes/HMAC, clipboard expiry and native dialogs. Handle Windows-account-bound files according to the approved design; do not silently change their meaning or claim cross-account portability.

**Done when**: Windows disposable-file tests cover supported utilities, destructive/cancel/error paths, and documented platform limitations. Commit a tested checkpoint; retain Windows-account encryption as Windows-only.

---

### 06-cross-platform-release: Validate distribution and retire WPF

Create `photino-06-release` from the tested tools milestone. Provide Windows packaging, dependency checks and release documentation. Document future Linux/macOS work separately. Remove WPF from the production path only after accepted feature parity; keep the fallback if manual acceptance is outstanding.

**Done when**: A reproducible Windows package launches offline, regression/security tests pass, release instructions and deferred work are documented, and the checkpoint is committed. Final cutover/stable tagging requires manual acceptance; Linux/macOS remains future work.
