# Windows desktop shell research

## Dependencies
NuGet metadata queried for Photino.NET: use stable 4.0.16 (net8/net9 assemblies, native dependency 4.0.22). Inspected API source at package revision `4e8b40a3732d7bacedccc4c718865990c148823a`, `Photino.NET/PhotinoWindow.NET.cs`. Confirmed Load, WaitForClose, Invoke, ShowOpenFileAsync/ShowSaveFileAsync, temporary-profile and browser-security setters. npm metadata reports htmx.org 2.0.10, 0BSD; vendor exact assets and license for offline use, no npm runtime or heavy JS framework.

## Host design
Create TM.Desktop (ASP.NET Core Web SDK, net10.0-windows/win-x64) referencing TM.Core and Photino.NET. Keep Razor/PageModel presentation separate from application services/ViewModels. Start Kestrel on IPv4 loopback with OS-assigned port; obtain the actual bound origin before launching the window. Run Photino's native loop on a dedicated STA thread while asynchronously awaiting application lifetime, so asynchronous host startup does not lose STA affinity.

Use an in-memory, random, one-use bootstrap URL to issue a separate HttpOnly SameSite=Strict session cookie. Disable request logging that could persist bootstrap credentials. Validate Host and remote loopback address, authenticate all assets/pages, require exact Origin on unsafe methods, and use Razor antiforgery tokens. Send no-store, no-referrer, nosniff, restrictive CSP/frame headers. Bundle scripts/styles, disable htmx evaluation/script processing/history snapshots. Disable webview devtools, file-system access, clipboard JS, media permissions and insecure certificate handling. No privileged web-message bridge; never navigate using user-provided URLs. Photino exposes no navigation-cancel callback in the inspected managed API, so do not claim a native navigation allowlist; CSP, link handling and authenticated endpoints are the boundary.

A fresh per-launch profile directory avoids shared cookies/cache. Window closure shuts down Kestrel and disposes session services. Deleting the temporary profile is best-effort after native closure and must not mask shutdown errors.

## Validation
Add in-process HTTP integration tests for unauthenticated requests, single-use bootstrap, session cookies, Host/Origin rejection, antiforgery, offline assets/security headers and shutdown. Keep all existing WPF tests. Native window smoke validation on this Windows machine is separate from HTTP tests; do not claim Linux/macOS validation.
