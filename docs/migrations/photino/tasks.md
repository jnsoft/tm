# Photino Rewrite Progress

## Overview

Replace WPF incrementally with a shared core and Photino/Razor/htmx UI, working on Windows first with jnUtil retained. Platform-tool implementation is complete: password/clipboard workflows, hashes/Base64, file encryption, HMAC, EFS, public-key crypto, detached CMS signing, certificate import/public export and encrypted project transfer build and pass 174 tests. Evidence and remaining work are recorded in [tools-results.md](tools-results.md) and [ui-results.md](ui-results.md). EFS tests use a fake native boundary; manual native acceptance, stable tagging and Linux/macOS validation remain deferred.

**Progress**: 4/6 tasks complete <progress value="67" max="100"></progress> 67%

## Tasks

- ✅ 01-baseline: Establish behavioral and encrypted-document regression coverage
- ✅ 02-portable-core: Separate shared domain, persistence and security services (Windows first)
- ✅ 03-desktop-shell: Introduce the secured Photino and htmx desktop foundation
- 🔄 04-application-ui: Replace remaining WPF interactions — tree sorting/expand-collapse/focus, timestamping, drag/drop, keyboard/accessibility, and native UX acceptance
- ✅ 05-platform-tools: Port security utilities and native integrations — code/test checkpoint complete; real native dialogs, EFS, certificates, clipboard and transfer-key handling still require manual Windows acceptance
- 🔲 06-cross-platform-release: Validate Windows distribution, decide/implement non-Windows key protection and native replacements, validate Linux/macOS packaging/runtime, then retire WPF
