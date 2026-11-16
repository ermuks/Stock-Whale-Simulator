# DonDon — Claude Code 정밀 분석 인계서

작성 기준: 2026-11-16, Asia/Seoul. 대상: 사용자가 지정한 Claude Code Fable 5.2 Medium.
프로젝트 루트: `D:\GitHub\DonDon`

이 문서는 게임 전체를 새로 만들라는 지시가 아니라, **현재 작업 트리의 구현과 게임 의도를 대조하여 정확하게 분석하기 위한 인계서**다. 이 문서 작성 작업에서는 게임 코드를 수정하지 않았다. 모델 이름은 사용자가 제공한 표기이며 실제 서비스의 모델 가용성은 확인하지 않았다.

## 0. 먼저 읽을 주의사항

1. 이 저장소는 상당량의 미커밋 변경을 포함한다. 조사 시 `git status --short` 항목이 411개였다. 모두 이번 문서나 마지막 차트 작업에서 만든 변경이 아니다. `reset --hard`, `clean`, 일괄 checkout, 자동 포맷, 과거 생성 스크립트 재실행을 하지 않는다.
2. `HEAD`는 조사 시 `5b58941 feat(chat): complete stage 8 dialogue variety diagnostics and final validation`이었다. 이후 기업·설정·태블릿·차트 등의 많은 작업은 작업 트리에 존재한다. HEAD만 분석하면 현재 게임을 분석한 것이 아니다.
3. `PROJECT_OVERVIEW.md`는 초기 개요다. Unity/패키지 버전 및 기능 설명 일부가 오래됐다. 아래 환경 및 실제 파일이 우선이다.
4. `docs/*-changes.md`, `*-code.md`는 각각 작성 당시의 증거다. 후속 기능으로 대체된 설명이 있다. 특히 “전체 저장 없음”, “고정 사업 수입만 있음”, “플레이어 방 3개”, “FOMO는 영구 수치”를 현재 규칙으로 무조건 인용하지 않는다.
5. 테스트 PASS는 해당 실행·시드·조건의 증거이지 현재 전체 게임과 장기 균형의 보증이 아니다. 함수 호출 검사, 실제 UI 이벤트 검사, 실제 마우스 조작, 렌더링 검사, 자율 시장 관찰을 구분한다.
6. 사용자는 CPU 과부하로 PC가 종료된 경험이 있다. **Unity/게임 검증은 반드시 하나씩**, 낮은 부하로 수행한다. 별도 프로젝트의 Unity도 함께 확인한다. 실행 중 에디터나 게임을 임의 종료하지 않는다.
7. 게임 배속 기능은 사용자가 명확히 거부했다. 1×/4×/16× 등의 설정을 추가하지 않는다. 테스트용 시간 경계 이동과 제품의 배속 기능도 구별해서 보고한다.
8. 현재 조사에서 루트 이하 `AGENTS.md`는 발견되지 않았다. 다음 작업 시작 시 부모 경로와 추가된 지침을 다시 확인한다.
9. 아직 확인하지 않은 원인을 확정하거나 구현되지 않은 화면을 완료라고 보고하지 않는다. 로그가 없으면 실제 채팅 예시나 시장 결과를 꾸며 쓰지 않는다.

## 1. 게임의 정체성과 원하는 경험

DonDon은 3D 방에 앉아 모니터 3대, 태블릿 2대, 휴대폰으로 시장을 관찰하고 행동하는 싱글플레이 경제·사회 시뮬레이션이다. 자유 이동 중심 게임이 아니다. 어둡고 습하고 눅눅한 작업실, 모니터 빛, 실제처럼 움직이는 시장과 채팅이 분위기의 핵심이다.

사용자가 강조한 재미:

- 수익 숫자 증가만이 아니라 시장을 지배한다고 믿던 세력이 경쟁·손실·몰락하는 과정을 관찰한다.
- 관찰 → 추리 → 중요한 순간의 결정 → 실제 시장과 투자자의 반응 → 성장 → 새로운 권한이라는 순환.
- 의도한 비중은 관찰 약 70%, 추리 약 20%, 결정 약 10%. 계속 버튼을 누르게 만드는 업무 프로그램을 원하지 않는다.
- 플레이어가 아무것도 안 해도 봇·기업·뉴스·채팅·관계가 지속적으로 변해야 한다.
- 플레이어를 즐겁게 하려고 정해진 시점에 가격을 올리고 내리는 연출보다 실제 자금·재고·체결·심리로 결과가 나와야 한다.
- 잘못된 예측, 읽고 답하지 않음, 반박, 중재 실패, 퇴장도 자연스럽다. 모든 발언에 강제 응답하지 않는다.

장기 성장 방향(모두 완성됐다는 뜻이 아니다):

1. 개인 투자자: 봇 리딩방 참여, 관찰, 직접 거래.
2. 리딩방 운영자: 실현 성과를 쌓아 자기 방 개설, 첫 회원 직접 초대, 이용료 설정.
3. 시장 세력: 자본·평판·집단의 독립적인 행동으로 영향력 확대.
4. 기업 지배: 지분 확보, 인수, 배당과 경영, 여러 회사로 확장.
5. 거래소 설립: 자본·기업·투자자 기반을 갖춰 거래소 운영.
6. 거래소 성장: 시장 참여자와 기업 유치, 독립 가격 형성.
7. 시장 장악: 기존 거래소와 경쟁, 이동·폐쇄 가능성.
8. 시장 지배자: 상장·정지·폐지 등 규칙 결정, 누적 선택에 따른 엔딩, 이후 자유 플레이.

후반 5~8단계는 존재하는 클래스 이름만 보고 완료 판정하지 말 것. 실제 해금 조건→UI→명령→경제 효과→저장→복원까지 추적해야 한다.

## 2. 현재 환경과 작업 방식

- Unity `6000.5.5f1` (`ProjectSettings/ProjectVersion.txt`). 설치 실행 파일은 `C:/Program Files/Unity/6000.5.5f1/Editor/Unity.exe`.
- URP `17.5.0`, Input System `1.19.0`, Localization `1.5.14`, uGUI `2.5.0`, Test Framework `1.7.0`, Newtonsoft JSON `3.0.2` (`Packages/manifest.json`).
- C#, uGUI/World Space Canvas, TextMeshPro SDF, 차트 Mesh, URP. Input System 패키지가 있다고 모든 입력이 새 Input System이라는 뜻은 아니다. 차트 등에서 `UnityEngine.Input`을 사용한다.
- 주 씬: `Assets/Scenes 1/TradeScene.unity`.
- Windows PowerShell. 경로에 공백이 있으므로 literal path/따옴표를 사용한다.
- `.meta` GUID와 씬 참조를 보존한다. 씬 YAML에서 `:` 문자가 포함된 이름을 잘못 편집해 파싱 오류가 있었으므로 대규모 문자열 치환을 피한다.
- C# if/for/while 중괄호 생략 금지. 즉시 반환은 다음 줄에 `{ return; }`, `{ continue; }`, `{ break; }` 형태를 선호한다. 기존 파일의 오래된 스타일 전체를 무관하게 정리하지 않는다.
- GameObject 명명: `Area :: 이름`, `Panel :: 이름`, `Text :: 이름`, `Button :: 이름`, `Image :: 이름`.
- 신규 스크립트는 완결된 파일로 제공한다. 한 파일 수정 덩어리가 3개를 넘으면 함수별 기존 동작/새 동작과 이유를 보고한다.

## 3. 필수 읽기 순서

1. 현재 git 상태, 씬/패키지/설정, `PROJECT_OVERVIEW.md`(역사적 참고).
2. `Stock.cs`, `StockManager.cs`, `StockManager.Economy.cs`, `Order.cs`, `OrderBook.cs`, `Trader.cs`, `PriceTickPolicy.cs`.
3. `Assets/Scripts/MarketDay/MarketDayController.cs`, `Settings/SessionClock.cs`, `Settings/SessionStore.cs`.
4. `NightShift/PlayerAccounts.cs`, `DeviceOrderSession.cs`, `TradingOperationController.cs`, `.Spread.cs`.
5. `TradeBot/TradeBot.cs`, `TradeBot.Risk.cs`, `TradeBotManager.cs`, `MarketRhythmDirector.cs`, 해당 전략 클래스.
6. `InvestorSociety.cs`, `InvestorPsychology.cs`, `ChatSocial*.cs`, `LeadingRoomOrders.cs`, `NpcLeadingRoom.cs`.
7. `CompanyBusiness.cs`, `CompanyGrowth.cs`, `CorporateEconomy.cs`, `CorporateEconomy.Growth.cs`.
8. 기존 모니터/태블릿 UI와 입력, 현지화, 최근 차트 변경.
9. 해당 `docs`와 `Validation`을 읽어 실제 검증 범위를 대조한다.

`NightShift/`에는 이름과 달리 야간 전용이 아닌 거래·사회·기업·UI 코드가 많이 있다. 루트 파일만 보고 누락 판정하지 않는다.

## 4. 시장·원장·주문 데이터 흐름

### 핵심 책임

- `Stock`: 심볼, 이름, 현재가, 발행 주식 수, 실질/인지 가치, 주문장, 가격·체결·가치·위험 이벤트.
- `StockManager` 및 partial: 종목 등록/선택, 초기 경제, 대표 계좌, 상장/복원 연결.
- `OrderBook`: 실제 호가·매칭·부분체결·취소. 차트나 뉴스가 직접 체결을 만들어서는 안 된다.
- `Trader`: 계좌 현금·보유량·평균 원가·예약 자원·체결/취소 콜백.
- `ExchangeWarehouse`, `TradeBot/ExchangeBot`와 관련 partial: 초기 주식 공급 및 거래소 참여. 무한 재고/무한 현금으로 의도하지 않게 변하지 않는지 중요하다.
- `PriceTickPolicy`: 한국 주식식 호가 단위 및 표시. 1원 미만 주가·소수 원 주문 가격을 원하지 않는다.
- `StockStatistics`: 전일 종가, 당일 고저, 거래량·체결강도 등. 전일 대비 기준을 최근 봉이나 매수 평균가와 섞지 않는다.

### 반드시 지켜야 할 불변조건

- 계좌별 현금·보유량·예약 자금·예약 주식·체결 손익이 분리된다.
- 주문 가능 현금과 총 현금은 다르다. 매수 예약금은 아직 소유 자산이며 총자산에서 증발하지 않는다.
- 미체결 매도 수량도 체결 전에는 보유 자산이다. 미예약 수량만 추가 매도에 쓸 수 있다.
- 평가금 = 보유 수량 × 평가가격. 평가손익 = 평가금 − 잔존 취득원가. 전체 수익률은 전체 원가로 계산하며 종목별 수익률 단순 평균이 아니다.
- 실현손익을 평가손익/회사 수익/회원료와 혼합하지 않는다.
- 체결 대기 주문이 있어도 잔여 가용 자원이 있으면 새 주문을 낼 수 있어야 한다. 행별 취소와 일부체결 후 잔여 취소가 실제 원장에 반영돼야 한다.
- UI Refresh 함수는 주문이나 자산 상태 변경을 하지 않는다.
- 태블릿 개편 조사 당시 거래 엔진 수수료는 0이었다. UI만 임의 수수료를 추가하지 않는다. 이후 코드와 달라졌다면 실제 수수료 정의를 먼저 찾아야 한다.

### 두 계좌

`NightShift/PlayerAccounts.cs`의 `primary`, `automated`, 두 `DeviceOrderSession`을 사용한다.

- 1번 태블릿: 기본 계좌.
- 2번 태블릿: 별도 작업 계좌. 기본 계좌 `Equity() - TaxDebt(primary)`가 **100,000,000원 초과**일 때 해금. 정확히 1억 원은 잠김.
- `EvaluateUnlock()`는 해금 후 다시 잠그지 않는 현재 구현이다.
- 잠금 화면은 자물쇠 아이콘만. 불필요한 설명문 금지.
- `Available(account)`는 현금에서 예약금·해당 계좌 세금 채무·예약 실행할 매수비용을 제외한다. 중복 차감을 분석할 것.
- 계좌 이체는 돈을 생성하지 않는다. 기업 현금도 개인 계좌 현금과 별개다.

### 자동 매매

`TradingOperationController.Spread.cs`, `SpreadTradingUI.cs`.

- 단일 스프레드 슬라이더, 시작/취소, 실제 주문/체결/손익 표시. 현재 `MaxSpreadTicks=20`, 초기 `SpreadTicks=2`.
- 작업 계좌의 미예약 현금과 주식 범위에서 주문. 임의 5만원/일부 비율 한도를 만들어 사용자가 가진 돈을 못 쓰게 하지 않는다.
- 시작 중 중복 실행을 막고, 취소는 이 자동 매매가 소유한 양쪽 주문을 실제 취소하며 예약 자원을 반환한다.
- 돈이나 주식 한쪽이 없을 때의 대기·단측 주문·부족 상태를 추적한다. 없는 자원을 생성하지 않는다.
- 실행 경과와 최근 갱신/체결 시각은 게임시간이다.

## 5. 봇과 시장 균형 — 지금 가장 중요한 분석 대상

관련 디렉터리: `TradeBot/`, `Liquidity/`, `Institutions/`, `NightShift/`.

- 일반 전략: Fomo, Holder, Stupid, Random, Scalper, Book, SpreadFiller, Smart, Panic, Monitor, Whale, WhaleFollower 등.
- `Liquidity/InternalValueBot`: `realValue` 대비 기본 10% 괴리에 반응하는 내재가치 전략이 이미 존재한다.
- `TradeBot.GetReferencePrice` 기본은 `stock.fakeValue`이다. 오래된 주석에 RealPrice라고 쓰인 곳이 있어 주석만 믿으면 안 된다.
- `StockManager.Update`: `fakeValue`를 `realValue` 쪽으로 수렴시킨다. 뉴스·세력·기업 업데이트의 다른 쓰기 경로와 함께 추적한다.
- `MarketRhythmDirector`: 거래 활성도·체결 흐름·리듬과 심리/평단 등을 바탕으로 일부 주문을 유도하고 `BoundOrder`로 수량·가격을 제한한다. 전략 클래스만 분석하면 실제 주문 흐름을 놓친다.
- 기관/거래소/대표/사회 명령은 일반 봇 경로와 예외가 다르므로 각각 추적한다.

### 실제 사용자 보고: 일방향 폭등

노보손해보험(NM)이 500원 기준에서 16,130원(+3,126%) → 45,300원(+8,960%) → 51,100원으로 거의 계속 상승하는 캡처가 있었다. 다른 종목도 다수 상승했다. 이 현상을 정상 밸런스나 특정 세력 조작의 증거로 단정하지 않는다. 실제 주문 원인 로그가 필요하다.

코드 조사로 확인한 점:

1. `PanicBot.ThinkForStock`: 자신이 관찰한 고점 대비 기본 5% 하락부터 패닉 판단. 계속 오르면 작동하지 않는다. 보유량도 있어야 한다.
2. `FomoBot.ThinkForStock`: 상승 추격 분기에서 현금/수량이 부족하면 조기 반환하여 마지막 `TryTakeProfitSell`에 도달하지 않는 경로가 있다. **이번까지 이 조기 반환 문제를 수정하지 않았다.**
3. Fomo 차익실현은 기본 수익 5% 이상, 확률 40%, 보유량 30% 지정가 매도다. 가격·호가·예약 자원 때문에 실제 체결과는 다르다.
4. `WhaleFollowerBot.ExecutePanicSell`: 일부만 공격적으로 매도하며 나머지는 기준가 및 spread-safe 지정가를 낸다. 공포가 곧 즉시 시장가 매도라는 가정은 틀리다.
5. `GetEffectiveSellPrice`는 일부 저가 목표를 현재가 근처로 끌어올린다. `BoundOrder`의 현재가 근접 제한도 실제 매도 공격성과 가격 하락을 약화하는지 조사할 가치가 있다.
6. 폭등 직후 무조건 가격을 폭락시키는 이벤트는 해결책으로 승인되지 않았다. 차익실현→매수 잔량 약화→공포/매수 철회→연쇄 체결이 실제로 발생해야 한다.

### 최근 적용한 가치·라운드 넘버·증원 패치

- `SmartBot.GetReferencePrice`를 override하여 유효한 `realValue`를 사용하도록 했다. 기존 평단 목표 매매 로직 전체를 가치투자 AI로 재작성한 것은 아니다.
- `MarketRhythmDirector.TryAct`의 가치 괴리 계산도 SmartBot/InternalValueBot은 realValue, 나머지는 fakeValue를 참고하게 했다.
- `PriceTickPolicy.RoundRetailQuote(price,buy)` 추가. 예: 1,760 매도→1,800, 매수→1,700. 규모에 맞는 십진 단위와 유효 tick을 사용한다.
- `TradeBot.ApplyRoundQuotePreference`는 계좌 ID의 안정 해시로 약 45%를 선택. 일반 봇의 수동적 목표가 경로(`GetEffectiveBuy/SellPrice`)에 적용한다. 거래소/대표, 현재가를 넘겨 즉시 체결하려는 목표는 제외한다.
- 따라서 **모든 주문의 45%가 둥근 가격이라는 뜻은 아니다.** 이 helper를 통하지 않는 경로가 있고 후속 보정도 있다. 실제 주문장 밀집 비율은 아직 측정하지 않았다.
- SmartBot 60→120, PanicBot 40→60. C# 기본값과 TradeScene 직렬화 값을 변경했다. targetActiveBots 910→990. 씬의 minActiveBots=600, maxActiveBots=1250은 유지했다.
- 추격 매수 봇을 무작정 증원하지 않았다. 그래도 증원이 총 자본·재고 배분에 미치는 영향은 미검증이다.
- **초기 생성 수, 실제 active 수, 별도 유동성/기관 봇, 후발 풀, target/max 간 정합성을 반드시 조사할 것.** 목표 수가 초기 실제 수보다 작으면 유입/이탈 로직이 증원 의도를 상쇄할 수 있다.
- 검증: 전체 runtime/editor 컴파일 통과, 1~10,000원 가격에 대해 라운딩 방향·유효 호가 검사 통과. 장기 가격·CPU 균형은 검증하지 않았다.

### 우선 수집해야 할 시장 로그

봇 유형/ID/종목별 가용 현금·보유·예약 수량, 매수/매도 의도 수, 실제 제출/거절 이유, 취소, 공격적/대기 주문 구분, 실제 체결량과 대금, 시장가/기준가 괴리, 매수·매도 호가 깊이, 자금·재고 집중, 신규 자금 유입/반환 경로, 기사/관심도/리듬 전이. 시장 전체 주식·현금 보존식과 실제 외부 유입(사업 이익 등)을 구분한다.

## 6. 기업 가치·성장·인수·배당

핵심 파일: `NightShift/CompanyBusiness.cs`, `CompanyGrowth.cs`, `CorporateEconomy.cs`, `CorporateEconomy.Growth.cs`, `CorporateControlUI.cs`, `CompanyDashboardUI.cs`, `CompanyGrowthUI.cs`.

- `CompanyBusiness`: 독립 기업 현금/부채, 기준 매출, 수요, 생산성, 매출/비용/이익, 배당/투자, 마감 자료.
- 매출은 기준 매출 × 수요 × 생산성. 비용은 변동비·고정비·효율·부채 부담을 반영한다. 업종 주기·회사별 변동·뉴스 수요 효과가 있다.
- `CompanyGrowth.Revalue`: `floor(max(1, Capital + Contributions + Retained + (AverageProfit - ExpectedProfit) × earningsMultiple))`.
- `CorporateEconomy.ApplyValuation`: 기업 평가액/발행 수량을 주당 `realValue`에 반영. 비상장일 때만 평가가격/인지가치도 맞춘다. 상장된 실제 시장 가격을 이 함수로 강제 상승시키지 않는다.
- 마감 시총은 **종가 × 실제 발행량**. 기업 평가액과 같은 필드로 취급하지 않는다.
- 플레이어 기업은 비상장 상태로 시작하여 자기 기업을 키우는 방향으로 변경됐다. 과거 “게임 시작부터 PLAYER 상장” 설명과 구별한다.
- 기존 종목 소유주 WhaleBot을 대표 경영자로 재사용. 기업별 심리·현금·부채·프로젝트에 따라 자율 결정하며 플레이어가 지배한 회사 설정을 덮어쓰지 않아야 한다.
- 설비/영업/연구는 기업 현금에서 실제 비용을 한 번 차감하고 기간 후 효과를 한 번 적용한다. 출자는 개인 현금→기업 현금 이동이며 주식 원가도 보정한다.
- 배당은 게임적 허용으로 매일 마감 지급한다. 기업 매출 전체를 플레이어 개인 현금으로 주는 구조가 아니다. 이익, 정책, 회사 현금/부채, 실제 지분에 맞춰 지급한다.
- 인수 뉴스는 기존 기사/투자자 반응을 통해 영향을 준다. 호재라는 이유로 확정 상승을 보장하지 않는다.

`Assets/Resources/Progression/CompanyGrowth.json` 조사 시 기본값:

- 창업 10,000주, 평가액 20,000,000원; 프로젝트 기간 2일, 최대 레벨 5.
- 상장 조건: 평가액 ≥50,000,000 / 흑자 마감 누적 ≥3 / 프로젝트 완료 ≥2 / 미지급 기업 부채 0.
- earningsMultiple=20, publicIssueRatio=.25, projectCostRatio=.05, levelCostScale=.35.
- capacityGain=.25, salesGain=.05, researchSaving=.04, npcCashReserve=.25.
- 상장 신청 후 다음 개장에 조건/인수 자금을 다시 검사한다. 신주 수량은 기존 발행량의 25%; 전량 창업자 소유였다면 발행 후 지분 80%, 유통 20%. 인수 현금이 없으면 공모가 완료된 것처럼 처리하지 않는다.

과거 고정 회사 수입 250,000원은 `DailyIncome.json`에 남아 있다. 그러나 `DailyIncomeController.Settle`에서 FounderBusiness 기업 경영 경로이면 고정 수입은 0으로 넘겨 배당과 중복 지급하지 않는다. 오래된 daily-income 문서만 읽고 “현재 무조건 매일 25만원”이라고 설명하지 않는다.

## 7. 리딩방·심리·사회 채팅

### 명세와 의도

- 필수 문서: `docs/chat-social-spec.md`, `chat-social-decisions.md`, `chat-social-progress.md`.
- 0~8단계의 과거 개별 커밋과 검증 자료가 있다. 그 작업 당시 완료와 이후 변경의 검증은 구별한다.
- 사용자 초기 “플레이어 리딩방 1/2/3” 요청 이후 **플레이어 방은 하나만 개설**하도록 정정했다. 내부 다중 방 모델/NPC 방이 남을 수 있다. 내부 슬롯과 실제 개설·노출되는 플레이어 방 수를 구분한다.
- 최초 회원은 플레이어가 직접 초대한다. 아무도 초대하지 않고 입소문도 없는데 방 개설과 동시에 몰려오는 동작은 사용자가 거부했다.
- NPC 리딩방에서 시작→실제 매매 성과→내 리딩방 개설. `PlayerProgression.json`: 거래대금 100,000원 및 실현이익 5,000원 조건. 현재 밸런스 값이지 최종 검증된 난이도는 아니다.
- 이용료는 다음 장의 일일 요금으로 결정하고, 봇은 가용 자금·신뢰·배신·탐욕·공포·최근 순성과에 따라 거절/탈퇴할 수 있다. 실제 납부 금액은 예수금을 거쳐 마감 때 플레이어에 입금한다.

### 현재 책임 분리

- `InvestorSociety`, `InvestorPsychology`: 봇별 사회 상태, 기억, 심리.
- `ChatSocialPipeline/Speech/Voices/Language/Numbers`: 동기→화행→사실→말투, 수익률/금액 구어체, 언어별 표현.
- `ChatSocialThreads/Typing/TopicFocus`: 응답 스레드, 입력 지연, 주제 선택.
- `ChatSocialMemories/Emotions/Decisions/Promises`: 경험, 감정, 판단, 지시 관련 기억.
- `ChatSocialMediation/Moderation/Recruitment`: 중재·갈등·입퇴장·모집.
- `ChatSocialMarketRisk/MarketWatch`: 위험 반응, 급등락 관찰.
- `ChatSocialMentions`, `MarketDay/ChatStockLinkUI`: 닉네임/종목 강조 및 종목 선택 연결.
- `LeadingRoomOrders`: 기존 실제 주문 엔진과 조건부 명령을 연결. 명령을 수락했다고 즉시 체결하거나 무한 자금을 주지 않는다.
- `MarketDay/TabletGroupChatUI`, `ChatScrollInput`: 기존 메시지 풀·스크롤·최신 추적·UI.

### 사용자 확정 예외와 후속 규칙

- 신뢰도·배신감은 100 초과 가능, 기존 과열 맥동 유지. 확률·배율에 쓸 때는 min(value,100). 4분면 판정은 실제 값. 100 초과분에 임의 새 보상을 추가하지 않는다.
- 분노 0~100, 호감 -100~100, 탐욕·공포·경각심·위축 0~100.
- 최신 명세에서는 FOMO를 시간 제한 태그로 다루는 결정 D02가 있다. 초기 FOMO 0~100 설명을 무조건 복원하지 않는다.
- 실제 경험은 강하게, 목격한 소문은 제한적으로. 동일 사건 반복 노출 중복 방지. 부정 소문이 직접 손실과 똑같이 배신감을 올리지 않도록 명세 결정 확인.
- 신뢰와 배신은 동시에 높을 수 있다. 금융 경험 원장과 발화용 제한 기억은 다른 자료다.
- 진지함/비진지함 성향이 있고 비진지한 봇이 커뮤니티 말투를 자주 쓴다. 말투만 바꿔 실제로 없는 수익·체결·뉴스를 주장하면 안 된다.
- 한국 금액/손익은 맥락에 맞게 대략 말한다. 50%는 “50퍼/50%”. 현지 영어/일본어는 한국 욕설 직역이 아닌 자연스러운 현지 표현을 원한다.
- 싸움을 말리면 성공/반발/스트레스 퇴장/상대 후속 반응이 자율적으로 발생할 수 있다. 대본대로 반드시 끝내지 않는다.
- 입장/퇴장/강퇴는 일반 아바타 메시지가 아닌 가운데 한 줄 시스템 행.
- 주제는 엄격히 3개 제한이 아니라 최근 주요 세 주제를 선호하고 새로운 주제를 꺼내는 데 망설이는 소프트 제한. 급등·거래량 급증 종목의 관심과 채팅이 증가해야 한다.
- 입력 중은 한 명이면 닉네임, 여러 명이면 인원 수. 실제 예약/취소 상태와 일치해야 한다.
- 최신 메시지를 보는 중이면 자동 추적, 과거로 스크롤하면 고정, 가운데 원형 아래 화살표로 최신 복귀.

### 검증 해석 주의

과거 실제 시장 자율 관찰 `Validation/AutonomousChat/run-02`는 봇 1,191명, 체결 156,405건, 사회 발화 354건이었다. 지목 47건 중 응답 2건(4.26%), 언쟁/중재 0건으로 **자율 싸움·중재는 이 실행에서 실패**했다. 테스트로 조건을 만든 259개 검사 PASS와 다르다. 이후 주제/입력/말투 패치가 있지만 이 결과를 자동으로 해결된 것으로 바꾸지 않는다. 최신 장기 재측정 자료가 있는지 찾아야 한다.

## 8. 뉴스·기업에 대한 감정·작업 기능

- `News/NewsManager`, `NewsArticle`, `CompanyNewsAudience`, `CompanyOpinionBook`, `FakeNewsManager`, `NewsDeskUI`.
- 제목만 무작위로 바뀌는 알림이 아니라 배경·이해관계·사회 반응·확인 상태·관련 기업을 가진 기사 구조를 원한다.
- 봇의 기업 애착/확신/회의/수익 경험은 플레이어에 대한 신뢰/배신과 별개다. 팬덤 때문에 약한 악재를 다르게 해석할 수 있지만 영구 무적 신앙이 되어서는 안 된다.
- 실제 읽은 봇/목격한 봇만 영향, 같은 기사 중복 제한, 연결 기업 전파도 제한한다.
- 2번 태블릿 가짜 뉴스는 별도 하위 페이지. 미리보기는 시장에 영향이 없고 발행할 때만 기존 뉴스/관심/주문 경로에 연결.
- 플레이어 쿨다운은 **실제 600초**, 일시정지/오프라인 경과 반영. 화면은 게임시간 길이로 환산해 표시한다. 게임 시계 정지·수면과 실제시간이 다르므로 고정된 “게임 시각 몇 시 해금” 약속은 피한다.
- 현실 시각은 이 쿨다운의 내부 경과 기록 및 저장 메타데이터에 필요할 수 있다. 사용자에게 노출되는 주문/뉴스/차트 시각은 게임시간이다. `DateTime.UtcNow`를 저장용으로 썼다는 이유만으로 UI 시간 규칙 위반이라고 단정하지 않는다.

## 9. 게임시간·마감·세금·저장·수면

- `MarketDayController`: 09:00~17:00 거래. 과거 구현 보고에서는 이 8시간의 실제 길이 900초를 유지했다. 실행 씬 필드가 우선이다.
- `GameMinute`, `ExactGameMinute`, `AbsoluteGameMinute`, `Day`, `Phase`를 사용한다. 야간에는 GameMinute가 1440을 넘는 설계다. `CalendarDay/ClockMinute`와 구별한다.
- `SessionClock`: 세션 타이머용. 독립 UI 시계로 `Time.time`/PC 시각을 만들지 않는다.
- 마감 미체결 취소→자원/원장 검증→실적/배당/수입/세금→보고서/체크포인트 순서를 실제 함수에서 확인한다.
- 저장은 장 마감 보고서를 받는 시점. 중간 종료는 직전 마감 상태에서 시작한다. ESC의 이름만 “저장”으로 바꾸고 중간 저장을 허용하지 않는다.
- `Settings/SessionStore.cs`: schema 2, 명시적으로 등록한 시뮬레이션 루트를 reflection으로 직렬화한다. UI 계층을 저장하지 않는다. decimal/객체 공유/주문 참조/복원 구독 중요. MaxNodes=500000, MaxBytes=128MiB.
- 세션 검증 시 `DONDON_SETTINGS_OUTPUT`이 있으면 그 경로로 저장을 격리한다. 사용자 저장을 테스트로 덮어쓰지 않는다.
- 장기 아카이브 추가는 저장 노드/크기 한도를 증가시킬 수 있다. 최근 차트 추가 후 대용량 체크포인트를 재측정해야 한다.

세금과 구제:

- 당일 순실현 매매이익이 양수면 floor(이익×0.0005)원 과세, 즉 0.05%.
- 손실/본전이면 신규 매매세 0. 회사 수익·회원료·배당 등의 처리와 중복 과세 여부는 현행 원장에 맞춰 확인.
- 최초 손실일의 **당일 실현손실 전체를 한 번 보전**한다는 사용자 확정. 구제 사용 여부와 날짜는 복원 뒤에도 유지.
- 세금 부족분은 미납으로 보관. 예약 자원을 빼앗거나 현금 음수/강제 매도를 만들지 않는다.
- 이미 반영된 실현손익을 마감 때 다시 현금 입금하지 않는다.

취침/피로:

- `PlayerRestState`, `PlayerRestController`, `FatigueBlurFeature`.
- 취침 시 08:00 기상, 09:00 개장. 수면 1게임 시간당 피로 15%p 감소, 7시간이면 0까지 회복 가능.
- 사용자는 최대 3일 밤샘, 4일 이상 밤샘 시 사망/직전 마감 재시작을 요청했다. 실제 코드가 경과시간/연속일/수면 초기화를 어떻게 처리하는지 경계 테스트할 것.
- 흐릿함 최대 약 2픽셀 및 피로에 따른 모션 효과 요청. 금융 숫자를 못 읽는 과도한 블러는 원하지 않는다. 움직임 감소 옵션과 UI 선명도 등 사용자 경험도 분석할 것.
- ESC: 게임 정지, 실제 렌더 캡처의 블러 + 반투명 검정, 중앙 메뉴, 설정은 같은 배경의 상단 탭. 밝은 별도 설정 카드 배경은 사용자가 거부했다.

## 10. UI·입력·현지화의 확정 방향

### 화면과 입력

- 기존 세 모니터/두 태블릿/휴대폰의 모델·World Space 위치·회전·입력 카메라를 유지한다. 새로운 전체 화면 대시보드를 덧씌워 대체하지 않는다.
- 왼쪽 종목, 중앙 차트/호가, 오른쪽 채팅. 휴대폰/태블릿은 기존 보기와 상호작용을 재사용한다.
- `DeskViewController`, `DeskViewFraming`, `MouseCameraController`, `TabletViewController`, `DisplayAimCursor`가 입력·포커스 관련 핵심이다.
- Space: 차트 확대/확대 해제. 확대 중 A/D: 좌우 화면 이동. 1/2 태블릿은 토글. Tab은 휴대폰이며 예전 커서 토글 역할을 복원하지 않는다. P는 현재 안내에서 일시정지.
- W/S는 화면에 정렬된 목록 순서로 선택하며 처음/끝 순환, 중앙 모니터도 함께 변경.
- 정렬 탭은 거래량/상승률 오름·내림차순. 눌렀을 때 정렬하고 시세 갱신마다 재배열하지 않는다.
- 기본 카메라는 제자리 시점 회전. 확대 상태는 자유 커서, 태블릿을 볼 때 회전은 극히 약하게.
- 불필요한 상단 HUD 가이드/전화 수신 설명을 제거하라는 요청이 반복됐다. 기능 없는 큰 `[E] Show Tablet` 프롬프트를 다시 추가하지 않는다.
- 상호작용은 반투명 흰 원, 짧은 키 도움말은 왼쪽 아래. 진행 목표는 중앙 모니터 오른쪽 아래의 실제 포스트잇 3장. 포스트잇은 클릭 UI가 아니다. 뻣뻣한 Cloth와 목표 달성 시 낙하 요청이 있다.
- 중앙 뒤 벽 상단의 빨간 7세그먼트 시계는 HH:MM, 약한 주변광, 과한 Bloom 금지.

### 시각 스타일

- Dark Minimal, Flat UI, 차콜/청회색, 흰 솔리드 아이콘. 청록은 선택/주요 동작에 절제 사용.
- 숫자 우측 정렬, 고정된 열, tabular numerals, TMP SDF. 크기/명도로 위계를 만든다.
- 설명문·상태문·장식 카드·반복 테두리를 줄인다. Description/Button/Input이 같은 모습이 되지 않게 한다.
- 주요 동작 버튼 1개 우선. 매수/매도 segmented tab과 주문 유형은 별도. 가격/수량 필드 규격과 비율 버튼 간격을 통일한다.
- 주문 실패는 필요한 짧은 사유, 빈 화면은 짧은 빈 상태. 장문의 사용법을 임의 추가하지 않는다.
- UI 에셋은 supplied atlas 또는 실제 생성 PNG를 사용한다. 레퍼런스 화면을 배경 한 장으로 붙이지 않는다.
- 태블릿 키트에는 요청된 256 atlas/개별 PNG가 실제로 없어서 기존 48셀 128 atlas를 사용한 기록이 있다. `docs/tablet-kit-progress.md`와 실제 에셋을 확인한다.
- 주요 태블릿 UI: `TabletKitStyle`, `TabletKitLayout`, `InvestmentDashboard`, `InvestmentPositionRow`, `TradingOrderView`, `PendingOrderListUI`, `CompanyGrowthUI`, `SpreadTradingUI`, `NewsDeskUI`.

### 상승/하락 색상

- `Settings/MarketColors.cs`의 공통 팔레트와 GameSettings 저장값.
- 설정 UI에서 두 이미지 버튼만: 빨강/파랑/빨강 캔들, 초록/빨강/초록 캔들. 버튼 안 텍스트로 테마 설명하지 않는다.
- `Resources/UI/SystemMenu/CandleThemeRedBlue.png`, `CandleThemeGreenRed.png`는 생성한 PNG다.
- 수익/손실/시세에 적용하되 위험·오류·과열의 의미 색을 시장 팔레트로 뒤집지 않는다.
- ESC 배경은 열린 시점의 고정 캡처라 메뉴 중 뒤 배경에 보이는 차트가 즉시 바뀌지 않을 수 있다. 실제 씬 팔레트 업데이트와 캡처 이미지를 구분한다.

### 언어

- **ko/en/ja만 지원**. OS 언어 목록을 무작정 확장하지 않는다.
- Unity Localization String Table Collection `DonDon`, `Assets/Localization/Import/DonDon.csv`, `Assets/Resources/Localization/SourceIndex.json`.
- `GameLocale`, `GameLocalizedText`, `GameLocaleFonts`, `LocalizedChat`를 재사용. 짧고 자연스럽게 의역하여 UI 넘침 방지.
- 일반 TMP SDF와 일본어 fallback; 포스트잇 한/영 Nanum Pen Script, 일어 Yomogi. 폰트 원본/라이선스도 프로젝트에 포함하는 방향.
- 1600×900, 1920×1080의 실제 게임 카메라에서 ko/en/ja 가독성을 확인해야 한다. RectTransform 크기 검사만으로 완료라 하지 않는다.

## 11. 가장 최근 작업: 차트 과거 탐색과 시간봉

파일:

- 수정 `Assets/Scripts/CandleHistoryManager.cs`
- 수정 `Assets/Scripts/ChartManager.cs` (partial로 변경)
- 신규 `Assets/Scripts/ChartUI/ChartHistorySeries.cs`
- 신규 `Assets/Scripts/ChartUI/ChartManager.Navigation.cs`
- 신규 `Assets/Editor/ChartNavigationValidation.cs`
- 보고 `docs/chart-navigation.md`

실제 구현:

1. 봇/기존 통계용 완료봉 100개는 유지한다. 별도 chartHistory는 최대 8,192개 기본봉.
2. `OnMatched`에서 실제 체결을 게임시각 분별로 기록하는 minuteHistory를 추가했다. 최대 8,192개의 **거래가 있었던 분**이다. 모든 분을 채우는 방식이 아니다.
3. 시간 선택: 기본 / 1m / 5m / 15m / 1h / 1D. 5초 기본봉을 부정확하게 쪼개 1분봉을 만들지 않고 체결로 만든 분봉을 합산한다.
4. OHLCV는 첫 open, max high, min low, 마지막 close, 합계 volume/비거래소 volume. 초기 생성 시세는 게임 날짜가 없으므로 시간봉에서 제외한다.
5. 시작은 최신 추적. 과거로 드래그하면 왼쪽 봉의 timestamp/bucket key를 anchor로 고정한다. 새 봉 추가/앞 기록 제거 후에도 단순 인덱스 이동으로 밀리지 않게 했다.
6. 최신 구간까지 드래그하거나 `→` 버튼으로 복귀하면 추적 재개. 버튼은 과거 조회 중 표시. 시간봉 변경은 최신으로 복귀한다.
7. 중앙 차트 확대/안정화 상태에서만 버튼·드래그·십자선 동작. 다른 화면/태블릿/폰/텍스트 입력/ESC 중 차단. 일시정지 진입 시 pointer capture도 해제.
8. 차트 UI는 기존 overlay에 실제 Image/Button/TMP를 한 번 생성한다. 씬/모델/카메라 위치는 이 작업에서 변경하지 않았다.

정확한 검증 상태:

- 전체 C# 컴파일 PASS: `Validation/chart-navigation-compile.log`.
- `Validation/Settings/ChartNavigation01`: 10 checks PASS, 45.3초, 한 코어, peak 2,221MB.
- `Validation/Settings/ChartNavigation02`: 11 checks PASS, 46.0초, 한 코어, peak 1,956MB.
- OHLCV/거래소 제외 거래량/날짜 분리/상한, 비확대 입력 차단, 확대 상태, 표시 56개, append/eviction 시 anchor, latest 버튼, 실제 EventSystem World Space hit와 5m pointerClick 경로 검사.
- 1600×900/1920×1080 실제 scene camera PNG 확인. **캔들은 검증용 합성 체결 데이터**다. 시장 밸런스 증거가 아니다.
- OS 마우스로 실제 드래그하는 자동화는 미수행. 드래그 코드 연결 및 viewport 상태 검사와 구별한다.
- 새 history dictionaries는 SessionStore root의 필드로 저장될 구조이나 **새 기록 저장→재실행→복원 왕복은 아직 테스트하지 않았다**.
- 기존 저장에는 분봉 기록이 없으므로 로드 뒤 실제 체결부터 쌓인다. 이미 버린 과거 100개 밖의 봉을 복원했다고 주장하지 않는다.
- 과거 record가 capacity 밖으로 만료되면 가장 오래 남은 구간으로 clamp. 무한 히스토리가 아니다.
- 다음 분석: 기록량 증가에 따른 저장 크기/노드 수, 전일/야간/개장 경계, no-trade 분 처리 UX, paused raw timer/저장 후 timestamp 충돌, aggregate 선택 패널의 시작/완료 시각 표시, 실제 pointer drag와 버튼 충돌.

## 12. 테스트 실행과 증거 읽는 방법

### 컴파일 (Unity 실행 없음)

```powershell
Set-Location 'D:\GitHub\DonDon'
& .\Tests\ChatSocial\Compile.ps1
```

이 스크립트는 설치된 Unity Roslyn 및 기존 Library/Bee response file을 사용하고 새 C# 파일을 추가한다. 한 코어/BelowNormal/-parallel-로 runtime와 editor를 순차 컴파일한다. Library가 없거나 경로가 바뀌면 먼저 조사한다. 컴파일만 성공해도 Unity import/shader/scene 실행이 성공한 것은 아니다.

### 보호된 Unity 실행

```powershell
Get-Process Unity,DonDon -ErrorAction SilentlyContinue
& .\Tests\Settings\Run.ps1 -Job ChartNavigationReview -Method ChartNavigationValidation.Run -Seconds 180
```

- 기존 Unity/DonDon 프로세스가 있으면 실행을 거부한다. 이 제한을 삭제해서 병렬로 돌리지 않는다.
- 한 코어 affinity, BelowNormal, worker 1, import worker 1, 숨긴 창, 4GiB 및 시간 watchdog.
- 검사 구현에서 20FPS 등 추가 제한. 결과는 `Validation/Settings/<Job>/`의 unity.log/checks/result/resources 및 PNG.
- 시간/메모리 한도로 종료되면 FAIL/미완료다. 로그가 일부 PASS여도 전체 PASS라 하지 않는다.
- 기존 결과 디렉터리를 덮어쓰지 말고 새 Job 이름을 사용한다.

### 기존 테스트 묶음

`Tests/ChatSocial`, `CompanyGrowth`, `Corporate`, `DailyIncome`, `Localization`, `Settings`, `TabletKit`, `TerminalOrders`.

- `Tests/TerminalOrders` 독립 host는 과거 domain stub 불일치로 컴파일 실패한 기록이 있다(중복 StockRiskGrade/InvestorSociety.day, 누락 NewsArticle/CompanyBusiness/CompanyOpinionBook). 최신 상태를 확인하고, Unity 주문 검증 통과와 혼동하지 않는다.
- 과거 TabletKit 최종 7실행은 441 checks PASS였지만 매번 반복되는 아이콘 48개 검사 등이 포함돼 있다. 441개 독립 플레이 시나리오라고 쓰지 않는다.
- 기업21 + Corporate16 + DailyIncome271 = 308개의 과거 수치 검사 결과가 있다. 최신 변경 후 자동으로 회귀 보증되는 것은 아니다.
- 과거 시장/채팅 3일 검증 자료는 `docs/chat-social-results/` 및 `Validation`에 있다. 현재 증원/가치/라운딩/새 채팅 지연 후의 장기 결과와 별개다.

## 13. Claude에게 요청하는 정밀 분석 산출물

현재 요청의 목적은 분석이다. 코드 수정·실험용 자산 변경·씬 저장은 별도 필요성과 범위를 설명하고 사용자 지시에 맞춰 진행한다. 읽기 전용 조사와 보호된 검증을 우선한다.

### A. 구조와 실제 실행 경로

- 시작→초기 경제/봇 생성→NPC 리딩방→매매/원장→사회 반응→기업 성장→마감→저장→재실행을 실제 함수 호출/이벤트로 추적.
- 서로 비슷한 역할의 manager, 오래된 legacy 경로, runtime 생성 UI와 scene authored UI의 중복, OnEnable/restore 재구독 중복 여부.
- 하나의 stock/order/account 객체가 저장/복원 후 같은 객체를 가리키는지, UI가 복원 전 참조를 들고 있지 않은지.

### B. 버그/설계 결함 우선순위

각 항목에 경로:함수/라인, 실제 관찰 또는 가설, 트리거, 경제/UX 영향, 재현법, 최소 수정안, 회귀 시나리오를 붙인다.

- P0: 자산 증발/복제, 중복 정산·배당, 예약 자원 재사용, 저장 손상, 상장 발행량/현금 불일치, 심각한 runaway 가격 원인.
- P1: 전일 기준/시간봉/차트 탐색, 거래/취소 입력, 잠금 해금, 봇 가치 판단과 매도 경로, 자동 운용·뉴스 영향·명령 실제 체결 연결.
- P2: 가독성/버튼 피드백/과도한 문구/반복 채팅/번역 넘침/보조 표시.

### C. 독립 경제 검증

시장 정체와 폭등을 구별한다. 봉 개수·주문 수만 보지 말고 체결 수량/대금·자금/재고 집중·참여율·유동성·종목별 attention을 측정한다. 성공 기준을 먼저 제시하고 결과 때문에 기준을 넓히지 않는다. 강제 폭락·현금 refill·강제 매수로 그래프만 정상화하지 않는다.

### D. 발전 단계 완결성

8단계 성장별로 실제 사용 가능한 기능, 데이터만 있는 기능, UI만 있는 기능, 미구현을 구별한다. 특히 거래소 설립/경쟁/최종 권한/엔딩은 존재를 추측하지 않는다. 다음 구현은 처음부터 엔딩까지의 연결을 개선하는 작은 검증 가능한 단위로 제안한다.

### E. 성능

실제 active bot 수와 초기/목표/최대/풀 수 비교, 신규 자금 배분, frame spike, 사회 갱신 예산, 주문장 정렬/할당/취소, Canvas rebuild, TMP glyph 생성, chart archive GC/저장 크기, SessionStore peak memory를 측정한다. PC CPU 100%로 부하 테스트하지 않는다.

### F. 최종 보고

확인한 사실/의심/미확인을 명시하고, 재현 가능 버그부터 정리한다. 계획만 그럴듯하게 쓰지 말고 실제 코드 근거를 제시한다. 사용자에게는 핵심 결론과 다음 조치를 짧게, 상세 근거는 별도 섹션으로 제공한다.

## 14. 참고 문서 인덱스와 우선 충돌 목록

- 경제/기업: `docs/company-growth-changes.md`, `corporate-economy-changes.md`, `daily-income-changes.md`, `tax-clock-changes.md`.
- 주문/자동: `docs/terminal-orders-watch-changes.md`, `spread-trading-changes.md`, `trading-grid-changes.md`.
- 태블릿: `docs/tablet-kit-changes.md`, `tablet-kit-progress.md`, `tablet-kit-code.md`.
- 사회: `docs/chat-social-spec.md`, `chat-social-decisions.md`, `chat-social-progress.md`, `chat-voice-changes.md`, `chat-conversation-changes.md`, `autonomous-chat-observation.md`, `market-risk-room-changes.md`.
- 뉴스: `docs/news-society-changes.md`.
- 시스템: `docs/settings-night-implementation.md`, `settings-night-source.md`, `fatigue-blur-changes.md`, `localization-changes.md`, `supported-languages.md`.
- 시각: `docs/sticky-notes-changes.md`, `market-color-theme.md`, `market-color-cards-imagegen.md`.
- 최신 차트: `docs/chart-navigation.md`.

특히 다시 확인해야 할 충돌/미완료:

1. 구 개요의 Unity6000.3/URP17.3 ↔ 현재6000.5.5/URP17.5.
2. 과거 전체 저장 미구현 ↔ 현재 SessionStore schema2.
3. 고정 사업 수입 ↔ FounderBusiness일 때 고정 수입 차단/실제 기업 배당.
4. 신뢰·배신 0~100 명세 ↔ 사용자 승인 100초과 예외, 계산 clamp.
5. 3개 플레이어 방 초기 요청 ↔ 이후 단일 개설 요청과 NPC 방 공존.
6. 기본 한국 빨강/파랑 ↔ 현재 사용자 선택 가능한 시장 색상 테마.
7. 원래100개 차트기록 ↔ 새 chart/minute 아카이브; 봇용100개는 유지.
8. 과거 장기 시장 PASS ↔ 현재 사용자가 관찰한 +10,000%급 일방향 상승 및 최근 증원 이후 미검증.
9. 사회 단위검사 PASS ↔ 자율 시장에서 언쟁/중재0건인 실제 실패 로그.
10. “똑똑한 봇” 클래스 이름 ↔ 실제 가치 사용/평단/리듬/주문 보정의 복합 행동. 최신 SmartBot 연결은 시작점이지 완성형 가치평가 AI가 아니다.

문서와 코드가 다르면 최신 사용자 의도와 실제 실행 경로를 함께 제시하고 판단할 것. 오래된 보고서에 맞추기 위해 정상 구현을 되돌리지 않는다.
