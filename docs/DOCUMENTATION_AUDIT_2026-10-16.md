# GitHub documentation audit — 2026-10-16

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## English summary

This is a local documentation preparation task, not a publication. All 40 document/configuration paths
from the public main tree were inventoried and downloaded for review. User-facing Windows pages are
updated for 2.5.0; historical release facts/licenses and remote-only macOS work are preserved.
The scanned binaries/ZIP remain unchanged. New real portable screenshots and the shared-component
public source dependency are tracked as remaining publication preparation.

## 조사 기준과 확인 범위

- 대상 저장소: `maze-mei/DX-Manager`, 공개 main 스냅샷 `2a1ec107450d8755e3691e9fa52aa086ed034e8d`.
- 실제 문서40경로를 읽기 전용으로 수집했다. API tree는 truncated=false이며 wiki는 사용하지 않는다.
- 기존 릴리스6개의 설명도 수집해 버전·해시·예전 보안 검사 기록을 현재 설명과 구분했다.
- 작업 폴더는 포터블 `feature/portable-backport-v2.0.1-20260928`, 로컬 HEAD8b19906이다.
- 원격 전용 macOS 안내·패키지 텍스트·라이선스·workflow는 이 작업선에 없으며 별도 보존 대상이다.
- 현재 공개 Windows 릴리스는2.0.1이고,2.5.0 최종 ZIP은 로컬 준비본이다.
- 원문 스냅샷/목록은 개발용 `.build-tools/documentation-250/remote-main`, `document-inventory.json`,
  `remote-tree.json`, `remote-releases.json`에 보관했다. 작업 전 로컬 문서는 `before`에 보관했다.

## 실제 발견과 조치

- README의2.0.1·scrcpy4.1·Companion2.1.2 안내를 현재2.5.0/5.0.1/2.1.6으로 정리했다.
- 한·영 설명서/FAQ의 옛 ‘진단 > Companion’, ‘경로 / ADB’ 메뉴 경로를 현재 전용 페이지로 수정했다.
- 새 실행 USB 우선, 실행 중 serial 유지, USB 해제 시 무선 자동 이전 없음과 기기별 상태를 설명했다.
- 첫 실행 동의, 트레이 재실행·부속 창 닫기, 업데이트 하루1회·오프라인·오른쪽 아래 배너 동작을 반영했다.
- 하드웨어 디코딩과 로그 확인, `--flex-display` 표기, ADB 실제 버전과 scrcpy 전체 버전을 바로잡았다.
- Companion 2.1.6 알림 종료와 복구 대기, 전송 준비 상태/정리 권한을 구분했다.
- 빌드/프로젝트/아이콘/현재 상태 문서도 대조했고, 과거 이력은 현재 표와 날짜별 기록으로 구분했다.
- 정확한 파일 해시와 사용자가 제공한 보안 검사 범위는 VERIFICATION_v2.5.0.md로 정리했다.

## 원격40경로별 처리

| GitHub 경로 | 이번 처리 |
| --- | --- |
| `.github/workflows/macos-portable.yml` | 다른 플랫폼/원격 전용: 원문 보존, Windows 설명과 분리; 향후 main 통합 시 삭제 금지 |
| `AGENTS.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `DXDisplayCleanup/PACKAGE_README.md` | 공용 컴포넌트 이관 대상; 옛 사본을 최신 원본으로 복구하지 않음, 공개 소스 의존성 준비 필요 |
| `DXDisplayCleanup/README.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `DXDisplayCleanup/SIGNING.md` | 공용 컴포넌트 이관 대상; 옛 사본을 최신 원본으로 복구하지 않음, 공개 소스 의존성 준비 필요 |
| `DexManager/README.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `DexManager/Resources/README_아이콘_적용가이드.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `DexManager/config/README.txt` | 버전 중립 런타임 안내: 유지, 검사 ZIP의 파일 변경 없음 |
| `DexManager/licenses/DX-Manager-MIT-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/LGPL-2.1-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/MinGW-w64-winpthreads-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/SDL3-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/THIRD_PARTY_NOTICES.md` | 현행 런타임 라이선스 대조; 현재5.0.1 정리본 유지 |
| `DexManager/licenses/THIRD_PARTY_NOTICES_MACOS.md` | 다른 플랫폼/원격 전용: 원문 보존, Windows 설명과 분리; 향후 main 통합 시 삭제 금지 |
| `DexManager/licenses/dav1d-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/scrcpy-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `DexManager/licenses/zlib-LICENSE.txt` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `LICENSE` | 라이선스 원문 유지; 법적 문구를 버전 교체 대상으로 취급하지 않음 |
| `README.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `build/macos/PORTABLE_PACKAGE.txt` | 다른 플랫폼/원격 전용: 원문 보존, Windows 설명과 분리; 향후 main 통합 시 삭제 금지 |
| `docs/AI_WORKFLOW.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/CHANGELOG.md` | 과거 기록 보존,2026-10-16 현재 상태/미해결 항목 추가 |
| `docs/DECISIONS.md` | 과거 기록 보존,2026-10-16 현재 상태/미해결 항목 추가 |
| `docs/FAQ_EN.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/FAQ_KO.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/HANDOFF.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/KNOWN_ISSUES.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/MACOS_GUIDE.md` | 다른 플랫폼/원격 전용: 원문 보존, Windows 설명과 분리; 향후 main 통합 시 삭제 금지 |
| `docs/PACKAGE_README.md` | 현재2.5.0/5.0.1/2.1.6 최소 배포 안내 확인: 유지 |
| `docs/PACKAGE_README_MACOS.md` | 다른 플랫폼/원격 전용: 원문 보존, Windows 설명과 분리; 향후 main 통합 시 삭제 금지 |
| `docs/PROJECT_BRIEF.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/README.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/RELEASE_NOTES_v1.3.0.md` | 과거 버전·해시·검사 기록 보존, 현재 안내 링크 추가 |
| `docs/RELEASE_NOTES_v2.0.0.md` | 과거 버전·해시·검사 기록 보존, 현재 안내 링크 추가 |
| `docs/RELEASE_NOTES_v2.0.1.md` | 과거 버전·해시·검사 기록 보존, 현재 안내 링크 추가 |
| `docs/SESSION.md` | 과거 기록 보존,2026-10-16 현재 상태/미해결 항목 추가 |
| `docs/TECH_NOTES.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/TODO.md` | 과거 기록 보존,2026-10-16 현재 상태/미해결 항목 추가 |
| `docs/USER_GUIDE_EN.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |
| `docs/USER_GUIDE_KO.md` | 로컬2.5.0 현재 안내로 갱신; 게시하지 않음 |

## 새 문서

- [빌드 전제와 경로](BUILDING.md)
- [파일 해시와 검증 범위](VERIFICATION_v2.5.0.md)
- [실제 스크린샷 대조와 촬영 목록](SCREENSHOT_PLAN_v2.5.0.md)

## 스크린샷 결론

사용자가 지정한 Store 원본9장을 모두 보았다. 포터블2.5.0과 차이가 있어 그대로 최신 포터블 화면이라고
사용하지 않는다. 기존 저장소23개 중 Android 시스템 설정 예시6개는 유지 가능하며, 앱 화면17개는
재촬영/참조 교체 대상이다. 파일은 삭제하지 않고 현재 사용자 안내에서 구형 앱 화면 링크만 제외했다.
새 이미지는 사용자가 실제 최종 프로그램으로 촬영해 전달하면 연결한다. 검사 fixture의 가짜 상태를
실기 스크린샷으로 게시하지 않는다. 자세한 대상과 우선순위는 촬영 목록을 따른다.

## 배포 전 남은 준비

1. 현재 포터블 프로그램의 실제 README/설명서 사진을 받아 참조·캡션을 연결한다.
2. 공개 main의 macOS 파일을 보존하는 통합 방법을 확정한다. 현재 로컬 작업선의 부재를 삭제 승인으로 해석하지 않는다.
3. 공용 Companion을 외부 로컬 폴더 없이 가져올 공개 소스 의존성/동결 사본을 준비하고 독립 clone 빌드를 검증한다.
   현재 프로젝트는 외부 `CompanionSharedRoot`를 필요로 한다. 이 문서 작업에서 제품 코드나 검사된 파일을 바꾸지 않는다.
4. 메이님이 배포를 직접 지시한 뒤에만 소스·원격 문서·태그·릴리스를 갱신한다. 검사 완료 보고는 배포 승인이 아니다.

## 검증 기록

문서 상대 링크·헤더 앵커·이미지 참조·현재 안내의 오래된 버전/경로·기능 사실을 확인한다.
실행 파일/APK/ZIP 해시를 기존 검증값과 대조한다. 결과는 이 문서 끝에 추가한다.
현재 사진 작업이 끝났거나 독립 소스 빌드를 통과했다고 주장하지 않는다.
## 2026-10-16 validation completed

- Public main inventory:40 paths;6 historical release descriptions; no wiki.
- Local documentation:40 Markdown files,106 relative references/anchors and7 image references;0 issues.
- Current README/guides/FAQ/source notes: no remaining obsolete4.1/2.1.2/2.1.5 bundle claims,
  old Companion/Paths menu routes, stale ADB37.0.0 value or incorrect flex-display short option.
  Historical4.0/SDL3 regression explanations remain explicitly historical.
- All23 original image files retained. Nine Store originals inspected; no new portable screenshot claimed.
- Final EXE/APK/ZIP hashes match the scanned release records; no rebuild or archive changes.
- Local HEAD remains8b19906. Remote main still2a1ec107450d8755e3691e9fa52aa086ed034e8d;
  latest public release remainsv2.0.1. No commit/push/tag/release or remote documentation edits.
- New portable photos and shared-component/public-source setup remain pending before publication.
