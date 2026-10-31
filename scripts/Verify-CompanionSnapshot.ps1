param([string]$Root = '')
$ErrorActionPreference = 'Stop'
if (!$Root) { $Root = Join-Path $PSScriptRoot '..\companion' }
$Root = [IO.Path]::GetFullPath($Root)
$release = Get-Content -LiteralPath (Join-Path $Root 'release.json') -Raw | ConvertFrom-Json
$seen = @{}
foreach ($file in $release.SourceFiles) {
    $relative = [string]$file.Path
    $sourceRoot = [IO.Path]::GetFullPath((Join-Path $Root 'source')) + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
    if (!$path.StartsWith($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($relative) -or
        $file.Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Invalid Companion source manifest entry.' }
    $seen[$relative] = $true
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) {
        throw "Companion source hash mismatch: $relative"
    }
}
$apk = Join-Path $Root $release.ApkRelativePath
if ((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash -ne $release.ApkSha256) { throw 'Official Companion APK hash mismatch.' }
$constants = Get-Content -LiteralPath (Join-Path $Root 'integration\CompanionRelease.cs') -Raw
foreach ($value in @($release.PackageName, $release.VersionName, $release.ApkSha256, $release.CertificateSha256)) {
    if (!$constants.Contains([string]$value)) { throw 'Companion constants do not match the release manifest.' }
}
if ($constants -notmatch ('VersionCode\s*=\s*' + $release.VersionCode + '\s*;')) { throw 'Companion version code mismatch.' }
Write-Host ("Verified official Companion {0}/code{1}: {2} source files and the signed APK." -f $release.VersionName,$release.VersionCode,$seen.Count)
