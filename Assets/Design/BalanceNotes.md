# 시장·봇 밸런스 정리

> 현금 유동성, 물량 유동성, 보유 가치, 봇 수량을 한곳에서 정리. 수치 변경 시 이 문서와 TradeBotManager 주석을 함께 갱신할 것.

---

## 1. 시장 규모 (StockManager 기본값)

| 항목 | 값 |
|------|-----|
| 종목 수 | 10 |
| 종목당 발행 주식 | 1천만 주 (10_000_000) |
| 시가 범위 | 3,200~6,800원 (종목별 상이) |
| 평균 시가 | ~5,000원 |
| **종목당 시총** | **~50B** (발행 × 시가) |
| **전체 시총** | **~500B** |
| 호가 단위 (1,000~9,995원) | 5원 (PriceTickPolicy) |

---

## 2. 현금 유동성

| 구분 | 금액 | 비고 |
|------|------|------|
| 일반 봇 초기 현금 합계 | ~14.4B | GetBotInitialCash × 봇 수 (910명), 실제는 봇별 Start() 랜덤 범위 |
| 거래소 봇(ExchangeBot) | 25B | 급락 방어·매수 지원용 |
| **총 유동 현금** | **~39.4B** | 시총 500B 대비 ~8% |

- 봇 이탈 시 현금은 시장에 남지 않음 (보유 매도 후 비활성화).
- 유입/이탈·캐시 인젝션: `TradeBotManager` PopulationFlowLoop, ProcessCashInjection.

---

## 3. 물량 유동성 (발행량 = 창고 + 봇 보유 + 호가창 매도)

| 구분 | 비율/값 | 비고 |
|------|---------|------|
| 거래소 창고 초기 | 100% 발행량 | InitializeFromStocks |
| 고래 이전 | 20% (whaleInitialShareRatio) | 창고 → 고래, 종목당 |
| 거래소 잔량 | 80% | 고래 이전 후 기준 |
| 거래소 초기 매도 | 10~15% (initialSellRatioMin/Max) | 호가창에 매도 주문으로 공급 |
| 랜덤봇 초기 배분 | 종목당 최대 10% (capTotal) | 40% 확률로 선정된 봇에게 0.2%~0.5%씩 |

**무결성 검사**: `StockIntegrityChecker.CheckStock()`  
- 발행량 = 창고 보유 + 모든 Trader 보유 + 호가창 매도 물량 (tolerance 내).

---

## 4. 보유 가치 (시총·고래·거래소)

| 구분 | 기준 | 상당 금액 |
|------|------|-----------|
| 전체 시총 | Σ(종목 시가 × 발행량) | ~500B |
| 고래 보유 가치 | 시총의 20% | ~100B |
| 거래소 보유 가치 (고래 이전 후) | 시총의 80% | ~400B |
| 거래소 초기 매도 공급분 | 80%의 10~15% | ~40~60B 상당 호가창 |

---

## 5. 봇 수량·초기 현금 (910명 + 고래 1 + 거래소 1)

| 봇 | 수 | 봇당 초기 현금(의도) | 소계 | 역할 |
|----|-----|---------------------|------|------|
| FomoBot | 60 | 24M | 1.44B | 상승 추격 |
| HolderBot | 50 | 40M | 2.00B | 장기 보유 |
| StupidBot | 100 | 12M | 1.20B | 충동 매수 |
| RandomBot | 400 | 16M | 6.40B | 유동성 |
| ScalperBot | 90 | 8M | 0.72B | 초단타 |
| BookBot | 60 | 12M | 0.72B | 호가 채움 |
| SpreadFillerBot | 30 | 10M | 0.30B | 스프레드 |
| SmartBot | 60 | 16M | 0.96B | 평단 기반 |
| PanicBot | 40 | 16M | 0.64B | 패닉셀 |
| MonitorBot | 20 | 20M | 0.40B | 급등락 대응 |
| **일반 합계** | **910** | — | **~14.38B** | — |
| WhaleBot | 1 | 0 | — | 20% 보유 |
| ExchangeBot | 1 | 25B | 25B | 창고 80%, 급락 방어 |

- 실제 봇 현금은 각 봇 `Start()`에서 랜덤 범위로 설정되며, 위 “봇당 초기 현금”은 **GetBotInitialCash** 기준(밸런스·캐시 인젝션·이탈 판정용).
- 팔로워 풀: 260명 (고래 펌프 시 투입, 별도 현금 범위).

---

## 6. 인구 유동·캐시 인젝션

- 목표 봇 수: 910 (최소 600, 최대 1250).
- 이탈: 손실 시·수익 시·보통 시 각각 leaveChance; GetBotEquity vs GetBotInitialCash로 판정.
- 복귀: returnChance, 목표 미달 시 가중.
- 신규: minNewBotsPerCheck ~ maxNewBotsPerCheck, SpawnRandomNewBot 타입 비율.
- 캐시 인젝션: 현금이 초기의 20% 미만이면 50~100% 보충; 5% 확률 보너스.

---

## 7. 수치 변경 시 체크리스트

- [ ] StockManager 기본 stockDefinitions (발행량·시가 범위) → 시총 500B 수준 유지 여부
- [ ] TradeBotManager GetBotInitialCash 합계 (~14.4B) 및 봇 수 합계 (910)
- [ ] ExchangeBot.initialCash (25B), initialSellRatioMin/Max (10~15%)
- [ ] whaleInitialShareRatio (0.2)
- [ ] DistributeInitialSharesToRandomBots: 종목당 10% 상한, 40% 확률
- [ ] StockIntegrityChecker.checkInterval, tolerance (무결성 검사)
