param([switch]$SkipBuild, [switch]$LiveUpdateCheck)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($repo -ne 'E:\vs\dex system-portable-backport'){throw 'Use the approved official portable worktree.'}
$validation=Join-Path $repo '.build-tools\official250'
[IO.Directory]::CreateDirectory($validation) | Out-Null
$msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe'
$compiler=Join-Path ([IO.Path]::GetDirectoryName($msbuild)) 'Roslyn\csc.exe'
& (Join-Path $PSScriptRoot 'Sync-ScrcpyRuntime.ps1') -DestinationRoot (Join-Path $repo 'DexManager\bin\Release\tools\scrcpy')
if(!$SkipBuild) {
    & $msbuild (Join-Path $repo 'DexManager.sln') /t:Build /p:Configuration=Release ("/p:TargetFrameworkRootPath="+(Join-Path $repo '.build-tools\net462\build')) /m:1 /nodeReuse:false *> (Join-Path $validation 'build.log')
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $validation 'build.log') -Tail 20;throw 'Official build failed.'}
}
$product=Join-Path $repo 'DexManager\bin\Release\DXManager.exe'
& (Join-Path $repo 'DexManager.MultiDeviceTests\bin\Release\DexManager.MultiDeviceTests.exe') *> (Join-Path $validation 'foundation.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $validation 'foundation.log') -Tail 10;throw 'Foundation regression failed.'}
Get-Content -LiteralPath (Join-Path $validation 'foundation.log') -Tail 1
$results=@()
foreach($test in @(
    @('UpdateCheckIntegrationTests','update-core'),
    @('PhoneTransferIntegrationTests','phone-transfer'),
    @('TransferUiIntegrationTests','transfer-ui'),
    @('WirelessResponsivenessIntegrationTests','wireless-ui'),
    @('ScrcpyRuntimeIntegrationTests','runtime-check'),
    @('DesktopNoticeIntegrationTests','desktop-ui'),
    @('OnboardingUiIntegrationTests','onboarding-ui'),
    @('ExperimentalFrameRateIntegrationTests','experimental-fps'),
    @('LocalizationUiIntegrationTests','english-ui'))) {
    $name=$test[0]
    $root=Join-Path $validation $test[1]
    $recordRoot=$root
    if($test[1] -in @('experimental-fps','english-ui')) {
        # Keep generated fixture state in the OS temporary area. No product data is touched.
        $root=Join-Path ([IO.Path]::GetTempPath()) ('DXManager-'+$test[1]+'-Verification')
        New-Item -ItemType Directory -Path $recordRoot -Force | Out-Null
    }
    [IO.Directory]::CreateDirectory((Join-Path $root 'ko')) | Out-Null
    Copy-Item -LiteralPath $product -Destination (Join-Path $root 'DXManager.exe') -Force
    Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\ko\DXManager.resources.dll') -Destination (Join-Path $root 'ko\DXManager.resources.dll') -Force
    if($test[1] -eq 'update-core') {
        # The shipped config folder already exists. Match that ordinary layout.
        [IO.Directory]::CreateDirectory((Join-Path $root 'update-state-fixture')) | Out-Null
    }
    if($test[1] -eq 'onboarding-ui') {
        foreach($theme in @('Light','Dark')){New-Item -ItemType Directory -Path (Join-Path $root ('consent-'+$theme)) -Force | Out-Null}
    }
    if($test[1] -in @('phone-transfer','english-ui')) {
        [IO.Directory]::CreateDirectory((Join-Path $root 'tools\companion')) | Out-Null
        Copy-Item -LiteralPath 'E:\vs\DX Companion Shared\release\DX-Companion.apk' -Destination (Join-Path $root 'tools\companion\DX-Companion.apk') -Force
        if($test[1] -eq 'phone-transfer') {
            Copy-Item -LiteralPath 'E:\vs\dex system-ai-keymap-test\DXDisplayCleanup\bundled\DX-Companion-AI-Keymap-Test.apk' -Destination (Join-Path $root 'foreign-signer.apk') -Force
        }
    }
    $target=Join-Path $root ($name+'.exe')
    Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe.config') -Destination ($target+'.config') -Force
    & $compiler /nologo /target:exe /platform:x64 ("/out:"+$target) ("/reference:"+$product) (Join-Path $repo ('DexManager.MultiDeviceTests\'+$name+'.cs')) *> (Join-Path $root 'compile.log')
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $root 'compile.log');throw ('Test compilation failed: '+$name)}
    if($test[1] -eq 'runtime-check') { & $target (Join-Path $repo 'DexManager\tools\scrcpy\scrcpy.exe') *> (Join-Path $root 'results.log') }
    else { & $target *> (Join-Path $root 'results.log') }
    if($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath (Join-Path $root 'results.log') | Where-Object {$_ -notlike 'IMAGE:*'} | Select-Object -Last 12
        throw ('Actual product verification failed: '+$name)
    }
    $last=Get-Content -LiteralPath (Join-Path $root 'results.log') | Where-Object {$_ -notlike 'IMAGE:*'} | Select-Object -Last 1
    Write-Host $last
    $results += [pscustomobject]@{Test=$name;Result=$last}
    foreach($line in Get-Content -LiteralPath (Join-Path $root 'results.log')) {
        if($line -match '^IMAGE:([A-Za-z0-9-]+):([A-Za-z0-9+/=]+)$') {
            [IO.File]::WriteAllBytes((Join-Path $root ($Matches[1]+'.png')),[Convert]::FromBase64String($Matches[2]))
        }
    }
    if($root -ne $recordRoot) {
        Copy-Item -LiteralPath (Join-Path $root 'results.log') -Destination $recordRoot -Force
        Get-ChildItem -LiteralPath $root -Filter '*.png' -File | Copy-Item -Destination $recordRoot -Force
    }
}
$demo=Join-Path $validation 'update-ui-preview'
[IO.Directory]::CreateDirectory((Join-Path $demo 'ko')) | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\DXManager.exe.config') -Destination (Join-Path $demo 'UpdateNoticePreview.exe.config') -Force
Copy-Item -LiteralPath $product -Destination (Join-Path $demo 'DXManager.exe') -Force
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\bin\Release\ko\DXManager.resources.dll') -Destination (Join-Path $demo 'ko\DXManager.resources.dll') -Force
& $compiler /nologo /target:winexe /platform:x64 ("/out:"+(Join-Path $demo 'UpdateNoticePreview.exe')) ("/reference:"+$product) (Join-Path $repo 'DexManager.MultiDeviceTests\UpdateNoticePreview.cs') *> (Join-Path $demo 'compile.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $demo 'compile.log');throw 'UI preview compilation failed.'}
if($LiveUpdateCheck) {
    & (Join-Path $validation 'update-core\UpdateCheckIntegrationTests.exe') --live *> (Join-Path $validation 'live-update-check.log')
    $liveCode=$LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $validation 'live-update-check.log')
    if($liveCode -ne 0){Write-Warning 'Live GitHub connectivity was unavailable; offline behavior remains verified.'}
}
[IO.File]::WriteAllText((Join-Path $validation 'verification-summary.json'),($results | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($true))
Write-Host ('Update UI preview: '+(Join-Path $demo 'UpdateNoticePreview.exe'))
