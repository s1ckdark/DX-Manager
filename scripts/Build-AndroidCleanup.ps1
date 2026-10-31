param([switch]$Debug,[switch]$SkipTests,[string]$SigningPropertiesPath,
 [string]$ChangeSummary='Build the shared official Companion from this workroom.')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$shared=[IO.Path]::GetFullPath((Join-Path $repo '..\DX Companion Shared'))
if(!$SigningPropertiesPath){
 $candidates=@((Join-Path $repo 'DXDisplayCleanup\signing.properties'),'E:\vs\dex system-portable-backport\DXDisplayCleanup\signing.properties')
 $SigningPropertiesPath=$candidates|Where-Object{Test-Path -LiteralPath $_ -PathType Leaf}|Select-Object -First 1
}
& (Join-Path $shared 'Build-Companion.ps1') -Debug:$Debug -SkipTests:$SkipTests -SigningPropertiesPath $SigningPropertiesPath -Workroom 'GitHub' -ThreadTitle 'dx manager 깃헙판 ui 업데이트' -ThreadId '01a0e7aa-4ff2-76b3-a282-8125eab39e62' -ChangeSummary $ChangeSummary