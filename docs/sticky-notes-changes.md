# 포스트잇·손글씨·HUD 변경

2026-11-15. 기존 중앙 모니터 아래 우측에 클릭하지 않는 3D 포스트잇 세 장을 배치했다. 상단 시장 알림, 전화 수신 문구, 좌측 상단 시간 HUD, 하단 화면 가이드를 표시하지 않는다. 전화의 수신·응답 동작은 유지하며 사운드는 추가하지 않았다.

## 적용 결과

- `리딩방 가입`: 기존 NPC 리딩방 가입으로 완료.
- `매매 성과`: 기존 거래대금·실현손익 조건 충족으로 완료.
- `내 리딩방`: 기존 개설 기능에서 실제로 방을 열면 완료.

메모는 목표 상태만 표시한다. 버튼, 클릭 콜라이더, GraphicRaycaster가 없으며 TMP와 일본어 폴백 글리프도 포인터를 받지 않는다. 완료된 장은 접착을 풀고 실제 물리로 떨어지며, 착지 후 Cloth와 Rigidbody 계산을 종료한다. 완료 이력은 기존 PlayerProgression 스냅샷에 저장하므로 손실이나 방 이탈, 저장 복원으로 종이가 다시 붙지 않는다. 기존 경제 계산과 개설 조건은 변경하지 않았다.

## 변경 파일·핵심 함수

- `Assets/Scenes 1/TradeScene.unity`: 실제 포스트잇 프리팹 배치, `Text :: Market Event`와 `Text :: Phone Alert` 오브젝트 삭제, 기존 HUD 비활성화. 기존 기기 Transform의 위치·회전·크기·앵커는 보존했다.
- `Assets/Resources/Environment/InvestorNotes/InvestorJourneyNotes.prefab`: 세 장의 Mesh, Cloth, 접착 계수, 손글씨 Canvas, 세 가지 종이 재질, 약한 모니터 주변광.
- `Assets/Scripts/NightShift/InvestorJourneyNotes.cs`: `Refresh`가 기존 NPC 방·플레이어 성과를 읽고 표시 및 완료를 갱신한다. `InitializeMilestones`는 저장 이력을 복원하고 `SetLanguage`는 영어·한국어·일본어만 받는다.
- `Assets/Scripts/NightShift/StickyNotePaper.cs`: `Release` → `Fall` → `Settle`로 접착 해제·낙하·정적 Mesh 전환. `HideCompleted`는 이미 완료한 메모를 재생성하지 않는다.
- `Assets/Scripts/NightShift/InvestorJourneyUI.cs`: `Start`, `Update`, `ResolveStep`에서 기존 HUD 대신 메모를 연결한다. 기존 태블릿의 실제 개설 기능은 유지한다.
- `Assets/Scripts/NightShift/PlayerProgression.cs`: `RememberJourneyMilestones`, `Capture`, `Restore`에 세 비트 완료 이력만 추가했다. 기존 버전 1 데이터는 누락 필드를 0으로 읽는다.
- `Assets/Scripts/DeskViewController.cs`: `TryMonitorPose`의 차트 확대 범위에 메모를 포함한다. 메모용 입력 처리는 없다.
- `Assets/Scripts/DeskControlHints.cs`: `LateUpdate`에서 `showShortcutHints = false`이면 키캡과 설명을 숨긴다. 기존 단축키와 흰 원형 기기 상호작용 표시는 유지한다.
- `Assets/Scripts/MarketDay/TabletGroupChatUI.cs`: `Update`에서 꺼진 HUD 텍스트 갱신을 건너뛴다.
- `Assets/Scripts/NightShift/PhoneCallController.cs`: `Update`의 HUD 수신 문구 대입 제거. 전화의 상태 전환은 유지한다.
- `Assets/Editor/InvestorJourneyNotesSetup.cs`: `Install`, `Rebuild`, `RemoveHudGuidance`, `BuildPrefab`, `Position`, `PaperMesh`가 현재 모니터 구조를 기준으로 생성·연결한다.
- `Assets/Editor/StickyNoteFontSetup.cs`: 원본 폰트를 TMP SDF와 명시적 일본어 폴백으로 구성한다.

신규 스크립트 네 개의 전문과 기존 변경 함수의 원본·수정본은 [코드 기록](sticky-notes-code.md)에 있다. 생성 프롬프트·원본 해시는 [이미지 기록](sticky-notes-imagegen.md)에 있다.

## 종이·Cloth 설정

장당 63개 정점, 96개 삼각형. 위쪽 두 줄 14개 정점은 고정한다. Bend 0.96, Stretch 1, Damping 0.82, Solver Frequency 30, Stiffness Frequency 60으로 뻣뻣한 종이 느낌을 유지한다. 접착 상태에서는 작게 흔들리고 자체 충돌은 사용하지 않는다.

실제 폭 약 0.51, 높이 약 0.306 Unity 단위로 Mesh 자체를 만들고 Transform scale은 1로 유지한다. 낙하 Mesh를 Bake할 때 종이가 축소되던 문제를 수정하고 실제 폭 유지까지 검사했다. 착지용 콜라이더는 Ignore Raycast 레이어이며 부착 중 및 정착 후에는 꺼진다.

생성한 `StickyPaper_Albedo.png`를 원본 그대로 사용한다. 원본은 1254×1254이며 Import Max Size는 1024다. Default, sRGB, 불투명, Mip Maps 켬, Clamp, Trilinear, Aniso 4, Compression None. Sprite/9-slice가 아닌 실제 종이 Mesh의 Albedo다. 머스터드·살구·세이지 색상은 세 URP Lit Material로 적용한다.

## 폰트·지원 언어

한글·영문은 Nanum Pen Script, 일본어는 Yomogi 폴백이다. 두 원본 TTF와 OFL 1.1 라이선스를 `Assets/Fonts/StickyNotes/`에 포함했고 배포용 라이선스도 `Assets/StreamingAssets/ThirdPartyLicenses/`에 넣었다. 출처·해시는 해당 폴더의 `SOURCES.md`에 기록했다.

`Assets/Resources/UI/StickyNotes/`의 TMP 에셋은 Dynamic SDFAA, Sample 72, Padding 8, Atlas 1024×1024, Multi Atlas 사용, Face Dilate 0.08이다. 원본 TTF 참조를 유지한다. 실제 영어·한국어·일본어 메모 문구의 글리프와 오버플로를 확인했다. Nanum의 현대 한글 11,172자와 인쇄 가능 ASCII 95자 포함 여부도 원본 cmap에서 확인했다.

문구는 `Assets/Resources/Localization/StickyNotes/en.json`, `ko.json`, `ja.json`에 분리했다. 기본 한국어이며 네 번째 언어 값은 거부한다. 이번 범위는 포스트잇 번역과 폰트 연결이다. 기존 게임 전체 문구의 번역 완료를 의미하지 않는다. [지원 언어 정책](supported-languages.md).

## 검증 결과

- 런타임·Editor C# 컴파일 통과. 기존 Unity API obsolete 경고는 남아 있다.
- 실제 TradeScene Play Mode: 163개 검사 통과, 실패 0. 게임 시간 배속 1 유지.
- 1600×900·1920×1080 실제 게임 카메라의 일반/차트 확대 화면에서 HUD 제거, 종이 배치, 손글씨를 확인했다.
- 실제 NPC 방 가입, 기존 시장의 매수·매도 체결, 기존 방 개설 버튼으로 목표 세 개를 순서대로 완료했다.
- 각각 한 장만 낙하, 착지, 완료 후 물리 계산 종료, 손실 후 이력 유지, 기존 저장 Capture/Restore 확인.
- 시장 이벤트·전화 수신 로직을 실행해도 HUD 문구가 재생성되지 않는다.
- 세 언어 글리프 누락·텍스트 잘림 없음. 메모와 폴백 글리프가 입력을 가로채지 않음.
- 한 번에 Unity 하나, CPU 코어 하나, 20fps. 최종 실행 69.5초, 최대 메모리 2,486MiB, 종료 코드 0. Unity는 종료했다.
- 기존 씬 Transform 값과 거래·해금 계산 보존, 생성 PNG 원본 해시 일치, Play Mode 실행 전후 씬 파일 불변 확인.
- 이번 작업 시작 시점 백업과 비교한 신규·수정 C#의 후행 공백 검사는 통과했다. 전체 Git 기준 `diff --check`는 기존 변경의 CRLF 및 Unity가 저장한 빈 직렬화 값의 후행 공백을 보고하므로 깨끗한 결과로 기재하지 않는다. 관련 없는 기존 변경을 일괄 정리하지 않았다.

최종 증거: `Validation/Screenshots/2026-11-15-sticky-notes-06/`의 `checks.txt`, `capture.json`, `resources.json`, `cloth-motion.json`, `unity.log`, 화면 PNG. 정적 보존 검사: `Validation/StickyNotes/preservation-proof.json`. 컴파일 로그: `Validation/StickyNotes/compile.log`.

실제 화면:

- [1920×1080 기본 시야](../Validation/Screenshots/2026-11-15-sticky-notes-06/attached-home-1920x1080.png)
- [1920×1080 차트 확대](../Validation/Screenshots/2026-11-15-sticky-notes-06/attached-chart-1920x1080.png)
- [1600×900 차트 확대](../Validation/Screenshots/2026-11-15-sticky-notes-06/attached-chart-1600x900.png)
- [첫 메모 낙하](../Validation/Screenshots/2026-11-15-sticky-notes-06/first-note-falling.png)
- [영문 손글씨](../Validation/Screenshots/2026-11-15-sticky-notes-06/handwriting-english.png)
- [일본어 손글씨](../Validation/Screenshots/2026-11-15-sticky-notes-06/handwriting-japanese.png)

제한: 독립 실행 Player 빌드는 이번 검증에서 만들지 않았다. 언어 변경은 기존 시스템이 호출할 수 있는 API와 리소스로 제공하며 별도의 언어 선택 UI는 추가하지 않았다. 새 전체 게임 저장 시스템을 만들지 않고 기존 스냅샷에 완료 이력만 연결했다.
