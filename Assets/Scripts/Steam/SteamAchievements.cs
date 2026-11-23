using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Steam 도전과제. 게임의 큰 기능마다 하나씩, "한 번 해 봤다"를 기록한다.
/// 두 가지로 알아낸다: (1) 게임 상태를 1초마다 읽어 보는 것(Poll), (2) 그 일이 일어나는 자리에서 Unlock을 부르는 것.
/// 달성한 것은 PlayerPrefs에도 적어 둔다. Steam에 연결되지 않은 채로 달성했어도 다음에 연결되면 올라간다.
/// 저장(SessionStore)에 들어가는 객체에는 아무 필드도 더하지 않는다. 시세·주문·채팅은 건드리지 않고 읽기만 한다.
/// 아이디는 Steamworks에 등록한 API 이름과 글자 그대로 같아야 한다(SteamBuild/achievements.md).
/// </summary>
public sealed class SteamAchievements : MonoBehaviour
{
    // 거래
    public const string FirstBuy = "FIRST_BUY", FirstSell = "FIRST_SELL", FirstProfit = "FIRST_PROFIT", SplitOrder = "SPLIT_ORDER";
    // 채팅과 사람
    public const string Chat = "CHAT", Profile = "PROFILE", ProfitProof = "PROFIT_PROOF";
    // 방 안의 것들
    public const string DoorNote = "DOOR_NOTE", Drawer = "DRAWER", Sleep = "SLEEP", Week = "WEEK", UnlockInfo = "UNLOCK_INFO";
    // 리딩방
    public const string DawnRoom = "DAWN_ROOM", OpenRoom = "OPEN_ROOM", FirstMember = "FIRST_MEMBER", FirstCall = "FIRST_CALL", Promote = "PROMOTE", OpeningRushCall = "OPENING_RUSH";
    // 작전
    public const string TacticIgnite = "TACTIC_IGNITE", TacticWash = "TACTIC_WASH", TacticClose = "TACTIC_CLOSE", TacticWall = "TACTIC_WALL", TacticSlam = "TACTIC_SLAM";
    // 두 번째 계좌, 기사, 밤일
    public const string SecondAccount = "SECOND_ACCOUNT", Spread = "SPREAD", Accumulate = "ACCUMULATE", FakeNews = "FAKE_NEWS", NightWork = "NIGHT_WORK";
    // 기업
    public const string SmallCompany = "SMALL_COMPANY", FoundCompany = "FOUND_COMPANY", Policy = "POLICY", Growth = "GROWTH", Ipo = "IPO", Takeover = "TAKEOVER";
    // 그 밖
    public const string Delisted = "DELISTED", NetWorth = "NET_WORTH_1B", Ending = "ENDING";

    /// <summary>NET_WORTH_1B의 기준: 두 계좌의 순자산 합(세금 뺀 값).</summary>
    private const decimal NetWorthGoal = 1_000_000_000m;
    /// <summary>WEEK의 기준: 거래일 5일을 마치고 맞는 여섯 번째 거래일(둘째 주 월요일).</summary>
    private const int WeekDay = 6;
    private const string PrefPrefix = "steam.achievement.";
    private static readonly string[] TimedUnlocks = { PlayerProgression.CrowdValueUnlock, PlayerProgression.RealValueUnlock, PlayerProgression.NightTipUnlock, PlayerProgression.NightPlantUnlock, PlayerProgression.NightTeaseUnlock };

    private static readonly HashSet<string> done = new();
    private static readonly List<string> waiting = new();
    private static bool dirty;
    private float nextPoll, nextStore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        done.Clear(); waiting.Clear(); dirty = false;
        var root = new GameObject("System :: Steam"); DontDestroyOnLoad(root);
        root.AddComponent<SteamAchievements>();
    }

    /// <summary>도전과제를 달성했다. 여러 번 불러도 한 번만 올라간다. Steam이 꺼져 있어도 기록은 남는다.</summary>
    public static void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || !done.Add(id)) { return; }
        // 편집기 일괄 실행(검증·촬영)은 진짜 플레이가 아니다. 기록하지 않는다.
        if (Application.isBatchMode) { return; }
        if (PlayerPrefs.GetInt(PrefPrefix + id, 0) == 0) { PlayerPrefs.SetInt(PrefPrefix + id, 1); PlayerPrefs.Save(); }
        Push(id);
    }

    private static void Push(string id)
    {
        if (!SteamLink.Ready) { if (!waiting.Contains(id)) { waiting.Add(id); } return; }
        if (SteamLink.HasAchievement(id)) { return; }
        if (SteamLink.SetAchievement(id)) { dirty = true; Debug.Log("[Steam] 도전과제 달성: " + id); }
        else { Debug.LogWarning("[Steam] 도전과제를 올리지 못함(Steamworks에 등록된 이름인지 확인): " + id); }
    }

    void Awake()
    {
        if (!SteamLink.Connect()) { return; }
        // 전에 Steam 없이 달성해 둔 것을 올린다.
        foreach (string id in All) { if (PlayerPrefs.GetInt(PrefPrefix + id, 0) != 0) { done.Add(id); Push(id); } }
    }
    void OnEnable() { StockManager.OnStockDelisted += OnDelisted; }
    void OnDisable() { StockManager.OnStockDelisted -= OnDelisted; }
    void OnApplicationQuit() { if (dirty) { SteamLink.Store(); dirty = false; } SteamLink.Disconnect(); }

    void Update()
    {
        SteamLink.Pump();
        float now = Time.unscaledTime;
        if (now >= nextPoll) { nextPoll = now + 1f; Poll(); }
        if (SteamLink.Ready && now >= nextStore)
        {
            nextStore = now + 2f;
            if (waiting.Count > 0) { var pending = waiting.ToArray(); waiting.Clear(); foreach (string id in pending) { Push(id); } }
            if (dirty && SteamLink.Store()) { dirty = false; }
        }
    }

    /// <summary>들고 있던 종목이 상장폐지됐다.</summary>
    private static void OnDelisted(Stock stock)
    {
        var accounts = PlayerAccounts.Instance;
        if (stock != null && accounts != null && accounts.Ready && accounts.primary != null && accounts.automated != null && accounts.Owned(stock) > 0m) { Unlock(Delisted); }
    }

    /// <summary>게임 상태를 읽어, 이미 이뤄진 일을 달성으로 올린다. 저장을 불러온 뒤에도 같은 식으로 다시 맞춰진다.</summary>
    private static void Poll()
    {
        var accounts = PlayerAccounts.Instance; var progress = PlayerProgression.Instance;
        if (accounts == null || progress == null || !accounts.Ready) { return; }

        if (progress.CanOperateRooms) { Unlock(OpenRoom); }
        if (progress.CompanyFounded) { Unlock(FoundCompany); }
        if (progress.Dreamt) { Unlock(Ending); }
        if (!done.Contains(UnlockInfo)) { foreach (string id in TimedUnlocks) { if (progress.IsUnlocked(id)) { Unlock(UnlockInfo); break; } } }

        var dawn = NpcLeadingRoom.Instance;
        if (dawn != null && dawn.PlayerJoined) { Unlock(DawnRoom); }
        var society = InvestorSociety.Instance;
        if (society != null && society.PlayerLines > 0) { Unlock(Chat); }
        var day = MarketDayController.Instance;
        if (day != null && day.Day >= WeekDay) { Unlock(Week); }
        var drawer = DrawerProp.Instance;
        if (drawer != null && drawer.Open) { Unlock(Drawer); }

        if (accounts.AutomatedUnlocked) { Unlock(SecondAccount); }
        var operation = TradingOperationController.Instance;
        if (operation != null)
        {
            if (operation.SpreadRunning) { Unlock(Spread); }
            if (operation.AccumulateRunning) { Unlock(Accumulate); }
        }

        var economy = CorporateEconomy.Instance;
        if (economy != null)
        {
            var mine = economy.PlayerCompany;
            if (mine != null && mine.IsListed) { Unlock(Ipo); }
            if (!done.Contains(Takeover)) { foreach (var rung in economy.Ladder()) { if (rung.Controlled && rung.Stock != null && rung.Stock.IsListed) { Unlock(Takeover); break; } } }
        }

        if (!done.Contains(NetWorth) && accounts.primary != null && accounts.automated != null
            && accounts.primary.Equity() + accounts.automated.Equity() - PlayerAccounts.TaxDebt(accounts.primary) >= NetWorthGoal) { Unlock(NetWorth); }
    }

    public static readonly string[] All =
    {
        FirstBuy, FirstSell, FirstProfit, SplitOrder, Chat, Profile, ProfitProof, DoorNote, Drawer, Sleep, Week, UnlockInfo,
        DawnRoom, OpenRoom, FirstMember, FirstCall, Promote, OpeningRushCall, TacticIgnite, TacticWash, TacticClose, TacticWall, TacticSlam,
        SecondAccount, Spread, Accumulate, FakeNews, NightWork, SmallCompany, FoundCompany, Policy, Growth, Ipo, Takeover, Delisted, NetWorth, Ending,
    };
}
