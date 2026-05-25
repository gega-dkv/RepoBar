param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$ArtifactsDir = "Windows/artifacts"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$publishRoot = Join-Path $root "$ArtifactsDir/publish/$Runtime"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

dotnet publish (Join-Path $root "Windows/RepoBar.Desktop/RepoBar.Desktop.csproj") `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:PublishReadyToRun=true `
  -p:PublishSingleFile=false `
  -o $publishRoot

dotnet publish (Join-Path $root "Windows/RepoBar.Cli/RepoBar.Cli.csproj") `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:PublishReadyToRun=true `
  -p:PublishSingleFile=false `
  -o (Join-Path $publishRoot "cli")

Write-Host "Published RepoBar Windows artifacts to $publishRoot"
