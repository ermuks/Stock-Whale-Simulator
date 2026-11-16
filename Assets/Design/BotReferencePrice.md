# 봇의 FakePrice vs CurrentPrice 기준 정리

> 봇들이 매매 판단 시 **어떤 가격을 기준(ReferencePrice)**으로 쓰는지 정리.

---

## 1. 기준가(GetReferencePrice) 정의

- **RealPrice** = `stock.realValue` (펀더멘털/itCompany에서 갱신되는 “진짜 가치”)
- **CurrentPrice** = `stock.currentPrice` (최근 체결가 = 시장가)

`TradeBot.GetReferencePrice(Stock)` 이 **기준가**를 반환한다.  
각 봇은 이 기준가로 “목표 매수가/매도가”를 계산하고, 필요 시 `GetEffectiveBuyPrice` / `GetEffectiveSellPrice`로 **현재가 쪽으로 1틱 당겨서** 실제 호가를 낸다.

---

## 2. 봇별 기준가 (Fake vs Current 비율)

| 봇 | 기준가 | 비고 |
|----|--------|------|
| **FomoBot** | **100% Fake** | 상승 추격, fakeValue 기준으로 저평가/고평가 판단 |
| **HolderBot** | **100% Fake** | 장기 보유, fake 대비 고/저평가로 매매 |
| **RandomBot** | **100% Fake** | fake 기준 랜덤 오프셋으로 호가 |
| **ScalperBot** | **100% Fake** | fake 기준 스프레드/마켓메이킹 |
| **BookBot** | **100% Fake** | fake 기준 호가 채움 |
| **SmartBot** | **100% Fake** | fake 대비 할인가 매수 등 |
| **PanicBot** | **100% Fake** | fake 기준 패닉/반등 매매 |
| **MonitorBot** | **100% Fake** | fake 기준 급등락 대응 |
| **WhaleFollowerBot** | **100% Fake** | fake 기준 FOMO 매수/익절/패닉 |
| **ExchangeBot** | **100% Fake** | fake 기준 초기 공급/개입 |
| **ExchangeStabilizerBot** | **Fake = fair** | current vs fakeValue 괴리로 유동성 공급 |
| **StupidBot** | **100% Current** | `GetReferencePrice` 오버라이드 → `currentPrice` |
| **SpreadFillerBot** | **100% Current** | 스프레드 갭 채우기용으로 현재가 기준 |
| **ExchangeStockBot** | **Current 우선** | `currentPrice > 0` 이면 current, 아니면 fakeValue |

즉, **비율로 보면**:

- **대부분 봇**: 기준가 = **100% FakePrice** (fakeValue, 초당 realValue 쪽으로 20% 수렴)
- **StupidBot, SpreadFillerBot**: 기준가 = **100% CurrentPrice**
- **ExchangeStockBot**: **Current 우선**, 없을 때만 Fake

인원 수 기준으로는 (TradeBotManager 기본값):

- Fake 100%: Fomo 60 + Holder 50 + Random 400 + Scalper 90 + Book 60 + Smart 60 + Panic 40 + Monitor 20 + WhaleFollower 260 = **1,030명**
- Current 100%: Stupid 100 + SpreadFiller 30 = **130명**
- → **약 89%는 FakePrice, 약 11%는 CurrentPrice** 를 기준가로 사용 (인원 기준).

---

## 3. FakeValue 수렴 (StockManager)

- **fakeValueReversionPerSecond** (기본 0.2 = 20%): 매 초 `fakeValue += (realValue - fakeValue) * 0.2 * deltaTime`
- 세력이 펌프로 fakeValue를 올려도, 조작이 없으면 fakeValue가 서서히 realValue 쪽으로 돌아감.

## 4. 실제 주문가 보정 (EffectiveBuyPrice / EffectiveSellPrice)

기준가만 쓰면 **현재가와 너무 멀어진 호가**가 나갈 수 있어서, 베이스 `TradeBot`에서 다음 보정을 한다.

- **GetEffectiveSellPrice(refBasedSellPrice)**  
  - Real(또는 Current)로 계산한 매도가와 **현재가 1틱 아래** 중 **더 높은 쪽** 사용  
  - → 현재가가 Real보다 높을 때, 시장에 매도 호가가 나가도록 Current가 간접 반영됨.
- **GetEffectiveBuyPrice(refBasedBuyPrice)**  
  - Real(또는 Current)로 계산한 매수가와 **현재가 1틱 위** 중 **더 낮은 쪽** 사용  
  - → 현재가가 Real보다 낮을 때, 시장에 매수 호가가 나가도록 Current가 간접 반영됨.

그래서 **“기준가”는 100% Real 또는 100% Current**이지만, **실제 호가**는 “현재가에서 너무 멀지 않게” **Current가 보조적으로 섞인다**.

---

## 5. 요약

| 구분 | 비율 (인원 기준) | 비율 (의미) |
|------|------------------|-------------|
| **기준가 = FakePrice** | ~89% (1,030명) | 매매 판단·목표가에 fakeValue 사용. fakeValue는 초당 20%씩 realValue로 수렴 |
| **기준가 = CurrentPrice** | ~11% (130명) | 매매 판단에 currentPrice 사용 |
| **실제 호가** | - | 기준가 + (현재가 1틱 근처로 당기는 보정) → Current가 간접 반영 |

**정리**: 봇들은 **Real과 Current를 “비율로 섞어서” 쓰는 게 아니라**,  
- **기준가**는 봇 타입별로 **100% Real** 또는 **100% Current** 중 하나이고,  
- **실제 주문가**는 그 기준가에 더해 **현재가에서 너무 멀어지지 않도록** Current가 보조적으로 반영된다.
