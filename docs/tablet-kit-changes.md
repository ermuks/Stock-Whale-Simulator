# DonDon 태블릿 UI 키트 적용 결과

## 1. 적용 범위와 파일

기존 두 World Space 태블릿의 내부 화면을 실제 TMP SDF, Image, Button, TMP_InputField, Slider, ScrollRect로 교체했다. 레퍼런스 이미지는 런타임 배경으로 사용하지 않았다. 기존 계좌·주문·기업·스프레드·뉴스 모델과 이벤트를 재사용했다.

이번 작업은 씬/프리팹을 다시 만들거나 저장하지 않았다. `Assets/Scenes 1/TradeScene.unity`의 작업 시작/종료 SHA256은 모두 `622351BBCC1A603CE6C96A757148EB11E82C0262BDEAA4CE5ABF2A0D88217C40`이다. 원래 작업 트리에 있던 수정은 유지했다. 기존 모델 Transform, Canvas 렌더링 모드와 Event Camera, 카메라 및 입력 라우팅을 변경하지 않았다.

런타임 적용 위치는 기존 `TabletAccountUI.contentRoot` 아래이다. 새 `Button :: Tablet …` 탭, 기존 자산 목록과 주문 패널, `Panel :: Company Growth`, 기존 자동매매 deskPanel, 기존 NewsDeskUI studio를 사용한다. 최상위 태블릿 Canvas 대신 내부 화면 RectTransform만 조정했다.

신규 런타임 파일:

- `Assets/Scripts/NightShift/TabletKitStyle.cs`: `Icon`, `TextStyle`, `Flat`, `Action`, `Glyph`, `Place`. 공통 색상·아이콘·평면 컴포넌트.
- `Assets/Scripts/NightShift/TabletKitLayout.cs`: `Install`, `Build`, `BuildAssets`, `Add`, `RefreshTabs`. 기존 화면 연결과 내부 레이아웃.

신규 에셋:

- `Assets/Resources/UI/TabletKit/Icons.png`: 제공된 128 atlas 원본.
- `Assets/Resources/UI/TabletKit/DesignTokens.json`: 제공된 디자인 토큰.
- 각각의 `.meta`. 기존 에셋 GUID를 재발급하지 않았다.

수정한 런타임 파일과 함수별 비교는 아래에, 정확한 작업 전/후 diff와 신규 C# 전문은 `docs/tablet-kit-code.md`에 있다. 비교 기준은 Git HEAD가 아니라 이번 작업 시작 시 보관한 `Validation/TabletKit/before/`이다. 따라서 이전 작업의 미커밋 변경을 이번 작업으로 잘못 포함하지 않는다.

## 2. 네 화면의 연결 상태

### 자산 — 완료

주식 평가금·평가손익/수익률·주문 가능 현금·총자산을 실제 계좌에 연결했다. 네 열의 보유 목록, 평가금/수익률 정렬, 선택 상태, 목록 드래그/휠, 상세 및 주문 진입을 유지했다. 정렬 후 가격이 변해도 자동으로 재배열하지 않는다. 작업 계좌의 수동 주문과 이체 진입도 유지했다.

데이터 출처는 기존 `InvestmentDashboard`와 `Trader`이다. 수량은 매도 예약 수량을 포함한 실제 보유량, 취득원가는 기존 계좌의 잔존 원가, 평가금은 실제 평가가격에 기반한다. 총자산은 예약 현금을 다시 더하지 않으며 기존 세금 채무 정의를 유지한다. 기업 운영 현금은 개인의 주문 가능 현금에 합치지 않는다. UI에서 자산을 증감하지 않는다.

### 주문 — 완료, 지원 유형은 기존 지정가

왼쪽에 매수/매도·가격·수량·비율·합계·주문 버튼, 오른쪽에 가용 자원과 미체결 목록을 배치했다. 기존 분할 주문/확인 흐름을 유지했다. 여러 주문, 부분체결, 행별 취소, 매도 예약 수량을 실제 엔진에 연결했다. 미체결이 있어도 별도 주문을 낼 수 있다.

신규 시장가 주문 엔진은 추가하지 않았다. 기존 지정가 유형을 별도 영역에 표시한다. 현재 엔진의 거래 수수료는 0원이므로 유효성 검사는 가격×수량에 실제 예약/세금 제약을 적용한다. UI에 임의 수수료를 추가하지 않았다.

분할 주문의 아직 제출되지 않은 매수 잔여분을 다른 주문·이체·기업 출자에서 다시 쓸 수 있던 경로를 보완했다. 실제 호가 예약과 미래 분할 예약을 한 번씩만 차감한다. 자기 분할 물량을 제출할 때는 자기 예약 범위를 사용할 수 있다.

### 내 기업 — 완료, 정책은 기존 세 가지 프리셋

기업 평가액·운영 현금·일매출·순이익·지분율, 설비/영업/연구의 비용·효과·기간·진행, 네 가지 상장 조건을 기존 `CorporateEconomy`, `CompanyBusiness`, `CompanyGrowth`에 연결했다. 첫 기업 화면이 실제 경영 화면으로 열린다. 상세 기업 정보 화면은 그대로 연결했다.

상장 기준은 기존 데이터 설정의 평가액 50,000,000원 이상 / 흑자 결산 3회 이상 / 확장 완료 2회 이상 / 미지급 비용 0이다. 각 조건이 빠지면 실제 신청 버튼이 비활성화된다. 기업 투자는 회사 현금에서 한 번 차감되고, 개인 자금은 명시적인 출자 버튼으로만 이동한다. 배당 입금은 기존 장 마감 정산 시점에 유지한다.

정책은 배당/재투자/비축의 기존 프리셋을 재사용한다. 현재 데이터 기준 비율은 각각 60/10/30, 15/60/25, 15/0/85이며 합계는 100%이다. 시안의 자유로운 비율 +/- 편집기는 구현하지 않았다. 상장 후 실제 종가×발행 수량으로 계산하는 시가총액은 기존 기업 정보 하위 화면에서 기업 평가액과 별도로 확인한다.

### 자동매매 — 완료

작업 계좌의 종목 선택·스프레드 단일 슬라이더·예상 양쪽 가격·가용 현금/주식·시작/취소·실제 미체결·체결·실현손익·게임 경과 시간을 연결했다. 실행 중 중복 시작과 종목 변경을 막고 취소 버튼을 주요 액션으로 표시한다. 취소/재시작 시 실제 예약 자원이 반환된다.

기본 계좌 총자산이 정확히 100,000,000원이면 잠금, 100,000,001원이면 해금되는 기존 규칙을 검증했다. 잠금은 자물쇠 아이콘만 표시한다. 해금 자체가 작업 계좌에 돈이나 주식을 지급하지 않는다.

가짜 뉴스는 별도 특수 기능 페이지로 이동했다. 기존 해금/실제 기사 생성/발행 엔진을 유지한다. 실제 버튼 클릭으로 기사 한 건이 발행되고 UI 갱신 전 반복 클릭으로도 쿨다운을 우회하지 못하는 것을 확인했다.

## 3. 함수 단위 원본 → 수정본 비교

- `TabletAccountUI.Start/ShowPage/Update`: 이전 네비게이션과 일부 페이지의 숨겨진 시계 → 공통 계좌/게임 시계와 새 탭을 설치, 기업 경영을 기본 진입으로 사용, 뉴스 읽기/특수 기능 분리. 기존 체결만 표시하던 기록에 주문 접수·변경 시각 및 상태도 추가했다.
- `InvestmentPositionRow.Select`: 기존 전역 종목 선택만 수행 → 같은 선택을 먼저 수행한 뒤 상장 종목은 주문, 비상장 기업은 경영 화면으로 연결한다.
- `TabletControlStyle.Button/Input/Emphasize`: 이전 생성 PNG 표면 → 토큰 기반 평면 색과 흰색/청록 텍스트. 기존 버튼 이벤트는 그대로 유지한다.
- `TradingOrderView.Configure/InputRow/Metric/Forecast`: 기존 공간 배치 → 주문을 왼쪽 고정 그리드로, 자원/미체결을 오른쪽으로 정렬한다. `FitCanvas`는 내부 Canvas 크기가 확정된 뒤 글꼴 및 입력 크기를 보정한다. `Confirm`은 분할 간격을 게임 시간 단위로 보여준다.
- `DeviceTerminalUI.ConfigureQuickOrder/Check/Review/Order`: 기존 입력과 주문 엔진을 유지하면서 최종 레이아웃 맞춤을 호출한다. 분할 간격은 게임 초로 입력/표시하고 기존 실행용 실제 초로 변환한다. `Money`는 KO/EN/JA의 원화 단위와 숫자 간격을 일관되게 적용한다.
- `PendingOrderListUI.Refresh/SetScale`: 기존 풀링/행별 취소 유지, 종목 보조 줄에 실제 접수 게임 일시를 표시하며 최종 화면 스케일을 적용한다.
- `CompanyGrowthUI.Build/Refresh`: 이전 기업 경영 화면 → 정렬된 핵심 지표/프로젝트 목록/상장 조건. 이전에 빠진 미지급 비용 조건을 실제 상태에 연결했다. 기존 정책을 표시할 때 실제 배당·재투자·비축 비율을 보여준다.
- `SpreadTradingUI.Build/BuildSlider/Update`: 기존 스프레드 컨트롤 → 좌측 실행 설정과 우측 실제 결과. `Select`는 기존 선택 종목 서비스를 사용하고 실행 중 변경을 막는다. `FitCanvas`로 최종 공간에 맞춘다. 뉴스 발행 입력은 이 화면에 넣지 않는다.
- `NewsDeskUI.Build/OpenStudio/OpenReader/RefreshStudio/RefreshPreview`: 같은 화면에 섞이던 기사 목록 UI와 발행 UI → 재사용하는 studio를 독립 하위 페이지로 표시한다. 기사 본문은 스크롤로 유지하고 중복 도움말은 제거한다.
- `PlayerAccounts.Available/ScheduledCash`: 실제 주문 예약만 제외 → 아직 제출하지 않은 분할 매수 예약도 계좌 단위에서 제외한다. 이체/출자/자동매매의 공용 가용 자금 계산에 반영된다.
- `DeviceOrderSession.AvailableCash/Submit/SendSlice/Cancel/OnFill`: 기존 다중 주문 기능 유지. 공용 예약 계산과 중복 차감되지 않도록 조정하고, 접수·체결·취소의 게임 시각과 최대 64건 요청 이력을 유지한다.
- `TradingOperationController.StartSpread/RefreshSpread/CancelSpread/OnFill/Finish`: 기존 실제 주문 생성/취소 유지. 게임 시작·갱신·종료 시각과 최대 32건의 해당 실행 체결 목록을 기록한다. 가상의 체결이나 수익을 만들지 않는다.
- `DeviceMarketJournal.Stamp/FullStamp/Observe/Add/Register/ReconnectSession`: HH:mm 기록 → DAY+HH:mm 기록과 게임 경과 시간 API. 로드 후 종목 이벤트를 중복 구독하지 않고 실제 계좌별 체결/뉴스 기록에 재연결한다.
- `NewsManager.Publish/PublishAcquisition`, `FakeNewsManager.Preview/FormatCooldown`: 기사 표시 시각을 같은 게임 날짜로 통일. 쿨다운 표시만 게임 시계 비율로 변환한다.
- `DailyIncomeUI.OpenStatement`: 기존 정산 창을 기록 하위 화면에서 열 수 있도록 진입 함수를 추가했다. 정산 계산과 저장 시점은 변경하지 않았다.
- `SessionStore`의 루트 목록 및 복원 후 재연결: 기존 주문/계좌 저장에 `DeviceMarketJournal`을 추가한다. 새 기록은 날짜/종목 참조를 복원한다. 구버전 저장에 기록 루트가 없으면 새 세션의 임시 기록을 비우며 과거 체결을 임의로 복원하지 않는다.

## 4. 게임 시간과 저장 정책

단일 표시 기준은 기존 `MarketDayController.CalendarDay`, `ClockMinute`, `AbsoluteGameMinute`이다. `DeviceMarketJournal`은 이 서비스의 표시 어댑터이며 별도 게임 시계를 만들지 않는다. UI 갱신 스로틀에 쓰는 unscaledTime이나 주문 스케줄링용 SessionClock을 표시 시각으로 사용하지 않는다.

게임 거래 시간은 09:00~17:00이며 현재 기본 설정은 실제 900초에 게임 480분이다. 환산율은 게임 28,800초 / `tradingSeconds`로 읽는다. 예: 실제 600초 × 32 = 게임 19,200초 = 05:20.

가짜 뉴스 동작 기준은 **여전히 실제 600초**이다. 기존 일시정지 중 실제 시간 경과 및 저장/로드의 실제 오프라인 경과 정책도 유지한다. 화면에는 남은 실제 시간을 현재 게임 시계 비율로 환산한 HH:mm 기간을 표시한다. 따라서 일시정지 중 게임 시계는 멈춰 있어도 이 제한은 줄어든다. 취침은 게임 시간을 건너뛰므로 미래의 고정 DAY/시각을 약속하지 않는다. 이것이 게임 시계와 실제 쿨다운 사이의 의도적인 차이이며, 기능 시간을 게임 10분으로 변경하지 않았다.

주문 접수/부분체결/취소 기록, 기사 일시, 자동매매 경과/갱신 시각은 게임 시간이다. 기업 완공은 기존 게임 DAY 단위이며 가격/수량/금액처럼 레퍼런스의 예시 값을 런타임에 하드코딩하지 않았다. 저장은 기존 장 마감 보고서 체크포인트 정책을 유지한다. 같은 프로세스 복원과 새로운 Unity 프로세스에서의 복원을 별도로 검증했다.

## 5. 검증과 실제 화면

검증은 Unity 프로세스를 하나씩 실행했다. CPU affinity 1, BelowNormal, worker 1, import worker 1, 20 FPS, 4 GiB 메모리 감시를 사용했다. 아래 최종 검증 작업들은 리소스 감시 중단 없이 종료했다. 이는 모든 PC에서의 성능을 보장하거나 장시간 시장 부하 검증을 대체하는 결과는 아니다.

자동화는 Play Mode의 실제 게임 카메라와 World Space GraphicRaycaster/EventSystem을 사용한다. 단순 onClick 호출만으로 클릭 테스트를 통과시키지 않았다. 1600×900 / 1920×1080, 한국어/영어/일본어를 촬영하고 고정 텍스트 영역 잘림을 검사했다. 사람이 직접 마우스를 조작한 사용자 테스트는 아니다.

자산/주문/자동매매 캡처는 부분체결·예약·손익·스크롤 상태를 재현하기 위해 검증 전용 계좌/종목 조건을 설정한 Play Mode 화면이다. 제품 UI에 예시 숫자를 넣은 것이 아니며 실제 Trader/OrderBook의 결과를 표시한다. 기업 캡처는 별도로 실제 투자 진행을 거친 저장 데이터에서 얻었다. 쿨다운 경계 검사는 시간 계산에 경계 입력을 넣는 방식이며 실제 10분을 기다리는 장시간 검증은 수행하지 않았다.

- 자산: 빈 계좌/현금 0, 실제 계좌 구분, 목록 정렬 후 시세 변화, 실제 레이캐스트 정렬 버튼, 드래그/휠.
- 주문: 미체결 중 추가 주문, 예산 초과 거부, 부분체결, 행별 취소, 다른 주문 보존, 미래 분할 예약/실제 예약의 중복 차감 방지, 매도 예약 수량, 실제 입력 포커스/실행/취소 버튼.
- 기업: 네 상장 조건 각각 미충족/충족 시 실제 버튼 상태, 회사 현금만 차감, 중복 투자 차단. 촬영은 기존 실제 투자 진행을 저장한 5일차 체크포인트로 수행했다.
- 작업 계좌: 정확히 1억/1억 초과 경계, 단독 잔고, 실제 스프레드 체결, 중복 시작 거부, 정지 시 예약 반환, 재시작, 자산→수동 주문 버튼, 가짜 뉴스 실발행/반복 클릭 차단.
- 시간/저장: 실제 600초 쿨다운 경계, 오프라인 경과, 비정상 저장 시각, 날짜가 있는 기록과 실제 종목 객체 참조의 동일/새 프로세스 저장 복원.
- 에셋: 48개 셀 누락 없음, 흰색 전경/투명 모서리, PreserveAspect. 원본 알파를 재생성하지 않았다.

경제 회귀 테스트는 CompanyGrowth 21개, Corporate 16개, DailyIncome 271개로 총 308개를 통과했다. 기존 `Tests/TerminalOrders`의 독립 호스트는 현재 도메인과 맞지 않는 중복 StockRiskGrade/InvestorSociety.day 및 누락 NewsArticle/CompanyBusiness/CompanyOpinionBook 때문에 컴파일 단계에서 실패했다. 이를 성공으로 집계하지 않았다. 이번 주문 검증은 실제 Unity 엔진으로 별도 수행했다. 독립 호스트의 정비는 남은 개발 작업이다.

최종 C# 런타임/에디터 전체 컴파일도 수행했다. 기존 Unity deprecated API 경고는 남아 있으며 에러는 없다. 배포용 standalone Player 빌드는 이번에 수행하지 않았다.

초기 검증 실패와 수정:

- Orders01: UI 배치 전의 Canvas 크기로 폰트를 계산하여 넘침 → 최종 배치 이후 FitCanvas 적용.
- Orders02: 아직 Start가 호출되지 않은 잠금 태블릿의 view 접근 → null 안전 접근 및 열릴 때 스케일 적용.
- Company01: 두 줄 상장 조건의 높이 부족 → 실제 두 줄 영역 확보.
- Auto02: 테스트가 페이지 전환 직후 GraphicRegistry 반영 전에 클릭 → 다음 프레임 이후 실제 레이캐스트로 재검증. 직접 onClick 호출로 우회하지 않았다.

최종 실행별 수치와 캡처 경로는 같은 폴더의 `tablet-kit-progress.md`에 누적했다. 각 폴더의 checks.txt / result.txt / resources.json / unity.log를 보존했다.

실제 카메라 결과:

- [자산](D:/GitHub/DonDon/Validation/Settings/TabletKitAssets02/page-0-ko-1920x1080.png)
- [주문](D:/GitHub/DonDon/Validation/Settings/TabletKitOrders04/page-1-ko-1920x1080.png)
- [내 기업](D:/GitHub/DonDon/Validation/Settings/TabletKitCompany03/page-2-ko-1920x1080.png)
- [자동매매](D:/GitHub/DonDon/Validation/Settings/TabletKitAuto03/page-3-ko-1920x1080.png)
- [가짜 뉴스](D:/GitHub/DonDon/Validation/Settings/TabletKitSpecial02/page-5-ko-1920x1080.png)

## 6. 시안과 남은 차이

- ZIP에는 명시된 256 atlas 및 Icons_256 개별 파일이 없었다. 동일 순서의 실제 128 atlas(1024×768)를 사용했다. Sprite Single / Full Rect / PPU100 / Center / Bilinear / Clamp / no mipmap / no compression / max1024 / alpha transparency 설정이며, 이름별 48개 셀 Sprite를 한 번 생성해 캐시한다. UI는 Simple+PreserveAspect를 사용한다.
- 시안의 장식 카드/그래프를 복제하지 않았다. 회사와 주문은 기존 실제 기능 범위에 맞춘다. 기업 정책 자유 비율 편집과 시장가 주문은 현재 미구현이다.
- 뉴스 발행은 별도 페이지이며, 기사 본문은 정보 콘텐츠이므로 짧은 레이블로 삭제하지 않고 스크롤에 둔다.
- 기존 사용자가 직접 정한 회사명은 언어 전환으로 번역하지 않는다. UI 레이블과 시스템 문구는 KO/EN/JA로 바뀐다.
- 전체 게임의 모든 과거 화면을 다시 디자인한 작업은 아니다. 기업 상세·정산·기사 목록 등 기존 하위 화면 기능을 연결해 보존했다.
- 다음 우선순위는 오래된 독립 주문 테스트 호스트 정비와 실제 사용자 플레이의 읽기/클릭 평가다. 모든 체형의 글꼴·임의의 극단적 자산값·모든 기기/해상도를 검증했다고 주장하지 않는다.
