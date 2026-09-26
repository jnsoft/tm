# Windows Photino release checklist

## Scope

This checklist produces a **Windows x64 self-contained folder publish** for `TM.Desktop`. It does not retire the WPF application, establish cross-platform support, create an installer, or make a security/trust claim for the application or user-created certificates.

## Build

Run from the repository root on a supported Windows build machine:

```powershell
dotnet publish TM.Desktop/TM.Desktop.csproj -c Release -p:PublishProfile=Windows-x64-Folder -o artifacts/photino-win-x64
```

The `Windows-x64-Folder` profile intentionally keeps files unpacked and ReadyToRun disabled. The local `wwwroot` assets, Photino runtime files, and managed assemblies must remain beside the executable because the local host starts from `AppContext.BaseDirectory`.

## GitHub Actions

The repository's `.github/workflows/dotnet.yml` builds and tests on `windows-latest`, then creates a separately verified `photino-win-x64` workflow artifact from this same profile. Tag builds also attach a `TM-Photino-win-x64.zip` containing the complete folder publish to the GitHub release. The existing legacy WPF executable remains a separate release asset during migration.

The workflow verifies `TM.Desktop.exe`, `wwwroot`, `Photino.NET.dll`, and `WebView2Loader.dll` before upload. The uploaded folder contains a sorted `SHA256SUMS.txt` manifest for every published file. Tag releases also attach `TM-Photino-win-x64.sha256`, the SHA-256 checksum for the complete ZIP. Verify both before using an artifact. The workflow does not sign artifacts; complete the pre-sign validation and approved signing process before distributing any release asset.

- The local workflow-equivalent publish and ZIP check completed successfully on the checkpoint commit: the folder contained 371 files and `TM-Photino-win-x64.zip` was 50,300,549 bytes. Generated validation output was removed afterward and is not release evidence beyond this recorded result.
- The local SHA-256 manifest and ZIP-checksum check completed successfully on the integrity checkpoint: `SHA256SUMS.txt` contained 371 entries and `TM-Photino-win-x64.zip` was 50,318,519 bytes. Generated validation output was removed afterward.

## Checkpoint evidence

- `dotnet publish TM.Desktop\TM.Desktop.csproj -c Release -p:PublishProfile=Windows-x64-Folder -o artifacts\photino-win-x64` completed successfully on the checkpoint commit.
- The ignored local output contained 371 files totaling 115,245,940 bytes, including `TM.Desktop.exe`, `wwwroot`, `Photino.NET.dll`, and `WebView2Loader.dll`.
- This confirms publish layout only. Do not distribute this unsigned local artifact or treat it as clean-machine/native acceptance evidence.

## Pre-sign validation

1. Delete any prior output directory before publishing; do not mix artifacts from different commits.
2. Confirm the output contains the desktop executable and its adjacent `wwwroot`, Photino runtime, and managed dependency files.
3. Launch the executable from the output directory with normal-user permissions and without the developer working tree available.
4. Confirm bootstrap/session startup, collection create/open/save/save-as/lock/unlock, tree navigation/move/sort/timestamp, keyboard shortcuts, and dirty-close warning.
5. Confirm native dialogs handle cancel, inaccessible locations, existing output refusal, and Unicode paths.
6. Exercise generated passwords, clipboard expiry, password changes, file/hash/Base64 tools, document keys/HMAC, EFS, public-key files, CMS signing/verification, PFX/CER import/export, and encrypted transfer import/export with disposable test data.
7. Verify no paths, passwords, transfer keys, private keys, or plaintext protected values appear in normal status messages, browser dev tools are disabled, and the local host is unavailable without its bootstrap session.
8. Run the complete automated test suite from the release commit before signing.

## Signing and distribution

Code-sign every distributable executable and native binary using the organization-approved certificate and timestamp service. The root `README.md` contains existing Azure Trusted Signing and local development-signing guidance; update its commands to target `TM.Desktop` before automating a release pipeline.

Package only the complete contents of the publish directory. Produce hashes and release notes that identify the commit, .NET SDK/runtime, Photino.NET version, signing identity, and known Windows-only limitations.

## Current limitations

- WPF remains supported during the migration; this is a Photino preview/release-readiness artifact, not a WPF retirement.
- Windows DPAPI, EFS, Windows clipboard behavior, and native file dialogs require real Windows acceptance. EFS automated tests use a fake protection boundary.
- Linux/macOS builds, runtime behavior, native-dialog replacements, and non-Windows key protection are not validated by this profile.
- A successful publish does not substitute for accessibility, high-DPI, screen-reader, clean-machine, signing, installer, or enterprise-policy validation.
