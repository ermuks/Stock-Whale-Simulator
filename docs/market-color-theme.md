# 상승·하락 색상 테마

설정 → UI → 시세 색상에 이미지 버튼 두 개를 추가했다. 현재 버튼 내부에는 텍스트가 없다.

- 빨강 / 파랑: 상승 `#EF5964`, 하락 `#4C96E8`. 기본값이며 이전 설정에 새 필드가 없을 때도 적용한다.
- 초록 / 빨강: 상승 `#37C995`, 하락 `#EF5964`.

왼쪽 버튼은 빨강·파랑·빨강, 오른쪽 버튼은 초록·빨강·초록 3캔들 PNG를 표시하며 선택 테두리로 구별한다. 이전 색 이름과 ▲상승/▼하락 문자는 제거했다. 언어 변경은 선택한 시세 색상에 영향을 주지 않는다. 에셋 경로·프롬프트는 `market-color-cards-imagegen.md`에 있다.

## 저장과 적용

`GameSettings.Values.marketColors`에 정수 0/1을 저장한다. 기존 `DonDon.Settings.v1` PlayerPrefs와 Commit/Boot/기본값 복원 흐름을 사용한다. 게임의 장 마감 체크포인트와 독립적인 사용자 설정이다. 잘못된 값은 기본 빨강/파랑으로 복원한다. 기본값 복원 버튼도 기존 흐름대로 빨강/파랑을 선택한다.

새 `MarketColors`는 표시 색상만 제공하며 자산·주가·주문을 변경하지 않는다. 실제 테마가 달라질 때만 변경 이벤트와 버전을 갱신한다. 다른 설정 슬라이더 조작이나 같은 테마를 다시 선택할 때 차트를 다시 칠하지 않는다.

차트/호가/종목 목록처럼 시세가 멈추면 갱신하지 않던 화면은 색상 변경 이벤트로 갱신한다. 태블릿의 손익은 기존 unscaledTime 기반 UI 갱신을 유지한다. 일시정지 메뉴 뒤의 블러는 기존 메뉴 진입 시 촬영한 정지 화면이며, 그 이미지를 매 프레임 재촬영하지 않는다. 선택 견본은 메뉴 안에서 바로 보이고 실제 차트는 이미 변경되어 메뉴를 닫으면 새 색상으로 표시된다.

## 수정 파일과 함수별 비교

- `Assets/Scripts/Settings/MarketColors.cs` 신규: `Preview`, `Up`, `Down`, `ForChange`, `Apply`. 기존에 흩어져 있던 방향별 색상에 하나의 선택 기준을 제공한다.
- `GameSettings.Values/Clamp/Apply`: 저장 항목 추가, 구버전/잘못된 값의 기본값 처리, 설정 변경 시 테마 적용.
- `SystemMenuUI.BuildSettings/MarketColorChoices`: UI 카테고리에 텍스트 없는 3캔들 이미지 버튼 두 개 추가. `Report`: 장 마감 손익 합계도 선택한 색상 사용.
- `CandleDrawManager.OnEnable/OnDisable/RefreshMarketColors/ApplyMaterialColors/GenerateMeshWithMaterials`: 기존 공유 Material 색상 → 렌더러의 submesh별 MaterialPropertyBlock 오버라이드. 공유 에셋은 수정하지 않는다. 버전이 바뀔 때만 갱신한다. Material 없는 경로도 같은 테마로 vertex 색상을 재생성한다.
- `ChartManager.OnEnable/OnDisable/RefreshMarketColors/RefreshTradeBasedValues/RefreshStatistics`: 종목 헤더·통계·기존 체결 기록을 정지된 시장에서도 갱신한다. 위험 배지 색상은 유지한다.
- `ChartGuideOverlay.RefreshTrades/TradeColor`: 데이터/geometry 변경만으로 갱신하던 캐시에 테마 버전을 포함한다. 기존 매수/매도 대기선의 방향 색상도 변경한다. 평단선은 기존 청록색이다.
- `ChartCandleSelectData.RefreshTexts`: 고정 상승/하락 색 → 공통 테마. 기존 선택 캔들에도 변경 이벤트를 반영한다.
- `StockListUIManager.OnEnable/OnDisable`: 테마 변경 시 `RefreshAll` 한 번 호출. 기존 정렬과 스크롤 위치는 그대로다.
- `StockListItem.Set/ApplyCandleAnchors`: 전일대비 등락률과 당일 미니 캔들의 색상을 같은 테마로 표시한다.
- `OrderBookUIManager.InitializeRows`, `DrawRawOrder.SetSide/RefreshColors`: 프리팹에 고정된 색 대신 실제 매수/매도 영역을 명시적으로 연결한다. 기존처럼 낮은 가격의 매수 호가에는 하락색, 높은 가격의 매도 호가에는 상승색을 사용한다. 깊이 막대는 같은 색의 낮은 알파다.
- `TradeHistoryItem.Set/RefreshColors`: 매수 주도/매도 주도 체결을 해당 방향 색상으로 표시하고 과거 표시 행도 갱신한다.
- `HoldListItem.Set/RefreshColors`: 보유 상세 손익에 선택한 테마를 적용한다. 기존 행의 표시 손익을 기억하여 새 체결 없이도 갱신한다.
- `InvestmentDashboard.ProfitColor`: 공통 테마 사용. 기존 `InvestmentPositionRow`, `CompanyDashboardUI`, `CompanyGrowthUI`, `DailyIncomeUI` 호출부에도 적용된다. 0은 기존 중립색이다.
- `DeviceTerminalUI.Header`, `TradingOrderView.SignColor`, `PendingOrderListUI.Refresh`, `SpreadTradingUI.Update`, `TabletKitStyle.Up/Down`: 태블릿의 가격 변동·손익·주문 방향·자동매매 실현손익을 연결한다. 주문 오류 문구는 별도 고정 Error 색을 유지한다.
- `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json` 및 기존 importer가 갱신한 KO/EN/JA 문자열 테이블.

위험 등급·심리 게이지 과열·오류 경고·선택 강조·거래량 막대의 색상을 시세 테마로 대체하지 않았다. 씬/프리팹/공유 Material 에셋은 이번 작업에서 변경하지 않았다.

## 검증

CPU affinity 1 / worker 1 / BelowNormal / 20 FPS / 메모리 제한 4 GiB로 Unity를 하나씩 실행했다.

- `MarketColors02`: 174 checks PASS, 42.9초, peak 2264 MiB. 체크 수에는 언어/테마별 반복 검증을 포함한다.
- `MarketColorsCold01`: 별도 새 Unity 프로세스에서 4 checks PASS, 27.5초, peak 1982 MiB.
- 후속 이미지 버튼 변경 `MarketColorCards01`: 160 checks PASS, 41.6초, peak 2096 MiB. 버튼 내부 TMP 0개, 정확한 PNG 연결, 비율 유지, 선택 테두리, 실제 클릭과 표시 검증. 완료 시 사용자 설정 복원.
- 기존 설정에 필드가 없는 경우 기본 빨강/파랑, 실제 EventSystem 레이캐스트 버튼 선택, 두 테마 왕복, 이미 그린 캔들/호가/종목 목록/체결 색상 갱신, 0 손익 중립, 위험 과열 빨강 유지, 같은 설정 반복 시 불필요한 갱신 없음.
- 일시정지 유지 및 변경 전후 계좌 현금/주문장 버전 동일. 공유 캔들 Material의 원래 색상 동일.
- 저장 JSON과 재실행 초기 테마 일치. 기존 음량/감도 값 유지. 검증 완료 뒤 사용자의 원래 PlayerPrefs를 복원했다.
- 한국어/영어/일본어, 1600×900/1920×1080의 설정 화면과 텍스트 영역을 확인했다.
- 전체 런타임·에디터 C# 컴파일 성공. 배포 Player 빌드는 수행하지 않았다.

초기 `MarketColors01`은 MaterialPropertyBlock 초기 접근 오류로 실패했다. 컴포넌트 필드 초기화 대신 렌더링 함수에서 지연 생성하고, 색상 적용 완료 후에만 버전을 기록하도록 수정한 뒤 위 검증을 다시 통과했다.

로그는 `Validation/Settings/MarketColors02/`, `MarketColorsCold01/`에 있다. 설정 테스트는 사용자 세션 대신 격리된 저장 경로를 사용한다.

## 실제 캡처

- [한국어 설정](D:/GitHub/DonDon/Validation/Settings/MarketColorCards01/settings-green-red-ko-1920x1080.png)
- [영어 설정](D:/GitHub/DonDon/Validation/Settings/MarketColorCards01/settings-green-red-en-1600x900.png)
- [일본어 설정](D:/GitHub/DonDon/Validation/Settings/MarketColorCards01/settings-green-red-ja-1600x900.png)
- [초록/빨강 실제 차트](D:/GitHub/DonDon/Validation/Settings/MarketColors02/chart-green-red-1920x1080.png)

신규 C# 전문과 작업 직전 스냅샷 기준 diff는 `market-color-theme-code.md`에 별도로 제공한다.
