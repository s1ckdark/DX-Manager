# Companion update backlog for Store / Store 다음 업데이트 항목

> Historical scope — current documentation at [the documentation index](README.md). This dated record describes its own build/artifact/state; current2.5.0 behavior and checksums are in the user guides and verification record.
> 과거 시점의 작업 기록입니다. 현재 동작·파일 해시는 문서 목차의2.5.0 안내를 따릅니다.


## English

The user approved this for a later Store update. Do not rebuild or resubmit the current Store package solely for this note.

- Portable Companion2.1.4 (versionCode11, minSdk33) fixes per-parent collisions after trim/slash normalization, including natural numbered suffix names and file/folder siblings. Original content URIs and DXMR v1 remain unchanged.
- The2.1.4 source also contains the earlier2.1.3 queued-request timeout/destruction cleanup fix. Compare both changes with the Store source before porting; do not replace Store files wholesale.
- Verified signed APK: E:\vs\dex system-portable-backport\DXDisplayCleanup\bundled\DX-Companion-v2.1.4.apk
- SHA256: 95DAF4E734FD36FE07AD8388FE8E4C98D96D1EFA0A8E359D8C8E3E27383B6536
- Certificate SHA256: AD615803C63760439750C36801E8152AB8664C60EE481EF1473F1DF5E80733BE
- Package: io.github.mazemei.dxdisplaycleanup
- Android20 unit tests, Release lint/assemble, official v2 signature and package/permissions/hash checks passed. Physical provider transfer and Android timeout confirmation remain pending.
- Next Store update: port required source changes, update its bundled APK/version/hash checks, use build-store-preview.ps1, and perform real-device verification before submission.
- Windows USB priority and badge changes belong to portable. Store was reference-only and must be checked separately if the user later requests changes.

## 한국어

메이님 요청에 따라 현재 제출한 Store판은 그대로 두고 다음 업데이트에 반영한다.
휴대폰 문서 제공자의 앞뒤 공백·경로 구분자 치환 후 이름 충돌을 번호로 분리하는 수정이다. 일반적인 사진  .txt의 확장자 앞 공백은 유지한다. 2.1.3의 대기 전송 종료 보강도 함께 비교한다.
APK와 PC의 정확한 버전·해시 검증값을 함께 갱신하고 Store 전용 프리뷰 스크립트로 검증해야 한다. 실제 휴대폰 전송 검증은 아직 남아 있다.
현재 Store 소스·APK·제출물은 변경하지 않았다.

## 통합 인계 문서

2026-10-05 이후 Store 적용에는 [STORE_UPDATE_HANDOFF_2026-10-05.md](STORE_UPDATE_HANDOFF_2026-10-05.md)를 사용한다. APK뿐 아니라 Fluent 아이콘/마우스·미니바·재연결 정책과 Store 실제 적용 위치/검증 목록을 포함한다. 동일 문서를 E:\vs\dex system-store\docs에도 보관했다.
