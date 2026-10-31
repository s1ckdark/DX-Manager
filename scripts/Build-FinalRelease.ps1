param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($repo -ne 'E:\vs\dex system-portable-backport'){throw 'Use the approved official portable worktree.'}
$dist=[IO.Path]::GetFullPath('E:\vs\dex system\dist')
$target=[IO.Path]::GetFullPath((Join-Path $dist 'DX Manager v2.5.0'))
$archive=[IO.Path]::GetFullPath((Join-Path $dist 'DX-Manager-win-x64.zip'))
$records=Join-Path $repo '.build-tools\final250'
$backup=[IO.Path]::GetFullPath((Join-Path $records 'previous-final'))
if(!$target.StartsWith($dist+'\',[StringComparison]::OrdinalIgnoreCase) -or
   !$archive.StartsWith($dist+'\',[StringComparison]::OrdinalIgnoreCase) -or
   !$backup.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Final/backup paths escape their approved roots.'}
foreach($process in Get-Process -Name DXManager -ErrorAction SilentlyContinue) {
    if($process.Path -and [IO.Path]::GetFullPath($process.Path).Equals((Join-Path $target 'DXManager.exe'),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'The final folder is running. Exit it before creating a clean distribution.'
    }
}
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'Verify-OfficialBuild.ps1')}
if(Test-Path -LiteralPath $target) {
    foreach($directory in @($target,$backup)) {
        if(Test-Path -LiteralPath $directory) {
            if((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Final/backup directory is a reparse point.'}
            if(@(Get-ChildItem -LiteralPath $directory -Recurse -Attributes ReparsePoint).Count){throw 'Final/backup tree contains a reparse point.'}
        }
    }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach($item in Get-ChildItem -LiteralPath $target -Force){Copy-Item -LiteralPath $item.FullName -Destination $backup -Recurse -Force}
    foreach($item in Get-ChildItem -LiteralPath $target -File -Recurse) {
        $relative=$item.FullName.Substring($target.Length+1)
        if((Get-FileHash -LiteralPath $item.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $backup $relative)).Hash){throw ('Previous final backup verification failed: '+$relative)}
    }
}
& (Join-Path $PSScriptRoot 'Package-Release.ps1') -SkipBuild -NoArchive -Version '2.5.0' -OutputRoot $dist -PackageDirectoryName 'DX Manager v2.5.0'
foreach($name in @('settings.json','onboarding.json','update-check.json','update-notice.json')) {
    if(Test-Path -LiteralPath (Join-Path $target ('config\'+$name))){throw ('Private state was bundled: '+$name)}
}
if(Test-Path -LiteralPath (Join-Path $target 'docs')){throw 'External documentation/images must not be included.'}
if(@(Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.log','.cs','.java','.keystore','.jks') -or $_.Name -in @('signing.properties','preview-files.json','official-build-identity.json') }).Count){throw 'Development/private files were bundled.'}
$manifest=@(Get-ChildItem -LiteralPath $target -Recurse -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($target.Length+1);Length=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
[IO.File]::WriteAllText((Join-Path $records 'release-manifest.json'),($manifest | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($true))
Compress-Archive -LiteralPath $target -DestinationPath $archive -CompressionLevel Optimal -Force
Write-Host ('Final executable: '+(Join-Path $target 'DXManager.exe'))
Write-Host ('Final archive: '+$archive)
Write-Host ('Previous final including personal data preserved in: '+$backup)
Write-Host 'Fresh distribution: first-run acknowledgement is required. Stable ZIP name; versioned internal folder.'
