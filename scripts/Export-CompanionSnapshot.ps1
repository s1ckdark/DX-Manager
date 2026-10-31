param([string]$SourceRoot = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$SourceRoot) { $SourceRoot = Join-Path $repo '..\DX Companion Shared' }
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$target = Join-Path $repo 'companion'
if ($SourceRoot.Equals($target, [StringComparison]::OrdinalIgnoreCase)) { throw 'Export requires the canonical shared component as input.' }
# Copy only the release manifest allowlist; signing inputs and caches are never enumerated.
& (Join-Path $PSScriptRoot 'Verify-CompanionSnapshot.ps1') -Root $SourceRoot
$release = Get-Content -LiteralPath (Join-Path $SourceRoot 'release.json') -Raw | ConvertFrom-Json
$files = @($release.SourceFiles | ForEach-Object { 'source/' + $_.Path }) + @(
    'integration/CompanionRelease.cs', 'release/DX-Companion.apk', 'licenses/Pretendard-LICENSE.txt'
)
foreach ($relative in $files) {
    $destination = [IO.Path]::GetFullPath((Join-Path $target $relative))
    if (!$destination.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Export path escapes companion/.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceRoot $relative) -Destination $destination -Force
}
$public = [ordered]@{}
foreach ($key in @('VersionName','VersionCode','PackageName','ApkRelativePath','ApkSha256','CertificateSha256','OriginalWorkroom','OriginalCommit','MaintainedByWorkroom','ChangedAt','SourceFiles')) {
    $public[$key] = $release.$key
}
$public['SourcePolicy'] = 'Generated release snapshot. Maintain Android code in the canonical shared component and re-export.'
[IO.File]::WriteAllText((Join-Path $target 'release.json'), ($public | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Verify-CompanionSnapshot.ps1') -Root $target
