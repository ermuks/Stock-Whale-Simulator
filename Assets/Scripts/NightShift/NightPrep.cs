using System;
using UnityEngine;

/// <summary>
/// 밤 준비: 장이 끝난 뒤부터 아침까지, 내일 장에서 쓸 우위를 미리 깔아 둔다. 깨어 있는 값은 돈과 피로다.
///
/// - 기사 심기: 돈을 주고 내일 개장 직후에 나갈 소문 기사를 맡긴다. 낮의 소문 기사와 같은 길로 나가고, 정정 기사가 따라붙을 수 있다.
/// - 내일 기사 미리 사기: 거래소에 이미 예약된 그 회사의 다음 기사를 먼저 읽는다. 지어내지 않는다. 예약이 멀면 "조용하다"가 답이다.
/// - 리딩방 예고: 내 방 회원들에게 내일 볼 종목을 흘려 둔다. 개장하자마자 지시가 저절로 나가 회원들이 먼저 판단한다.
///
/// 밤마다 한 번씩만 할 수 있다. 가격·계좌를 직접 고치지 않는다. 기사와 지시는 모두 기존의 길(뉴스 발행, 리딩방 지시)로 나간다.
/// </summary>
public sealed class NightPrep : MonoBehaviour
{
    public static NightPrep Instance { get; private set; }
    /// <summary>기사 한 건을 맡기는 값, 예약된 기사를 미리 사는 값.</summary>
    public const decimal PlantCost = 3_000_000m, TipCost = 1_500_000m;
    /// <summary>밤일 하나가 더하는 피로(%).</summary>
    public const double ActionFatigue = 6d;
    /// <summary>개장 뒤 심어 둔 기사가 나가기까지, 예고한 지시가 나가기까지(실제 초).</summary>
    private const float PlantDelay = 20f, TeaseDelay = 3f;
    /// <summary>예고한 지시의 값: 개장가보다 이만큼 위까지 사고, 그 위로 이만큼에서 판다.</summary>
    private const decimal TeaseBuyRoom = .01m, TeaseTarget = .05m;

    public sealed class Tip
    {
        public Stock Stock;
        /// <summary>내일 장중에 나올 예약 기사가 있는지. 없으면 Title은 비어 있다.</summary>
        public bool Found, Positive;
        public string Title;
    }

    private MarketDayController day;
    private int night = -1;
    public Stock Planted { get; private set; }
    public bool PlantedPositive { get; private set; }
    public Stock Teased { get; private set; }
    public Tip LastTip { get; private set; }
    public bool TipUsed { get; private set; }
    public string LastResult { get; private set; } = "";
    /// <summary>확인용: 지금까지 실제로 나간 심은 기사와 예고 지시의 수.</summary>
    public int PlantsPublished { get; private set; }
    public int TeasesIssued { get; private set; }
    private float plantAt = -1f, teaseAt = -1f;
    private MarketPhase lastPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }
    void Awake() { Instance = this; day = GetComponent<MarketDayController>(); lastPhase = MarketPhase.PreOpen; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    private static string NeedsUnlock => Word("해금 탭에서 먼저 열어야 합니다", "Unlock it first in the Unlocks tab", "先に解放タブで開いてください");
    /// <summary>밤인지: 장이 끝났고 정산 화면도 닫았다.</summary>
    public bool IsNight => day != null && day.Ready && day.Phase == MarketPhase.PostClose && !day.ReportPending;

    private Trader Payer => PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : StockManager.Instance?.playerAccount;

    /// <summary>밤이 바뀌면 그 밤의 한 번씩을 다시 준다(이미 깔아 둔 내일 계획은 그대로 둔다).</summary>
    private void Roll()
    {
        if (day == null || night == day.Day) { return; }
        night = day.Day; TipUsed = false; LastTip = null; LastResult = "";
    }

    public bool CanPlant(Stock stock, out string reason)
    {
        Roll();
        if (!PlayerProgression.Has(PlayerProgression.NightPlantUnlock)) { reason = NeedsUnlock; return false; }
        if (!IsNight) { reason = Word("장 마감 뒤에 할 수 있습니다", "Available after the close", "引け後に行えます"); return false; }
        if (Planted != null) { reason = Word("오늘 밤 기사는 이미 맡겼습니다", "A story is already placed tonight", "今夜の記事はもう手配済みです"); return false; }
        if (stock == null || !stock.IsListed || stock.IsDelisting || stock.IsDelisted) { reason = Word("상장 종목을 고르세요", "Pick a listed stock", "上場銘柄を選んでください"); return false; }
        var payer = Payer;
        if (payer == null || PlayerAccounts.Available(payer) < PlantCost) { reason = Word("돈이 모자랍니다", "Not enough cash", "資金が足りません"); return false; }
        reason = ""; return true;
    }

    /// <summary>내일 개장 직후에 나갈 소문 기사를 맡긴다.</summary>
    public bool Plant(Stock stock, bool positive)
    {
        if (!CanPlant(stock, out string reason)) { LastResult = reason; return false; }
        Payer.cash -= PlantCost; Tire();
        Planted = stock; PlantedPositive = positive; plantAt = -1f;
        LastResult = Word("기사를 맡겼습니다. 내일 개장 직후에 나갑니다", "Story placed. It runs right after tomorrow's open", "記事を手配しました。明日の寄り付き直後に出ます");
        SteamAchievements.Unlock(SteamAchievements.NightWork);
        Debug.Log($"[밤 준비] 기사 심기: {stock.stockName}({stock.symbol}) {(positive ? "호재" : "악재")} · {PlantCost:N0}돈 · 피로 {PlayerRestController.Instance?.State.fatigue:0}");
        return true;
    }

    public bool CanTip(Stock stock, out string reason)
    {
        Roll();
        if (!PlayerProgression.Has(PlayerProgression.NightTipUnlock)) { reason = NeedsUnlock; return false; }
        if (!IsNight) { reason = Word("장 마감 뒤에 할 수 있습니다", "Available after the close", "引け後に行えます"); return false; }
        if (TipUsed) { reason = Word("오늘 밤은 이미 한 건을 샀습니다", "Already bought one tonight", "今夜はもう一件買いました"); return false; }
        if (stock == null || !stock.IsListed || stock.IsDelisting || stock.IsDelisted) { reason = Word("상장 종목을 고르세요", "Pick a listed stock", "上場銘柄を選んでください"); return false; }
        var payer = Payer;
        if (payer == null || PlayerAccounts.Available(payer) < TipCost) { reason = Word("돈이 모자랍니다", "Not enough cash", "資金が足りません"); return false; }
        reason = ""; return true;
    }

    /// <summary>그 회사의 예약된 다음 기사를 미리 읽는다. 내일 장이 끝나기 전에 나올 것만 "있다"로 친다.</summary>
    public bool BuyTip(Stock stock)
    {
        if (!CanTip(stock, out string reason)) { LastResult = reason; return false; }
        Payer.cash -= TipCost; Tire(); TipUsed = true;
        var tip = new Tip { Stock = stock };
        // 지금 바로 자면 내일 장이 끝날 때까지 흐르는 시간: 개장 전 준비와 장중. 기사 시계는 자는 동안 건너뛰지 않는다.
        double window = ExchangeBot.OvernightNewsSeconds + day.tradingSeconds / 8d + day.tradingSeconds;
        if (ExchangeBot.Instance != null && ExchangeBot.Instance.TryPeekFuture(stock, window, out string title, out float value, out _))
        { tip.Found = true; tip.Title = title; tip.Positive = value >= 1f; }
        LastTip = tip;
        SteamAchievements.Unlock(SteamAchievements.NightWork);
        LastResult = tip.Found ? Word("내일 나올 기사를 받았습니다", "You have tomorrow's story", "明日の記事を手に入れました") : Word("내일은 이 회사에 나올 기사가 없습니다", "Nothing is scheduled for this company tomorrow", "明日この会社の記事はありません");
        Debug.Log($"[밤 준비] 내일 기사 미리 사기: {stock.stockName}({stock.symbol}) → {(tip.Found ? (tip.Positive ? "호재 · " : "악재 · ") + tip.Title : "예약 없음")} · {TipCost:N0}돈");
        return true;
    }

    /// <summary>회원이 있는 내 리딩방. 없으면 null.</summary>
    public InvestorSociety.Room MyRoom()
    {
        var society = InvestorSociety.Instance;
        if (society == null) { return null; }
        foreach (var room in society.Rooms) { if (room.PlayerOwned && society.IsRoomOpen(room.Id) && room.Members.Count > 0) { return room; } }
        return null;
    }

    public bool CanTease(Stock stock, out string reason)
    {
        Roll();
        if (!PlayerProgression.Has(PlayerProgression.NightTeaseUnlock)) { reason = NeedsUnlock; return false; }
        if (!IsNight) { reason = Word("장 마감 뒤에 할 수 있습니다", "Available after the close", "引け後に行えます"); return false; }
        if (Teased != null) { reason = Word("오늘 밤은 이미 예고했습니다", "Already teased tonight", "今夜はもう予告しました"); return false; }
        if (MyRoom() == null) { reason = Word("회원이 있는 내 리딩방이 필요합니다", "Needs a room of your own with members", "会員のいる自分の部屋が必要です"); return false; }
        if (stock == null || !stock.IsListed || stock.IsDelisting || stock.IsDelisted) { reason = Word("상장 종목을 고르세요", "Pick a listed stock", "上場銘柄を選んでください"); return false; }
        reason = ""; return true;
    }

    /// <summary>내 방 회원들에게 내일 볼 종목을 흘린다. 방에 한 줄이 올라가고, 개장하면 지시가 저절로 나간다.</summary>
    public bool Tease(Stock stock)
    {
        if (!CanTease(stock, out string reason)) { LastResult = reason; return false; }
        Tire(); Teased = stock; teaseAt = -1f;
        var room = MyRoom();
        string line = Word("내일 개장하면 " + stock.stockName + " 봅니다. 자리 지키세요.", "Watching " + GameLocale.Text(stock.stockName) + " at tomorrow's open. Be at your desk.", "明日の寄り付きで" + GameLocale.Text(stock.stockName) + "を見ます。席にいてください。");
        InvestorSociety.Instance.Speak(null, room.Id, line, default);
        LastResult = Word("회원들에게 예고했습니다", "Your members have been told", "会員に予告しました");
        SteamAchievements.Unlock(SteamAchievements.NightWork);
        Debug.Log($"[밤 준비] 리딩방 예고: {stock.stockName}({stock.symbol}) · 방 {room.Id} · 회원 {room.Members.Count}명");
        return true;
    }

    private static void Tire()
    {
        var rest = PlayerRestController.Instance;
        if (rest != null) { rest.State.fatigue = Math.Min(100d, rest.State.fatigue + ActionFatigue); }
    }

    void Update()
    {
        if (day == null || !day.Ready) { return; }
        var phase = day.Phase;
        if (phase == MarketPhase.Trading && lastPhase != MarketPhase.Trading)
        {
            // 개장: 밤에 깔아 둔 것들의 시계를 켠다.
            if (Planted != null) { plantAt = SessionClock.Time + PlantDelay; }
            if (Teased != null) { teaseAt = SessionClock.Time + TeaseDelay; }
        }
        lastPhase = phase;
        if (phase != MarketPhase.Trading || !MarketDayController.CanTrade) { return; }

        if (Teased != null && teaseAt >= 0f && SessionClock.Time >= teaseAt)
        {
            var stock = Teased; Teased = null; teaseAt = -1f;
            var room = MyRoom();
            if (room != null && stock.AcceptsOrders && stock.currentPrice > 0m)
            {
                decimal buy = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + TeaseBuyRoom));
                decimal sell = PriceTickPolicy.CeilToTick(buy * (1m + TeaseTarget));
                if (sell <= buy) { sell = PriceTickPolicy.CeilToTick(buy + 1m); }
                bool issued = InvestorSociety.Instance.Issue(room.Id, stock, buy, sell);
                if (issued) { TeasesIssued++; }
                Debug.Log($"[밤 준비] 예고한 지시 발령: {stock.stockName}({stock.symbol}) 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0} · {(issued ? "나감" : "거절: " + InvestorSociety.Instance.LastAction)}");
            }
        }

        if (Planted != null && plantAt >= 0f && SessionClock.Time >= plantAt)
        {
            var stock = Planted; bool positive = PlantedPositive; Planted = null; plantAt = -1f;
            var article = FakeNewsManager.Instance == null ? null : FakeNewsManager.Instance.PublishPlanted(stock, positive);
            if (article != null) { PlantsPublished++; }
            Debug.Log($"[밤 준비] 심은 기사 발행: {stock.stockName}({stock.symbol}) {(positive ? "호재" : "악재")} · {(article == null ? "실패" : article.title)}");
        }
    }
}
