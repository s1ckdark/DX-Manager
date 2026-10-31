#!/bin/bash
# Build a local, ad-hoc signed DXManager.app (Apple Silicon) for personal use.

set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_directory/.." && pwd)"

configuration="Release"
output_directory="$repository_root/dist"
skip_tools=0
sign=1
rid="osx-arm64"
dotnet_runtime_version="8.0.30"
bundle_identifier="io.github.s1ckdark.dxmanager"
desktop_project="$repository_root/DexManager.Desktop/DexManager.Desktop.csproj"

export PATH="$PATH:$HOME/.dotnet"

usage() {
    cat <<'USAGE'
Build a local DXManager.app bundle (Apple Silicon, ad-hoc signed, personal use).

Usage:
  scripts/Build-Mac-App.sh [options]

Options:
  --configuration Release|Debug  Build configuration (default: Release)
  --output DIRECTORY             Output directory (default: ./dist)
  --skip-tools                   Do not bundle scrcpy/adb (tools folder omitted)
  --no-sign                      Skip the ad-hoc codesign step
  -h, --help                     Show this help

Existing config/, logs/ and screenshot/ inside the previous bundle's
Contents/MacOS are preserved across rebuilds.
USAGE
}

fail() {
    echo "Build-Mac-App: $*" >&2
    exit 1
}

log() {
    echo "==> $*"
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "required command not found: $1"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration)
            [[ $# -ge 2 ]] || fail "--configuration requires a value"
            configuration="$2"
            shift 2
            ;;
        --output)
            [[ $# -ge 2 ]] || fail "--output requires a value"
            output_directory="$2"
            shift 2
            ;;
        --skip-tools)
            skip_tools=1
            shift
            ;;
        --no-sign)
            sign=0
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "unknown argument: $1"
            ;;
    esac
done

[[ "$(uname -s)" == "Darwin" ]] || fail "this script must run on macOS"
[[ "$configuration" == "Release" || "$configuration" == "Debug" ]] ||
    fail "--configuration must be Release or Debug"

for command_name in dotnet file sips iconutil plutil codesign sed mktemp; do
    require_command "$command_name"
done

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$desktop_project" | head -n 1)"
[[ -n "$version" ]] || fail "could not read <Version> from $desktop_project"

output_directory="$(mkdir -p "$output_directory" && cd "$output_directory" && pwd)"
app_path="$output_directory/DXManager.app"

temporary_base="${TMPDIR:-/tmp}"
temporary_base="${temporary_base%/}"
work_root="$(mktemp -d "$temporary_base/dx-manager-app.XXXXXX")"
bundle_staging="$(mktemp -d "$output_directory/.dxmanager-app.XXXXXX")"
preserved="$work_root/preserved"
mkdir -p "$preserved"

cleanup() {
    case "$work_root" in
        "$temporary_base"/dx-manager-app.*) /bin/rm -rf -- "$work_root" ;;
    esac
    case "$bundle_staging" in
        "$output_directory"/.dxmanager-app.*) /bin/rm -rf -- "$bundle_staging" ;;
    esac
}
trap cleanup EXIT

# 1. Preserve user state from the previous bundle.
old_macos="$app_path/Contents/MacOS"
for state in config logs screenshot; do
    if [[ -d "$old_macos/$state" ]]; then
        log "Preserving previous $state/"
        /bin/cp -Rf "$old_macos/$state" "$preserved/$state"
    fi
done

contents="$bundle_staging/DXManager.app/Contents"
macos="$contents/MacOS"
resources="$contents/Resources"
mkdir -p "$macos" "$resources"

# 2. Avalonia GUI (self-contained folder, not single-file).
log "Publishing DexManager.Desktop ($configuration, $rid)"
dotnet publish "$desktop_project" \
    -c "$configuration" -r "$rid" --self-contained true \
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false \
    -p:RuntimeFrameworkVersion="$dotnet_runtime_version" \
    -o "$macos" --nologo
[[ -f "$macos/DXManager.Desktop" ]] || fail "DXManager.Desktop was not produced"
chmod +x "$macos/DXManager.Desktop"
file "$macos/DXManager.Desktop" | grep -q "arm64" ||
    fail "DXManager.Desktop is not an arm64 Mach-O: $(file "$macos/DXManager.Desktop")"

# 3. ADB proxy (same flags as Package-Mac-Release.sh) + clean-env self-test.
log "Publishing DXMAdbProxy"
proxy_directory="$macos/tools/adb-proxy"
dotnet publish "$repository_root/DexManager.AdbProxy.Mac/DexManager.AdbProxy.csproj" \
    --configuration "$configuration" \
    --framework net8.0 \
    --runtime "$rid" \
    --self-contained true \
    --output "$proxy_directory" \
    -p:Version="$version" \
    -p:RuntimeFrameworkVersion="$dotnet_runtime_version" \
    -p:TreatWarningsAsErrors=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishTrimmed=false \
    -p:DebugSymbols=false \
    -p:DebugType=None --nologo
[[ -f "$proxy_directory/DXMAdbProxy" ]] || fail "DXMAdbProxy was not produced"
chmod +x "$proxy_directory/DXMAdbProxy"
proxy_output="$(env -i PATH=/usr/bin:/bin HOME="$(mktemp -d "$work_root/proxy-home.XXXXXX")" \
    "$proxy_directory/DXMAdbProxy" --self-test 2>&1)" ||
    fail "DXMAdbProxy --self-test failed: $proxy_output"
[[ "$proxy_output" == *"self-test passed"* ]] ||
    fail "DXMAdbProxy self-test output unexpected: $proxy_output"

# 4. Bundled scrcpy/adb and portable marker.
if [[ $skip_tools -eq 0 ]]; then
    log "Bundling tools/scrcpy and tools/adb"
    [[ -f "$repository_root/tools/scrcpy/scrcpy" ]] || fail "tools/scrcpy/scrcpy is missing"
    [[ -f "$repository_root/tools/adb/adb" ]] || fail "tools/adb/adb is missing"
    mkdir -p "$macos/tools/scrcpy" "$macos/tools/adb"
    /bin/cp -Rf "$repository_root/tools/scrcpy/." "$macos/tools/scrcpy/"
    /bin/cp -f "$repository_root/tools/adb/adb" "$macos/tools/adb/adb"
    chmod +x "$macos/tools/scrcpy/scrcpy" "$macos/tools/scrcpy/adb" "$macos/tools/adb/adb"
    # The marker only makes MacPathProvider.IsPortablePackage true so that
    # <BaseDirectory>/tools/... is preferred over system scrcpy/adb.
    /bin/cp -f "$repository_root/build/macos/PORTABLE_PACKAGE.txt" "$macos/PORTABLE_PACKAGE.txt"
else
    log "Skipping bundled tools (--skip-tools)"
fi

# 5. Icon.
log "Generating DXManager.icns"
icon_source="$repository_root/DexManager/Resources/DXManager_256.png"
[[ -f "$icon_source" ]] || fail "icon source missing: $icon_source"
iconset="$work_root/DXManager.iconset"
mkdir -p "$iconset"
make_icon() {
    sips -z "$1" "$1" "$icon_source" --out "$iconset/$2" >/dev/null
}
make_icon 16 icon_16x16.png
make_icon 32 icon_16x16@2x.png
make_icon 32 icon_32x32.png
make_icon 64 icon_32x32@2x.png
make_icon 128 icon_128x128.png
make_icon 256 icon_128x128@2x.png
make_icon 256 icon_256x256.png
make_icon 512 icon_256x256@2x.png
make_icon 512 icon_512x512.png
make_icon 1024 icon_512x512@2x.png
iconutil -c icns "$iconset" -o "$resources/DXManager.icns"

# 6. Info.plist.
cat > "$contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>DX Manager</string>
    <key>CFBundleDisplayName</key><string>DX Manager</string>
    <key>CFBundleIdentifier</key><string>$bundle_identifier</string>
    <key>CFBundleExecutable</key><string>DXManager.Desktop</string>
    <key>CFBundleIconFile</key><string>DXManager</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleShortVersionString</key><string>$version</string>
    <key>CFBundleVersion</key><string>$version</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
    <key>NSHumanReadableCopyright</key><string>© 2026 maze, s1ckdark. MIT License.</string>
</dict>
</plist>
PLIST
plutil -lint "$contents/Info.plist" >/dev/null || fail "Info.plist is invalid"

# 7. Restore preserved user state, then swap atomically.
for state in config logs screenshot; do
    if [[ -d "$preserved/$state" ]]; then
        /bin/rm -rf -- "$macos/$state"
        /bin/cp -Rf "$preserved/$state" "$macos/$state"
        log "Restored $state/"
    fi
done

if [[ $sign -eq 1 ]]; then
    log "Ad-hoc signing"
    codesign --force --deep --sign - "$bundle_staging/DXManager.app"
    codesign --verify --deep --strict "$bundle_staging/DXManager.app" ||
        fail "codesign verification failed"
fi

if [[ -e "$app_path" ]]; then
    /bin/mv -f "$app_path" "$bundle_staging/previous.app"
fi
if ! /bin/mv -f "$bundle_staging/DXManager.app" "$app_path"; then
    [[ ! -e "$bundle_staging/previous.app" ]] || /bin/mv -f "$bundle_staging/previous.app" "$app_path"
    fail "could not publish $app_path"
fi

# 8. Layout smoke check (the GUI is not launched).
log "Verifying bundle layout"
final_macos="$app_path/Contents/MacOS"
plutil -lint "$app_path/Contents/Info.plist" >/dev/null || fail "final Info.plist invalid"
required_executables=("$final_macos/DXManager.Desktop" "$final_macos/tools/adb-proxy/DXMAdbProxy")
if [[ $skip_tools -eq 0 ]]; then
    required_executables+=("$final_macos/tools/scrcpy/scrcpy" "$final_macos/tools/scrcpy/adb" "$final_macos/tools/adb/adb")
fi
for executable in "${required_executables[@]}"; do
    [[ -x "$executable" ]] || fail "missing or non-executable: ${executable#$app_path/}"
done
[[ -f "$app_path/Contents/Resources/DXManager.icns" ]] || fail "DXManager.icns is missing"

echo
echo "DXManager.app $version ($configuration, $rid)"
echo "  Path:  $app_path"
echo "  Tools: $([[ $skip_tools -eq 1 ]] && echo 'not bundled' || echo 'scrcpy + adb bundled')"
echo "  Sign:  $([[ $sign -eq 1 ]] && echo 'ad-hoc' || echo 'none')"
echo "  State: $final_macos/{config,logs,screenshot}"
echo "First launch: right-click -> Open if Gatekeeper blocks it (unsigned, personal use)."
