# 일일 기업 결산 · 지분 인수 · 기업 정보

2026-11-16. 기존 시장·거래·태블릿·뉴스·마감 저장에 연결했다. 새 Canvas, 모니터, 배속 기능은 만들지 않았다.

## 플레이 동작

- 태블릿의 **사건·경영 → 기업 경영**에서 선택 종목의 기업 정보를 확인한다. 기존 양쪽 태블릿의 경영 페이지에 같은 UI가 붙는다. 두 번째 계좌의 잠금은 유지된다.
- 발행 주식 수, 마감 종가, **마감 종가 × 그날 발행량**인 시총, 두 계좌를 합한 지분율을 표시한다. 시총은 다음 마감까지 고정된다.
- 매출·운영비·순이익·내 배당금·기업 현금·미지급 비용·재투자·배당 비율을 확인한다. 첫 기업 결산 전에는 실적을 `—`로 표시한다.
- **지분 매입**은 현재 실제 최우선 매도호가에 주문한다. 과반 미만이면 과반에 필요한 주식 수, 이미 과반이면 완전 소유까지 남은 주식 수가 목표다. 가용 현금과 기존 미체결 매수량을 반영한다. 매도호가가 현재가보다 2% 이상 높으면 기존 보호 조건에 따라 대기한다. 부족한 매물은 미체결 주문으로 남을 수 있다.
- 실제 체결된 보유량이 **50%를 초과**하면 경영권이 생긴다. 매도 대기 주식은 여전히 보유분이다. 미체결 매수는 소유권이 아니다. 지분이 줄어들어 과반을 잃으면 정책 변경 권한도 없어진다.
- 경영권 확보 후 **배당 / 재투자 / 비축** 중 기업 정책을 선택한다. 변경은 이후 장 마감 결산에 적용된다. 기업 현금을 개인 계좌 현금과 합치지 않는다.
- 장 마감에 기업별 일일 결산을 하고, 배당을 모든 실제 주주에게 지급한다. 플레이어 수령액은 마감 보고서의 **기업 배당**, **기업 결산** 목록에 표시한다. 배당은 개인 매매 실현손익과 별개이며 기존 0.05% 실현 매매이익 세금에 중복 포함하지 않는다.
- 최초 경영권 확보가 확인되면 해당 종목의 인수 확정 기사 한 건을 발행한다. 신뢰도 0.98, 긍정 감정 1, 기존 뉴스 영향 계수 1.30을 사용한다. 기존 관심도·뉴스 목격·기업 의견·주문 성향 경로로 호재가 전달된다. 주가를 코드에서 강제로 올리지 않는다. 보유 지분을 반복 매매해 인수 기사를 무한 생성할 수 없다.

## 기업 자금과 기본 수치

설정: `Assets/Resources/Progression/CompanyBusiness.json`.

- 초기 기준 자본 = 초기 기업 평가가격 × 발행량. 초기 기업 현금은 기준 자본의 8%, 기준 일매출은 8%다. 기업 현금은 기존 주식의 기업 가치에 해당하는 별도 사업 원장으로 초기화되며 개인 계좌로 지급하지 않는다.
- 일매출 = 기준 일매출 × 업종 수요 × 생산성. 업종 주기, 기업별 시드, 신뢰할 수 있는 실제 뉴스의 제한된 수요 변화가 반영된다.
- 비용 = 기준 일매출 × (수요 × 55% + 고정비 20%) + 미지급 비용의 일일 이자 0.1%. 매출은 내림, 비용은 올림하여 원 단위로 결산한다.
- 순이익 = 매출 − 비용. 현금과 이익으로 미지급 비용부터 상계한다. 현금이 음수가 되면 미지급 비용으로 기록한다.
- 배당 정책: 양의 순이익 중 60% 배당, 10% 재투자.
- 재투자 정책: 15% 배당, 60% 재투자.
- 비축 정책: 15% 배당, 나머지 현금 유보.
- 배당 총액은 보유 현금을 초과할 수 없고 미지급 비용이 있으면 지급하지 않는다. 주주 지급액 = 배당 예산 × 보유량 ÷ 발행량, 1원 미만 내림. 반올림 잔액과 미배분 주식 몫은 기업에 남는다.
- 재투자는 기업 현금을 사용하고 생산성을 높인다. 기본 생산성 상한은 2배다. 기업에 남긴 이익은 기존 `realValue`에 반영되지만 시장 체결가격은 직접 변경하지 않는다.
- 새 게임의 첫 인수 후보 NM은 800,000주로 조정했다. 초기 500원 기준 시총 4억 원, 과반 400,001주의 단순 가격은 약 2억 원이다. 실제 시장에서 충분한 매물과 자금이 필요하다. 기존 저장의 발행량은 이 설정으로 덮어쓰지 않는다.

## 저장과 호환

`Stock.business`에 회사별 재무·정책·마감일·종가·발행량·시총·인수 공시 여부를 저장한다. 기존 마감 체크포인트의 그래프 저장에 포함된다.

기업 원장이 없던 저장은 복원된 가격과 발행량으로 사업 원장을 한 번 초기화하고, 저장된 마지막 종가에서 시총을 복원한다. 과거 매출과 배당을 추측하거나 소급 지급하지 않는다. 다음 실제 마감부터 실적이 생성된다. 옛 저장에 배당 필드가 없으면 정산 보고서의 배당은 0으로 읽는다. 기존 경영권을 새 인수처럼 다시 공시하지 않는다.

마감 순서: 주문 취소 → 주식/예약 자산 정합성 검사 → 기업 결산·배당 → 투자자 일일 성과 정산 → 기본 수입·이용료·세금 → 마감 보고서와 저장. 주주 배당은 이미 각 계좌에 지급되므로 보고서의 개인 계좌 이체액에 다시 더하지 않는다.

## 변경 파일과 함수

- `Assets/Scripts/NightShift/CompanyBusiness.cs` 신규: `BeginClose`, `DividendBudget`, `SharePayment`, `FinishClose`, `DemandFor`, `Valid`. Unity와 분리한 재무 모델·원 단위 배분·결산 중복 방지.
- `Assets/Scripts/NightShift/CorporateEconomy.cs` 신규: `ObserveControl`, `Settle`, `SetPolicy`, `ReconnectSession`. 1초 주기 지배권 확인, 기존 뉴스 구독, 배당 지급, 저장 이관. 주주 64명마다, 회사마다 프레임을 양보한다.
- `Assets/Scripts/NightShift/CompanyDashboardUI.cs` 신규: `Install`, `Build`, `Refresh`, `Acquire`, `Policy`. 기존 경영 패널 안의 4열 정보 배치, 버튼, 실제 값 표시. 갱신은 0.5초 주기이며 표시 코드에서 사업 원장이나 주문을 생성하지 않는다.
- `Stock.cs`: 기업 원장 필드 추가. `StockManager.Economy.cs / BuildEconomyStocks`: 신규 게임 첫 인수 후보 발행량 설정.
- `WorldDramaDirector.cs / Awake, AcquireShares, BoardDecision`: 사업 시스템 연결, 실제 호가 매입, 정책 변경. 이전 저장과 호환되도록 기존 경영 필드는 제거하지 않았다.
- `CorporateControlUI.cs / Update`, `TabletAccountUI.cs / Start`: 기존 진입점과 신형 경영 화면 연결.
- `MarketDayController.cs / Start, AdvancePhase`: 주문 이전 소유권 기준선 설정, 마감 결산 순서에 기업 배당 추가.
- `NewsManager.cs / Publish, PublishAcquisition`: 직접 작성한 인수 기사도 기존 뉴스 발행 경로로 전달.
- `DailyIncomeLedger.cs / ClosingReport, CloseDay, Copy, CanRestore`, `DailyIncomeController.cs / Settle`: 배당 보고·저장, 이중 지급 방지, 기존 과세 기준 유지.
- `DailyIncomeUI.cs / InstallTablet, Refresh`, `SystemMenuUI.cs / 생성자, Report, ReportCompanies`: 기존 정산 화면에 배당과 기업별 내역 추가.
- `SessionStore.cs / RootNames, RestoreData, Graph.FillFields`: 새 원장 저장, 이전 저장에서 초기 씬의 기업 원장이 잘못 남지 않도록 이관.
- `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json`, `Assets/Localization/Tables/DonDon_ko.asset`, `DonDon_en.asset`, `DonDon_ja.asset`, `DonDon Shared Data.asset`: 짧은 한·영·일 UI와 인수 기사 번역. 기존 SDF 폰트와 문자열 테이블 사용.
- `Assets/Editor/CorporateValidation.cs`, `Tests/Corporate/Program.cs`, `Run.ps1`, `translations.py`, `report.py`: 순차 실행 검사·번역 추가·작업 전후 코드 기록.

씬·프리팹·모니터/태블릿의 3D Transform은 수정하지 않았다. `TradeScene.unity`는 작업 시작과 종료 SHA256이 `622351BBCC1A603CE6C96A757148EB11E82C0262BDEAA4CE5ABF2A0D88217C40`으로 동일하다. 기존 미커밋 수정은 유지했다.

## 검증 결과

- 순수 재무 검사 **16개 통과**: 120일 연속 결산, 현금 보존, 지분별 원 단위 배당, 적자 무배당·미지급 비용, 생산성 재투자, 당일 중복 금지, 저장 복원, 기존 보고서 호환. `Validation/Corporate/Core/results.txt`.
- 기존 기본수입·리딩방 이용료·세금·손실 구제 회귀 검사 **271개 통과**. `Validation/DailyIncome/Core/results.txt`.
- 최종 Unity 통합 검사 **35개 통과**. 실제 TradeScene에서 주문/체결로 400,001주 확보, 미체결 주문의 경영권 미인정, 최초 인수 기사, 정책 버튼, 배당, 주식 원장 정합성, 마감 저장·복원, 구버전 필드 누락 저장 이관, 배당 재지급 차단을 확인했다. `Validation/Settings/Corporate02/checks.txt`.
- 실제 기사 목격 로직에서 **113명**이 인수 기사를 읽고 매수 성향이 1보다 높아졌다. 감정이나 목격 확률을 테스트에서 강제로 지정하지 않았다. 이는 통합 검사 중의 관측값으로 모든 시장에서 동일한 인원이나 주가 상승을 보장한다는 뜻은 아니다.
- 기존 게임 카메라와 태블릿 열기 경로로 **1600×900 / 1920×1080**, 한국어·영어·일본어를 촬영하고 화면 및 TMP 잘림 검사를 확인했다. 테스트 자금과 매도 호가는 검증용 상태이며 실제 사용자 저장을 변경하지 않았다.
- Unity는 항상 하나씩, 코어 1개·BelowNormal·20fps·4GB 중단 한도로 실행했다. 첫 실행 96.8초/3,746MiB, 최종 실행 108.7초/3,890MiB. 정상 종료했고 별도 실행 중인 Unity는 없다. 테스트 메모리가 한도에 가까웠으므로 추가 장시간 Unity 검사는 실행하지 않았다.
- 전체 런타임/에디터 C# 컴파일 통과. 마지막 표시 함수의 읽기 전용 null 처리와 코드 포맷 변경은 컴파일로 확인했다. 앞선 실행 검사와 화면 촬영 후 화면 배치는 변경하지 않았다. 독립 실행형 플레이어 빌드는 생성하지 않았다.

## 적용 화면

![한국어 기업 정보 1600×900](D:/GitHub/DonDon/Validation/Settings/Corporate02/company-ko-1600x900.png)

![영어 기업 정보 1920×1080](D:/GitHub/DonDon/Validation/Settings/Corporate02/company-en-1920x1080.png)

![일본어 기업 정보 1600×900](D:/GitHub/DonDon/Validation/Settings/Corporate02/company-ja-1600x900.png)

![마감 보고서](D:/GitHub/DonDon/Validation/Settings/Corporate02/closing-report-1920x1080.png)

## 남은 범위

실적 모델은 조정 가능한 업종 수요·비용·생산성 모델이다. 공장·사업부·제품 단위 경영을 새로 만들지는 않았다. 일일 배당은 매일 계산하지만 적자·자금 부족일에는 지급되지 않을 수 있다. 이번 120일 검사는 재무 모델 검사이며 장기간 전체 시장의 밸런스 검증과는 구분한다. 다음 우선순위는 실제 플레이에서 첫 인수까지 걸리는 시간과 배당/재투자 정책 간 수익 격차를 확인해 설정을 조정하는 것이다.

신규 C# 전문과 수정 함수 원본/수정본은 [코드 부록](D:/GitHub/DonDon/docs/corporate-economy-code.md)에 있다.
