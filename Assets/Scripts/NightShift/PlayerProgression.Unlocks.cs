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
    /// <summary>밤 준비의 세 가지. 돈과 시간을 들여 여는 보통의 해금이다(저장도 보통 해금과 같이 된다).</summary>
    public const string NightTipUnlock = "night.tip", NightPlantUnlock = "night.plant", NightTeaseUnlock = "night.tease";

    /// <summary>
    /// 해금 설명은 세 줄로 쓴다: 얻는 것(사면 무엇이 생기는가), 이럴 때(왜 사는가), 쓰는 곳(어디서 쓰는가).
    /// 예전 설명은 작동 방식만 적어서("매도 호가를 5%까지 쓸어 올립니다") 사야 할 이유가 보이지 않았다.
    /// </summary>
    private static string Pitch(string language, string gain, string when, string where)
    {
        string a = language == "ja" ? "得られるもの" : language == "en" ? "You get" : "얻는 것";
        string b = language == "ja" ? "こんな時に" : language == "en" ? "Use it when" : "이럴 때";
        string c = language == "ja" ? "使う場所" : language == "en" ? "Where" : "쓰는 곳";
        return a + " · " + gain + "\n\n" + b + " · " + when + "\n\n" + c + " · " + where;
    }
    private static UnlockItem Written(UnlockItem item, (string gain, string when, string where) ko, (string gain, string when, string where) en, (string gain, string when, string where) ja)
    {
        item.description = Pitch("ko", ko.gain, ko.when, ko.where); item.enDescription = Pitch("en", en.gain, en.when, en.where); item.jaDescription = Pitch("ja", ja.gain, ja.when, ja.where);
        // 화면에는 프리뷰 그림과 함께 이 한 줄(얻는 것)만 보인다.
        item.summary = ko.gain; item.enSummary = en.gain; item.jaSummary = ja.gain;
        return item;
    }

    /// <summary>
    /// 차트에 선을 하나 더 그려 주는 정보 두 가지. 값과 걸리는 시간은 시작 자금(5천만 돈)으로도 첫날에 살 수 있게 낮췄다
    /// (예전: 500만 돈·게임 2시간, 3,000만 돈·게임 8시간).
    /// </summary>
    private static UnlockItem[] ChartUnlocks() => new[]
    {
        Written(new UnlockItem { authored = true, id = CrowdValueUnlock, cost = 2_000_000, minutes = 30, title = "개미들이 믿는 가격", en = "What the crowd believes", ja = "個人が信じる値段" },
            ("개미들이 이 종목을 얼마짜리로 믿는지 차트에 선으로 보입니다.", "지금 값이 그 선보다 한참 아래면 따라 살 사람이 많고, 한참 위면 거품입니다. 살 때와 팔 때를 고르기 쉬워집니다.", "가운데 모니터의 차트. 한 번 열면 늘 보입니다."),
            ("A line on the chart showing what small investors believe this stock is worth.", "Price far below the line means buyers are coming; far above means a bubble. It makes entries and exits easier to pick.", "The chart on the center monitor. Always on once unlocked."),
            ("個人投資家がこの銘柄をいくらだと信じているかを、チャートに線で表示します。", "今の値がその線よりずっと下なら買いが続き、ずっと上ならバブルです。買い時と売り時を選びやすくなります。", "中央モニターのチャート。一度開けば常に表示されます。")),
        Written(new UnlockItem { authored = true, id = RealValueUnlock, requires = CrowdValueUnlock, cost = 8_000_000, minutes = 60, title = "회사의 진짜 가치", en = "What the company is really worth", ja = "会社の本当の価値" },
            ("회사의 실적으로 계산한 주당 진짜 가치를 차트에 선으로 보입니다.", "개미들이 믿는 가격과의 차이가 거품의 크기입니다. 진짜 가치보다 싸면 들고 버틸 만하고, 한참 비싸면 언젠가 꺼집니다.", "가운데 모니터의 차트. 한 번 열면 늘 보입니다."),
            ("A line on the chart showing the real per-share value, worked out from the company's results.", "The gap to the crowd's price is the size of the bubble. Below real value is safe to hold; far above will deflate someday.", "The chart on the center monitor. Always on once unlocked."),
            ("会社の業績から計算した1株あたりの本当の価値を、チャートに線で表示します。", "個人が信じる値段との差がバブルの大きさです。本当の価値より安ければ持ち続けられ、ずっと高ければいつか弾けます。", "中央モニターのチャート。一度開けば常に表示されます。")),
    };

    /// <summary>밤 준비의 해금. 값을 낮췄다(예전: 1,000만·3,000만·500만 돈).</summary>
    private static UnlockItem[] NightUnlocks() => new[]
    {
        Written(new UnlockItem { authored = true, id = NightTipUnlock, cost = 3_000_000, minutes = 30, title = "밤일 · 내일 기사 미리 보기", en = "Night · Tomorrow's story", ja = "夜 · 明日の記事を先読み" },
            ("밤마다 한 종목을 골라, 내일 나올 기사가 호재인지 악재인지 미리 읽습니다.", "내일 무엇을 사고팔지 밤에 정해 두고 싶을 때. 한 번 볼 때마다 150만 돈이 듭니다.", "장이 끝난 뒤의 밤 준비 화면. 하룻밤에 한 건입니다."),
            ("Each night, pick one stock and read whether tomorrow's story about it is good or bad news.", "You want to decide tonight what to buy or sell tomorrow. Each look costs 1.5M DON.", "The night prep screen after the close. One per night."),
            ("毎晩、銘柄を一つ選び、明日出る記事が好材料か悪材料かを先に読みます。", "明日の売買を夜のうちに決めておきたい時。1回見るごとに150万ドンかかります。", "引け後の夜の準備画面。一晩に一件です。")),
        Written(new UnlockItem { authored = true, id = NightPlantUnlock, requires = NightTipUnlock, cost = 10_000_000, minutes = 60, title = "밤일 · 기사 내보내기", en = "Night · Plant a story", ja = "夜 · 記事を仕込む" },
            ("밤에 한 종목을 골라, 내일 개장 직후에 나갈 호재나 악재 기사를 내가 정합니다.", "들고 있는 종목을 띄우거나, 싸게 사고 싶은 종목을 눌러 두고 싶을 때. 한 번에 300만 돈이 들고, 정정 기사가 따라붙을 수 있습니다.", "장이 끝난 뒤의 밤 준비 화면. 하룻밤에 한 건입니다."),
            ("At night, pick one stock and choose a good or bad story to run right after tomorrow's open.", "You want to lift a stock you hold, or push down one you want to buy cheap. Each story costs 3M DON, and a correction may follow.", "The night prep screen after the close. One per night."),
            ("夜に銘柄を一つ選び、明日の寄り付き直後に出る好材料・悪材料の記事を自分で決めます。", "持っている銘柄を上げたい時、安く買いたい銘柄を下げておきたい時。1回300万ドンかかり、訂正記事が続くことがあります。", "引け後の夜の準備画面。一晩に一件です。")),
        Written(new UnlockItem { authored = true, id = NightTeaseUnlock, cost = 2_000_000, minutes = 30, title = "밤일 · 리딩방 예고", en = "Night · Tease my room", ja = "夜 · 部屋に予告" },
            ("밤에 내 리딩방 회원들에게 내일 볼 종목을 미리 알려 둡니다. 개장하자마자 회원들이 그 종목을 삽니다.", "개장 직후 한 종목에 매수세를 몰아 주고 싶을 때. 회원이 있는 내 리딩방이 있어야 합니다.", "장이 끝난 뒤의 밤 준비 화면."),
            ("At night, tell your room's members which stock to watch tomorrow. They buy it the moment the market opens.", "You want a wave of buying in one stock right at the open. Needs a room of your own with members.", "The night prep screen after the close."),
            ("夜に自分の部屋の会員へ明日見る銘柄を知らせておきます。寄り付きと同時に会員がその銘柄を買います。", "寄り付き直後に一つの銘柄へ買いを集めたい時。会員のいる自分の部屋が必要です。", "引け後の夜の準備画面。")),
    };
    private static string Local(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>해금 탭에 함께 보여 주는, 다른 곳에서 열리는 해금들. 성장하는 순서대로 놓는다.</summary>
    private UnlockItem[] ExternalUnlocks()
    {
        string turnover = rules.tacticCloseTurnover.ToString("N0"), dawnTurnover = rules.dawnRoomTurnover.ToString("N0"), dawnFee = rules.dawnRoomDailyFee.ToString("N0");
        return new[]
        {
            Written(new UnlockItem { external = true, id = DawnUnlock, cost = rules.dawnRoomCost, title = "새벽 관찰실", en = "Dawn Watch Room", ja = "夜明けの観察室" },
                ("세력이 지금 어느 종목을 사고파는지 실제 주문을 엿보고, 종목 분석 AI에게 물어볼 수 있습니다.", "세력이 값을 올리기 전에 먼저 타고 싶을 때. 누적 거래대금 " + dawnTurnover + "돈부터 열 수 있고, 들어가 있는 동안 하루 " + dawnFee + "돈이 나갑니다.", "오른쪽 모니터 채팅의 '새벽방' 탭."),
                ("See the whale's real orders as they happen, and ask the stock analysis AI.", "You want to get in before the whale lifts a stock. Opens from " + dawnTurnover + " DON lifetime turnover, and costs " + dawnFee + " DON a day while you stay.", "The Dawn tab of the chat on the right monitor."),
                ("仕手筋が今どの銘柄を売買しているか実際の注文をのぞき、銘柄分析AIに質問できます。", "仕手筋が値を上げる前に先に乗りたい時。累計売買代金" + dawnTurnover + "ドンから開けられ、入室中は1日" + dawnFee + "ドンかかります。", "右モニターのチャットの「夜明け」タブ。")),
            Written(new UnlockItem { external = true, id = CloseUnlock, title = "작전 도구 · 종가 관리", en = "Tactic · Mark the close", ja = "仕掛け · 終値の吊り上げ" },
                ("예약해 두면 장 마감 40초 전부터 저절로 사들여 오늘 종가를 높게 끝냅니다. 불붙이기(한 번에 끌어올리기)와 내려찍기(한 번에 끌어내리기)도 함께 열립니다.", "내일 아침 '어제 올랐네' 하고 따라 사는 개미를 부르고 싶을 때.", "두 번째 태블릿(E)의 '작전' 탭. 누적 거래대금 " + turnover + "돈이 되면 저절로 열립니다."),
                ("Arm it and it buys through the last 40 seconds by itself to print a higher close. Ignite (lift in one shot) and Slam (drop in one shot) open with it.", "You want small investors to chase tomorrow morning because it closed up.", "The Tactics tab on the second tablet (E). Opens by itself at " + turnover + " DON lifetime turnover."),
                ("予約しておくと引けの40秒前から自動で買い上げ、今日の終値を高くします。点火(一気に上げる)と叩き落とし(一気に下げる)も一緒に開きます。", "明日の朝「昨日上がった」と追いかけて買う個人を呼びたい時。", "2台目のタブレット(E)の「仕掛け」タブ。累計売買代金が" + turnover + "ドンになると自動で開きます。")),
            Written(new UnlockItem { external = true, id = WashUnlock, title = "작전 도구 · 거래량 부풀리기", en = "Tactic · Paint the tape", ja = "仕掛け · 出来高の水増し" },
                ("차명 계좌와 주고받아 거래가 활발한 것처럼 보이게 합니다. 45초 동안 저절로 돌고, 내 돈은 거의 들지 않습니다.", "조용한 종목에 개미들의 눈길을 끌고 싶을 때. 그 종목 주식을 조금 갖고 있어야 합니다.", "두 번째 태블릿(E)의 '작전' 탭. 내 리딩방을 열면 함께 열립니다."),
                ("Trade back and forth with a nominee account so the stock looks busy. Runs by itself for 45 seconds and costs you almost nothing.", "You want eyes on a quiet stock. You need to hold a few shares of it.", "The Tactics tab on the second tablet (E). Opens when you open your own room."),
                ("名義口座と売り買いして、取引が活発に見えるようにします。45秒間自動で動き、自分のお金はほとんどかかりません。", "静かな銘柄に個人の注目を集めたい時。その銘柄の株を少し持っている必要があります。", "2台目のタブレット(E)の「仕掛け」タブ。自分のルームを開くと一緒に開きます。")),
            Written(new UnlockItem { external = true, id = FoundUnlock, cost = rules.companyFoundingCost, title = "기업 설립", en = "Found a company", ja = "会社設立" },
                ("내 회사가 생깁니다. 낸 돈의 대부분은 회사 자본금이 되고, 매일 사업 수입이 들어옵니다. 허수 매수벽도 함께 열립니다.", "매매 말고도 꾸준한 수입을 얻고, 나중에 상장시켜 더 큰 돈을 굴리고 싶을 때.", "첫 번째 태블릿(Q)의 '내 기업' 탭."),
                ("Your own company. Most of what you pay becomes its capital, and it earns business income every day. The fake bid wall opens with it.", "You want steady income besides trading, and a company to list and grow later.", "The My Company tab on the first tablet (Q)."),
                ("自分の会社ができます。支払った資金の大半は会社の資本金になり、毎日事業収入が入ります。見せ板の買い壁も一緒に開きます。", "売買以外にも安定した収入が欲しい時、後で上場させてより大きな資金を動かしたい時。", "1台目のタブレット(Q)の「自社」タブ。")),
            Written(new UnlockItem { external = true, id = WallUnlock, title = "작전 도구 · 허수 매수벽", en = "Tactic · Fake bid wall", ja = "仕掛け · 見せ板の買い壁" },
                ("현재가 아래에 큰 매수 주문을 보여 줘, 받쳐 주는 큰돈이 있는 것처럼 보이게 합니다. 값이 다가오면 저절로 거둡니다.", "개미들이 안심하고 사게 만들고 싶을 때. 제때 못 거두면 진짜로 사게 되니 조심하세요.", "두 번째 태블릿(E)의 '작전' 탭. 내 기업을 세우면 열립니다."),
                ("Show a big bid below the market so it looks like big money is supporting the price. It pulls itself when the price comes near.", "You want small investors to feel safe buying. If it is not pulled in time, you really buy.", "The Tactics tab on the second tablet (E). Opens when you found your company."),
                ("現在値の下に大きな買い注文を見せ、支える大口がいるように見せます。値が近づくと自動で取り消します。", "個人に安心して買わせたい時。取り消しが遅れると本当に買ってしまうので注意してください。", "2台目のタブレット(E)の「仕掛け」タブ。自社を設立すると開きます。")),
        };
    }
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
        /// <summary>글을 언어마다 여기 적어 둔 항목(번역 표를 거치지 않는다). 여는 방식은 보통 해금과 같다.</summary>
        [NonSerialized] public bool authored;
        /// <summary>제목·설명을 번역 표 대신 여기 적힌 글로 보여 주는지.</summary>
        public bool Written => external || authored;
        [NonSerialized] public string en, ja, enDescription, jaDescription;
        /// <summary>한 줄 요약(사면 무엇이 생기는가). 프리뷰 그림 아래에 이것만 적는다.</summary>
        [NonSerialized] public string summary, enSummary, jaSummary;
        public string LocalSummary => string.IsNullOrEmpty(summary) ? LocalDescription : GameLocale.Code == "ja" ? jaSummary : GameLocale.Code == "en" ? enSummary : summary;
        /// <summary>이 항목을 골랐을 때 보여 줄 그림.</summary>
        public MiniPreview.Kind Preview => id == CrowdValueUnlock ? MiniPreview.Kind.CrowdValue : id == RealValueUnlock ? MiniPreview.Kind.RealValue : id == NightTipUnlock ? MiniPreview.Kind.NightTip
            : id == NightPlantUnlock ? MiniPreview.Kind.NightPlant : id == NightTeaseUnlock ? MiniPreview.Kind.NightTease : id == DawnUnlock ? MiniPreview.Kind.Dawn : id == FoundUnlock ? MiniPreview.Kind.Found
            : id == CloseUnlock ? MiniPreview.Kind.Close : id == WashUnlock ? MiniPreview.Kind.Wash : id == WallUnlock ? MiniPreview.Kind.Wall : MiniPreview.Kind.None;
        public string LocalTitle => !Written ? title : GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : title;
        public string LocalDescription => !Written ? description : GameLocale.Code == "ja" ? jaDescription : GameLocale.Code == "en" ? enDescription : description;
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
                        // 차트 정보 두 가지는 글과 값을 코드에 적어 두었다(ChartUnlocks). 설정 파일의 같은 항목은 건너뛴다.
                        if (item != null && !string.IsNullOrEmpty(item.id) && item.cost >= 0 && item.minutes >= 0 && item.id != CrowdValueUnlock && item.id != RealValueUnlock)
                        {
                            valid.Add(item);
                        }
                    }
                }

                // 밤 준비의 해금, 그리고 다른 곳에서 열리는 해금을 뒤에 잇는다.
                valid.InsertRange(0, ChartUnlocks());
                valid.AddRange(NightUnlocks());
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
            if (item.id == WallUnlock) { reason = Local("내 기업을 세우면 열립니다", "Opens when you found your company", "自社を設立すると開きます"); return false; }
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
            if (item != null && item.authored) { MarketEventFeed.Instance?.Notify(Local("해금 완료 · ", "Unlocked · ", "解放 · ") + item.LocalTitle); }
            else { MarketEventFeed.Instance?.Notify("해금 완료 · " + (item == null ? id : item.title)); }
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
