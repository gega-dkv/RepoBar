# Windows Release Checklist

## Before Packaging

- Update `version.env`.
- Run:
  - `PATH="$PWD/.dotnet:$PATH" dotnet build Windows/RepoBar.Windows.sln --no-restore`
  - `PATH="$PWD/.dotnet:$PATH" dotnet test Windows/RepoBar.Windows.sln --no-build`
- Confirm `windows-port-progress.md` has no unresolved Phase 8/9 release blockers.

## Build Artifacts

```powershell
pwsh Windows/Packaging/Scripts/package-windows.ps1 -Configuration Release -Runtime win-x64
```

Expected outputs:

- `Windows/artifacts/RepoBar-Windows-<version>-win-x64.zip`
- `Windows/artifacts/RepoBar-Windows-<version>-win-x64.msi` when WiX CLI is installed

## Signing

- Set `REPOBAR_SIGNTOOL`.
- Set `REPOBAR_SIGN_CERT_SHA1`.
- Set `REPOBAR_TIMESTAMP_URL` or accept the default timestamp URL.
- Run package script with `-Sign`.
- Verify signatures with `signtool verify /pa`.

## Manual Windows 11 Verification

- Install cleanly from MSI.
- Launch from Start menu and confirm tray icon appears.
- Authenticate with PAT and verify release credentials use Windows Credential Manager.
- Enable launch at login and verify after reboot.
- Scan a local project folder and open a local repo path.
- Open a repository URL through the default browser.
- Upgrade from the previous MSI and verify app binaries changed while `%AppData%\RepoBar` and `%LocalAppData%\RepoBar` data remains.
- Uninstall and verify app binaries and shortcuts are removed while settings/cache are not deleted.

## Publishing

- Upload Windows ZIP/MSI artifacts to the same GitHub release as macOS artifacts.
- Do not change the macOS Sparkle appcast flow.
- Windows v1 update path is manual MSI upgrade from GitHub Releases. Automatic Windows updates are deferred until the installer route has multiple successful releases.
