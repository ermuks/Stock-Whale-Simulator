using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 돈과 시간을 들여 여는 정보. 비용은 실제 계좌 현금에서 한 번 빠지고, 게임시간이 지나야 열린다.
/// 상태 필드는 PlayerProgression의 일부로 세션 저장에 함께 들어간다.
/// </summary>
public sealed partial class PlayerProgression
{
    public const string CrowdValueUnlock = "chart.crowdValue", RealValueUnlock = "chart.realValue";
    public const string DawnUnlock = "room.dawn", CloseUnlock = "tactic.close", WashUnlock = "tactic.wash", FoundUnlock = "company.found", WallUnlock = "tactic.wall";
    private static string Local(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>해금 탭에 함께 보여 주는, 다른 곳에서 열리는 해금들. 성장하는 순서대로 놓는다.</summary>
    private UnlockItem[] ExternalUnlocks() => new[]
    {
        new UnlockItem { external = true, id = DawnUnlock, cost = rules.dawnRoomCost, title = "새벽 관찰실", en = "Dawn Watch Room", ja = "夜明けの観察室",
            description = "운영자의 실제 주문 관측과 종목 분석 AI를 볼 수 있는 방입니다. 누적 거래대금 " + rules.dawnRoomTurnover.ToString("N0") + "원부터 열 수 있고, 들어가 있는 동안 하루 " + rules.dawnRoomDailyFee.ToString("N0") + "원이 나갑니다.",
            enDescription = "A room with the host's real order sightings and the stock analysis AI. Opens from " + rules.dawnRoomTurnover.ToString("N0") + " KRW lifetime turnover, and costs " + rules.dawnRoomDailyFee.ToString("N0") + " KRW a day while you stay.",
            jaDescription = "運営者の実際の注文観測と銘柄分析AIが見られる部屋です。累計売買代金" + rules.dawnRoomTurnover.ToString("N0") + "ウォンから開けられ、入室中は1日" + rules.dawnRoomDailyFee.ToString("N0") + "ウォンかかります。" },
        new UnlockItem { external = true, id = CloseUnlock, title = "작전 도구 · 종가 관리", en = "Tactic · Mark the close", ja = "仕掛け · 終値の吊り上げ",
            description = "장 마감 40초 전부터 사들여 종가를 높게 찍습니다. 누적 거래대금 " + rules.tacticCloseTurnover.ToString("N0") + "원이 되면 저절로 열립니다. 두 번째 태블릿의 '작전' 탭에서 씁니다.",
            enDescription = "Buys through the last 40 seconds to print a higher close. Opens by itself at " + rules.tacticCloseTurnover.ToString("N0") + " KRW lifetime turnover. Use it from the Tactics tab on the second tablet.",
            jaDescription = "引けの40秒前から買い上げて終値を高くします。累計売買代金が" + rules.tacticCloseTurnover.ToString("N0") + "ウォンになると自動で開きます。2台目のタブレットの「仕掛け」タブで使います。" },
        new UnlockItem { external = true, id = WashUnlock, title = "작전 도구 · 거래량 부풀리기", en = "Tactic · Paint the tape", ja = "仕掛け · 出来高の水増し",
            description = "차명 계좌와 같은 값에 주고받아 거래량만 늘립니다. 눈길이 그 종목으로 쏠립니다. 내 리딩방을 열면 함께 열립니다.",
            enDescription = "Trade back and forth with a nominee account at one price so only volume grows and eyes turn to the stock. Opens when you open your own room.",
            jaDescription = "名義貸しの口座と同じ値で売り買いして出来高だけを増やします。注目がその銘柄に集まります。自分のルームを開くと一緒に開きます。" },
        new UnlockItem { external = true, id = FoundUnlock, cost = rules.companyFoundingCost, title = "기업 설립", en = "Found a company", ja = "会社設立",
            description = "내 비상장기업을 세웁니다. 낸 돈의 대부분은 회사 자본금이 됩니다. 사업을 키워 상장하고, 다른 회사를 삼키는 발판이 됩니다.",
            enDescription = "Found your own private company. Most of what you pay becomes its capital. Grow it, list it, and use it as the base for swallowing others.",
            jaDescription = "自分の非上場会社を設立します。支払った資金の大半は会社の資本金になります。育てて上場させ、他社を飲み込む足場にします。" },
        new UnlockItem { external = true, id = WallUnlock, title = "작전 도구 · 허수 매수벽", en = "Tactic · Fake bid wall", ja = "仕掛け · 見せ板の買い壁",
            description = "현재가 아래에 큰 매수 주문을 걸어 받치는 돈처럼 보이게 합니다. 개미들은 매수벽을 보면 오른다고 여깁니다. 내 기업이 상장하면 열립니다.",
            enDescription = "Rest a big bid below the market so it looks like support. Small investors read a bid wall as a sign it will rise. Opens when your company lists.",
            jaDescription = "現在値の下に大きな買い注文を置いて支えがあるように見せます。個人は買い壁を見ると上がると考えます。自社が上場すると開きます。" },
    };
    private bool ExternalDone(string id) => id == DawnUnlock ? DawnUnlocked : id == FoundUnlock ? CorporateEconomy.Instance != null && CorporateEconomy.Instance.PlayerCompany != null
        : id == CloseUnlock ? PlayerTactics.Unlocked(PlayerTactics.Tool.Close) : id == WashUnlock ? PlayerTactics.Unlocked(PlayerTactics.Tool.Wash) : PlayerTactics.Unlocked(PlayerTactics.Tool.Wall);

    [Serializable]
    public sealed class UnlockItem
    {
        public string id, title, description, requires;
        public long cost;
        public int minutes;
        /// <summary>다른 곳에서 열리는 해금(새벽 관찰실, 기업 설립, 작전 도구). 상태를 그쪽에서 읽어 오고, 글은 언어마다 여기 적힌 것을 그대로 쓴다.</summary>
        [NonSerialized] public bool external;
        [NonSerialized] public string en, ja, enDescription, jaDescription;
        public string LocalTitle => !external ? title : GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : title;
        public string LocalDescription => !external ? description : GameLocale.Code == "ja" ? jaDescription : GameLocale.Code == "en" ? enDescription : description;
    }

    [Serializable]
    public sealed class UnlockCatalog
    {
        public int schemaVersion = 1;
        public UnlockItem[] items;
    }

    public enum UnlockState
    {
        Locked,
        Available,
        InProgress,
        Unlocked
    }

    // 목록은 설정 파일에서 읽는 정적 자료다. 저장하지 않는다.
    private static UnlockItem[] unlockItems;
    private readonly List<string> unlocked = new();
    private string pendingUnlock;
    private double pendingReadyAt;
    public event Action UnlocksChanged;
    public string UnlockResult { get; private set; } = "";

    public IReadOnlyList<UnlockItem> UnlockItems
    {
        get
        {
            if (unlockItems == null)
            {
                var data = Resources.Load<TextAsset>("Progression/PlayerUnlocks");
                var catalog = data == null ? null : JsonUtility.FromJson<UnlockCatalog>(data.text);
                var valid = new List<UnlockItem>();
                if (catalog != null && catalog.schemaVersion == 1 && catalog.items != null)
                {
                    foreach (var item in catalog.items)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.id) && item.cost >= 0 && item.minutes >= 0)
                        {
                            valid.Add(item);
                        }
                    }
                }

                // 다른 곳에서 열리는 해금을 뒤에 잇는다.
                valid.AddRange(ExternalUnlocks());
                unlockItems = valid.ToArray();
            }

            return unlockItems;
        }
    }

    public static bool Has(string id)
    { return Instance != null && Instance.IsUnlocked(id); }

    public bool IsUnlocked(string id)
    { return !string.IsNullOrEmpty(id) && unlocked.Contains(id); }

    public UnlockItem FindUnlock(string id)
    {
        foreach (var item in UnlockItems)
        {
            if (item.id == id)
            { return item; }
        }

        return null;
    }

    public UnlockState StateOf(UnlockItem item)
    {
        if (item == null)
        { return UnlockState.Locked; }

        if (item.external)
        {
            if (ExternalDone(item.id)) { return UnlockState.Unlocked; }
            // 돈을 내고 여는 것은 조건이 되면 '가능', 조건만으로 열리는 것은 그때까지 '잠김'.
            return item.id == FoundUnlock || item.id == DawnUnlock && DawnEligible ? UnlockState.Available : UnlockState.Locked;
        }

        if (IsUnlocked(item.id))
        { return UnlockState.Unlocked; }

        if (pendingUnlock == item.id)
        { return UnlockState.InProgress; }

        return string.IsNullOrEmpty(item.requires) || IsUnlocked(item.requires) ? UnlockState.Available : UnlockState.Locked;
    }

    public string PendingUnlock => pendingUnlock;

    /// <summary>진행 중인 해금이 끝날 때까지 남은 게임 분.</summary>
    public double UnlockRemainingMinutes
    {
        get
        {
            if (string.IsNullOrEmpty(pendingUnlock) || MarketDayController.Instance == null)
            { return 0; }

            return Math.Max(0, pendingReadyAt - MarketDayController.Instance.AbsoluteGameMinute);
        }
    }

    public bool CanStartUnlock(UnlockItem item, out string reason)
    {
        reason = "";
        if (item == null)
        {
            reason = "항목 없음";
            return false;
        }

        if (item.external)
        {
            var account = StockManager.Instance == null ? null : StockManager.Instance.playerAccount;
            if (ExternalDone(item.id)) { reason = Local("해금 완료", "Unlocked", "解放済み"); return false; }
            if (item.id == CloseUnlock) { reason = Local("누적 거래대금 ", "Lifetime turnover ", "累計売買代金 ") + decimal.Floor(Turnover).ToString("N0") + " / " + rules.tacticCloseTurnover.ToString("N0"); return false; }
            if (item.id == WashUnlock) { reason = Local("내 리딩방을 열면 열립니다", "Opens with your own room", "自分のルームを開くと開きます"); return false; }
            if (item.id == WallUnlock) { reason = Local("내 기업이 상장하면 열립니다", "Opens when your company lists", "自社が上場すると開きます"); return false; }
            if (item.id == DawnUnlock && !DawnEligible) { reason = Local("누적 거래대금 ", "Lifetime turnover ", "累計売買代金 ") + decimal.Floor(Turnover).ToString("N0") + " / " + rules.dawnRoomTurnover.ToString("N0"); return false; }
            if (account == null || MarketDayController.Instance == null || !MarketDayController.Instance.Ready) { reason = Local("준비 중", "Getting ready", "準備中"); return false; }
            if (PlayerAccounts.Available(account) < item.cost) { reason = Local("가용 현금 부족", "Not enough cash", "現金不足"); return false; }
            return true;
        }

        var state = StateOf(item);
        if (state == UnlockState.Unlocked)
        {
            reason = "해금 완료";
            return false;
        }

        if (state == UnlockState.InProgress)
        {
            reason = "해금 진행 중";
            return false;
        }

        if (state == UnlockState.Locked)
        {
            reason = "선행 해금 필요";
            return false;
        }

        if (!string.IsNullOrEmpty(pendingUnlock))
        {
            reason = "다른 해금 진행 중";
            return false;
        }

        var payer = StockManager.Instance == null ? null : StockManager.Instance.playerAccount;
        if (payer == null || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        {
            reason = "준비 중";
            return false;
        }

        if (PlayerAccounts.Available(payer) < item.cost)
        {
            reason = "가용 현금 부족";
            return false;
        }

        return true;
    }

    public bool TryStartUnlock(string id)
    {
        var item = FindUnlock(id);
        if (!CanStartUnlock(item, out string reason))
        {
            UnlockResult = reason;
            return false;
        }

        if (item.external)
        {
            // 실제로 여는 일은 원래 그 기능이 한다. 여기서는 그것을 불러 줄 뿐이다.
            bool opened = item.id == DawnUnlock ? UnlockDawn(StockManager.Instance.playerAccount) : item.id == FoundUnlock && CorporateEconomy.Instance != null && CorporateEconomy.Instance.FoundCompany();
            UnlockResult = opened ? Local("해금 완료", "Unlocked", "解放済み") : Local("해금하지 못했습니다", "Could not unlock", "解放できませんでした");
            if (opened) { MarketEventFeed.Instance?.Notify(Local("해금 완료 · ", "Unlocked · ", "解放 · ") + item.LocalTitle); }
            UnlocksChanged?.Invoke();
            return opened;
        }

        var payer = StockManager.Instance.playerAccount;
        // 정보 비용은 시장 밖으로 나가는 지출이다. 일일 손익에는 매매 손실이 아닌 외부 지출로 잡힌다.
        payer.cash -= item.cost;
        pendingUnlock = item.id;
        pendingReadyAt = MarketDayController.Instance.AbsoluteGameMinute + item.minutes;
        UnlockResult = "해금 시작";
        if (item.minutes <= 0)
        {
            CompleteUnlock();
        }
        else
        {
            UnlocksChanged?.Invoke();
        }

        return true;
    }

    private void CompleteUnlock()
    {
        string id = pendingUnlock;
        pendingUnlock = null;
        pendingReadyAt = 0;
        if (!string.IsNullOrEmpty(id) && !unlocked.Contains(id))
        {
            unlocked.Add(id);
            var item = FindUnlock(id);
            MarketEventFeed.Instance?.Notify("해금 완료 · " + (item == null ? id : item.title));
        }

        UnlockResult = "해금 완료";
        UnlocksChanged?.Invoke();
    }

    void Update()
    {
        if (string.IsNullOrEmpty(pendingUnlock) || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }

        if (FindUnlock(pendingUnlock) == null)
        {
            // 목록에서 사라진 항목은 조용히 정리한다.
            pendingUnlock = null;
            return;
        }

        if (MarketDayController.Instance.AbsoluteGameMinute >= pendingReadyAt)
        {
            CompleteUnlock();
        }
    }
}
