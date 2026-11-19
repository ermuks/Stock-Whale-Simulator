using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 태블릿 1의 "밤 준비" 화면. 위에서 종목을 고르고, 아래 세 칸(기사 심기 · 내일 기사 · 리딩방 예고)에서 하나씩 한다.
/// 규칙은 NightPrep에 있다. 여기는 보여 주고 누르는 것만 한다.
/// </summary>
public sealed class NightPrepUI : MonoBehaviour
{
    private static readonly Color Warn = new Color32(242, 188, 100, 255), Dim = new Color32(110, 129, 142, 255);
    private TabletAccountUI tablet;
    private TextMeshProUGUI stockName, price, change, feedback, lockLine;
    private Image fatigueFill, veil, lockIcon;
    private readonly TextMeshProUGUI[] titles = new TextMeshProUGUI[3], costs = new TextMeshProUGUI[3], bodies = new TextMeshProUGUI[3];
    private readonly Image[] marks = new Image[3];
    private readonly Image[][] borders = new Image[3][];
    private Button plantUp, plantDown, tip, tease;
    private TextMeshProUGUI plantUpLabel, plantDownLabel, tipLabel, teaseLabel;
    private Stock selected;
    private float next;

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    public static NightPrepUI Install(TabletAccountUI owner)
    {
        if (owner == null || owner.second) { return null; }
        var image = TabletKitStyle.Surface("Night Prep", owner.contentRoot.transform, .02f, .18f, .96f, .80f, TabletKitStyle.Background);
        image.name = "Panel :: Night Prep";
        var screens = new List<GameObject>(owner.screens) { image.gameObject }; owner.screens = screens.ToArray();
        var view = image.gameObject.AddComponent<NightPrepUI>(); view.tablet = owner; view.Build();
        image.gameObject.SetActive(false);
        return view;
    }

    public void Show() { gameObject.SetActive(true); next = 0f; Refresh(); }

    private void Build()
    {
        // 종목 고르기: 화살표 둘과 큰 이름, 오른쪽에 종가와 등락.
        TabletKitStyle.Action("Night Previous Stock", transform, "‹", 0, .0f, .06f, .10f, () => Step(-1));
        TabletKitStyle.Action("Night Next Stock", transform, "›", .94f, .0f, .06f, .10f, () => Step(1));
        stockName = TabletKitStyle.Label("Night Stock", transform, "", .08f, .0f, .46f, .10f, 44);
        price = TabletKitStyle.Label("Night Price", transform, "", .55f, .0f, .22f, .10f, 40, true); TabletKitStyle.TextStyle(price, 40, true); price.alignment = TextAlignmentOptions.MidlineRight;
        change = TabletKitStyle.Label("Night Change", transform, "", .78f, .0f, .14f, .10f, 30, true); TabletKitStyle.TextStyle(change, 30, true); change.alignment = TextAlignmentOptions.MidlineRight;
        // 피로 막대: 밤일은 피로를 쓴다. 끝까지 차면 내일을 통째로 잃는다.
        var track = TabletKitStyle.Surface("Night Fatigue Track", transform, 0, .125f, 1, .014f, TabletKitStyle.Line);
        fatigueFill = TabletKitStyle.Surface("Night Fatigue Fill", track.transform, 0, 0, 1, 1, TabletKitStyle.Accent);

        string[] icons = { "news", "search", "bell" };
        for (int i = 0; i < 3; i++)
        {
            float x = i * .345f;
            var card = TabletKitStyle.Surface("Night Card " + i, transform, x, .18f, .31f, .66f, TabletKitStyle.Panel);
            TabletKitStyle.Glyph(icons[i], card.transform, .07f, .06f, .14f, .14f);
            titles[i] = TabletKitStyle.Label("Night Card Title " + i, card.transform, "", .26f, .06f, .70f, .14f, 32);
            costs[i] = TabletKitStyle.Label("Night Card Cost " + i, card.transform, "", .07f, .23f, .86f, .09f, 26, false); TabletKitStyle.TextStyle(costs[i], 26, true); costs[i].color = TabletKitStyle.Muted;
            marks[i] = TabletKitStyle.Glyph("check", card.transform, .07f, .38f, .12f, .12f); marks[i].color = TabletKitStyle.Accent;
            bodies[i] = TabletKitStyle.Label("Night Card Body " + i, card.transform, "", .07f, .37f, .86f, .30f, 26);
            bodies[i].textWrappingMode = TextWrappingModes.Normal; bodies[i].alignment = TextAlignmentOptions.TopLeft; bodies[i].overflowMode = TextOverflowModes.Ellipsis;
            borders[i] = TabletCall.Border(card.transform, TabletKitStyle.Accent, .012f, .008f);
        }
        var first = transform.Find("Image :: Night Card 0"); var second = transform.Find("Image :: Night Card 1"); var third = transform.Find("Image :: Night Card 2");
        plantUp = TabletKitStyle.Action("Night Plant Up", first, "", .07f, .76f, .41f, .17f, () => Plant(true)); plantUpLabel = plantUp.GetComponentInChildren<TextMeshProUGUI>();
        plantDown = TabletKitStyle.Action("Night Plant Down", first, "", .52f, .76f, .41f, .17f, () => Plant(false)); plantDownLabel = plantDown.GetComponentInChildren<TextMeshProUGUI>();
        tip = TabletKitStyle.Action("Night Tip", second, "", .07f, .76f, .86f, .17f, Tip); tipLabel = tip.GetComponentInChildren<TextMeshProUGUI>();
        tease = TabletKitStyle.Action("Night Tease", third, "", .07f, .76f, .86f, .17f, Tease); teaseLabel = tease.GetComponentInChildren<TextMeshProUGUI>();
        feedback = TabletKitStyle.Label("Night Feedback", transform, "", 0, .87f, 1, .07f, 26); feedback.color = Warn;

        // 낮에는 잠겨 있다: 칸 위에 어두운 막과 자물쇠, 한 줄.
        veil = TabletKitStyle.Surface("Night Veil", transform, 0, .18f, 1, .66f, new Color(TabletKitStyle.Background.r, TabletKitStyle.Background.g, TabletKitStyle.Background.b, .82f)); veil.raycastTarget = true;
        lockIcon = TabletKitStyle.Glyph("lock", veil.transform, .46f, .26f, .08f, .16f);
        lockLine = TabletKitStyle.Label("Night Lock Line", veil.transform, "", .1f, .48f, .8f, .12f, 32); lockLine.alignment = TextAlignmentOptions.Center; lockLine.color = TabletKitStyle.Muted;
    }

    private List<Stock> Listed()
    {
        var list = new List<Stock>(); var manager = StockManager.Instance;
        if (manager == null) { return list; }
        foreach (var stock in manager.stocks) { if (stock != null && stock.IsListed && !stock.IsDelisting && !stock.IsDelisted) { list.Add(stock); } }
        return list;
    }

    private void Step(int delta)
    {
        var list = Listed(); if (list.Count == 0) { return; }
        int at = list.IndexOf(selected); selected = list[((at < 0 ? 0 : at + delta) % list.Count + list.Count) % list.Count];
        Refresh();
    }

    private void Plant(bool positive) { NightPrep.Instance?.Plant(selected, positive); Refresh(); }
    private void Tip() { NightPrep.Instance?.BuyTip(selected); Refresh(); }
    private void Tease() { NightPrep.Instance?.Tease(selected); Refresh(); }

    void OnEnable() { next = 0f; }
    void Update() { if (Time.unscaledTime < next) { return; } next = Time.unscaledTime + .5f; Refresh(); }

    private static void Lit(TextMeshProUGUI label, string text) { if (!GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); } }

    public void Refresh()
    {
        var prep = NightPrep.Instance; var manager = StockManager.Instance;
        if (prep == null || manager == null) { return; }
        if (selected == null || !selected.IsListed) { selected = manager.CurrentStock != null && manager.CurrentStock.IsListed ? manager.CurrentStock : Listed().Count > 0 ? Listed()[0] : null; }
        bool night = prep.IsNight;

        if (selected != null)
        {
            DeviceTerminalUI.Set(stockName, selected.stockName);
            Lit(price, PriceTickPolicy.FormatPrice(selected.currentPrice));
            bool known = StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyChange(selected, out decimal moved);
            decimal shown = known ? StockStatistics.Instance.TryGetDailyChange(selected, out decimal value) ? value : 0m : 0m;
            Lit(change, known ? shown.ToString("+0.0;-0.0;0.0") + "%" : ""); change.color = MarketColors.ForChange(shown, TabletKitStyle.Muted);
        }

        var rest = PlayerRestController.Instance; float tired = rest == null ? 0f : Mathf.Clamp01((float)rest.State.fatigue / 100f);
        fatigueFill.rectTransform.anchorMax = new Vector2(tired, 1); fatigueFill.color = tired >= .85f ? Warn : TabletKitStyle.Accent;

        Lit(titles[0], Word("기사 심기", "Plant a story", "記事を仕込む"));
        Lit(titles[1], Word("내일 기사", "Tomorrow's story", "明日の記事"));
        Lit(titles[2], Word("리딩방 예고", "Tease my room", "部屋に予告"));
        Lit(costs[0], DeviceTerminalUI.Money(NightPrep.PlantCost));
        Lit(costs[1], DeviceTerminalUI.Money(NightPrep.TipCost));
        var room = prep.MyRoom();
        Lit(costs[2], room == null ? "—" : room.Members.Count + Word("명", " members", "人"));

        // 칸 1: 맡긴 기사. 종목과 방향만 보인다.
        bool planted = prep.Planted != null;
        marks[0].gameObject.SetActive(planted);
        if (planted) { marks[0].sprite = TabletKitStyle.Icon(prep.PlantedPositive ? "growth" : "loss"); marks[0].color = prep.PlantedPositive ? MarketColors.Up : MarketColors.Down; }
        Place(bodies[0], planted); if (planted) { DeviceTerminalUI.Set(bodies[0], prep.Planted.stockName); } else { Lit(bodies[0], ""); }
        Lit(plantUpLabel, Word("호재", "Good news", "好材料")); Lit(plantDownLabel, Word("악재", "Bad news", "悪材料"));
        bool canPlant = prep.CanPlant(selected, out _);
        plantUp.interactable = plantDown.interactable = canPlant;
        Paint(plantUp, plantUpLabel, canPlant, MarketColors.Up); Paint(plantDown, plantDownLabel, canPlant, MarketColors.Down);
        TabletCall.Show(borders[0], planted, TabletKitStyle.Accent);

        // 칸 2: 받아 온 기사. 제목이 곧 정보다.
        var held = prep.LastTip; bool hasTip = held != null;
        marks[1].gameObject.SetActive(hasTip && held.Found);
        if (hasTip && held.Found) { marks[1].sprite = TabletKitStyle.Icon(held.Positive ? "growth" : "loss"); marks[1].color = held.Positive ? MarketColors.Up : MarketColors.Down; }
        Place(bodies[1], hasTip && held.Found);
        if (!hasTip) { Lit(bodies[1], ""); }
        else if (held.Found) { DeviceTerminalUI.Set(bodies[1], held.Title); }
        else { Lit(bodies[1], GameLocale.Text(held.Stock.stockName) + Word(" · 내일은 조용합니다", " · quiet tomorrow", " · 明日は静かです")); }
        Lit(tipLabel, Word("사 보기", "Buy it", "買って読む"));
        bool canTip = prep.CanTip(selected, out _); tip.interactable = canTip; Paint(tip, tipLabel, canTip, TabletKitStyle.Accent);
        TabletCall.Show(borders[1], hasTip, hasTip && held.Found ? (held.Positive ? MarketColors.Up : MarketColors.Down) : Dim);

        // 칸 3: 예고한 종목.
        bool teased = prep.Teased != null;
        marks[2].gameObject.SetActive(teased); if (teased) { marks[2].sprite = TabletKitStyle.Icon("check"); marks[2].color = TabletKitStyle.Accent; }
        Place(bodies[2], teased); if (teased) { DeviceTerminalUI.Set(bodies[2], prep.Teased.stockName); } else { Lit(bodies[2], ""); }
        Lit(teaseLabel, Word("예고하기", "Tell them", "予告する"));
        bool canTease = prep.CanTease(selected, out _); tease.interactable = canTease; Paint(tease, teaseLabel, canTease, TabletKitStyle.Accent);
        TabletCall.Show(borders[2], teased, TabletKitStyle.Accent);

        string[] ids = { PlayerProgression.NightPlantUnlock, PlayerProgression.NightTipUnlock, PlayerProgression.NightTeaseUnlock };
        for (int i = 0; i < 3; i++)
        {
            bool open = PlayerProgression.Has(ids[i]);
            titles[i].color = open ? TabletKitStyle.Ink : Dim; costs[i].color = open ? TabletKitStyle.Muted : Dim;
            if (!open) { marks[i].gameObject.SetActive(true); marks[i].sprite = TabletKitStyle.Icon("lock"); marks[i].color = Dim; Place(bodies[i], false); Lit(bodies[i], ""); }
        }
        Lit(feedback, night ? prep.LastResult : "");
        if (veil.gameObject.activeSelf == night) { veil.gameObject.SetActive(!night); }
        // 깔아 둔 것이 있으면 낮에도 무엇을 걸어 놨는지는 보여야 한다. 그때는 막을 옅게 하고 자물쇠만 치운다.
        bool pending = planted || teased || hasTip;
        veil.color = new Color(TabletKitStyle.Background.r, TabletKitStyle.Background.g, TabletKitStyle.Background.b, pending ? .35f : .82f);
        lockIcon.gameObject.SetActive(!pending); lockLine.gameObject.SetActive(!pending);
        Lit(lockLine, Word("장 마감 뒤에 열립니다", "Opens after the close", "引け後に開きます"));
    }

    /// <summary>표시(화살표·확인)가 있으면 글자를 그 옆으로 민다.</summary>
    private static void Place(TextMeshProUGUI body, bool marked)
    { TabletKitStyle.Place(body.rectTransform, marked ? .22f : .07f, .37f, marked ? .71f : .86f, .30f); }

    private static void Paint(Button button, TextMeshProUGUI label, bool on, Color color)
    {
        TabletKitStyle.Flat(button.image, on ? color : (Color)TabletKitStyle.Hover);
        label.color = on ? TabletKitStyle.Background : TabletKitStyle.Muted;
    }
}
