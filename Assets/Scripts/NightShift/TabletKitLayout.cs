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
    private Button firstOrder; private TextMeshProUGUI firstOrderLabel, emptyLabel, todayRealized;
    private float nextRealized;
    private void Shortcut(Transform root,string icon,float x,float y,System.Action action)
    {
        var button=TabletKitStyle.Action("Shortcut "+icon,root,"",x,y,.062f,owner.second ? .06f : .07f,action);
        TabletKitStyle.Glyph(icon,button.transform,.22f,.22f,.56f,.56f);
    }
    /// <summary>밤 준비 탭: 이름은 언어마다 따로 적고, 밤이 되면 아이콘이 밝아진다.</summary>
    private void RefreshNightTab()
    {
        int index=pages.IndexOf(TabletAccountUI.Page.Night); if (index<0) { return; }
        var label=buttons[index].GetComponentInChildren<TextMeshProUGUI>();
        string text=GameLocale.Code=="ja" ? "夜の仕込み" : GameLocale.Code=="en" ? "Night" : "밤 준비";
        if (!GameLocalizedText.Matches(label,text)) { GameLocalizedText.Set(label,text,true); }
        bool night=NightPrep.Instance!=null && NightPrep.Instance.IsNight;
        var icon=buttons[index].transform.Find("Image :: timer"); if (icon!=null) { icon.GetComponent<Image>().color=night ? TabletKitStyle.Accent : TabletKitStyle.Ink; }
    }
    /// <summary>오늘 매도로 실현한 손익. 판 것이 없으면 줄을 비운다.</summary>
    private void RefreshRealized()
    {
        if (todayRealized==null || owner.events==null || owner.events.records==null || MarketDayController.Instance==null) { return; }
        int today=MarketDayController.Instance.CalendarDate; decimal realized=0m; int sells=0;
        foreach (var fill in owner.events.records.Fills)
        {
            if (fill.Buy || TabletRecordsView.DayOf(fill.Time)!=today) { continue; }
            sells++; realized+=fill.Realized;
        }
        string text=sells==0 ? "" : (GameLocale.Code=="ja" ? "本日の実現 " : GameLocale.Code=="en" ? "Realized today " : "오늘 실현 ")+InvestmentDashboard.Signed(realized)+(GameLocale.Code=="ko" ? "돈" : " "+GameLocale.Text("돈"));
        if (!GameLocalizedText.Matches(todayRealized,text)) { GameLocalizedText.Set(todayRealized,text,true); }
        todayRealized.color=realized>0m ? TabletKitStyle.Up : realized<0m ? TabletKitStyle.Down : TabletKitStyle.Muted;
    }
    void Update()
    {
        if (Time.unscaledTime>=nextRealized) { nextRealized=Time.unscaledTime+.5f; RefreshRealized(); RefreshNightTab(); }
        if (firstOrder==null || emptyLabel==null) { return; }
        bool none=emptyLabel.gameObject.activeInHierarchy && owner.accounts!=null;
        if (firstOrder.gameObject.activeSelf!=none) { firstOrder.gameObject.SetActive(none); }
        if (!none) { return; }
        string text=GameLocale.Code=="ja" ? "最初の注文を出す" : GameLocale.Code=="en" ? "Place your first order" : "첫 주문 넣으러 가기";
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
        Add("자산","assets",TabletAccountUI.Page.Dashboard,0);
        Add(owner.second ? "자동 매매" : "주문",owner.second ? "auto_trade" : "buy",owner.second ? TabletAccountUI.Page.Automation : TabletAccountUI.Page.Orders,1);
        Add(owner.second ? "특수 기능" : "내 기업",owner.second ? "news" : "company",owner.second ? TabletAccountUI.Page.Special : TabletAccountUI.Page.Management,2);
        // 기록은 탭이 아니다: 지나간 일이라 다음 행동을 바꾸지 않는다. 자산 화면 아래의 작은 단추로 들어간다.
        if (!owner.second)
        {
            UnlockGridUI.Install(owner);
            Add("해금","unlock",TabletAccountUI.Page.Unlocks,3);
            // 밤 준비: 장이 끝난 뒤에만 쓸 수 있다. 밤이 되면 탭이 밝아진다.
            NightPrepUI.Install(owner);
            Add("밤 준비","timer",TabletAccountUI.Page.Night,4);
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
    private void Add(string text,string icon,TabletAccountUI.Page page,int index)
    {
        // 기본 계좌 태블릿만 다섯 번째 탭(해금)을 가진다. 네 탭일 때의 간격은 그대로다.
        float step=.96f/(owner.second ? 3 : 5);
        var button=TabletKitStyle.Action("Tablet "+page,owner.contentRoot.transform,text,.025f+index*step,.065f,step-.01f,.072f,()=>owner.ShowPage(page));
        button.image.color=Color.clear;
        TabletKitStyle.Glyph(icon,button.transform,.045f,.20f,.16f,.60f);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(); TabletKitStyle.Place(label.rectTransform,.22f,0,.76f,1); label.fontSize=34;
        TabletKitStyle.Surface("Active Tab",button.transform,0,.96f,1,.04f,TabletKitStyle.Accent);
        buttons.Add(button); pages.Add(page);
    }
    public void RefreshTabs()
    {
        for (int i=0;i<buttons.Count;i++)
        {
            bool active=owner.CurrentPage==pages[i] || pages[i]==TabletAccountUI.Page.Dashboard && (owner.CurrentPage==TabletAccountUI.Page.Records || owner.CurrentPage==TabletAccountUI.Page.Events || owner.CurrentPage==TabletAccountUI.Page.Detail);
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
        TabletKitStyle.Glyph("assets",root,0,.01f,.035f,.05f);
        TabletKitStyle.Label("Total Caption",root,"총자산",.045f,.01f,.40f,.05f,30).color=TabletKitStyle.Muted;
        Move(ui.total,0,.062f,.60f,.115f,68,true); ui.total.alignment=TextAlignmentOptions.MidlineLeft;
        Move(ui.profit,0,.18f,.60f,.055f,32,true);
        var mix=TabletKitStyle.Surface("Asset Mix",root,0,.248f,.60f,.020f,new Color32(110,129,142,255));
        ui.allocationFill=TabletKitStyle.Surface("Asset Mix Stocks",mix.transform,0,0,1,1,TabletKitStyle.Accent);
        TabletKitStyle.Surface("Stock Tile",root,.64f,.005f,.36f,.125f,TabletKitStyle.Panel).transform.SetAsFirstSibling();
        TabletKitStyle.Surface("Stock Key",root,.64f,.005f,.006f,.125f,TabletKitStyle.Accent);
        TabletKitStyle.Label("Valuation Caption",root,"주식 평가금",.66f,.012f,.33f,.045f,25).color=TabletKitStyle.Muted;
        Move(ui.valuation,.66f,.058f,.325f,.065f,36,true); ui.valuation.alignment=TextAlignmentOptions.MidlineRight;
        TabletKitStyle.Surface("Cash Tile",root,.64f,.143f,.36f,.125f,TabletKitStyle.Panel).transform.SetAsFirstSibling();
        TabletKitStyle.Surface("Cash Key",root,.64f,.143f,.006f,.125f,new Color32(110,129,142,255));
        TabletKitStyle.Label("Cash Caption",root,"주문 가능 현금",.66f,.15f,.33f,.045f,25).color=TabletKitStyle.Muted;
        Move(ui.available,.66f,.196f,.325f,.065f,36,true); ui.available.alignment=TextAlignmentOptions.MidlineRight;
        TabletKitStyle.Surface("Summary Divider",root,0,.29f,1,.0015f,TabletKitStyle.Line);
        Move(ui.count,0,.31f,.53f,.065f,34);
        Move(ui.sort,.68f,.31f,.32f,.065f); TabletControlStyle.Button(ui.sort); ui.sortLabel.fontSize=29;
        string[] headers={"종목 · 수량","현재가 · 평균가","평가금","손익 · 수익률"}; float[] xs={0,.36f,.58f,.78f}, widths={.34f,.20f,.18f,.22f};
        for (int i=0;i<4;i++) { TabletKitStyle.Label("Holding Column "+i,root,headers[i],xs[i],.40f,widths[i],.045f,27,i>0).color=TabletKitStyle.Muted; }
        Move(ui.scroll,0,.46f,1,.405f); ui.rowHeight=104;
        ui.content.gameObject.SetActive(true); ui.rowTemplate.gameObject.SetActive(false);
        foreach (var t in ui.rowTemplate.GetComponentsInChildren<TMP_Text>(true)) { TabletKitStyle.TextStyle(t,t==ui.rowTemplate.company ? 34 : 29,t!=ui.rowTemplate.company); }
        Move(ui.empty,0,.50f,1,.10f,32); ui.empty.color=TabletKitStyle.Muted; ui.empty.alignment=TextAlignmentOptions.Center;
        // 가진 종목이 없을 때: 무엇을 해야 하는지 단추 하나로 보여 준다.
        if (!owner.second)
        {
            firstOrder=TabletKitStyle.Action("First Order",root,"",.34f,.62f,.32f,.10f,()=>owner.ShowPage(TabletAccountUI.Page.Orders),true);
            TabletKitStyle.Glyph("buy",firstOrder.transform,.08f,.26f,.12f,.48f).color=TabletKitStyle.Background;
            firstOrderLabel=firstOrder.GetComponentInChildren<TextMeshProUGUI>(); TabletKitStyle.Place(firstOrderLabel.rectTransform,.20f,0,.76f,1); firstOrderLabel.fontSize=32;
            emptyLabel=ui.empty; firstOrder.gameObject.SetActive(false);
        }
        // 오늘 판 것으로 번 돈. 평가 손익 옆에 한 줄로 둔다.
        todayRealized=TabletKitStyle.Label("Realized Today",root,"",.30f,.18f,.30f,.055f,27,true); TabletKitStyle.TextStyle(todayRealized,27,true); todayRealized.alignment=TextAlignmentOptions.MidlineRight;
        // 뉴스·정산·기록으로 가는 작은 단추(아이콘만). 총 매수원금 글자가 있던 자리다.
        var statement=owner.dashboard.GetComponent<DailyIncomeUI>();
        float shortcutY=owner.second ? .865f : .91f, shortcutX=!owner.second ? 0f : statement!=null ? .79f : .862f;
        Shortcut(root,"news",shortcutX,shortcutY,()=>owner.ShowPage(TabletAccountUI.Page.Events));
        if (statement!=null) { Shortcut(root,"calendar",shortcutX+.072f,shortcutY,statement.OpenStatement); }
        Shortcut(root,"history",shortcutX+(statement!=null ? .144f : .072f),shortcutY,()=>owner.ShowPage(TabletAccountUI.Page.Records));
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
