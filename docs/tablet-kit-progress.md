# Tablet UI kit implementation

## Audit
- P0: Existing Trader/DeviceOrderSession reserves buy cash and sell inventory correctly, supports independent pending orders. No transaction fee exists in the engine: actual fee is 0, not an invented UI surcharge.
- P0: DeviceMarketJournal fill timestamps are game HH:mm but lack dates and are not checkpoint roots. Persist the actual journal and reconnect subscriptions on restore.
- P1: Existing navigation mixes unrelated tabs, hides clock on order/automation. Replace internal navigation only, preserve World Space Canvas/model/camera/input.
- P1: Existing company growth implements paid projects and all four IPO gates, but UI exposes only three. Show four gates and actual distribution ratios (existing three policy presets), not mock allocation values.
- P1: Spread engine uses finite work-account resources. Existing screen lacks stock picker, dated fills and game elapsed/update timestamps. Reuse engine, add presentation.
- P1: Fake news studio already exists; expose a separate special page. Keep real 600-second cooldown, including pause/offline, display a game-duration equivalent at the existing clock rate; no promised fixed unlock timestamp across sleep/pauses.
- P2: Shared flat colors, supplied icons, numeric columns, KO/EN/JA and bounded camera/input tests.

## Package differences
The ZIP does not include the documented 256 atlas or individual Icons_256. The supplied 128 atlas has the same 48 cells and is used without regeneration. References are design evidence only, never runtime backgrounds.

## File plan
TabletKitStyle/common components and atlas; TabletKitLayout/navigation/assets; existing TradingOrderView/order layout; CompanyGrowthUI/company overview with existing CompanyDashboardUI detail; SpreadTradingUI/automation; NewsDeskUI/separate studio; DeviceMarketJournal/game timestamps & checkpoint; locale catalog and isolated validation.

## Constraints
Scene/model poses unchanged. No speedup, no parallel Unity instances. Tests run with one CPU affinity, one worker, 20 FPS and 4 GiB watchdog. Existing dirty workspace retained.

## Final implementation
- 구조 조사 → 공통 테마/atlas → 자산·주문 → 기업 경영 → 작업 계좌 → 시간·다국어·검증 순으로 완료.
- 새 런타임 2개, 검증 에디터 1개. 기존 컴포넌트 재사용. 정확한 비교/전문: tablet-kit-code.md.
- 기능·제한·데이터 및 시간 정책: tablet-kit-changes.md.

### 이번 작업에서 스냅샷과 달라진 기존 C#
- `Assets/Scripts/News/FakeNewsManager.cs`
- `Assets/Scripts/News/NewsDeskUI.cs`
- `Assets/Scripts/NightShift/CompanyGrowthUI.cs`
- `Assets/Scripts/NightShift/DailyIncomeUI.cs`
- `Assets/Scripts/NightShift/DeviceMarketJournal.cs`
- `Assets/Scripts/NightShift/DeviceOrderSession.cs`
- `Assets/Scripts/NightShift/DeviceTerminalUI.cs`
- `Assets/Scripts/NightShift/InvestmentPositionRow.cs`
- `Assets/Scripts/NightShift/PendingOrderListUI.cs`
- `Assets/Scripts/NightShift/PlayerAccounts.cs`
- `Assets/Scripts/NightShift/SpreadTradingUI.cs`
- `Assets/Scripts/NightShift/TabletAccountUI.cs`
- `Assets/Scripts/NightShift/TabletControlStyle.cs`
- `Assets/Scripts/NightShift/TradingOperationController.cs`
- `Assets/Scripts/NightShift/TradingOperationController.Spread.cs`
- `Assets/Scripts/NightShift/TradingOrderView.cs`
- `Assets/Scripts/Settings/SessionStore.cs`
- `Assets/Scripts/News/NewsManager.cs` (FullStamp 두 곳)
- `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json` 및 기존 Localization importer가 갱신하는 문자열 테이블/SDF 자료.
- `Tests/TerminalOrders/Host.cs`: 예약/게임 시각 스텁 보완. 별도 호스트 전체의 기존 도메인 불일치로 이 테스트는 아직 실패.
- 준비·검증 도구: `Tests/TabletKit/prepare.py`, `build_validation.py`, `rewrite_spread.py`, `report.py`. rewrite_spread.py는 최초 편집용 도구이며 후속 수동 수정본을 덮어쓰므로 재실행하지 않는다.

### 최종 Unity 실행 결과
- TabletKitAssets02: PASS 60 checks; 39.9초; peak 2375.0 MiB. `Validation/Settings/TabletKitAssets02/`.
- TabletKitOrders04: PASS 70 checks; 35.1초; peak 2227.0 MiB. `Validation/Settings/TabletKitOrders04/`.
- TabletKitCompany03: PASS 68 checks; 40.2초; peak 2810.0 MiB. `Validation/Settings/TabletKitCompany03/`.
- TabletKitAuto03: PASS 69 checks; 36.9초; peak 2043.0 MiB. `Validation/Settings/TabletKitAuto03/`.
- TabletKitSave01: PASS 52 checks; 52.7초; peak 3341.0 MiB. `Validation/Settings/TabletKitSave01/`.
- TabletKitCold01: PASS 53 checks; 51.0초; peak 2432.0 MiB. `Validation/Settings/TabletKitCold01/`.
- TabletKitSpecial02: PASS 69 checks; 34.5초; peak 2515.0 MiB. `Validation/Settings/TabletKitSpecial02/`.

최종 7개 실행의 체크 합계 441개. 각 실행마다 반복하는 48개 아이콘 검증과 공통 검증이 포함되어 있으므로 서로 독립적인 441개 시나리오라는 뜻은 아니다.

### 회귀·실패 기록
- CompanyGrowth 21 / Corporate 16 / DailyIncome 271 = 308 PASS. 로그는 `Validation/TabletKit/*-core.log`.
- TerminalOrders 독립 호스트: 컴파일 실패. 중복 StockRiskGrade / InvestorSociety.day, 누락 NewsArticle / CompanyBusiness / CompanyOpinionBook. Unity 주문 검증을 별도 통과했으며 이 호스트는 성공 처리하지 않음.
- 최종 전체 C# 컴파일 로그: `Validation/TabletKit/compile-final.log`.
- 초기 Orders01/02, Company01, Auto02 실패 및 수정은 tablet-kit-changes.md에 기록.

### 씬 무변경 확인
`622351BBCC1A603CE6C96A757148EB11E82C0262BDEAA4CE5ABF2A0D88217C40` (시작/완료 일치).
