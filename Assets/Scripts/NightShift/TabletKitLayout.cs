using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reuses the existing account pages and listeners, changing only their internal layout.</summary>
public sealed class TabletKitLayout : MonoBehaviour
{
    private TabletAccountUI owner;
    private TextMeshProUGUI lockHint;
    /// <summary>작업 계좌가 언제 열리는지 한 줄로 알려 준다.</summary>
    private void RefreshLockHint()
    {
        if (lockHint==null || owner==null || owner.accounts==null) { return; }
        string amount=DeviceTerminalUI.Money(owner.accounts.unlockAssets);
        string text=GameLocale.Code=="ja" ? "基本口座の総資産が"+amount+"を超えると作業口座が開きます" :
            GameLocale.Code=="en" ? "The work account opens once the main account's total assets exceed "+amount :
            "기본 계좌의 총자산이 "+amount+"을 넘으면 작업 계좌가 열립니다";
        GameLocalizedText.Set(lockHint,text,true);
    }
    void OnDestroy() { GameLocale.Changed-=RefreshLockHint; }
    private readonly List<Button> buttons=new();
    private Button firstOrder; private TextMeshProUGUI firstOrderLabel, emptyLabel;
    private float nextRealized;
    private void Shortcut(Transform root,string icon,float x,float y,System.Action action)
    {
        var button=TabletKitStyle.Action("Shortcut "+icon,root,"",x,y,.062f,owner.second ? .06f : .07f,action);
        TabletKitStyle.Glyph(icon,button.transform,.22f,.22f,.56f,.56f);
    }
    private readonly List<(string ko,string en,string ja)> names=new();
    private static string Word(string ko,string en,string ja) => GameLocale.Code=="ja" ? ja : GameLocale.Code=="en" ? en : ko;
    /// <summary>탭과 제목의 이름은 언어마다 따로 적는다(번역 표를 거치지 않는다). 밤이 되면 밤 준비 탭의 아이콘이 밝아진다.</summary>
    private void RefreshNightTab()
    {
        for (int i=0;i<buttons.Count && i<names.Count;i++)
        {
            var tab=buttons[i].GetComponentInChildren<TextMeshProUGUI>(); string name=Word(names[i].ko,names[i].en,names[i].ja);
            if (!GameLocalizedText.Matches(tab,name)) { GameLocalizedText.Set(tab,name,true); }
        }
        string title=owner.second ? Word("작업대 · 주문과 작전","Desk · orders and operations","作業台 · 注文と作戦") : Word("내 자산 · 오늘 성적","My assets · today","資産 · 本日の成績");
        if (!GameLocalizedText.Matches(owner.accountTitle,title)) { GameLocalizedText.Set(owner.accountTitle,title,true); }
        int index=pages.IndexOf(TabletAccountUI.Page.Night); if (index<0) { return; }
        bool night=NightPrep.Instance!=null && NightPrep.Instance.IsNight;
        var icon=buttons[index].transform.Find("Image :: timer"); if (icon!=null) { icon.GetComponent<Image>().color=night ? TabletKitStyle.Accent : TabletKitStyle.Ink; }
    }
    private readonly TextMeshProUGUI[] todayCaptions=new TextMeshProUGUI[3], todayValues=new TextMeshProUGUI[3];
    private HoldingsMap holdingsMap;
    private HoldingsBoard holdingsBoard;
    private UiLineChart curve;
    private TextMeshProUGUI curveCaption;
    private readonly List<float> curvePoints=new(128);
    /// <summary>태블릿 캔버스의 글자 배율(디자인 1600 기준).</summary>
    private static float TabletKitScale(Transform root) { var rect=root as RectTransform; float width=rect!=null ? rect.rect.width : 0f; return width>1f ? width/1600f : 1f; }
    /// <summary>자산 지도와 오늘 자산 곡선을 다시 그린다(0.5초마다).</summary>
    private void RefreshPictures()
    {
        if (owner.second || owner.account==null) { return; }
        holdingsMap?.Refresh(owner.account);
        holdingsBoard?.Refresh(owner.account);
        if (curve==null) { return; }
        var trail=AssetTrail.Instance;
        curvePoints.Clear();
        if (trail!=null) { foreach (var point in trail.Today) { curvePoints.Add((float)point.value); } }
        // 09:00부터 지금까지를 폭 전체에 펼친다(장 초반에도 모양이 보이게). 몇 시까지의 곡선인지는 제목에 적는다.
        int last=trail!=null && trail.Today.Count>0 ? trail.Today[trail.Today.Count-1].at : 540;
        string until=(last/60).ToString("00")+":"+(last%60).ToString("00");
        Lit(curveCaption,Word("오늘 자산 흐름 · 09:00 → "+until+" · 가운데 선은 시가","Today's assets · 09:00 → "+until+" · line = opening","本日の資産推移 · 09:00 → "+until+" · 線は始値"));
        curve.SetPoints(curvePoints,trail!=null ? (float)trail.Opening : 0f,1f);
    }
    /// <summary>
    /// 오늘 성적 한 줄: 오늘 손익(지금 총자산이 개장 때보다 얼마나 늘었는가. 바깥에서 들어온 돈은 뺀다), 오늘 실현(판 것으로 확정한 손익), 오늘 거래대금.
    /// 보는 태블릿(1번)의 자산 화면에만 있다. 총자산 아래, 보유 종목 위에 둔다.
    /// </summary>
    private void BuildToday(Transform root)
    {
        if (owner.second) { return; }
        for (int i=0;i<3;i++)
        {
            float x=i*.158f;
            todayCaptions[i]=TabletKitStyle.Label("Today Caption "+i,root,"",x,.222f,.15f,.032f,20); todayCaptions[i].color=TabletKitStyle.Muted;
            todayValues[i]=TabletKitStyle.Label("Today Value "+i,root,"",x,.254f,.155f,.045f,25,true); TabletKitStyle.TextStyle(todayValues[i],25,true); todayValues[i].alignment=TextAlignmentOptions.MidlineLeft;
        }
    }
    private static void Lit(TextMeshProUGUI label,string text) { if (label!=null && !GameLocalizedText.Matches(label,text)) { GameLocalizedText.Set(label,text,true); } }
    private void RefreshRealized()
    {
        if (todayValues[0]==null || owner.events==null || owner.events.records==null || MarketDayController.Instance==null || owner.account==null) { return; }
        int today=MarketDayController.Instance.CalendarDate; decimal realized=0m, turnover=0m;
        foreach (var fill in owner.events.records.Fills)
        {
            if (TabletRecordsView.DayOf(fill.Time)!=today) { continue; }
            turnover+=fill.Price*fill.Quantity;
            if (!fill.Buy) { realized+=fill.Realized; }
        }
        decimal profit=owner.account.DayProfitSoFar, opened=owner.account.DayBaseline;
        string unit=GameLocale.Code=="ko" ? "돈" : " "+GameLocale.Text("돈");
        Lit(todayCaptions[0],Word("오늘 손익","Today","本日の損益")); Lit(todayCaptions[1],Word("오늘 실현","Realized today","本日の実現")); Lit(todayCaptions[2],Word("오늘 거래대금","Traded today","本日の売買代金"));
        string rate=opened>0m ? "  "+(profit/opened*100m).ToString("+0.0;-0.0;0.0")+"%" : "";
        Lit(todayValues[0],InvestmentDashboard.Signed(profit)+unit+rate); Lit(todayValues[1],InvestmentDashboard.Signed(realized)+unit); Lit(todayValues[2],turnover.ToString("N0")+unit);
        todayValues[0].color=profit>0m ? TabletKitStyle.Up : profit<0m ? TabletKitStyle.Down : TabletKitStyle.Muted;
        todayValues[1].color=realized>0m ? TabletKitStyle.Up : realized<0m ? TabletKitStyle.Down : TabletKitStyle.Muted;
        todayValues[2].color=TabletKitStyle.Muted;
    }
    void Update()
    {
        if (Time.unscaledTime>=nextRealized) { nextRealized=Time.unscaledTime+.5f; RefreshRealized(); RefreshNightTab(); RefreshPictures(); }
        if (firstOrder==null || emptyLabel==null) { return; }
        bool none=emptyLabel.gameObject.activeInHierarchy && owner.accounts!=null;
        if (firstOrder.gameObject.activeSelf!=none) { firstOrder.gameObject.SetActive(none); }
        if (!none) { return; }
        string text=GameLocale.Code=="ja" ? "注文は右のタブレット（E）で" : GameLocale.Code=="en" ? "Orders are on the right tablet (E)" : "주문은 오른쪽 태블릿(E)에서";
        if (!GameLocalizedText.Matches(firstOrderLabel,text)) { GameLocalizedText.Set(firstOrderLabel,text,true); }
        TabletCall.Set(firstOrder,true);
    }
    private readonly List<TabletAccountUI.Page> pages=new();
    public static TabletKitLayout Install(TabletAccountUI tablet)
    {
        var layout=tablet.gameObject.AddComponent<TabletKitLayout>(); layout.owner=tablet; layout.Build(); return layout;
    }
    private void Build()
    {
        var root=owner.contentRoot.transform;
        foreach (var tab in owner.tabs) { tab.gameObject.SetActive(false); }
        foreach (var screen in owner.screens)
        {
            TabletKitStyle.Place((RectTransform)screen.transform,.02f,.18f,.96f,.80f);
            TabletKitStyle.Flat(screen.GetComponent<Image>(),TabletKitStyle.Background);
        }
        owner.orders.tradeView?.FitCanvas();
        owner.orders.legacy.spreadView?.FitCanvas();
        var bg=root.Find("Panel :: Account Input Surface");
        if (bg!=null) { TabletKitStyle.Flat(bg.GetComponent<Image>(),TabletKitStyle.Background); }
        var oldLine=root.Find("Image :: Navigation Divider"); if (oldLine!=null) { oldLine.gameObject.SetActive(false); }
        owner.accountTitle.transform.SetParent(root,false); TabletKitStyle.Place(owner.accountTitle.rectTransform,.025f,.012f,.56f,.045f);
        DeviceTerminalUI.Set(owner.accountTitle,owner.second ? "작업 계좌" : "기본 계좌"); TabletKitStyle.TextStyle(owner.accountTitle,29);
        TabletKitStyle.Place(owner.clock.rectTransform,.66f,.012f,.315f,.045f); TabletKitStyle.TextStyle(owner.clock,27,true);
        owner.clock.alignment=TextAlignmentOptions.MidlineRight;
        // 1번 태블릿은 보는 곳이다: 자산과 오늘 성적, 지난 기록, 뉴스. 여기서는 아무것도 주문하지 않는다.
        // 2번 태블릿은 하는 곳이다: 주문, 작전(스프레드·매집·작전 도구), 내 기업, 기사 공작, 밤 준비.
        if (!owner.second)
        {
            Add("자산","assets",TabletAccountUI.Page.Dashboard,0,"자산","Assets","資産");
            Add("기록","history",TabletAccountUI.Page.Records,1,"기록","History","履歴");
            Add("뉴스","news",TabletAccountUI.Page.Events,2,"뉴스","News","ニュース");
        }
        else
        {
            Add("주문","buy",TabletAccountUI.Page.Orders,0,"주문","Orders","注文");
            Add("작전","auto_trade",TabletAccountUI.Page.Automation,1,"작전","Ops","作戦");
            Add("내 기업","company",TabletAccountUI.Page.Management,2,"내 기업","Company","自社");
            Add("기사","news",TabletAccountUI.Page.Special,3,"기사","Press","記事");
            // 밤 준비: 장이 끝난 뒤에만 쓸 수 있다. 밤이 되면 탭이 밝아진다.
            NightPrepUI.Install(owner);
            Add("밤 준비","timer",TabletAccountUI.Page.Night,4,"밤 준비","Night","夜の仕込み");
        }
        TabletKitStyle.Surface("Tab Divider",root,.025f,.142f,.95f,.001f,TabletKitStyle.Line);
        owner.managementButton.gameObject.SetActive(false);
        BuildAssets();
        var locked=owner.lockRoot.GetComponentsInChildren<Image>(true);
        foreach (var image in locked)
        {
            if (image.gameObject==owner.lockRoot) { TabletKitStyle.Flat(image,TabletKitStyle.Background); }
            else { image.sprite=TabletKitStyle.Icon("lock"); image.color=TabletKitStyle.Ink; image.preserveAspect=true; }
        }
        if (owner.second)
        {
            lockHint=TabletKitStyle.Label("Lock Hint",owner.lockRoot.transform,"",.10f,.64f,.80f,.07f,30);
            lockHint.alignment=TextAlignmentOptions.Center; lockHint.color=TabletKitStyle.Muted;
            RefreshLockHint(); GameLocale.Changed+=RefreshLockHint;
        }
        // Records retain the original account fill journal; related screens are explicit sub-pages.
        var records=owner.recordsPanel.transform;
        var heading=records.Find("Text :: Record Heading"); if (heading!=null) { heading.gameObject.SetActive(false); }
        TabletKitStyle.Action("Record News",records,"뉴스",0,0,.22f,.075f,()=>owner.ShowPage(TabletAccountUI.Page.Events));
        TabletKitStyle.Action("Record Company",records,"기업 정보",.24f,0,.22f,.075f,()=> { owner.ShowPage(TabletAccountUI.Page.Management); owner.GetComponentInChildren<CompanyGrowthUI>(true)?.Close(); });
        var income=owner.dashboard.GetComponent<DailyIncomeUI>();
        if (income!=null)
        { TabletKitStyle.Action("Record Settlement",records,"정산",.48f,0,.22f,.075f,income.OpenStatement); }
        TabletKitStyle.Action("Record Holdings",records,"보유 상세",.72f,0,.28f,.075f,()=>owner.ShowPage(TabletAccountUI.Page.Detail));
        owner.recordsView=TabletRecordsView.Install(owner);
        owner.transferPanel.transform.SetAsLastSibling();
    }
    private void Add(string text,string icon,TabletAccountUI.Page page,int index,string ko,string en,string ja)
    {
        names.Add((ko,en,ja));
        // 보는 태블릿은 탭이 셋, 하는 태블릿은 다섯이다.
        float step=.96f/(owner.second ? 5 : 3);
        var button=TabletKitStyle.Action("Tablet "+page,owner.contentRoot.transform,text,.025f+index*step,.065f,step-.01f,.072f,()=>owner.ShowPage(page));
        button.image.color=Color.clear;
        // 아이콘과 이름은 한 덩어리로 붙여 둔다(아이콘이 따로 한 칸처럼 보이면 탭이 두 배로 많아 보인다).
        TabletKitStyle.Glyph(icon,button.transform,.06f,.24f,.13f,.52f);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(); TabletKitStyle.Place(label.rectTransform,.22f,0,.76f,1); label.fontSize=34; label.alignment=TextAlignmentOptions.MidlineLeft;
        TabletKitStyle.Surface("Active Tab",button.transform,0,.96f,1,.04f,TabletKitStyle.Accent);
        buttons.Add(button); pages.Add(page);
    }
    public void RefreshTabs()
    {
        for (int i=0;i<buttons.Count;i++)
        {
            bool active=owner.CurrentPage==pages[i] || pages[i]==TabletAccountUI.Page.Dashboard && owner.CurrentPage==TabletAccountUI.Page.Detail;
            buttons[i].transform.Find("Image :: Active Tab").gameObject.SetActive(active);
            buttons[i].GetComponentInChildren<TextMeshProUGUI>().color=active ? TabletKitStyle.Accent : TabletKitStyle.Muted;
        }
    }
    private void BuildAssets()
    {
        var ui=owner.dashboard; var root=ui.transform;
        foreach (Transform child in root) { child.gameObject.SetActive(false); }
        void Move(Component item,float x,float y,float w,float h,float size=30,bool numeric=false)
        { item.gameObject.SetActive(true); TabletKitStyle.Place((RectTransform)item.transform,x,y,w,h); if (item is TMP_Text t) { TabletKitStyle.TextStyle(t,size,numeric); } }
        // 주인공은 총자산 하나. 그 밑의 띠가 주식과 현금의 몫이고, 오른쪽 두 칸이 그 띠의 범례다.
        // 위 왼쪽: 총자산(가장 크게)과 평가손익, 그 밑에 오늘 성적 세 칸. 위 오른쪽: 오늘 자산 흐름(크게).
        LiteralText.Apply(TabletKitStyle.Label("Total Caption",root,"",0,.0f,.44f,.045f,26),"총자산","Total assets","総資産").color=TabletKitStyle.Muted;
        Move(ui.total,0,.045f,.46f,.11f,66,true); ui.total.alignment=TextAlignmentOptions.MidlineLeft; ui.total.overflowMode=TextOverflowModes.Overflow;
        Move(ui.profit,0,.158f,.46f,.05f,30,true);
        // 예전의 주식/현금 막대는 숨기고(값은 대시보드가 계속 채운다), 그 자리에 보유 지도를 둔다:
        // 칸의 너비 = 그 몫의 금액, 색 = 수익률. 어디에 돈이 몰려 있고 어디서 벌고 있는지가 숫자 없이 보인다.
        var mix=TabletKitStyle.Surface("Asset Mix",root,0,.248f,.60f,.020f,new Color32(110,129,142,255));
        ui.allocationFill=TabletKitStyle.Surface("Asset Mix Stocks",mix.transform,0,0,1,1,TabletKitStyle.Accent);
        mix.gameObject.SetActive(false);
        LiteralText.Apply(TabletKitStyle.Label("Mix Caption",root,"",0,.318f,.5f,.034f,22),"자산 구성 · 너비는 금액, 색은 수익률","Where the money is · width = amount, colour = return","資産構成 · 幅は金額、色は損益率").color=TabletKitStyle.Muted;
        holdingsMap=HoldingsMap.Create(TabletKitStyle.Area("Holdings Map",root,0,.355f,1,.06f),Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading"),TabletKitScale(root));
        // 오른쪽 위: 오늘 자산 흐름(09:00부터 지금까지 총자산 곡선, 시가 기준선 위는 빨강 아래는 파랑) + 그 밑에 주식·현금 두 줄.
        curveCaption=TabletKitStyle.Label("Curve Caption",root,"",.5f,.0f,.5f,.04f,22); curveCaption.color=TabletKitStyle.Muted;
        var curveArea=TabletKitStyle.Area("Asset Curve",root,.5f,.05f,.5f,.2f);
        curve=curveArea.gameObject.AddComponent<UiLineChart>(); curve.raycastTarget=false; curve.thickness=2.4f*TabletKitScale(root); curve.dot=7f*TabletKitScale(root);
        LiteralText.Apply(TabletKitStyle.Label("Valuation Caption",root,"",.5f,.262f,.1f,.036f,22),"주식 평가금","Stocks","株式評価額").color=TabletKitStyle.Muted;
        Move(ui.valuation,.6f,.262f,.15f,.036f,24,true); ui.valuation.alignment=TextAlignmentOptions.MidlineRight;
        LiteralText.Apply(TabletKitStyle.Label("Cash Caption",root,"",.77f,.262f,.1f,.036f,22),"주문 가능 금액","Buying power","注文可能額").color=TabletKitStyle.Muted;
        Move(ui.available,.86f,.262f,.14f,.036f,24,true); ui.available.alignment=TextAlignmentOptions.MidlineRight;
        BuildToday(root);
        Move(ui.count,0,.435f,.5f,.045f,27);
        // 보유 종목: 표 대신 타일판(로고 · 평가금액 · 손익 · 비중 막대). 예전 목록은 숨겨 두고(대시보드가 계속 채운다) 정렬 단추·열 머리도 쓰지 않는다.
        if (!owner.second) { holdingsBoard=HoldingsBoard.Create(TabletKitStyle.Area("Holdings Board",root,0,.49f,1,.385f),ui,Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading"),TabletKitScale(root)); }
        Move(ui.scroll,0,.55f,1,.315f); ui.rowHeight=104; ui.scroll.gameObject.SetActive(owner.second);
        ui.content.gameObject.SetActive(true); ui.rowTemplate.gameObject.SetActive(false);
        foreach (var t in ui.rowTemplate.GetComponentsInChildren<TMP_Text>(true)) { TabletKitStyle.TextStyle(t,t==ui.rowTemplate.company ? 34 : 29,t!=ui.rowTemplate.company); }
        Move(ui.empty,0,.60f,1,.08f,30); ui.empty.color=TabletKitStyle.Muted; ui.empty.alignment=TextAlignmentOptions.Center;
        // 가진 종목이 없을 때: 무엇을 해야 하는지 단추 하나로 보여 준다.
        if (!owner.second)
        {
            firstOrder=TabletKitStyle.Action("First Order",root,"",.25f,.70f,.50f,.09f,()=>DeskViewController.Instance?.FocusTabletNumber(1),true);
            TabletKitStyle.Glyph("buy",firstOrder.transform,.08f,.26f,.12f,.48f).color=TabletKitStyle.Background;
            firstOrderLabel=firstOrder.GetComponentInChildren<TextMeshProUGUI>(); TabletKitStyle.Place(firstOrderLabel.rectTransform,.20f,0,.76f,1); firstOrderLabel.fontSize=32;
            emptyLabel=ui.empty; firstOrder.gameObject.SetActive(false);
        }
        // 오늘 판 것으로 번 돈. 평가 손익 옆에 한 줄로 둔다.
        // 뉴스·정산·기록으로 가는 작은 단추(아이콘만). 총 매수원금 글자가 있던 자리다.
        var statement=owner.dashboard.GetComponent<DailyIncomeUI>();
        float shortcutY=owner.second ? .865f : .91f, shortcutX=!owner.second ? 0f : statement!=null ? .79f : .862f;
        if (!owner.second)
        {
            // 보는 태블릿에는 뉴스·기록이 이미 위의 탭에 있다. 이름 없는 아이콘 단추를 겹쳐 두지 않고, 탭에 없는 정산 하나만 이름을 붙여 둔다.
            if (statement!=null)
            {
                var settle=TabletKitStyle.Action("Shortcut calendar",root,Word("정산","Statement","精算"),0,shortcutY,.20f,.07f,statement.OpenStatement);
                TabletKitStyle.Glyph("calendar",settle.transform,.08f,.24f,.16f,.52f);
                var caption=settle.GetComponentInChildren<TextMeshProUGUI>(); TabletKitStyle.Place(caption.rectTransform,.28f,0,.68f,1); caption.alignment=TextAlignmentOptions.MidlineLeft;
                LiteralText.Apply(caption,"정산","Statement","精算");
            }
        }
        else
        {
            Shortcut(root,"news",shortcutX,shortcutY,()=>owner.ShowPage(TabletAccountUI.Page.Events));
            if (statement!=null) { Shortcut(root,"calendar",shortcutX+.072f,shortcutY,statement.OpenStatement); }
            Shortcut(root,"history",shortcutX+(statement!=null ? .144f : .072f),shortcutY,()=>owner.ShowPage(TabletAccountUI.Page.Records));
        }
        Move(owner.transferButton,.45f,.91f,.22f,.07f); TabletControlStyle.Button(owner.transferButton);
        Move(ui.detail,.69f,.91f,.31f,.07f); TabletControlStyle.Button(ui.detail);
        DeviceTerminalUI.Set(ui.detail.GetComponentInChildren<TextMeshProUGUI>(),"선택 종목 상세");
        if (owner.second)
        {
            Move(ui.cost,0,.875f,.75f,.045f,27,true); ui.cost.color=TabletKitStyle.Muted;
            Move(owner.transferButton,0,.935f,.22f,.065f);
            Move(owner.orderButton,.24f,.935f,.28f,.065f); DeviceTerminalUI.Set(owner.orderButton.GetComponentInChildren<TextMeshProUGUI>(),"주문"); TabletControlStyle.Button(owner.orderButton);
            Move(ui.detail,.54f,.935f,.46f,.065f);
        }
    }
}
