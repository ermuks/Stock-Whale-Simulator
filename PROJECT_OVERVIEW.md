# DonDonProject - 프로젝트 분석 및 개요 문서

> **작성 목적**: 세션 전환 시 AI가 프로젝트 컨텍스트를 유지할 수 있도록 영구 문서화

---

## 1. 프로젝트 한 줄 요약

**3D 환경에서의 실시간 주식 거래 시뮬레이션 게임** — 플레이어가 태블릿/화면을 통해 여러 IT 종목을 거래하며, AI 봇·고래(세력)·뉴스·펀더멘털이 시장을 형성하는 Unity 프로젝트.

---

## 2. 기술 스택 및 환경

| 항목 | 값 |
|------|-----|
| **Unity 버전** | 6000.3.9f1 (Unity 6) |
| **렌더 파이프라인** | URP 17.3.0 |
| **입력 시스템** | Input System 1.18.0 |
| **UI** | uGUI 2.0.0, TextMesh Pro |
| **언어** | C# |
| **메인 씬** | `Assets/Scenes 1/TradeScene.unity` |

### 주요 패키지 (manifest.json)
- `com.unity.render-pipelines.universal` — URP
- `com.unity.inputsystem` — 새 Input System
- `com.unity.ugui` — UI
- `com.unity.ai.navigation` — AI 네비게이션
- `com.unity.timeline` — 타임라인
- `com.unity.visualscripting` — 비주얼 스크립팅

---

## 3. 프로젝트 구조

```
DonDonProject/
├── Assets/
│   ├── Scripts/                    # C# 스크립트
│   │   ├── TradeBot/               # 거래 봇 (11종 이상)
│   │   ├── ItCompany/              # IT 기업 펀더멘털
│   │   ├── OrderBookUI/            # 호가창 UI
│   │   ├── StockListUI/            # 종목 리스트 UI
│   │   ├── ChartUI/                # 차트 UI
│   │   ├── Hold/                   # 보유 종목 UI
│   │   ├── News/                   # 뉴스 UI
│   │   └── Interaction/            # 상호작용 (클릭, 레이캐스트)
│   ├── Scenes 1/
│   │   └── TradeScene.unity        # 메인 거래 씬
│   ├── Resources/                  # 동적 로드 리소스
│   │   ├── Modeling/               # FBX 모델 (손, 아이폰 등)
│   │   ├── StockListItem.prefab
│   │   ├── HoldListItem.prefab
│   │   ├── NewsListItem.prefab
│   │   ├── TradeHistoryItem.prefab
│   │   ├── BuyRowOrder.prefab
│   │   └── SellRowOrder.prefab
│   ├── Design/
│   │   └── FakeValueAndNews.md     # 가짜 가치·뉴스 설계 문서
│   ├── itCompany.js                # 기업 이벤트/노드 데이터 (JSON)
│   ├── itCompany_description_korean_*.txt
│   ├── Models/                     # 3D 모델
│   ├── CandleData.asset
│   ├── News.asset
│   ├── StockStatistics.asset
│   └── TabletUI.asset
├── Packages/
│   └── manifest.json
├── ProjectSettings/
└── Library/
```

---

## 4. 핵심 스크립트 및 역할

### 4.1 데이터/거래 핵심

| 스크립트 | 역할 |
|----------|------|
| `Stock.cs` | 종목 데이터(symbol, currentPrice, issuedShares, realValue, fakeValue), OrderBook, OnPriceChanged/OnTradeExecuted/OnRealOrFakeValueChanged 이벤트 |
| `StockManager.cs` | 종목 목록 관리, 상장폐지, CurrentStock 선택, StockDefinition으로 Inspector에서 종목 추가 |
| `Order.cs` | 단일 주문 데이터 |
| `OrderBook.cs` | 호가창, 매칭, 주문 취소/정리 |
| `Trader.cs` | 기본 트레이더(현금·보유량, 지정가 매수/매도, 예치금) |
| `PriceTickPolicy.cs` | 호가 단위(tick), 가격 반올림, 포맷 |
| `IOrderOwner` | 주문 소유자 인터페이스(체결/취소 콜백) |

### 4.2 거래소/창고

| 스크립트 | 역할 |
|----------|------|
| `ExchangeWarehouse.cs` | 거래소 창고, 전 종목 발행 수 보유, 매도 공급 |
| `ExchangeBot.cs` | 거래소 봇, 초기 매도 공급 |
| `ExchangeStockBot.cs` | 거래소 주식 봇 |
| `ExchangeStabilizerBot.cs` | 거래소 시장 안정화 봇 |
| `DelistingManager.cs` | 상장폐지 처리 |

### 4.3 트레이딩 봇 (TradeBot 계층)

| 스크립트 | 대략 인원 | 역할 |
|----------|-----------|------|
| `TradeBot.cs` | - | 봇 추상 기본 클래스, Think 루프, 지정가/지연 주문 |
| `TradeBotManager.cs` | - | 봇 생성·유입/이탈·자금 주입, 고래 추종자 풀 |
| `FomoBot.cs` | ~60 | 상승 추격 |
| `HolderBot.cs` | ~50 | 장기 보유 |
| `StupidBot.cs` | ~100 | 충동 매수 |
| `RandomBot.cs` | ~400 | 랜덤 매매, 유동성 |
| `ScalperBot.cs` | ~90 | 초단타 |
| `BookBot.cs` | ~60 | 호가 채움 |
| `SpreadFillerBot.cs` | ~30 | 스프레드 채움 |
| `SmartBot.cs` | ~60 | 평균단가 기반 매매 |
| `PanicBot.cs` | ~40 | 하락 시 패닉 매도 |
| `MonitorBot.cs` | ~20 | 급등락 대응 |
| `WhaleBot.cs` | 1 | 세력, 펌프/덤프 |
| `WhaleFollowerBot.cs` | 260 풀 | 고래 추종자 |

**목표 인구 규모**: 600~1,250명 유지

### 4.4 차트/캔들

| 스크립트 | 역할 |
|----------|------|
| `CandleData.cs` | OHLC 캔들 데이터 |
| `CandleHistoryManager.cs` | 캔들 이력 관리 |
| `CandleDrawManager.cs` | 캔들 메시/렌더링 |
| `ChartManager.cs` | 차트 UI, 드래그 스크롤, 십자선, 통계 |
| `ChartCandleSelectData.cs` | 선택된 캔들 정보 |

### 4.5 펀더멘털/뉴스/심리

| 스크립트 | 역할 |
|----------|------|
| `ItCompanyManager.cs` | itCompany.js 로드, 노드 그래프, realValue 갱신 |
| `ItCompanyData.cs` | itCompany JSON 구조 |
| `StockNewsAttentionManager.cs` | 뉴스 기반 종목 관심도 |
| `MarketSentiment.cs` | 시장 심리 |
| `PsychologicalLevelManager.cs` | 심리적 저항선, 돌파 이벤트 |
| `StockInterestManager.cs` | 종목별 관심도, 봇 종목 선택 가중치 |

### 4.6 UI

| 스크립트 | 역할 |
|----------|------|
| `StockListUIManager.cs` / `StockListItem.cs` | 종목 리스트 UI |
| `OrderBookUIManager.cs` / `DrawRawOrder.cs` | 호가창 UI |
| `HoldUIManager.cs` / `HoldListItem.cs` | 보유 종목 UI |
| `TradeHistoryItem.cs` | 최근 체결 내역 |
| `NewsManager.cs` / `NewsListItem.cs` | 뉴스 표시 |
| `TabletViewController.cs` | 태블릿 클릭→카메라 고정, ESC로 해제 |

### 4.7 입력/카메라/상호작용

| 스크립트 | 역할 |
|----------|------|
| `MouseCameraController.cs` | 마우스 에임, 커서 잠금/해제 |
| `GameKeyBindings.cs` | 키 바인딩 |
| `DisplayAimCursor.cs` | 에임 커서 표시 |
| `InteractionManager.cs` | 상호작용 관리 |
| `Interactable.cs` | 상호작용 대상 인터페이스 |

### 4.8 유틸리티

| 스크립트 | 역할 |
|----------|------|
| `StockStatistics.cs` | 52주 고/저, 당일 고/저, 체결강도, 최근 체결 |
| `StockIntegrityChecker.cs` | 호가/보유량 무결성 검사 |

---

## 5. 기본 종목 (StockDefinition)

Inspector에서 비어 있으면 아래 10종목 기본 사용:

| 심볼 | 이름 | 가격대 | 발행 수 |
|------|------|--------|---------|
| ALPH | Alphabet N | 100k~140k | 250,000 |
| PSLA | Pesla | 18k~26k | 2.5M |
| NETA | Neta | 3.2k~9.7k | 250k |
| MICR | MicroSoft | 1.2k~4.5k | 200k |
| APLE | Apole | 150~220 | 25M |
| AMZN | Amazom | 130~190 | 25M |
| NVDA | Nvidia | 280~350 | 25M |
| META | Metaverse | 160~240 | 25M |
| GOOG | Googol | 110~155 | 25M |
| TSMC | TSMC | 95~135 | 25M |

**밸런스**: 시총 합 ~55B, 호가 단위 1원(100~999구간), 고래 20%+ 봇 현금으로 유동성 확보.

---

## 6. 핵심 개념

### 6.1 주식 거래 시뮬레이션
- 10종목 상장
- 지정가 매수/매도, 시장가 대체(공격적 지정가)
- 발행 주식 수, 거래소 창고, 고래·봇 보유량으로 시장 규모 설계

### 6.2 realValue vs fakeValue
- **realValue**: 펀더멘털(itCompany)에서 갱신되는 "진짜 가치"
- **fakeValue**: 세력 조작·itCompany 등에서 갱신되는 "가짜 가치"
- UI는 `OnRealOrFakeValueChanged` 구독

### 6.3 펀더멘털 (itCompany.js)
- 노드 그래프: 상장 → 뉴스 → 가치 변화
- 노드별 `criticalValueMin/Max`, `stayTimeRangeMin/Max`, `next`, `nextWeight`
- `realValue` 갱신에 사용

### 6.4 세력(Whale) & 가짜 뉴스
- `FakeValueAndNews.md` 설계: 세력이 fake value 조작 → **N초 경과 후** 가짜 뉴스 발행 가능
- 조작을 먼저 하고, 뉴스로 나중에 정당화하는 구조

### 6.5 시장 메커니즘
- 심리적 저항선, 돌파, 매도벽
- 뉴스 기반 종목 관심도
- 상장폐지, 호가 정리, 무결성 검사

### 6.6 3D/태블릿
- 태블릿 클릭 → 카메라 앞 고정, ESC 해제
- 차트 드래그, 캔들 선택, 십자선
- 마우스 에임/커서 잠금

---

## 7. Stock.ExecuteTrade 주요 파라미터

```csharp
TradeResult ExecuteTrade(
    decimal price, decimal volume, bool isBuy,
    bool addUnfilledToBook = true,    // false → 시장가 (미체결 분 호가 등록 안 함)
    int subTickSteps = 0,
    IOrderOwner orderOwner = null,
    decimal? maxMatchAskPrice = null   // 매수 시 고가 매도 소진 방지
);
```

---

## 8. 설계 문서

| 경로 | 설명 |
|------|------|
| `Assets/Design/FakeValueAndNews.md` | 세력이 fake value 조작 후 N초 경과 시점부터 가짜 뉴스 발행 가능, itCompany 노드 description을 뉴스로 활용 |

---

## 9. 참고 사항 (세션 복원용)

- **메인 씬**: `Assets/Scenes 1/TradeScene.unity` (EditorBuildSettings에는 SampleScene 등록되어 있을 수 있음)
- **데이터 파일**: `itCompany.js` — 상장사 이벤트 노드 그래프, `criticalValueMin/Max` 등
- **싱글톤/관리자**: `StockManager.Instance`, `TradeBotManager` 등
- **이벤트 구독**: `Stock.OnPriceChanged`, `OnTradeExecuted`, `OnRealOrFakeValueChanged` 등을 UI/차트가 구독

---

*이 문서는 DonDonProject 프로젝트의 개요로, 세션 전환 시 빠른 컨텍스트 복원을 위해 작성되었습니다.*
