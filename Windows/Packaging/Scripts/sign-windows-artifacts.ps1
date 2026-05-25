param(
  [Parameter(Mandatory = $true)][string[]]$Artifacts
)

$ErrorActionPreference = "Stop"
$signtool = $env:REPOBAR_SIGNTOOL
$thumbprint = $env:REPOBAR_SIGN_CERT_SHA1
$timestampUrl = if ($env:REPOBAR_TIMESTAMP_URL) { $env:REPOBAR_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }

if ([string]::IsNullOrWhiteSpace($signtool) -or -not (Test-Path $signtool)) {
  throw "REPOBAR_SIGNTOOL must point to signtool.exe."
}

if ([string]::IsNullOrWhiteSpace($thumbprint)) {
  throw "REPOBAR_SIGN_CERT_SHA1 must contain the signing certificate thumbprint."
}

foreach ($artifact in $Artifacts) {
  if (Test-Path $artifact) {
    & $signtool sign /sha1 $thumbprint /fd SHA256 /tr $timestampUrl /td SHA256 $artifact
  }
}
