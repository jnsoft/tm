# Photino and htmx Rewrite

## Strategy
Incrementally replace the WPF UI with Photino, local ASP.NET Core, Razor HTML partials, and htmx while extracting a portable .NET 10 application core.

## Preferences
- **Flow Mode**: Automatic; continue through the remaining work, pausing for genuine blockers or security decisions rather than routine milestone approval.
- **Commit Strategy**: After Each Phase; commit validated milestone changes locally and tag stable milestones only after their manual acceptance checks pass.
- **UI**: Photino and htmx with minimal JavaScript; no heavy JavaScript framework.
- **Platforms**: Windows first; defer Linux/macOS builds and runtime validation, while retaining them as the eventual portability goal.
- **jnUtil**: Retain the existing dependency for the Windows implementation. The user states it can be built for Linux; do not replace it merely because the installed package is Windows-targeted.
- **Build Tool**: Use Visual Studio build tools; user approved this after canceling a terminal rebuild.
- **Compatibility**: Preserve existing encrypted document formats and behavior unless an explicit security decision requires a documented change.
- **Continuity**: Keep WPF runnable until the replacement reaches accepted feature parity.

## Source Control
- **Source Branch**: rewrite
- **Source Commit**: cf0a98b154b06cb7529e6a0c2433d543e3875174
- **Initial Working Branch**: photino-01-baseline
- **Branch Sequence**: photino-01-baseline, photino-02-core, photino-03-shell, photino-04-ui, photino-05-tools, photino-06-release
- **Branch Rule**: Create each next branch from the previous accepted milestone commit, not from rewrite.
- **Commit Strategy**: Local checkpoint per validated milestone; stable tag after manual acceptance.
- **Branch Sync**: Disabled; no automatic merges, pushes, or history rewriting.

## Decisions
- The user approved the full rewrite and milestone sequence, with explicit testing and branch checkpoints.
- No installed modernization scenario covers WPF-to-Photino; track this work with repository documents and general plan tools, not an unrelated upgrade scenario or fabricated scenario state.
- Baseline work must use synthetic test data, never real passwords or user documents.
- Windows DPAPI and non-Windows in-memory key protection require explicit runtime validation; do not silently weaken protection.
- This first milestone changes tests and documentation, not the production UI or document format.
- The user requested continuing the remaining rewrite with Windows working first; this supersedes routine milestone approval pauses and immediate cross-platform acceptance gates.
- Preserve DPAPI on Windows; the portable jnUtil build and authenticated process-local non-Windows key protection are implemented but not yet runtime-validated on Linux/macOS.
- Continue to commit tested checkpoints and branch before major changes; outstanding manual checks prevent stable tags, not work on subsequent checkpoint branches.
- User approved continuing validation using Visual Studio build after canceling the terminal rebuild; no claim of a clean command-line rebuild is made.

## Custom Instructions
- For each milestone, record research before changing code and record build/test evidence and outstanding manual checks before committing.
- Do not mark the entire rewrite or a platform validated merely because the Windows solution builds.
