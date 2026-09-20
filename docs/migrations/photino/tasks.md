# Photino Rewrite Progress

## Overview

Replace WPF incrementally with a shared core and Photino/Razor/htmx UI, working on Windows first with jnUtil retained. Password workflows, clipboard copying, hashing/Base64 and password/document-key file encryption build and pass 139 tests; evidence and remaining work are recorded in [tools-results.md](tools-results.md) and [ui-results.md](ui-results.md). Continue between tested checkpoint branches; manual stable tags and Linux/macOS validation remain deferred.

**Progress**: 3/6 tasks complete <progress value="50" max="100"></progress> 50%

## Tasks

- ✅ 01-baseline: Establish behavioral and encrypted-document regression coverage
- ✅ 02-portable-core: Separate shared domain, persistence and security services (Windows first)
- ✅ 03-desktop-shell: Introduce the secured Photino and htmx desktop foundation
- 🔄 04-application-ui: Replace the main WPF interaction surface
- 🔄 05-platform-tools: Port security utilities and native integrations
- 🔲 06-cross-platform-release: Validate distribution and retire WPF
