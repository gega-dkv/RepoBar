param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$ArtifactsDir = "Windows/artifacts",
  [switch]$Sign
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$versionFile = Join-Path $root "version.env"
$envFile = Join-Path $root "Windows/Packaging/package.env"

function Read-EnvFile($path) {
  $map = @{}
  Get-Content $path | ForEach-Object {
    if ($_ -match "^\s*([^#][^=]+)=(.*)$") {
      $map[$matches[1].Trim()] = $matches[2].Trim()
    }
  }
  return $map
}

$version = Read-EnvFile $versionFile
$package = Read-EnvFile $envFile
$productVersion = "$($version["MARKETING_VERSION"]).$($version["BUILD_NUMBER"])"
$publishRoot = Join-Path $root "$ArtifactsDir/publish/$Runtime"
$packageRoot = Join-Path $root "$ArtifactsDir/package/$Runtime"
$zipPath = Join-Path $root "$ArtifactsDir/RepoBar-Windows-$productVersion-$Runtime.zip"
$msiPath = Join-Path $root "$ArtifactsDir/RepoBar-Windows-$productVersion-$Runtime.msi"

& (Join-Path $root "Windows/Packaging/Scripts/publish-windows.ps1") -Configuration $Configuration -Runtime $Runtime -ArtifactsDir $ArtifactsDir

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath

$generated = Join-Path $packageRoot "RepoBar.GeneratedFiles.wxs"
& (Join-Path $root "Windows/Packaging/Scripts/write-wix-file-list.ps1") -PublishDir $publishRoot -OutputPath $generated

$wix = Get-Command wix -ErrorAction SilentlyContinue
if ($null -eq $wix) {
  Write-Warning "WiX CLI not found. Install with: dotnet tool install --global wix"
  Write-Warning "ZIP artifact was created, MSI was skipped: $zipPath"
} else {
  wix build `
    (Join-Path $root "Windows/Packaging/Wix/Package.wxs") `
    $generated `
    -d "ProductVersion=$productVersion" `
    -d "Manufacturer=$($package["REPOBAR_WINDOWS_MANUFACTURER"])" `
    -d "UpgradeCode=$($package["REPOBAR_WINDOWS_UPGRADE_CODE"])" `
    -d "AppId=$($package["REPOBAR_WINDOWS_APP_ID"])" `
    -d "PublishDir=$publishRoot" `
    -o $msiPath
}

if ($Sign) {
  & (Join-Path $root "Windows/Packaging/Scripts/sign-windows-artifacts.ps1") -Artifacts @($zipPath, $msiPath)
}

Write-Host "Windows package artifacts are under $(Join-Path $root $ArtifactsDir)"
