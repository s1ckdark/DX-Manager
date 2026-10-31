param([string]$DestinationRoot = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!(Test-Path -LiteralPath (Join-Path $repo 'DexManager\DexManager.csproj'))) {
    throw 'The pinned scrcpy runtime requires a DX Manager checkout.'
}
$workspacePreview = 'E:\vs\dex system\dist\DX Manager Preview\tools\scrcpy'
$allowWorkspacePreview = $repo -eq 'E:\vs\dex system-portable-backport'
$pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'scrcpy-runtime.json') -Raw | ConvertFrom-Json
$source = Join-Path $repo 'DexManager\tools\scrcpy'
function Assert-RuntimePath([string]$path) {
    $absolute = [IO.Path]::GetFullPath($path)
    if (!$absolute.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase) -and !($allowWorkspacePreview -and ($absolute -eq $workspacePreview -or $absolute.StartsWith($workspacePreview + '\', [StringComparison]::OrdinalIgnoreCase)))) {
        throw 'Runtime path escapes the approved workspace.'
    }
    $ancestor = $absolute
    while ($ancestor.Length -gt 5) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Runtime paths cannot traverse a reparse point.'
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}
Assert-RuntimePath $source
$names = @()
foreach ($file in $pin.Files) {
    if ([string]::IsNullOrWhiteSpace($file.Name) -or
        [IO.Path]::GetFileName($file.Name) -ne $file.Name -or
        $file.Name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
        $file.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or $file.Name -in $names) {
        throw 'Invalid or duplicate scrcpy runtime manifest entry.'
    }
    $names += $file.Name
    $path = Join-Path $source $file.Name
    Assert-RuntimePath $path
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) {
        throw "Pinned scrcpy $($pin.Version) source mismatch: $($file.Name)"
    }
}
foreach ($required in @('scrcpy.exe','scrcpy-server','SDL3.dll','adb.exe')) {
    if ($required -notin $names) { throw "Incomplete runtime manifest: $required" }
}
if (@(Get-ChildItem -LiteralPath $source -Force | Where-Object {$_.Name -notin $names -and $_.Name -ne '.gitkeep'}).Count) {
    throw 'Unexpected files or folders in the pinned scrcpy source runtime.'
}
if ($DestinationRoot) {
    $destination = [IO.Path]::GetFullPath($DestinationRoot)
    $allowed = @(
        (Join-Path $repo 'DexManager\bin\Release\tools\scrcpy'),
        (Join-Path $repo '.build-tools\preview-staging\payload\tools\scrcpy'),
        (Join-Path $repo 'dist\DX Manager\tools\scrcpy')
    )
    if ($allowWorkspacePreview) { $allowed += $workspacePreview }
    if ($destination -notin $allowed) { throw 'Unsupported generated runtime destination.' }
    Assert-RuntimePath $destination
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    $existing = @(Get-ChildItem -LiteralPath $destination -Force)
    if (@($existing | Where-Object {$_.PSIsContainer -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)}).Count) {
        throw 'Generated scrcpy runtime must contain only ordinary files.'
    }
    # This exact generated runtime directory contains no configuration or user data.
    # Remove obsolete 4.1 DLLs before copying the complete verified 5.x set.
    foreach ($file in $existing) {
        if ($file.Name -notin $names) {
            Assert-RuntimePath $file.FullName
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }
    foreach ($file in $pin.Files) {
        $target = Join-Path $destination $file.Name
        if (!(Test-Path -LiteralPath $target) -or
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Sha256) {
            Copy-Item -LiteralPath (Join-Path $source $file.Name) -Destination $target -Force
        }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Sha256) {
            throw "Runtime copy verification failed: $($file.Name)"
        }
    }
}
Write-Host ("Verified pinned scrcpy " + $pin.Version + " (" + $names.Count + " official files).")