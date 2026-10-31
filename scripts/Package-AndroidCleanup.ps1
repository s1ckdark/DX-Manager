param([switch]$SkipBuild,[string]$SigningPropertiesPath,
 [string]$ChangeSummary='Build and verify the shared official Companion.')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Get-CompanionRoot.ps1')
$shared=Get-DxmCompanionRoot -RepositoryRoot $repo
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'Build-AndroidCleanup.ps1') -SigningPropertiesPath $SigningPropertiesPath -ChangeSummary $ChangeSummary}
& (Join-Path $PSScriptRoot 'Verify-CompanionSnapshot.ps1') -Root $shared
Get-Item -LiteralPath (Join-Path $shared 'release\DX-Companion.apk')|Select-Object FullName,Length