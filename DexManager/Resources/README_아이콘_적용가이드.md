# DX Manager icon resources

## English

The product logo is `DXManager.ico` with its source `DXManager_source.svg` and `DXManager_256.png`.
The project sets its executable icon and embeds the logo. Forms and tray controls use `AppIconProvider`;
new UI code must not depend on a loose icon file beside the EXE.

Small command icons use the Microsoft Fluent UI System Icons assets under `FluentIcons`, with SVG
sources and the generated runtime data used by `UiSymbol`/`SymbolView`. Keep the existing mapping,
including Screenshot for capture and Wifi for wireless ADB. The custom phone illustration is retained.
Use the current UI's20-pixel command-icon convention; mini bars have their own reviewed dimensions.
The Fluent and Pretendard notices are under `../licenses` and remain in user distributions.

This file documents existing resources. Updating GitHub screenshots does not require changing icons,
generating replacement artwork or rebuilding a previously scanned EXE.

## 한국어

프로그램 로고는 `DXManager.ico`, 원본 SVG와256 PNG로 관리합니다. 실행 파일·폼·트레이는 기존
리소스 및 `AppIconProvider` 경로를 사용하며 외부 아이콘 파일을 새로 요구하지 않습니다.
작은 기능 아이콘은 `FluentIcons`의 Microsoft Fluent 자산과 `UiSymbol`/`SymbolView` 매핑을 따릅니다.
화면 캡처·무선 ADB 매핑과 사용자 확인을 마친 미니바 크기를 유지하세요.
스크린샷 문서 작업 때문에 아이콘·글꼴·검사 완료 실행 파일을 바꾸지 않습니다.


## Windows2.5.0 UI sizing — 2026-10-24

Small command icons target20px; retain reviewed mini-bar dimensions and large custom phone artwork.
English Help sidebar text alone uses13px to fit; Korean Help/other sidebar text remains14px.
This is a text-layout exception. Preserve the renderer, global fonts, original SVG/license bytes and accepted mini-bar geometry.
