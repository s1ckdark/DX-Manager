param(
    [string]$BasePackageRoot = 'E:\vs\dex system\dist\DX Manager v2.5.0',
    [string]$Scrcpy5Root = 'E:\vs\.build-tools\scrcpy-v5.0-analysis-20261006\win64\scrcpy-win64-v5.0',
    [string]$OutputRoot = 'E:\vs\dex system\dist'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageRoot = Join-Path $OutputRoot 'DX Manager v2.5.0 scrcpy5 test'
$zipPath = Join-Path $OutputRoot 'DX-Manager-v2.5.0-scrcpy5-test-win-x64.zip'
if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
    throw 'Existing test artifacts must be preserved; choose another output folder.'
}
$baseExe = Join-Path $BasePackageRoot 'DXManager.exe'
$expectedExe = 'BACEA6027137EC0ACE770133034F258EF36F68CA977FD03FF9395F58717D3632'
if ((Get-FileHash -LiteralPath $baseExe).Hash -ne $expectedExe) { throw 'Unexpected final base executable.' }
$requiredRuntime = @('scrcpy.exe','scrcpy-server','SDL3.dll','avcodec-63.dll','avformat-63.dll','avutil-61.dll','swresample-7.dll','libusb-1.0.dll','adb.exe','AdbWinApi.dll','AdbWinUsbApi.dll')
foreach ($name in $requiredRuntime) { if (!(Test-Path -LiteralPath (Join-Path $Scrcpy5Root $name))) { throw "Missing 5.0 payload: $name" } }
$buildRoot = Join-Path $repoRoot '.build-tools\scrcpy5-test-packaging'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$presetHelper = Join-Path $buildRoot 'GeneratePreset.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 "/out:$presetHelper" /r:System.Core.dll /r:System.Runtime.Serialization.dll "/r:$baseExe" (Join-Path $PSScriptRoot 'Generate-Scrcpy5-TestPreset.cs')
if ($LASTEXITCODE -ne 0) { throw 'Preset helper compilation failed.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
foreach ($variant in @(@{Name='01-software';Mode='disabled'},@{Name='02-auto';Mode='auto'})) {
    $destination = Join-Path $packageRoot $variant.Name
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $BasePackageRoot) {
        if ($item.Name -ne 'tools') { Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse }
    }
    $tools = Join-Path $destination 'tools'
    New-Item -ItemType Directory -Path $tools | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath (Join-Path $BasePackageRoot 'tools')) {
        if ($item.Name -ne 'scrcpy') { Copy-Item -LiteralPath $item.FullName -Destination $tools -Recurse }
    }
    Copy-Item -LiteralPath $Scrcpy5Root -Destination (Join-Path $tools 'scrcpy') -Recurse
    $readmePath = Join-Path $destination 'README.md'
    $readme = [IO.File]::ReadAllText($readmePath).Replace('bundles scrcpy 4.1.','bundles scrcpy 5.0 in this experimental Windows7 test.').Replace('scrcpy 4.1','scrcpy 5.0')
    [IO.File]::WriteAllText($readmePath,$readme,[Text.UTF8Encoding]::new($true))
    $noticePath = Join-Path $destination 'licenses\THIRD_PARTY_NOTICES.md'
    $notice = [IO.File]::ReadAllText($noticePath).Replace('scrcpy 4.1','scrcpy 5.0').Replace('Bundled version: 4.1','Bundled version: 5.0').Replace('37.0.0-14910828','37.0.1').Replace('SDL 3.4.12','SDL 3.4.18').Replace('release-3.4.12','release-3.4.18').Replace('62.28.102','63.1.102').Replace('62.12.102','63.1.102').Replace('60.26.102','61.1.102')
    [IO.File]::WriteAllText($noticePath,$notice,[Text.UTF8Encoding]::new($true))
    $encoded = & $presetHelper $baseExe $variant.Mode
    if ($LASTEXITCODE -ne 0) { throw 'Preset generation failed.' }
    [IO.File]::WriteAllBytes((Join-Path $destination 'config\settings.json'),[Convert]::FromBase64String(($encoded -join '').Trim()))
}
$guide = @"
DX Manager 2.5.0 + scrcpy 5.0 — Windows 7 company test

01-software: CPU/software decoding (--hwdec=disabled). Start here on Windows 7.
02-auto: hardware decoder if available, otherwise software (--hwdec=auto).

Both use the same reviewed DXManager2.5.0 executable and signed Companion2.1.4.
Only scrcpy and the decoding preset differ from the original final package.
Legacy ADB is included; Windows7 is configured to select it automatically.

한국어 실행 순서
1. ZIP 전체를 새 폴더에 풉니다. 기존2.5.0 폴더에 덮어쓰지 마세요.
2. 기존 DX Manager를 트레이의 종료로 완전히 끝냅니다.
3. 먼저 01-software 폴더의 DXManager.exe를 실행합니다.
4. USB를 연결하고 DeX 및 앱을 선택한 단일창을 테스트합니다.
5. 프로그램을 트레이에서 완전히 종료한 뒤 02-auto의 DXManager.exe로 같은 조건을 비교합니다.

설정은 두 폴더에 각각 따로 저장됩니다. 처음에는 같은 깨끗한 기본값입니다.
DeX와 단일창1~3 모두 해당 디코딩 옵션이 들어가 있습니다.
비교할 때 해상도/FPS/비트레이트/앱을 같게 맞추세요.
각 폴더는 새 프로필이라 최초 동의를 각각 한 번 확인합니다.
기존 config를 복사하면 테스트 모드가 사라질 수 있으므로 처음에는 제공한 설정을 사용하세요.

확인할 내용
- Windows7에서 시작 자체가 되는지, DLL/진입점 오류가 있는지
- 기본 연결 로그가 tools\adb\legacy\adb.exe를 선택하는지
- 실행 인자에 --hwdec=disabled 또는 --hwdec=auto가 있는지
- 자동 모드에서 Texture (D3D11VA)가 보이면 하드웨어 경로가 선택된 것입니다.
- Hardware decoding unavailable; using software decoding은 자동 폴백 안내입니다.
- Win7에서는 자동 모드도 소프트웨어로 실행될 수 있습니다. 이는 이상 현상이라고 단정하지 않습니다.
- 화면 표시/크기 변경/CPU 사용량, 한영/Shift/Enter, F8 캡처, 미니바와 종료/재연결 확인

로그: 각 버전의 logs 폴더 또는 설정 → 진단 → 로그 창.
문제가 생기면 오류 문구와 로그를 보내 주세요. 기존 최종본으로 돌아가도 됩니다.
이 테스트는 Win7 실행을 보장한 것이 아니며, 현재 개발 PC에서 버전/도움말과 설정/패키지를 확인했습니다.
실행은 DXManager.exe로 하세요. scrcpy 폴더의 adb.exe나 직접 실행 스크립트는 Win7 레거시 선택을 우회합니다.
최종2.5.0 ZIP/실행 파일과 GitHub 게시 상태는 바뀌지 않았습니다.
"@
[IO.File]::WriteAllText((Join-Path $packageRoot '먼저 읽어주세요.txt'),$guide,[Text.UTF8Encoding]::new($true))
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Test folder: $packageRoot"
Write-Output "Test archive: $zipPath"