# RepoBar Windows Packaging

Windows v1 uses a conventional WiX MSI plus ZIP release artifact.

## Decision

- **Selected route**: WiX MSI.
- **Stable app ID**: `com.openclaw.repobar.windows`.
- **UpgradeCode**: `6C6D9F17-4A23-47F4-8E52-92AE57CF18C2`.
- **Update route**: install a newer signed MSI from GitHub Releases. Sparkle is macOS-only and is not used for Windows.

MSIX remains deferred until Phase 9 can prove tray behavior, Run-key or packaged startup behavior, arbitrary local Git folder access, shell launch, and Windows Credential Manager behavior on a Windows 11 VM or device. MSIX containerization is useful, but it changes file/registry behavior enough that WiX is the lower-risk v1 route for RepoBar's local Git and debug-auth workflows.

## Release Commands

From the repository root:

```powershell
pwsh Windows/Packaging/Scripts/publish-windows.ps1 -Configuration Release -Runtime win-x64
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64
pwsh Windows/Packaging/Scripts/validate-windows-package.ps1 -ArtifactsDir Windows/artifacts
```

Signing is required for release artifacts:

```powershell
$env:REPOBAR_SIGNTOOL = "C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe"
$env:REPOBAR_SIGN_CERT_SHA1 = "<certificate thumbprint>"
$env:REPOBAR_TIMESTAMP_URL = "http://timestamp.digicert.com"
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64 -Sign
```

## Required Manual Windows Verification

- Clean install on Windows 11.
- Launch from Start menu and verify tray icon appears.
- PAT login stores the token in Windows Credential Manager for release builds.
- Debug auth can be forced to file storage with `REPOBAR_TOKEN_STORE=file`.
- Local project scan can read an arbitrary user-selected folder.
- Shell launch opens repository URLs and local folders.
- Launch at login works after reboot.
- Upgrade to a newer MSI preserves `%AppData%\RepoBar` settings and `%LocalAppData%\RepoBar` cache.
- Uninstall removes app binaries and shortcuts but does not delete settings/cache unless a future explicit cleanup tool is used.
