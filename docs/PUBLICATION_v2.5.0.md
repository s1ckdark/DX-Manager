# Windows 2.5.0 publication preparation — 2026-10-25

## English

The maintainer explicitly authorized local commit and GitHub publication.
The Windows upload retains the previously tested and scanned bytes:

- ZIP: `DX-Manager-win-x64.zip` → `DX Manager v2.5.0/`, 39 files, 20,799,915 bytes.
- ZIP SHA-256: `EBB91A332B603C4096B1B1FB6D179F4D45B55F5A63FC657F8B989BEA8DE08764`.
- EXE SHA-256: `D3342F2A2B80DC5945D069CF408412A4DEDEBB6C0A54C78795485CB5DAD5FC19`.
- Official Companion 2.1.6/code13 SHA-256: `904491111341D46458BB9CE9B342D9210A20BB923BC0F605484D6AF7B90DFCC9`.
- The maintainer reports that the current final EXE scanned clean; vendor/count/report details were not supplied.

### Source availability

`companion/` is the version-pinned public export of the canonical shared Android component.
All 105 original source files match the release manifest; APK/constants/license are included.
Git preserves the source bytes so manifest hashes remain valid in a fresh source archive.
The export excludes private signing inputs, caches, local build configuration and private AI test code.
Public Windows builds fall back to the included component; maintainers can still use the canonical shared directory.

### Independent Windows source check

A new archive of the committed public source was built without an external Companion directory:
.NET Framework 4.6.2 x64 build, 0 warnings/errors; all 62 foundation checks passed.
Source packaging is also checked against the pinned runtime and bundled official APK.
The source snapshot and official scrcpy runtime preserve their original bytes through Git instead of line-ending normalization.
This is source-build verification; the uploaded binaries remain the prior 702-check, maintainer-scanned files.

### Platform integration

The public main at `2a1ec107450d8755e3691e9fa52aa086ed034e8d` is merged rather than overwritten.
Its Core/Mac code, tests, tools, package scripts and documentation are retained.
The .NET 8 Mac proxy/foundation projects live in `DexManager.AdbProxy.Mac` and `DexManager.MultiDeviceTests.Mac`,
with Mac solution/workflow/package references updated. The original Windows .NET Framework projects remain intact.
An existing Windows release is left unchanged by the Mac workflow's draft-release step.

This is a Windows binary release. No Mac portable runtime is repackaged or claimed to match the Windows feature/test scope.
No Store source or upload artifact is modified.

### Documentation and downloads

README, complete EN/KO guides, 35-question FAQs, release notes and the approved real captures target Windows 2.5.0.
Source documentation/images are excluded from the binary ZIP. Full checksum/test boundaries are in [verification](VERIFICATION_v2.5.0.md).

The fixed asset enables:
https://github.com/maze-mei/DX-Manager/releases/latest/download/DX-Manager-win-x64.zip

Future releases serving this link must upload that exact filename.
The old 2.0.1 asset is retained under a legacy filename at publication to break stale direct-file links, as requested.
It is not an automatic redirect or a way to update third-party server copies.

## 한국어

메이님이 검사한 실행 파일·APK·ZIP은 그대로 유지하면서 문서와 공식 Companion 소스를 공개합니다.
공용 원본은 한곳에서 유지하고 저장소에는 해당 버전의 소스·APK·해시 스냅샷을 포함합니다.
macOS 구현은 별도 프로젝트 경로로 보존하며 Windows·Store의 동작을 변경하지 않습니다.
최신 다운로드 주소는 고정 파일명을 사용하고 이전 2.0.1 파일은 이름을 바꿔 오래된 직접 링크를 끊습니다.
