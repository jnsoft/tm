# Photino Rewrite Progress

## Overview

Replace WPF incrementally with a shared core and Photino/Razor/htmx UI, working on Windows first with jnUtil retained. Continue between tested checkpoint branches; manual stable tags and Linux/macOS validation remain deferred.

**Progress**: 2/6 tasks complete <progress value="33" max="100"></progress> 33%

## Tasks

- ✅ 01-baseline: Establish behavioral and encrypted-document regression coverage
- ✅ 02-portable-core: Separate shared domain, persistence and security services (Windows first)
- 🔄 03-desktop-shell: Introduce the secured Photino and htmx desktop foundation
- 🔲 04-application-ui: Replace the main WPF interaction surface
- 🔲 05-platform-tools: Port security utilities and native integrations
- 🔲 06-cross-platform-release: Validate distribution and retire WPF
