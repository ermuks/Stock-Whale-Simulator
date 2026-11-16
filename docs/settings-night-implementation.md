# ESC·설정 / 마감 저장 / 취침·피로도 구현

## 적용 결과

- ESC는 게임을 멈추고, 실제 카메라 화면을 한 번 캡처하여 블러 처리한 뒤 반투명 검정으로 덮는다. 중앙에 이어서 하기·설정·세션 떠나기·게임 종료를 표시한다.
- 설정은 같은 배경을 사용한다. 별도 밝은 패널은 없으며 그래픽·오디오·게임·UI를 상단 탭으로 전환한다. 만들어 둔 페이지를 On/Off하며, 슬라이더는 사각형 내부가 채워진다.
- 한국어·영어·일본어를 즉시 전환하고 PlayerPrefs에 유지한다. 음량, 비활성 창 음소거, 품질, 프레임 제한, VSync, 화면 모드, 마우스 감도, 휠 감도, 메뉴 크기를 실제 설정에 연결했다.
- 기존 3D 배치와 TradeScene은 수정하지 않았다. 작업 시작 시 백업과 현재 씬의 SHA256이 같다.

![ESC 실제 게임 카메라](../Validation/Settings/Night05/pause-ko-1920x1080.png)
![설정 실제 게임 카메라](../Validation/Settings/Night05/ko-0-1920x1080.png)
![마감 보고서 일본어](../Validation/Settings/Night05/report-ja-1920x1080.png)
![피로도 90 테스트](../Validation/Settings/Night05/fatigue90-1920x1080.png)

## 하루 / 수면 규칙

- 거래는 09:00~17:00. 기존 거래 구간의 실제 길이 900초를 유지했다. 거래 중 스트레스의 시간 적분도 8시간으로 맞췄다.
- 17:00에 기존 미체결 취소·수량 검증·정산·세금 처리가 완료된 뒤 보고서를 표시하고 자동 저장한다. 보고서 확인 전에는 시간이 멈춘다.
- 보고서의 `준비 계속`은 야간으로 돌아간다. 야간에도 ESC 메뉴에서 `08:00까지 취침`할 수 있다.
- 17:00~익일 08:00과 08:00~09:00은 거래 시간과 같은 비율로 흐른다. 취침만 08:00으로 이동한다. 배속 설정은 없다. 최초 시작은 기존처럼 대기 없이 개장한다.
- 수면 1게임 시간당 피로도 15%p 회복. 3시간은 45%p, 7시간은 105%p이며 최솟값은 0이다. 새벽 01시에 취침하면 7시간을 자고 08시에 일어난다.
- 깨어 있는 시간의 누적은 우선 **시간당 2.5%p**다. `PlayerRestController.fatiguePerGameHour`로 조정 가능하다. ESC로 정지한 동안에는 증가하지 않는다.
- 밤샘은 08시 경계에서 누적한다. 1~3회는 생존하며, **연속 네 번째 밤샘의 다음 08시에 사망**한다. 실제로 수면 시간을 보낸 취침은 연속 밤샘을 끊는다. 수면 회복량은 잔여 시간에 비례한다.
- 피로도가 높아지면 시야 가장자리와 위아래가 부드럽게 어두워지고 느리게 움직인다. 카메라 위치·회전·사용자 감도는 이 효과로 변경하지 않는다.

## 저장 정의와 연결

- 수동 중간 저장 기능을 제거했다. 게임 설정 저장과 세계 진행 저장은 분리했다.
- 보고서 생성 완료 시점의 현금·보유분·장부·봇 심리/기억·채팅·리딩방·진행·뉴스·시계·피로도를 하나의 체크포인트로 저장한다.
- 중도 종료 또는 사망 후 불러오면 마지막 마감일 17:00의 보고서에서 재개한다. 그 뒤의 매매·취침은 다음 마감 전까지 저장되지 않는다. 아직 첫 마감을 한 번도 하지 않았다면 새 세션부터 시작한다.
- 경로: `Application.persistentDataPath/session-v2.json`. 현재 Windows 제품 설정에서는 `C:/Users/saerm/AppData/LocalLow/ROCT Games/DonDon/session-v2.json`이다. 검증은 `Validation/Settings/<실행명>`으로 격리했다.
- SHA256·스키마·타입·노드 수·파일 크기를 검사하며 임시 파일 확인 후 원자적으로 교체한다. 이전 파일은 `.bak`으로 보관한다. 저장 실패 시 이전 마감 유지 문구와 재시도 버튼을 표시한다.
- 복원 시 원본 Stock/Order/Trader의 공유 관계를 유지한다. 신규 유입 봇은 기존 컴포넌트 생명주기를 먼저 초기화하고 저장된 자산/심리를 적용한다. 쉬던 봇의 비활성 상태, 선택 종목과 중앙 차트 연결도 복원한다.
- 기존 `Time.time` 기반 만료·쿨타임이 재시작 후 엇나가지 않도록 43개 파일의 시뮬레이션 타임스탬프를 `SessionClock.Time`으로 연결했다. UI 실시간 타이머와 Unity의 deltaTime/timeScale은 유지했다. 가짜 뉴스의 실제 시간 쿨타임은 별도 UTC 규칙을 유지한다.

## 수정 위치 / 핵심 함수

- `Assets/Scripts/Settings/PauseMenuController.cs`: Open / Resume / Back / ReceiveClosingReport / Sleep / ShowDeath / Reload. ESC 우선 처리, 입력 격리, 페이지 전환, 마감 및 사망 복구 흐름.
- `SystemMenuUI.cs`: 생성자 / BuildSettings / Settings / Show / Slider / Report. 화면 계층·배치·활성 페이지·정산 수치.
- `MenuBackdrop.cs`: Capture / Dispose. 열 때만 768px 폭의 카메라 캡처와 가로·세로 블러 2회. RT를 재사용한다.
- `GameSettings.cs`: Boot / Commit / Apply. 즉시 적용·디스크 저장, 변경되지 않은 품질/화면 설정의 반복 적용 방지.
- `PlayerRestState.cs`: Advance / Sleep / MissNight. 순수 게임 시간 모델.
- `PlayerRestController.cs`: ObserveClock / SleepUntilMorning / NightPassedAwake / Update. 시계와 모델 연결, 시야 효과.
- `SessionStore.cs`: Save / Restore / RestoreInitialized / RestoreData / Graph. 마감 전용 세계 저장과 복원.
- `SessionClock.cs`: Restore / ResetClock. 복원 가능한 타임스탬프.
- `Assets/Scripts/MarketDay/MarketDayController.cs`: ExactGameMinute / Duration / Update / AdvancePhase / StartMorningAfterSleep. 8시간 장, 연속 야간, 보고서 시점.
- `BotChatData` 생성자, `MarketWallClock.RefreshFromMarket`, `DeviceMarketJournal.Stamp`, `TabletAccountUI.Update`, `TabletGroupChatUI` 표시부: 24시를 넘는 내부 시각을 달력 일자와 00~23시로 표시.
- `StockListUIManager.ReconnectSession`: 복원한 상장 목록을 기존 풀링 목록에 연결.
- 카메라·책상·태블릿·휴대폰·상호작용·차트·종목 전환 Update 입력은 `PauseMenuController.BlocksInput`을 확인한다. 메뉴 클릭이 세계 조작으로 전파되지 않게 한다.
- `Assets/Editor/SystemMenuSetup.cs`, `SystemMenuValidation.cs`: 이미지/번역 임포트와 명시적 실행 검증. `Tests/Settings/Run.ps1`: 단일 Unity, CPU·메모리·시간 제한.
- 번역: `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json`, Unity String Tables. 새 메뉴/취침 문구를 세 언어로 추가했다.
- `Packages/manifest.json`: 이미 설치된 Newtonsoft JSON 3.0.2를 저장 코드의 직접 의존성으로 명시했다.

[신규 C# 10개 파일 전문](settings-night-source.md) · [기존 파일의 함수별 원본/수정본](settings-night-function-changes.md) · [전체 변경 목록](../Validation/Settings/changed-files.json)

## 검증

- 최종 통합: `Validation/Settings/Night05/result.txt` **127항목 PASS**, Unity 오류 없음. 런타임/에디터 C# 컴파일 통과.
- 언어별 4카테고리, 1600×900 및 1920×1080 실제 카메라 렌더. TMP 잘림/overflow 검사와 육안 확인. 최댓값 110% 메뉴 크기도 검사했다.
- UGUI Raycast로 실제 상단 탭을 맞추고 pointerClick 경로 실행. 사각 Slider의 pointer down/drag 경로, 게이지 폭, 페이지 전환 시 오브젝트 개수 불변을 검사했다.
- 일시정지 중 카메라·시장 시계 불변, 이전 일시정지 상태 복원, 미체결 주문 해제 후 마감 저장, 봇 자산·주식 수량 보존, 새 씬으로 재로딩, 신규 유입 봇/오프라인 봇/선택 차트 복원.
- 야간 날짜 넘김, 01시 취침→08시 기상, 09시 개장, 피로 회복/누적/정지, 3회 생존과 4회 사망, 마지막 17시로 복귀.
- 시간 경계는 테스트 픽스처로 옮겨 실제 시장의 전환·정산 코루틴을 실행했다. 게임에 배속이나 테스트용 시간 점프 UI를 추가하지 않았다. 여러 날을 실제 시간으로 방치한 장시간 부하 검증은 아니다.
- 새로운 Unity 프로세스: `Validation/Settings/Restart05/result.txt` **3항목 PASS**. 일본어·음량·메뉴 크기 유지, 실제 재실행 화면 확인. 마지막에 사용자 원래 설정 복구 완료.
- 기존 채팅/심리 회귀: `Validation/Settings/social-regression.txt` **19 PASS / 0 FAIL**. 기존 5개 시드의 실제 생성 경로 확인.
- 자원: 통합 Unity **90.2초 / 최대 3937MiB**, 재실행 **35.7초 / 최대 2290MiB**. 한 번에 한 프로세스, affinity 1, BelowNormal, 20fps, 작업자 각 1개, 4GB 중단 장치. 테스트용 세션은 실제 저장 경로를 사용하지 않았다.
- 발견 후 수정: 피로 셰이더 `_MainTex` 누락, 동적 봇 복원 누락, 비활성 봇 활성화, 선택 차트 재연결. 포인터 테스트는 페이지가 표시된 다음 프레임에 실행하여 Unity의 실제 이벤트 순서를 따르게 했다.

## 확인 범위 / 남은 제한

- 그래픽/입력은 Windows Unity Editor의 실제 카메라와 UGUI 이벤트로 검증했다. 별도 플레이어 EXE 패키징과 OS 전체 화면 전환은 이번에 실행하지 않았다.
- 저장은 현재 코드 스키마와 현재 경제 카탈로그를 대상으로 한다. 초기 카탈로그에 있는 기업의 상장/소유 상태와 플레이 도중 유입된 봇을 복원한다. 외부 코드가 카탈로그에 없는 Stock 자체를 새로 생성하는 확장 경로, 다른 버전의 클래스 필드/경제 카탈로그로의 마이그레이션은 별도 지원이 필요하다. 이 경우 복원 오류를 표시하며 기존 저장을 덮어쓰지 않는다.
- 저장 그래프는 마감에만 구성하지만 수십 MB의 데이터와 순간적인 메모리를 쓴다. 이번 검증의 Unity Editor 최대 사용량을 위에 명시했다. 장시간 플레이의 큰 기록량과 배포 빌드 메모리는 추가 측정 대상이다.

## 생성 이미지

imagegen 스킬의 **내장 image_gen 도구**를 사용했다. 최종 파일은 `Assets/Resources/UI/SystemMenu/MenuControl-v2.png`이며 버튼에 실제 Sprite로 연결했다. 생성된 픽셀은 변경하지 않았고 Unity에서 반투명 색을 적용한다. 텍스트는 TMP이다. 이전 밝은 패널은 런타임 Resources에서 제거하고 `Validation/Settings/RejectedArtwork`에 보관했다.

최종 프롬프트:

> Create a production game UI sprite: a SINGLE perfectly clean flat white rounded rectangle, very wide and short, centered. Pure opaque white interior. Crisp smooth edges, tiny 4px corner radius, no outline, no stroke, no bevel, no texture, no grain, no gradient, no shadow, no glow. Transparent outside. It fills nearly all canvas width and height with only 4px transparent margin. Canvas 1536x512. Absolute geometric precision, like a plain blank modern menu button mask. No text, no icons, no additional objects. The game will tint it translucent gray.

임포트: Sprite / Single / Full Rect / Center, PPU 100, Alpha Is Transparency, MipMap Off, Clamp, Bilinear, Uncompressed, Max Size 2048. 9-slice Border 좌·하·우·상 각 32, UI Sliced / PPU Multiplier 2. 생성 요청 크기와 실제 반환 이미지 크기는 다를 수 있으며 원본 PNG를 유지했다.
