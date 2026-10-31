param(
    [string]$OutputRoot = 'E:\vs\dex system\dist',
    [string]$Name = 'DX-Font-Lab2-20261007',
    [string]$Python = 'python.exe'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache=Join-Path $repo '.build-tools\font-lab2-inputs-20261007'
$audit=Join-Path $repo ('.build-tools\font-lab2\'+$Name)
$package=Join-Path $OutputRoot $Name
$zip=Join-Path $OutputRoot ($Name+'.zip')
foreach($path in @($audit,$package,$zip)) {if(Test-Path -LiteralPath $path){throw 'Existing artifacts are preserved. Choose a new Name.'}}
New-Item -ItemType Directory -Path $cache -Force | Out-Null
New-Item -ItemType Directory -Path $audit,$package | Out-Null
$utf8=[Text.UTF8Encoding]::new($true)
$baseUrl='https://raw.githubusercontent.com/orioncactus/pretendard/v1.3.9/'
$inputHashes=@{Regular='6D0AF5258997AEC7354A6E340FC2325BA321C410CA48B3AF858C8C3D6E92A324';SemiBold='5E1C548732AF70873103066C16E1369B9A8A871F0B38C321A1D5BC73E43CEA2D'}
foreach($weight in @('Regular','SemiBold')) {
    $path=Join-Path $cache ('Pretendard-'+$weight+'.ttf')
    if(!(Test-Path -LiteralPath $path)){Invoke-WebRequest -Uri ($baseUrl+'packages/pretendard/dist/public/static/alternative/Pretendard-'+$weight+'.ttf') -OutFile $path}
    if((Get-FileHash -LiteralPath $path).Hash -ne $inputHashes[$weight]){throw 'Official TTF checksum mismatch'}
}
$license=Join-Path $cache 'Pretendard-OFL.txt'
if(!(Test-Path -LiteralPath $license)){Invoke-WebRequest -Uri ($baseUrl+'LICENSE') -OutFile $license}
$wheel=Join-Path $cache 'fonttools-4.60.1-py3-none-any.whl'
if(!(Test-Path -LiteralPath $wheel)) {
    $pypi=Invoke-RestMethod -Uri 'https://pypi.org/pypi/fonttools/4.60.1/json'
    $asset=$pypi.urls | Where-Object {$_.filename -eq 'fonttools-4.60.1-py3-none-any.whl'}
    Invoke-WebRequest -Uri $asset.url -OutFile $wheel
}
if((Get-FileHash -LiteralPath $wheel).Hash -ne '906306AC7AFE2156FCF0042173D6EBBB05416AF70F6B370967B47F8F00103BBB'){throw 'Build-only wheel checksum mismatch'}
$originalFonts=Join-Path $repo 'DexManager\Resources\Fonts'
$encoded=& $Python (Join-Path $PSScriptRoot 'FontLab\Prepare-FontLab2.py') --cache $cache --fonts $originalFonts
if($LASTEXITCODE -ne 0){throw 'Font preparation/outline preservation checks failed'}
$prepared=($encoded -join '') | ConvertFrom-Json
$generated=@{}
foreach($profile in $prepared.modified) {
    $fontDir=Join-Path $audit ('fonts\'+$profile.id)
    New-Item -ItemType Directory -Path $fontDir -Force | Out-Null
    foreach($weight in @('Regular','SemiBold')) {
        $fontItem=$profile.fonts.$weight
        [IO.File]::WriteAllBytes((Join-Path $fontDir ($weight+'.ttf')),[Convert]::FromBase64String($fontItem.base64))
        $fontItem.PSObject.Properties.Remove('base64')
    }
    $generated[$profile.id]=$fontDir
}
[IO.File]::WriteAllText((Join-Path $audit 'font-inspection.json'),($prepared | ConvertTo-Json -Depth 20),$utf8)
$smoothReg=$prepared.modified | Where-Object {$_.id -eq 'B2'}
$smoothOfficial=$prepared.modified | Where-Object {$_.id -eq 'D2'}
$profiles=@(
    @{Id='A2';Folder='A2-current';Label='A2 · 현재 Pretendard / GDI';Fonts='current';Reg='DX Store UI Regular';Semi='DX Store UI SemiBold';Plus=$false;Hint='AntiAliasGridFit';Notes='Current TTF / gasp absent / original rendering'},
    @{Id='B2';Folder='B2-current-smoothing';Label='B2 · 현재 파일 + 표시 설정 / GDI';Fonts='B2';Reg=$smoothReg.fonts.Regular.info.family;Semi=$smoothReg.fonts.SemiBold.info.family;Plus=$false;Hint='AntiAliasGridFit';Notes='Current outlines unchanged / gasp v1 0x000A at all sizes / no hint instructions added'},
    @{Id='C2';Folder='C2-official';Label='C2 · 공식 정적 Pretendard / GDI';Fonts='official';Reg='Pretendard';Semi='Pretendard SemiBold';Plus=$false;Hint='AntiAliasGridFit';Notes='Official v1.3.9 static TTF unchanged / gasp absent'},
    @{Id='D2';Folder='D2-official-smoothing';Label='D2 · 공식 파일 + 표시 설정 / GDI';Fonts='D2';Reg=$smoothOfficial.fonts.Regular.info.family;Semi=$smoothOfficial.fonts.SemiBold.info.family;Plus=$false;Hint='AntiAliasGridFit';Notes='Official outlines unchanged / gasp v1 0x000A at all sizes / no hint instructions added'},
    @{Id='E2';Folder='E2-smoothing-gray';Label='E2 · B2 파일 + GDI+ 회색 AA';Fonts='B2';Reg=$smoothReg.fonts.Regular.info.family;Semi=$smoothReg.fonts.SemiBold.info.family;Plus=$true;Hint='AntiAliasGridFit';Notes='B2 font / GDI+ AntiAliasGridFit / compare old D with gasp added'},
    @{Id='F2';Folder='F2-gray-no-gridfit';Label='F2 · 현재 파일 + GDI+ 보정 제외';Fonts='current';Reg='DX Store UI Regular';Semi='DX Store UI SemiBold';Plus=$true;Hint='AntiAlias';Notes='Current TTF unchanged / GDI+ AntiAlias / no hinting or grid fitting requested'},
    @{Id='G2';Folder='G2-malgun';Label='G2 · 맑은 고딕 / GDI';Fonts='current';Reg='Malgun Gothic';Semi='Malgun Gothic';Plus=$false;Hint='AntiAliasGridFit';System=$true;Notes='System Malgun Gothic / known-normal reference from company test'}
)
[IO.File]::WriteAllText((Join-Path $package 'profiles.json'),($profiles | ConvertTo-Json -Depth 8),$utf8)
$forms=Join-Path $repo 'DexManager\Forms'
$compiler='C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs='E:\vs\dex system\.build-tools\net462\build\.NETFramework\v4.6.2'
$common=@('/nologo','/platform:x64','/optimize+','/nostdlib+',('/win32manifest:'+(Join-Path $repo 'DexManager\app.manifest')))
foreach($assembly in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms')){$common+='/r:'+(Join-Path $refs ($assembly+'.dll'))}
$config='<?xml version="1.0" encoding="utf-8"?><configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6.2" /></startup></configuration>'
$summary=[Collections.Generic.List[string]]::new()
$provenance=[Collections.Generic.List[string]]::new()
foreach($file in @('UiFonts.cs','ThemedControls.cs','DesktopSurfaces.cs','UiRendering.cs','ThemeColors.cs','UiWindowStyle.cs','FluentIconRenderer.cs')) {$provenance.Add('Forms/'+$file+' '+(Get-FileHash -LiteralPath (Join-Path $forms $file)).Hash)}
foreach($file in @('FontLab.cs','Prepare-FontLab2.py','FontLab2Launcher.cs')) {$provenance.Add('FontLab/'+$file+' '+(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('FontLab\'+$file))).Hash)}
$provenance.Add('Build-FontLab2.ps1 '+(Get-FileHash -LiteralPath $PSCommandPath).Hash)
foreach($profile in $profiles) {
    $build=Join-Path $audit $profile.Folder; $destination=Join-Path $package $profile.Folder
    New-Item -ItemType Directory -Path $build,$destination | Out-Null
    $fontSource=[IO.File]::ReadAllText((Join-Path $forms 'UiFonts.cs')).Replace('UiFonts','BaselineUiFonts').Replace('AddFontMemResourceEx(memory,','FontProbe.Register(memory,')
    $fontSource=$fontSource.Replace('DX Store UI Regular',$profile.Reg).Replace('DX Store UI SemiBold',$profile.Semi)
    [IO.File]::WriteAllText((Join-Path $build 'UiFonts.cs'),$fontSource,$utf8)
    $sources=@((Join-Path $build 'UiFonts.cs'))
    foreach($file in @('ThemedControls.cs','DesktopSurfaces.cs','UiRendering.cs','ThemeColors.cs','UiWindowStyle.cs','FluentIconRenderer.cs')) {
        $text=[IO.File]::ReadAllText((Join-Path $forms $file))
        if($file -eq 'ThemedControls.cs'){$text=$text.Replace('TextRenderer.','TestTextRenderer.')}
        if($file -eq 'DesktopSurfaces.cs'){$text=$text.Replace('surface is MainForm || surface is SettingsForm || surface is HelpForm || surface is DocumentationForm || surface is LogForm','surface is SampleWindow')}
        # Composition stays identical for every Lab2 profile.
        $path=Join-Path $build $file; [IO.File]::WriteAllText($path,$text,$utf8); $sources+=$path
    }
    $plus=if($profile.Plus){'true'}else{'false'}; $system=if($profile.System){'true'}else{'false'}
    $metadata=@"
using System.Drawing.Text;
namespace DexManager.Forms { internal static class ComparisonProfile {
 internal const string Id = "$($profile.Id)";
 internal const string Label = "$($profile.Label)";
 internal const string Regular = "$($profile.Reg)";
 internal const string SemiBold = "$($profile.Semi)";
 internal const string Notes = "$($profile.Notes)";
 internal const bool Plus = $plus;
 internal const bool SystemFont = $system;
 internal const TextRenderingHint Hint = TextRenderingHint.$($profile.Hint);
} }
"@
    [IO.File]::WriteAllText((Join-Path $build 'Profile.cs'),$metadata,$utf8); $sources+=Join-Path $build 'Profile.cs'
    $sources+=Join-Path $PSScriptRoot 'FontLab\FontLab.cs'
    $options=$common+@('/define:FONTLAB2')
    foreach($weight in @('Regular','SemiBold')) {
        $path=if($profile.Fonts -eq 'current'){Join-Path $originalFonts ('DXStoreUI-'+$weight+'.ttf')}elseif($profile.Fonts -eq 'official'){Join-Path $cache ('Pretendard-'+$weight+'.ttf')}else{Join-Path $generated[$profile.Fonts] ($weight+'.ttf')}
        $options+='/resource:'+$path+',DexManager.Resources.Fonts.DXStoreUI-'+$weight+'.ttf'
        $provenance.Add($profile.Id+'/'+$weight+' '+(Get-FileHash -LiteralPath $path).Hash)
    }
    foreach($icon in Get-ChildItem -LiteralPath (Join-Path $repo 'DexManager\Resources\FluentIcons') -Filter '*.png'){$options+='/resource:'+$icon.FullName+',DexManager.Resources.FluentIcons.'+$icon.Name}
    $exe=Join-Path $destination 'DXFontLab.exe'; $verifier=Join-Path $build 'Verify.exe'
    & $compiler @options /target:winexe ("/out:$exe") @sources
    if($LASTEXITCODE -ne 0){throw ('Compilation failed: '+$profile.Id)}
    & $compiler @options /target:exe ("/out:$verifier") @sources
    if($LASTEXITCODE -ne 0){throw 'Verifier compilation failed'}
    [IO.File]::WriteAllText(($exe+'.config'),$config,$utf8); [IO.File]::WriteAllText(($verifier+'.config'),$config,$utf8)
    $preview=Join-Path $build 'previews'; New-Item -ItemType Directory -Path $preview | Out-Null
    $lines=[Collections.Generic.List[string]]::new()
    & $verifier --verify | ForEach-Object {
        if($_.StartsWith('PNG:')) {$pair=$_.Substring(4).Split('|');[IO.File]::WriteAllBytes((Join-Path $preview ($pair[0]+'.png')),[Convert]::FromBase64String($pair[1]))} else {$lines.Add($_)}
    }
    $exit=$LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $build 'verification.txt'),$lines,$utf8)
    if($exit -ne 0){$lines | Select-Object -Last 6; throw ('Verification failed: '+$profile.Id)}
    $summary.Add($profile.Id+': PASS 6 mode/page/theme renders, regular/SemiBold actual GDI faces, resize, navigation and window capture')
    Write-Output ($profile.Id+' passed')
}
$launcher=Join-Path $package 'DXFontLab2.exe'
& $compiler @common /target:winexe ("/out:$launcher") (Join-Path $PSScriptRoot 'FontLab\FontLab2Launcher.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed'}
[IO.File]::WriteAllText(($launcher+'.config'),$config,$utf8)
$licenses=Join-Path $package 'licenses'; New-Item -ItemType Directory -Path $licenses | Out-Null
Copy-Item -LiteralPath $license -Destination (Join-Path $licenses 'Pretendard-OFL.txt')
Copy-Item -LiteralPath (Join-Path $repo 'DexManager\licenses\Fluent-System-Icons-LICENSE.txt') -Destination $licenses
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $licenses 'DX-Manager-LICENSE.txt')
[IO.File]::WriteAllLines((Join-Path $package 'source-sha256.txt'),$provenance,$utf8)
[IO.File]::WriteAllLines((Join-Path $audit 'verification-summary.txt'),$summary,$utf8)
$guide=@'
DX Font Lab 2 — font-file and smoothing comparison
x64 Windows 7 SP1+ / .NET Framework4.6.2; no phone, installation or network at runtime.
Run DXFontLab2.exe. Each profile runs in its own process to isolate private fonts.
Official source: Pretendard v1.3.9 static alternative TTFs, unchanged for C2.
Modified OFL derivatives are renamed. B2/D2 add gasp v1=0x000A for every size.
No glyph hint instructions were added; outlines and layout tables remain unchanged.
fontTools is build-only and is not part of this package.

한국어 사용 순서
1. ZIP 전체를 새 폴더에 풀고 DXFontLab2.exe를 실행하세요.
2. A2~G2 버튼을 차례로 누르세요. 처음에는 작은 글자 비교용 글자표가 열립니다.
3. 작은 글씨와 큰 글씨, Regular와 SemiBold를 확인하고 정상/깨짐을 기록하세요.
4. 화면 선택에서 도움말·메인과 어두움 테마도 확인할 수 있습니다.
5. 종료 전 결과 전체 복사를 눌러 메모장에 붙여넣으세요. 여러 결과를 계속 추가하면 됩니다.
6. 현재 비교 창을 닫고 다음 버튼을 누르세요. 테스트2의 B2는 이전 테스트 B와 다릅니다.

A2: 현재 내장 Pretendard + 기존 GDI (깨짐 비교 기준).
B2: 현재 글꼴의 형태는 그대로, 모든 크기에서 부드러운 표시를 권장하는 gasp 설정 추가 + GDI.
C2: 공식 정적 Pretendard Regular/SemiBold TTF 원본 + GDI.
D2: 공식 글꼴 형태는 그대로, B2와 같은 표시 설정 추가 + GDI.
E2: B2 파일 + GDI+ 회색 AA (AntiAliasGridFit). 이전 D와 비교할 수 있습니다.
F2: 현재 파일 그대로 + GDI+ AntiAlias. 이전 D와 달리 힌팅/픽셀 보정을 요청하지 않습니다.
G2: 맑은 고딕 + 기존 GDI (정상 비교 기준).

B2/D2는 힌팅을 새로 만든 파일이 아닙니다. 글꼴의 크기별 표시 권장 설정만 추가했습니다.
공식 정적 TTF에도 힌팅/gasp가 없으므로 C2만으로 개선이 보장되지는 않습니다.
조건별 결과는 해당 프로세스의 임시 기록입니다. 종료하면 사라지니 먼저 복사하세요.
화면 복사는 해당 창이 그린 이미지를 복사합니다. 보이는 깨짐이 복사 이미지에서 사라지면
Windows 화면 캡처로 기록해 주세요. DX Manager 본체와 기존 최종본은 변경하지 않았습니다.
현재 Windows11에서 화면/글꼴 선택을 검증했으며 회사Win7의 표시 품질은 직접 확인할 항목입니다.
'@
[IO.File]::WriteAllText((Join-Path $package '먼저 읽어주세요.txt'),$guide,$utf8)
$checksums=Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName).Hash+'  '+$_.FullName.Substring($package.Length+1)}
[IO.File]::WriteAllLines((Join-Path $package 'SHA256.txt'),$checksums,$utf8)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
[IO.File]::WriteAllText((Join-Path $audit 'delivery.json'),([pscustomobject]@{Folder=$package;Zip=$zip;SHA256=(Get-FileHash -LiteralPath $zip).Hash} | ConvertTo-Json),$utf8)
Write-Output ('ZIP='+$zip)
Write-Output ('SHA256='+(Get-FileHash -LiteralPath $zip).Hash)
