# Windows Photino shell checkpoint

## Implemented
- Integrated TM.Desktop into the solution with Photino.NET 4.0.16, local Razor rendering, htmx 2.0.10 and bundled 0BSD license/digest.
- Loopback-only random-port Kestrel; one-use bootstrap token, per-launch HttpOnly/SameSite session cookie, Host/Origin checks, antiforgery protection and ephemeral data-protection keys.
- No-store/no-referrer/nosniff/frame/CSP headers, no remote scripts, disabled htmx evaluation/script execution/history snapshots and no privileged web-message bridge.
- Restricted webview permissions and a per-launch profile. Native window close stops the server; cache cleanup is best-effort if WebView2 holds files briefly.

## Validation
- Visual Studio solution build successful.
- **68 passed, 0 failed, 0 skipped**: existing core/crypto/WPF tests plus seven actual HTTP integration tests and one native Photino smoke test.
- HTTP tests verify unauthorized access, bootstrap replay, protected cookie flags, wrong Host/Origin rejection, antiforgery failure/success, offline assets and listener shutdown.
- Native smoke test verifies an authenticated Razor page in the Photino window, invokes its check button, closes the window and observes process exit code 0. It does not establish complete UI parity or network-isolated operation.
- Fixed test-only process ownership: FlaUI attaches by PID so disposing it cannot invalidate the test's separate Process handle.
- UI tests explicitly build TM.Desktop before running, preventing stale-executable validation.

## Remaining limits
Main document workflows and utilities are not implemented yet. Windows-only for now. Photino's inspected API lacks navigation cancellation; there is no claim of a native navigation allowlist. No manual stable tag, full accessibility/keyboard acceptance, Windows installer, or Linux/macOS validation is claimed.
