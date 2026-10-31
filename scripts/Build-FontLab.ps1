param(
    [string]$OutputRoot = 'E:\vs\dex system\dist',
    [string]$Name = ('DX-Font-Lab-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot = Join-Path $repoRoot ('.build-tools\font-lab\' + $Name)
$packageRoot = Join-Path $OutputRoot $Name
$zipPath = Join-Path $OutputRoot ($Name + '.zip')
if ((Test-Path -LiteralPath $buildRoot) -or (Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) { throw 'Choose a new name; existing artifacts are preserved.' }
New-Item -ItemType Directory -Path $buildRoot,$packageRoot | Out-Null
$forms = Join-Path $repoRoot 'DexManager\Forms'
$utf8 = [Text.UTF8Encoding]::new($true)
$sourceHashes = [Collections.Generic.List[string]]::new()
function Make-Copy([string]$file,[string]$text) {
    $sourceHashes.Add($file + ' ' + (Get-FileHash -LiteralPath (Join-Path $forms $file)).Hash)
    $path = Join-Path $buildRoot $file
    [IO.File]::WriteAllText($path,$text,$utf8)
    return $path
}
$fonts = [IO.File]::ReadAllText((Join-Path $forms 'UiFonts.cs'))
$fonts = $fonts.Replace('UiFonts','BaselineUiFonts').Replace('AddFontMemResourceEx(memory,','FontProbe.Register(memory,')
$sources = @((Make-Copy 'UiFonts.cs' $fonts))
$controls = [IO.File]::ReadAllText((Join-Path $forms 'ThemedControls.cs')).Replace('TextRenderer.','TestTextRenderer.')
$sources += Make-Copy 'ThemedControls.cs' $controls
$surface = [IO.File]::ReadAllText((Join-Path $forms 'DesktopSurfaces.cs'))
$typeCheck = 'surface is MainForm || surface is SettingsForm || surface is HelpForm || surface is DocumentationForm || surface is LogForm'
if (!$surface.Contains($typeCheck)) { throw 'Review DesktopSurface source change before copying.' }
$sources += Make-Copy 'DesktopSurfaces.cs' ($surface.Replace($typeCheck,'surface is SampleWindow'))
$rendering = [IO.File]::ReadAllText((Join-Path $forms 'UiRendering.cs'))
if (!$rendering.Contains('parameters.ExStyle |= 0x02000000;')) { throw 'Review composited-panel source change.' }
$sources += Make-Copy 'UiRendering.cs' ($rendering.Replace('parameters.ExStyle |= 0x02000000;','if (Experiment.Mode != 5) parameters.ExStyle |= 0x02000000;'))
foreach ($file in @('ThemeColors.cs','UiWindowStyle.cs','FluentIconRenderer.cs')) { $sources += Make-Copy $file ([IO.File]::ReadAllText((Join-Path $forms $file))) }
$sources += Join-Path $PSScriptRoot 'FontLab\FontLab.cs'
$sourceHashes.Add('scripts/FontLab/FontLab.cs ' + (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'FontLab\FontLab.cs')).Hash)
$sourceHashes.Add('scripts/Build-FontLab.ps1 ' + (Get-FileHash -LiteralPath $PSCommandPath).Hash)
$sourceHashes.Add('DexManager/app.manifest ' + (Get-FileHash -LiteralPath (Join-Path $repoRoot 'DexManager\app.manifest')).Hash)
$referenceRoot = 'E:\vs\dex system\.build-tools\net462\build\.NETFramework\v4.6.2'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$options = @('/nologo','/platform:x64','/optimize+','/nostdlib+',('/win32manifest:' + (Join-Path $repoRoot 'DexManager\app.manifest')))
foreach ($reference in @('mscorlib','System','System.Core','System.Drawing','System.Windows.Forms')) { $options += '/r:' + (Join-Path $referenceRoot ($reference + '.dll')) }
foreach ($weight in @('Regular','SemiBold')) {
    $font = Join-Path $repoRoot ('DexManager\Resources\Fonts\DXStoreUI-' + $weight + '.ttf')
    $options += '/resource:' + $font + ',DexManager.Resources.Fonts.DXStoreUI-' + $weight + '.ttf'
    $sourceHashes.Add('DXStoreUI-' + $weight + '.ttf ' + (Get-FileHash -LiteralPath $font).Hash)
}
foreach ($icon in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'DexManager\Resources\FluentIcons') -Filter '*.png') {
    $options += '/resource:' + $icon.FullName + ',DexManager.Resources.FluentIcons.' + $icon.Name
    $sourceHashes.Add('FluentIcons/' + $icon.Name + ' ' + (Get-FileHash -LiteralPath $icon.FullName).Hash)
}
$exe = Join-Path $packageRoot 'DXFontLab.exe'
& $compiler @options /target:winexe ("/out:$exe") @sources
if ($LASTEXITCODE -ne 0) { throw 'Font Lab compilation failed.' }
$verifier = Join-Path $buildRoot 'DXFontLab.Verify.exe'
& $compiler @options /target:exe ("/out:$verifier") @sources
if ($LASTEXITCODE -ne 0) { throw 'Verifier compilation failed.' }
$config = '<?xml version="1.0" encoding="utf-8"?><configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6.2" /></startup></configuration>'
[IO.File]::WriteAllText(($exe+'.config'),$config,$utf8)
[IO.File]::WriteAllText(($verifier+'.config'),$config,$utf8)
[IO.File]::WriteAllLines((Join-Path $buildRoot 'source-sha256.txt'),$sourceHashes,$utf8)
$licenseRoot = Join-Path $packageRoot 'licenses'
New-Item -ItemType Directory -Path $licenseRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'DexManager\Resources\Fonts\Pretendard-LICENSE.txt') -Destination $licenseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'DexManager\licenses\Fluent-System-Icons-LICENSE.txt') -Destination $licenseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $licenseRoot 'DX-Manager-LICENSE.txt')
$guide = @'
DX Font Lab — isolated Windows 7 font/rendering comparison
Requires x64 Windows 7 SP1 or later and .NET Framework 4.6.2.
Extract the entire ZIP and run DXFontLab.exe. No installation or phone required.
Fonts/icons are embedded; fonts are registered only inside this process.
This tool contains no ADB/Scrcpy/device services, network calls or settings import.
DX Manager and its final release are unchanged.

한국어 사용법
1. ZIP을 새 폴더에 풀고 DXFontLab.exe를 실행하세요.
2. 작은 선택 창에서 A부터 차례로 변경하세요. 큰 창은 매번 새로 열립니다.
3. 메인 / 도움말 / 글자표를 확인하고, 어두움 테마도 비교하세요.
4. 정상 또는 깨짐 버튼으로 조건과 PC 환경을 기록하고, 결과 전체 복사를 눌러
   메모장에 붙여넣어 저장하세요. 프로그램을 종료하면 임시 기록은 사라집니다.
5. 화면 복사를 누르면 큰 테스트 창이 그린 이미지가 복사됩니다.
   그림판에 붙여넣고 PNG로 저장하세요.
   화면에서 보이는 깨짐이 복사 이미지에는 안 보이면 Windows 화면 캡처로 찍어주세요.
6. 나중에 어느 방식이 정상인지 알려주거나 기록한 텍스트와 PNG를 가져오세요.

A: 현재 DX Manager의 내장 Pretendard, FontFamily 로딩, GDI 표시.
B: 맑은 고딕만 변경. 카드·버튼·배경·GDI 경로는 A와 동일.
C: 같은 내장 Pretendard와 GDI를 유지하고 글자 크기를 정수 픽셀로 맞춤 (96 DPI 기준).
D: 같은 내장 Pretendard를 GDI+ DrawString / 회색 안티앨리어싱으로 표시.
E: 같은 내장 Pretendard를 GDI+ DrawString / ClearType으로 표시.
F: A와 같고 BufferedPanel의 WS_EX_COMPOSITED만 제외.

창 외곽 크기, DPI/작업영역 맞춤, 기본 GDI Label, TextRenderer 버튼,
둥근 카드, 배경, 체크박스, 아이콘, 내장 글꼴 파일은 현재 앱을 기준으로 합니다.
메인 1200×800, 도움말 1040×760 (100%에서 제목 표시줄/테두리 포함).
메인은 현재 앱과 같은 내부 크기 기반 글꼴 축소 산식을 사용합니다.
기능 컨트롤은 읽기 전용 샘플이며 원래 앱 전체를 실행하지 않습니다.
해상도/앱 입력 예시는 버튼 텍스트로 표시하며 실제 콤보박스 동작은 포함하지 않습니다.
글자표에는 로그와 같은 Consolas/RichTextBox를 고정 기준으로 넣었습니다.
D/E는 라벨과 커스텀 버튼/체크박스의 표시 및 메뉴 글자 측정을 바꾸는 실험입니다.
글꼴 형식 자체의 미지원, 렌더러, 글꼴 등록, 패널 합성을 비교할 수 있지만
특정 방식이 여기서 정상이라고 앱 전체의 수정 성공을 보장하지는 않습니다.

Windows 11에서 조합별 생성/글꼴 선택/리사이즈/이미지 검증을 수행했습니다.
회사 Windows 7에서의 실제 획 표시 품질은 사용자가 확인할 항목입니다.
'@
[IO.File]::WriteAllText((Join-Path $packageRoot '먼저 읽어주세요.txt'),$guide,$utf8)
[IO.File]::WriteAllLines((Join-Path $packageRoot 'source-sha256.txt'),$sourceHashes,$utf8)
[IO.File]::WriteAllText((Join-Path $buildRoot 'paths.txt'),"$packageRoot`r`n$verifier`r`n$zipPath",$utf8)
Write-Output "PACKAGE=$packageRoot"
Write-Output "VERIFIER=$verifier"
Write-Output "ZIP_PENDING=$zipPath"
