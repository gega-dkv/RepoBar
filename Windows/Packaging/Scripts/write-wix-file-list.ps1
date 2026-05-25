param(
  [Parameter(Mandatory = $true)][string]$PublishDir,
  [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = "Stop"
$publishPath = Resolve-Path $PublishDir
$files = Get-ChildItem -Path $publishPath -File -Recurse | Sort-Object FullName
$directoryLines = New-Object System.Collections.Generic.List[string]
$componentLines = New-Object System.Collections.Generic.List[string]
$componentRefs = New-Object System.Collections.Generic.List[string]
$directoryIds = @{"." = "INSTALLFOLDER"}
$index = 0
$directoryIndex = 0

function Convert-ToWixId([string]$value) {
  $clean = [System.Text.RegularExpressions.Regex]::Replace($value, "[^A-Za-z0-9_]", "_")
  if ($clean -match "^[0-9]") {
    return "D_$clean"
  }
  return $clean
}

foreach ($directory in (Get-ChildItem -Path $publishPath -Directory -Recurse | Sort-Object FullName)) {
  $directoryIndex += 1
  $relative = [System.IO.Path]::GetRelativePath($publishPath, $directory.FullName)
  $id = "PublishDir$directoryIndex" + "_" + (Convert-ToWixId $relative)
  $directoryIds[$relative] = $id
  $parent = [System.IO.Path]::GetDirectoryName($relative)
  if ([string]::IsNullOrWhiteSpace($parent)) {
    $parent = "."
  }
  $parentId = $directoryIds[$parent]
  $name = [System.IO.Path]::GetFileName($directory.FullName)
  $directoryLines.Add("    <DirectoryRef Id=`"$parentId`">")
  $directoryLines.Add("      <Directory Id=`"$id`" Name=`"$name`" />")
  $directoryLines.Add("    </DirectoryRef>")
}

foreach ($file in $files) {
  $index += 1
  $id = "PublishFile$index"
  $relative = [System.IO.Path]::GetRelativePath($publishPath, $file.FullName)
  $relativeDirectory = [System.IO.Path]::GetDirectoryName($relative)
  if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
    $relativeDirectory = "."
  }
  $directoryId = $directoryIds[$relativeDirectory]
  $source = '$(var.PublishDir)\' + $relative
  $componentLines.Add("      <Component Id=`"$id`" Directory=`"$directoryId`" Guid=`"*`">")
  $componentLines.Add("        <File Source=`"$source`" KeyPath=`"yes`" />")
  $componentLines.Add("      </Component>")
  $componentRefs.Add("      <ComponentRef Id=`"$id`" />")
}

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
$($directoryLines -join [Environment]::NewLine)
  </Fragment>
  <Fragment>
    <ComponentGroup Id="RepoBarPublishFiles">
$($componentLines -join [Environment]::NewLine)
    </ComponentGroup>
  </Fragment>
</Wix>
"@

New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null
Set-Content -Path $OutputPath -Value $xml -Encoding UTF8
Write-Host "Generated WiX file list: $OutputPath"
