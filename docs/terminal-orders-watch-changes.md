# 일간 미니 캔들 · 복수 주문 · 관심주 반응 · 화면 정렬

2026-11-15 작업 기록. 거래 엔진의 가격·체결·원가 계산은 기존 `Trader` / `OrderBook`을 사용한다.

## 적용 내용

- 종목 목록 미니 캔들: 최근 6개 단기 봉 묶음에서 당일 시가·고가·저가·현재가로 변경. 시가는 개장 시 고정하고 다음 개장에만 초기화한다. 전일 대비 등락률과 최근 봉 거래량·거래대금의 기존 정의는 유지한다.
- 주문 화면: 미체결이 있어도 새 종목·가격·방향으로 추가 주문 가능. 남은 현금·주식에서 미체결과 아직 제출되지 않은 분할 예약을 제외한다. 첫 회차는 접수 즉시 실제 엔진에 제출해 연속 클릭의 중복 사용을 방지한다.
- 태블릿 왼쪽 아래: 주문별 종목/방향/가격/대기 수량/체결 수량/분할 예약과 취소 버튼. 목록 내부 드래그·휠 스크롤, 개별 취소 및 전체 취소. 행은 재사용한다. 기존 자동 운용의 실제 호가 주문도 같은 계좌이면 표시한다. 분할 주문은 하나의 요청으로 묶어 표시하며 취소 시 그 요청의 미체결 회차와 앞으로 제출할 회차를 함께 중단한다.
- 관심주 반응: 각 봇이 마지막으로 실제 확인한 가격에 비해 30% 이상 상승·하락하면 기존 사회 채팅 파이프라인에 REACT 의도를 넣는다. 전문 종목 또는 보유량/기존 선호를 반영한 관심 종목을 확인한다. 수익 경험을 만들어내거나 모든 봇에게 동시에 메시지를 전달하지 않는다.
- 측면 모니터: 종목 목록 카메라 월드 Y=-20°, 채팅 Y=+5.7°. 기존 카메라 이동 위치·높이·FOV·커서·Space/A/D 전환은 유지한다. 화면 비율 변경 때도 지정 Y를 다시 적용한다.
- 중앙 헤더: 종목명→작은 티커→현재가, 전일 대비 두 줄, 보조 통계 두 열로 정리. 장부 가치/시장 평가 및 거래량/거래대금의 제목·값 기준선과 오른쪽 여백을 통일한다. 기존 TMP SDF 폰트·데이터 연결을 유지한다.

## 함수 단위 전후 비교

### StockStatistics.cs / StockListItem.cs

- `RegisterStock`, `StockStats` 생성자: 기존 고가/저가 초기화에 `dayOpen`을 추가했다.
- `BeginTradingDay`: 고가/저가만 초기화하던 곳에서 당일 시가도 함께 확정한다.
- `OnMatchedPrice` 신규: 한 주문이 여러 가격대를 체결할 때 마지막 체결가만으로 고가/저가를 계산하지 않도록 개별 체결을 반영한다.
- `TryGetDailyCandle` 신규: 등록된 종목의 당일 OHLC를 반환한다. 자료가 준비되지 않았으면 false를 반환한다.
- `StockListItem.Set`: `AggregateCandleRange`로 최근 6봉을 묶던 부분을 당일 통계 조회로 교체했다. 캔들 몸통·꼬리 렌더러는 그대로 사용한다.

### DeviceOrderSession.cs

- 기존: `Target`, `pending`, `submitting` 하나로 한 주문만 추적했다. `Active`이면 `Prepare`/`Validate`에서 추가 주문을 거절했다.
- 변경: `Request`마다 종목·방향·수량·분할 일정·실제 Order 참조·체결량을 가진다. `Latest`는 최근 주문 표시용이며 다른 요청을 덮어쓰지 않는다.
- `Prepare`/`Validate`: 입력 종목 변경은 기존 주문을 건드리지 않는다. 진행 중 여부 대신 남은 현금/수량과 실제 시장 상태를 검사한다.
- `Submit`/`SendSlice`/`Update`: 요청별 회차를 제출하고 0.25초 주기로 일정과 체결 대기를 정리한다. 미래 회차 예약금은 실제 엔진 예약금에 중복 합산하지 않는다.
- `OnFill`: 실제 resting Order 참조 또는 현재 제출 중인 Request로 체결을 귀속한다. 이전 요청도 새 주문 제출 이후 계속 추적한다.
- `Cancel(Request)`/`CancelPending`: 선택한 요청·주문만 취소한다. 체결분은 유지하며 기존 엔진이 남은 현금/주식을 반환한다. 이미 종료된 주문과 다른 계좌 주문의 취소는 실패한다. 취소 직전 전량 체결됐으면 완료 상태로 정리하며 취소로 보고하지 않는다.
- `CopyPendingOrders`: 수동 요청에 속하지 않은 자동·기존 계좌 주문도 목록으로 제공한다.

### DeviceTerminalUI.cs / PendingOrderListUI.cs

- `ConfigureQuickOrder`: 포지션/현금을 간결하게 배치하고 기존 Canvas 안에 대기 목록을 만든다.
- `CurrentPrice`, `SetQuantity`, `LoadSignalDraft`, `Update`: 미체결 주문 때문에 입력·종목 전환을 막던 조건을 제거했다.
- `Order`: '체결 대기 중'으로 제출 버튼을 잠그던 분기를 제거했다. 현재 입력의 유효성으로 제출 여부를 판단하고, 대기 목록과 새 주문 예상 결과를 함께 표시한다.
- `PendingOrderListUI.Configure`/`Refresh`/`CreateRow`: 기존 TMP 템플릿과 폰트를 재사용하고 RectMask2D/ScrollRect 안에서 행을 풀링한다. 전체 레이아웃 강제 갱신은 사용하지 않는다.
- `LeadingRoomUI.NpcDetails`: 다른 미체결 주문이 있어도 리딩방 신호를 새 주문 초안으로 불러올 수 있다.
- `IncumbentExchangeAI.Observe`: 단일 최근 주문 대신 양쪽 계좌의 모든 진행 요청을 관찰한다. 미제출 회차의 기존 압력 계산도 요청별로 합산한다.

### 관심주 채팅

- `ChatSocialMarketWatch.TickMarketWatch`/`ObserveWatchedPrice`: 기존 분산 봇 갱신 안에서 관측하며 각 봇의 비교 가격과 시점을 보관한다. 첫 관측·종목 변경·날짜 변경·오래된 관측은 기준만 갱신한다.
- `ChatSocialUnity.ChoosePersonalWatch`: 전문 종목 우선, 그 외에는 보유 종목과 기존 선호를 가중해 선택한다. 거래용 RNG와 별개의 기존 사회 RNG를 사용한다.
- `ChatSocialPipeline.GenerateSocialMessage`: 실제 관측 당시 가격과 변화율을 발언 시점까지 유지한다. 새 문자열 필드는 Unity JsonUtility로 로그에 남길 수 있다.
- `chat_templates.json`: 상승/하락, 격식/일반/거친 말투의 36개 문구를 강도별로 등록한 108개 조건부 템플릿. 기존 방 발언 상한·개인 발언 간격·템플릿 반복 제한을 그대로 거친다.
- `chat_social_config.json`: `watchMovePercent=30`, `watchObservationMinMinutes=3`, `watchObservationMaxMinutes=8`, `watchMaximumGapMinutes=30`, `watchReactionCooldownMinutes=12`, `watchRetargetMinutes=30`. 시간은 게임 시간 기준이며 배속 기능은 추가하지 않는다.

### 카메라 / 헤더

- `DeskViewController.TryMonitorPose` 신규: 기존 정면 포즈 계산 후 Stocks/Chat의 Y만 지정값으로 바꾼다. `Focus`, 화면 비율 변경 처리에서 같은 함수를 사용한다.
- `ChartManager.ConfigureHeaderLayout`/`PlaceHeaderText`/`PlaceHeaderCaption` 신규: 시작 때 기존 TMP만 배치한다. 매 프레임 배치 계산이나 씬 파일 재작성은 하지 않는다.

## 검증 결과

- `Tests/TerminalOrders/Run.ps1`: 실제 Stock/Trader/OrderBook/DeviceOrderSession/StockStatistics 소스 사용, Unity 생명주기만 경량 호스트로 대체. 33개 통과. 복수 종목·같은 가격 FIFO·즉시/부분 체결·개별 취소·중복 취소·계좌 격리·자동 주문 표시·매수/매도 분할 예약·마감 취소·일간 OHLC 검증.
- `Tests/ChatSocial/Run.ps1 -Stage 8`: 기존 180개와 새 관심주 반응 13개, 총 193개 통과. 기존 R 검증 범위는 변경하지 않았다.
- 마지막 관측 사실을 Unity 직렬화 가능한 문자열로 조정하고 관측자 ID 검사를 추가한 뒤 관심주 14개 검사를 통과했다. 다른 봇의 관측을 자기 경험으로 발언하지 않는 경우도 포함한다. 기존 채팅 180개, 관심주 14개, 주문·일봉 33개로 고유 검사 총 227개이다.
- 전체 런타임/에디터 C# 컴파일 통과. 기존 API 사용 중단 경고는 남아 있다.
- 모든 검증 프로세스는 1코어·BelowNormal로 순차 실행했다. 원래 단계별 결과 파일은 보존하고 이번 실행 로그는 `Validation/TerminalOrders`, `Validation/chat-social-regression-current.txt`, `Validation/chat-watch-run.txt`에 보관했다.
- Unity 실화면 검증은 미완료. 샌드박스 실행은 라이선스/WMI 접근 단계에서 종료됐고, 권한을 갖춘 재시도에서는 실행 중인 다른 Unity 인스턴스가 감지되어 시작하지 않았다. 새 게임 카메라 캡처는 생성하지 않았다.
- 실행 가능한 검증 준비: 모든 Unity 종료 후 `Tests/TerminalOrders/RunUI.ps1`. 플레이 모드나 봇 시뮬레이션 없이 기존 씬의 Canvas와 실제 주문 엔진으로 클릭·스크롤·1600×900/1920×1080 캡처를 확인한다. 아직 통과했다고 보고하지 않는다.
- `TradeScene.unity`는 이 작업에서 직접 저장하지 않았다. 작업 중 외부 에디터에서 발생한 씬 변경은 덮어쓰거나 되돌리지 않았다.

## 플레이에서 확인할 부분

1. 플레이 모드를 다시 시작하고 서로 다른 종목/가격으로 주문 2건 이상 제출. 대기 중에도 입력과 새 주문 제출이 가능한지 확인.
2. 목록의 취소를 누르면 해당 주문만 없어지고 부분 체결분·다른 주문은 유지되는지 확인. 4건 이상일 때 목록 내부 드래그/휠 확인.
3. 종목/채팅 확대에서 Y=-20/+5.7과 중앙 모니터 동시 가시성 확인. A/D/Space와 화면 크기 변경 확인.
4. 중앙 헤더의 긴 이름/큰 거래대금, 태블릿 대기 목록의 글자 잘림 확인.
5. 실제 시장에서 봇의 마지막 관측 대비 급변이 생겼을 때 반응 확인. 정적인 가격이나 새로 보기 시작한 종목에는 놀람 발언이 나오지 않아야 한다.

## 신규 런타임 스크립트 전문

### Assets/Scripts/NightShift/PendingOrderListUI.cs

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 태블릿 안의 계좌별 대기 목록. 행을 재사용하고 실제 주문별 취소를 제공한다.</summary>
public sealed class PendingOrderListUI : MonoBehaviour
{
    private sealed class Row
    {
        public RectTransform Root;
        public TextMeshProUGUI Name, Detail, State;
        public DeviceOrderSession.PendingEntry Entry;
    }
    private DeviceOrderSession session;
    private TextMeshProUGUI template, title, empty;
    private RectTransform content;
    private readonly List<DeviceOrderSession.PendingEntry> entries = new(16);
    private readonly List<Row> rows = new(8);
    private int visibleCount = -1;
    private const float RowHeight = 112f;
    public int Count => entries.Count;

    public void Configure(DeviceOrderSession orders, TextMeshProUGUI fontTemplate)
    {
        session = orders; template = fontTemplate;
        title = Label("Pending Order Count",transform,31f,0,0,.68f,.115f);
        var viewport = Rect("Area :: Pending Order Scroll",transform,0,.15f,1,.85f);
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1,1,1,.008f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Area :: Pending Order Rows",viewport,0,0,1,1);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45f;
        empty = Label("No Pending Orders",viewport,29f,.02f,.03f,.96f,.25f);
        empty.text = "체결 대기 중인 주문이 없습니다.";
        Refresh();
    }
    public void Refresh()
    {
        session.CopyPendingOrders(entries);
        DeviceTerminalUI.Set(title,"체결 대기  " + entries.Count + "건");
        empty.gameObject.SetActive(entries.Count == 0);
        while (rows.Count < entries.Count) { rows.Add(CreateRow(rows.Count)); }
        if (visibleCount != entries.Count)
        {
            visibleCount = entries.Count;
            content.sizeDelta = new Vector2(0,visibleCount * RowHeight);
            for (int i = 0; i < rows.Count; i++) { rows[i].Root.gameObject.SetActive(i < visibleCount); }
        }
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i]; var row = rows[i]; row.Entry = entry;
            DeviceTerminalUI.Set(row.Name,(entry.Buy ? "매수 · " : "매도 · ") + entry.Stock.stockName);
            row.Name.color = entry.Buy ? new Color32(239,89,100,255) : new Color32(76,150,232,255);
            DeviceTerminalUI.Set(row.Detail,DeviceTerminalUI.Money(entry.Price) + " · 대기 " + entry.Remaining.ToString("N0") + "주");
            string state = entry.Request == null ? "자동·기존 주문" : "#" + entry.Request.Number.ToString("D4") + " · 체결 " + entry.Request.Filled.ToString("N0") + "주";
            if (entry.Request != null && entry.Request.Unsubmitted > 0m)
            { state += " · 분할 예약 " + entry.Request.Unsubmitted.ToString("N0") + "주"; }
            DeviceTerminalUI.Set(row.State,state);
        }
    }
    public void CancelAll()
    {
        session.CopyPendingOrders(entries);
        foreach (var entry in entries) { session.CancelPending(entry); }
        Refresh();
    }
    private Row CreateRow(int index)
    {
        var row = new Row { Root = Rect("Panel :: Pending Order " + index,content,0,0,1,1) };
        row.Root.anchorMin = new Vector2(0,1); row.Root.anchorMax = Vector2.one;
        row.Root.pivot = new Vector2(.5f,1); row.Root.sizeDelta = new Vector2(0,RowHeight);
        row.Root.anchoredPosition = new Vector2(0,-index * RowHeight);
        row.Name = Label("Pending Stock",row.Root,29f,.015f,.035f,.78f,.29f);
        row.Detail = Label("Pending Price Quantity",row.Root,27f,.015f,.34f,.78f,.29f);
        row.State = Label("Pending Fill State",row.Root,23f,.015f,.66f,.78f,.25f);
        row.State.color = new Color32(166,177,186,255);
        var divider = Rect("Image :: Pending Divider",row.Root,0,.985f,1,.008f).gameObject.AddComponent<Image>();
        divider.color = new Color32(52,67,75,180); divider.raycastTarget = false;
        var buttonRoot = Rect("Button :: Cancel Pending Order",row.Root,.82f,.17f,.18f,.61f);
        var image = buttonRoot.gameObject.AddComponent<Image>(); image.color = new Color32(31,44,50,255);
        var button = buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label("Cancel Pending Order",buttonRoot,27f,0,0,1,1);
        label.alignment = TextAlignmentOptions.Center; label.text = "취소";
        button.onClick.AddListener(() => { session.CancelPending(row.Entry); Refresh(); });
        return row;
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.gameObject.layer = gameObject.layer;
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private TextMeshProUGUI Label(string name, Transform parent, float size, float x, float y, float width, float height)
    {
        var label = Instantiate(template,parent); label.name = "Text :: " + name;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.fontSize = size; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        label.color = new Color32(230,235,239,255); label.text = "";
        return label;
    }
}

```

### Assets/Scripts/NightShift/ChatSocialMarketWatch.cs

```csharp
using System;

public sealed partial class InvestorPsychology
{
    internal Stock WatchedStock;
    internal decimal WatchedPrice;
    internal double WatchedAt = -1, NextWatchAt, WatchReactionUntil, WatchSelectedAt;
}

public sealed partial class InvestorSociety
{
    public Func<TradeBot, Stock> ChooseWatchedStock;

    private void TickMarketWatch(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        double now = SocialTime.GameMinute;
        if (!mind.ActiveAt((float)SocialTime.Seconds) || now < mind.NextWatchAt || ChooseWatchedStock == null)
        { return; }
        mind.NextWatchAt = now + Config.Get("watchObservationMinMinutes") + mind.SocialRng.Next() * (Config.Get("watchObservationMaxMinutes") - Config.Get("watchObservationMinMinutes"));
        var stock = mind.WatchedStock;
        if (stock == null || !stock.AcceptsOrders || now - mind.WatchSelectedAt >= Config.Get("watchRetargetMinutes"))
        { stock = ChooseWatchedStock(bot); mind.WatchSelectedAt = now; }
        ObserveWatchedPrice(bot, stock);
    }

    /// <summary>Compare only prices this bot actually observed. Initial/overnight/stale samples establish a new baseline.</summary>
    public bool ObserveWatchedPrice(TradeBot bot, Stock stock)
    {
        if (bot?.Social == null || stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m)
        { return false; }
        var mind = bot.Social.Psychology;
        double now = SocialTime.GameMinute, beforeAt = mind.WatchedAt;
        decimal before = mind.WatchedPrice, current = stock.currentPrice;
        bool comparable = mind.WatchedStock == stock && before > 0m && beforeAt >= 0 && now > beforeAt &&
            Math.Floor(beforeAt / 1440d) == Math.Floor(now / 1440d) && now - beforeAt <= Config.Get("watchMaximumGapMinutes");
        mind.WatchedStock = stock; mind.WatchedPrice = current; mind.WatchedAt = now;
        if (!comparable || now < mind.WatchReactionUntil)
        { return false; }
        decimal move = (current / before - 1m) * 100m;
        if (Math.Abs(move) < (decimal)Config.Get("watchMovePercent"))
        { return false; }
        var facts = CaptureFacts(bot, stock);
        facts.notice = move > 0m ? "WATCH_RISE" : "WATCH_FALL";
        facts.observationPrice = Money(before); facts.observedPrice = Money(current); facts.observationPercent = Percent(move);
        facts.observationBotId = mind.SocialId;
        facts.observationAt = beforeAt; facts.observedAt = now;
        facts.eventId = ChatSocialIds.Next();
        mind.WatchReactionUntil = now + Config.Get("watchReactionCooldownMinutes");
        Offer(bot, mind.Room, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: facts.eventId);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = facts.eventId, time = now, room = mind.Room, actor = mind.SocialId,
            kind = "WATCH_PRICE_SHOCK", value = (double)move, reason = stock.symbol + " " + Money(before) + " -> " + Money(current) });
        return true;
    }
    private static void PreserveWatchObservation(TradeBot bot, SocialIntent intent, SocialFacts current)
    {
        var offered = intent.facts;
        if (offered == null || offered.notice != "WATCH_RISE" && offered.notice != "WATCH_FALL")
        { return; }
        if (offered.observationBotId != bot.Social.Id || intent.replyToId != 0)
        {
            // A reply can discuss the topic, but must not claim another bot's personal price observation.
            current.notice = null;
            return;
        }
        current.observationBotId = offered.observationBotId;
        current.observationPrice = offered.observationPrice; current.observedPrice = offered.observedPrice; current.observationPercent = offered.observationPercent;
        current.observationAt = offered.observationAt; current.observedAt = offered.observedAt;
        current.price = offered.observedPrice;
    }
}

```
