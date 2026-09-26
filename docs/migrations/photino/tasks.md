# Photino Rewrite Progress

## Overview

Replace WPF incrementally with a shared core and Photino/Razor/htmx UI. The shared core and desktop host now target portable .NET 10 through jnUtil 2.0.0; Linux and macOS publish layouts compile, but native runtime acceptance remains pending. Platform tools include portable password file encryption, clipboard workflows, hashes/Base64, HMAC, public-key crypto, detached CMS signing, certificate import/public export and encrypted project transfer. Evidence and remaining work are recorded in [tools-results.md](tools-results.md), [ui-results.md](ui-results.md), and the [Windows release checklist](windows-release.md).

**Progress**: 4/6 tasks complete <progress value="67" max="100"></progress> 67%

## Tasks

- ✅ 01-baseline: Establish behavioral and encrypted-document regression coverage
- ✅ 02-portable-core: Separate shared domain, persistence and security services; TM.Core now targets portable .NET 10
- ✅ 03-desktop-shell: Introduce the secured Photino and htmx desktop foundation
- 🔄 04-application-ui: Replace remaining WPF interactions — tree sorting/expand-collapse/focus, timestamping, drag/drop, keyboard/accessibility, and native UX acceptance
- 🔄 05-platform-tools: Portable password-encryption workflow replaces account-file encryption; real native dialogs, certificates, clipboard and transfer-key handling still require platform acceptance
- 🔄 06-cross-platform-release: Windows x64 self-contained folder publish profile and Linux/macOS publish RIDs are available; validate signed/distributed clean-machine artifacts, native runtime behavior, platform key protection and clipboard integrations, then retire WPF
