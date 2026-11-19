using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum StickyNoteLanguage { English, Korean, Japanese }

/// <summary>
/// 모니터 아래의 손글씨 메모. 게임이 깔아 둔 길을 한 장에 한 걸음씩 적어 둔다(설명문이 아니라 내가 적어 둔 할 일처럼 보이게).
/// 자리는 셋이고, 아직 못 한 일 가운데 앞의 세 가지가 붙어 있다. 하나를 해내면 그 종이가 떨어지고, 잠시 뒤 같은 자리에 다음 할 일이 새로 붙는다.
/// 순서를 강제하지 않는다: 건너뛰어 이룬 일은 붙지 않고 넘어간다.
/// </summary>
public sealed class InvestorJourneyNotes : MonoBehaviour
{
    [Serializable]
    public sealed class Note
    {
        public StickyNotePaper paper;
        public TextMeshProUGUI number, title, detail, footer, status;
    }
    [Serializable]
    public sealed class Copy
    {
        public string[] titles;
        public string observing, waiting, firstSignal, turnover, profit, preparing, eligible, operate, complete, current;
        public string tenThousand, hundredMillion, trillion, won;
    }
    public Note[] notes = new Note[3];
    public Transform framingBottom;
    public StickyNoteLanguage language = StickyNoteLanguage.Korean;
    /// <summary>지금 해야 할 일(가장 앞의 못 한 일). 다 했으면 -1.</summary>
    public int CurrentStage { get; private set; } = -1;
    public const int GoalCount = 12;
    /// <summary>할 일 g의 완료 여부는 저장 값의 (g+3)번째 비트에 적는다. 아래 세 비트는 예전 메모 세 장(가입·개설 자금·내 리딩방)이다.</summary>
    public const int GoalShift = 3;
    private static readonly Color Ink = new Color32(46, 43, 35, 255);
    private static readonly Color SecondaryInk = new Color32(88, 80, 65, 255);
    private static readonly Color ActiveInk = new Color32(122, 45, 30, 255);
    private int doneMask;
    private bool initialized;
    private Copy copy;
    private StickyNoteLanguage loadedLanguage = (StickyNoteLanguage)(-1);
    private int localeRevision = -1;
    // 자리마다: 새 종이를 찍어 낼 원본, 지금 붙어 있는 종이, 그 종이에 적힌 할 일, 새 종이가 붙을 시각.
    private readonly GameObject[] templates = new GameObject[3];
    private readonly Note[] slots = new Note[3];
    private readonly int[] slotGoal = { -1, -1, -1 };
    private readonly float[] refillAt = { -1f, -1f, -1f };
    private readonly Queue<GameObject> fallen = new();
    private const float RefillDelay = 2.4f; private const int FallenKept = 6;

    /// <summary>예전 저장의 세 비트를 새 할 일 번호로 옮긴다: 가입 → 2, 개설 자금 → 4, 내 리딩방 → 5.</summary>
    public static int Completed(int savedMask)
    {
        int mask = savedMask >> GoalShift;
        if ((savedMask & 1) != 0) { mask |= 1 << 2; }
        if ((savedMask & 2) != 0) { mask |= 1 << 4; }
        if ((savedMask & 4) != 0) { mask |= 1 << 5; }
        return mask & ((1 << GoalCount) - 1);
    }

    public void InitializeMilestones(int savedMask)
    {
        if (initialized)
        { return; }
        initialized = true; doneMask = Completed(savedMask);
        for (int i = 0; i < notes.Length && i < 3; i++)
        {
            if (notes[i] == null || notes[i].paper == null) { continue; }
            // 붙어 있는 그대로를 원본으로 떠 둔다. 다음 할 일은 이 원본에서 새 종이를 찍어 붙인다.
            templates[i] = Instantiate(notes[i].paper.gameObject, notes[i].paper.transform.parent);
            templates[i].name = notes[i].paper.gameObject.name + " (template)"; templates[i].SetActive(false);
            slots[i] = notes[i];
        }
        // 이미 이룬 일은 붙이지 않는다. 못 한 일을 앞에서부터 세 자리에 나눠 붙인다.
        for (int i = 0; i < 3; i++)
        {
            int goal = NextGoal();
            slotGoal[i] = goal;
            if (goal < 0 && slots[i] != null) { slots[i].paper.HideCompleted(); slots[i] = null; }
        }
    }
    /// <summary>아직 못 했고 어느 자리에도 붙어 있지 않은 할 일 가운데 가장 앞의 것.</summary>
    private int NextGoal()
    {
        for (int goal = 0; goal < GoalCount; goal++)
        {
            if ((doneMask & (1 << goal)) != 0 || Array.IndexOf(slotGoal, goal) >= 0) { continue; }
            return goal;
        }
        return -1;
    }

    public void SetLanguage(StickyNoteLanguage value)
    {
        if (value != StickyNoteLanguage.English && value != StickyNoteLanguage.Korean && value != StickyNoteLanguage.Japanese)
        { throw new ArgumentOutOfRangeException(nameof(value)); }
        language = value; EnsureCopy(); CurrentStage = -2;
    }

    private void EnsureCopy()
    {
        if (GameLocale.Ready && localeRevision != GameLocale.Revision)
        {
            localeRevision = GameLocale.Revision;
            language = GameLocale.Code == "en" ? StickyNoteLanguage.English : GameLocale.Code == "ja" ? StickyNoteLanguage.Japanese : StickyNoteLanguage.Korean;
            copy = null;
        }
        if (copy != null && loadedLanguage == language)
        { return; }
        string code;
        switch (language)
        {
            case StickyNoteLanguage.English: code = "en"; break;
            case StickyNoteLanguage.Korean: code = "ko"; break;
            case StickyNoteLanguage.Japanese: code = "ja"; break;
            default: throw new ArgumentOutOfRangeException(nameof(language));
        }
        var data = Resources.Load<TextAsset>("Localization/StickyNotes/" + code);
        if (data == null)
        { throw new InvalidOperationException("Missing sticky note language: " + code); }
        copy = JsonUtility.FromJson<Copy>(data.text); loadedLanguage = language; CurrentStage = -2;
        if (GameLocale.Ready)
        {
            for (int i = 0; i < copy.titles.Length; i++) { copy.titles[i] = GameLocale.Get("notes.title." + i,copy.titles[i]); }
            foreach (var field in typeof(Copy).GetFields())
            { if (field.FieldType == typeof(string)) { field.SetValue(copy,GameLocale.Get("notes." + field.Name,(string)field.GetValue(copy))); } }
        }
    }
    private string L(string korean, string english, string japanese) => language == StickyNoteLanguage.Japanese ? japanese : language == StickyNoteLanguage.English ? english : korean;

    /// <summary>할 일 하나의 지금 모습: 제목, 진행 정도, 아래 한 줄, 이뤘는지.</summary>
    private void Describe(int goal, NpcLeadingRoom npc, PlayerProgression career, out string title, out string detail, out string footer, out bool done)
    {
        var economy = CorporateEconomy.Instance; var account = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        decimal cash = account == null ? 0m : decimal.Floor(PlayerAccounts.Available(account));
        title = detail = footer = ""; done = false;
        switch (goal)
        {
            case 0:
                title = L("하나 사 보기", "Buy one stock", "まず一つ買う"); detail = L("종목 목록에서 고르기", "pick from the stock list", "銘柄リストから選ぶ");
                footer = L("채팅방이 떠드는 종목부터", "start with what chat is loud about", "チャットが騒ぐ銘柄から"); done = career.Turnover > 0m; break;
            case 1:
                title = L("새벽 관찰실", "Dawn Watch Room", "夜明けの観察室");
                detail = career.DawnEligible ? L("해금 가능 · ", "can unlock · ", "解放可能 · ") + Amount(career.DawnCost) : L("거래 ", "traded ", "売買 ") + Amount(decimal.Floor(career.Turnover)) + " / " + Amount(career.DawnTurnoverNeeded);
                footer = L("세력의 주문 엿보기", "watch a whale's real orders", "仕手の注文をのぞく"); done = career.DawnUnlocked; break;
            case 2:
                title = copy.titles[0]; detail = npc.JoinRequested ? copy.waiting : copy.observing; footer = copy.firstSignal; done = npc.PlayerJoined; break;
            case 3:
            {
                decimal cheapest = 0m;
                if (economy != null) { foreach (var rung in economy.Ladder()) { if (!rung.Controlled && (cheapest <= 0m || rung.CostToControl < cheapest)) { cheapest = rung.CostToControl; } } }
                title = L("작은 회사 사기", "Buy a small company", "小さな会社を買う"); detail = Amount(cash) + " / " + Amount(decimal.Floor(cheapest));
                footer = L("내 기업 탭 · 인수 사다리", "My company tab · ladder", "自社タブ · 買収のはしご"); done = economy != null && economy.MyCompanies().Count > 0; break;
            }
            case 4:
                title = copy.titles[1]; detail = Amount(decimal.Floor(career.OpeningCash)) + " / " + Amount(career.OpeningCost);
                footer = copy.profit + " " + Amount(career.RealizedProfit); done = career.Eligible || career.CanOperateRooms; break;
            case 5:
                title = copy.titles[2]; detail = career.Eligible ? copy.eligible : copy.preparing; footer = copy.operate; done = career.CanOperateRooms; break;
            case 6:
            {
                var room = InvestorSociety.Instance == null ? null : InvestorSociety.Instance.GetRoom(1); int members = room == null ? 0 : room.Members.Count;
                title = L("첫 회원 초대", "Invite a member", "最初の会員を招く"); detail = L("라운지 채팅의 초대 단추", "invite button in lounge chat", "ラウンジの招待ボタン");
                footer = L("회원 ", "members ", "会員 ") + members; done = members > 0; break;
            }
            case 7:
                title = L("종가 관리", "Mark the close", "終値の吊り上げ"); detail = L("거래 ", "traded ", "売買 ") + Amount(decimal.Floor(career.Turnover)) + " / " + Amount(career.CloseTurnoverNeeded);
                footer = L("두 번째 태블릿 · 작전", "second tablet · tactics", "2台目のタブレット · 仕掛け"); done = PlayerTactics.Unlocked(PlayerTactics.Tool.Close); break;
            case 8:
                title = L("기업 설립", "Found a company", "会社設立"); detail = Amount(cash) + " / " + Amount(career.FoundingCost);
                footer = L("해금 탭", "Unlocks tab", "解放タブ"); done = career.CompanyFounded; break;
            case 9:
            {
                var own = economy == null ? null : economy.PlayerCompany; var growth = own?.business?.growth;
                title = L("내 기업 상장", "Take it public", "自社を上場"); detail = growth == null ? "—" : Amount(decimal.Floor(growth.Appraisal)) + " / " + Amount(economy.GrowthRules.listingValue);
                footer = L("성장·상장 화면", "Growth & IPO screen", "成長・上場の画面"); done = own != null && own.IsListed; break;
            }
            case 10:
            {
                decimal cheapest = 0m; bool taken = false;
                if (economy != null)
                {
                    foreach (var rung in economy.Ladder())
                    {
                        if (!rung.Stock.IsListed) { continue; }
                        if (rung.Controlled) { taken = true; } else if (cheapest <= 0m || rung.CostToControl < cheapest) { cheapest = rung.CostToControl; }
                    }
                }
                title = L("상장사 삼키기", "Swallow a listed company", "上場会社を飲み込む"); detail = Amount(cash) + " / " + Amount(decimal.Floor(cheapest));
                footer = L("지분 50% 넘기기", "own more than 50%", "持ち株50%超"); done = taken; break;
            }
            default:
            {
                var own = economy == null ? null : economy.PlayerCompany; var growth = own?.business?.growth;
                decimal cap = own == null || !own.IsListed ? 0m : own.currentPrice * own.issuedShares;
                title = L("최종 목표", "Final goal", "最終目標"); detail = economy == null ? "—" : Amount(decimal.Floor(cap)) + " / " + Amount(economy.GrowthRules.endingMarketCap);
                footer = L("그러면 문이 열린다", "then the door opens", "そうすれば扉が開く"); done = growth != null && growth.EndingDay > 0; break;
            }
        }
    }

    public void Refresh(NpcLeadingRoom npc, PlayerProgression career, int step)
    {
        if (npc == null || career == null || !initialized)
        { return; }
        EnsureCopy();
        // 이룬 일을 적어 둔다(붙어 있지 않은 일도 본다: 건너뛰어 이룬 일은 다시 붙지 않는다).
        int achieved = 0;
        for (int goal = 0; goal < GoalCount; goal++)
        {
            if ((doneMask & (1 << goal)) != 0) { continue; }
            Describe(goal, npc, career, out _, out _, out _, out bool done);
            if (done) { achieved |= 1 << goal; }
        }
        if (achieved != 0) { career.RememberJourney(achieved << GoalShift); }
        float now = Time.time, delay = .45f;
        for (int i = 0; i < 3; i++)
        {
            int goal = slotGoal[i];
            if (goal >= 0 && slots[i] != null && (achieved & (1 << goal)) != 0)
            {
                // 해냈다: 종이가 떨어지고, 잠시 뒤 이 자리에 다음 할 일이 붙는다.
                Set(slots[i].status, copy.complete); slots[i].status.color = SecondaryInk; slots[i].title.color = Ink;
                slots[i].paper.Release(delay); fallen.Enqueue(slots[i].paper.gameObject); delay += .65f;
                slots[i] = null; slotGoal[i] = -1; refillAt[i] = now + delay + RefillDelay; CurrentStage = -2;
            }
        }
        doneMask |= achieved;
        while (fallen.Count > FallenKept) { var old = fallen.Dequeue(); if (old != null) { Destroy(old); } }
        for (int i = 0; i < 3; i++)
        {
            if (slots[i] != null || refillAt[i] < 0f || now < refillAt[i] || templates[i] == null) { continue; }
            refillAt[i] = -1f;
            int goal = NextGoal();
            if (goal < 0) { continue; }
            var fresh = Instantiate(templates[i], templates[i].transform.parent);
            fresh.name = "Area :: Sticky Note " + (goal + 1); fresh.SetActive(true);
            slots[i] = Bind(fresh); slotGoal[i] = goal; CurrentStage = -2;
        }
        int current = -1;
        for (int i = 0; i < 3; i++) { if (slotGoal[i] >= 0 && slots[i] != null && (current < 0 || slotGoal[i] < current)) { current = slotGoal[i]; } }
        bool restyle = current != CurrentStage; CurrentStage = current;
        for (int i = 0; i < 3; i++)
        {
            var note = slots[i]; int goal = slotGoal[i];
            if (note == null || goal < 0) { continue; }
            Describe(goal, npc, career, out string title, out string detail, out string footer, out _);
            Set(note.number, (goal + 1).ToString("00")); Set(note.title, title); Set(note.detail, detail); Set(note.footer, footer);
            if (!restyle) { continue; }
            Set(note.status, goal == current ? copy.current : "");
            note.title.color = goal == current ? ActiveInk : Ink;
            note.status.color = goal == current ? ActiveInk : SecondaryInk;
        }
    }
    /// <summary>새로 찍어 낸 종이에서 글자 칸을 찾아 묶는다.</summary>
    private static Note Bind(GameObject paper)
    {
        var note = new Note { paper = paper.GetComponent<StickyNotePaper>() };
        foreach (var label in paper.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            switch (label.name)
            {
                case "Text :: Sticky Number": note.number = label; break;
                case "Text :: Sticky Title": note.title = label; break;
                case "Text :: Sticky Detail": note.detail = label; break;
                case "Text :: Sticky Goal": note.footer = label; break;
                case "Text :: Sticky Status": note.status = label; break;
            }
        }
        return note;
    }

    private string Amount(decimal value)
    {
        decimal magnitude = Math.Abs(value);
        if (language == StickyNoteLanguage.English)
        {
            decimal divisor = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 1000000000m ? 1000000000m : magnitude >= 1000000m ? 1000000m : magnitude >= 1000m ? 1000m : 1m;
            string suffix = divisor == 1000000000000m ? "T" : divisor == 1000000000m ? "B" : divisor == 1000000m ? "M" : divisor == 1000m ? "K" : "";
            return (value / divisor).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + suffix;
        }
        if (magnitude < 10000m)
        { return value.ToString("#,##0;-#,##0;0", System.Globalization.CultureInfo.InvariantCulture); }
        decimal unit = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 100000000m ? 100000000m : 10000m;
        string ending = unit == 1000000000000m ? copy.trillion : unit == 100000000m ? copy.hundredMillion : copy.tenThousand;
        return (value / unit).ToString("#,##0.#;-#,##0.#;0", System.Globalization.CultureInfo.InvariantCulture) + ending;
    }
    private static void Set(TMP_Text label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label,value))
        { GameLocalizedText.Set(label,value,true); }
    }
}
