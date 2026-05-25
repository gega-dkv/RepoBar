param(
  [string]$ArtifactsDir = "Windows/artifacts"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$publish = Get-ChildItem -Path (Join-Path $root "$ArtifactsDir/publish") -Directory -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $publish) {
  throw "No publish directory found under $ArtifactsDir/publish."
}

$desktop = Join-Path $publish.FullName "RepoBar.Desktop.exe"
$cli = Join-Path $publish.FullName "cli/RepoBar.Cli.exe"

if (-not (Test-Path $desktop)) { throw "Missing RepoBar.Desktop.exe." }
if (-not (Test-Path $cli)) { throw "Missing RepoBar.Cli.exe." }

$zip = Get-ChildItem -Path (Join-Path $root $ArtifactsDir) -Filter "RepoBar-Windows-*.zip" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $zip) { throw "Missing Windows ZIP artifact." }

Write-Host "Windows package validation passed."
