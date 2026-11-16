using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reuses the existing account pages and listeners, changing only their internal layout.</summary>
public sealed class TabletKitLayout : MonoBehaviour
{
    private TabletAccountUI owner;
    private readonly List<Button> buttons=new();
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
        Add("기록","history",TabletAccountUI.Page.Records,3);
        if (!owner.second)
        {
            UnlockGridUI.Install(owner);
            Add("해금","unlock",TabletAccountUI.Page.Unlocks,4);
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
        // Records retain the original account fill journal; related screens are explicit sub-pages.
        var records=owner.recordsPanel.transform;
        var heading=records.Find("Text :: Record Heading"); if (heading!=null) { heading.gameObject.SetActive(false); }
        TabletKitStyle.Action("Record News",records,"뉴스",0,0,.22f,.075f,()=>owner.ShowPage(TabletAccountUI.Page.Events));
        TabletKitStyle.Action("Record Company",records,"기업 정보",.24f,0,.22f,.075f,()=> { owner.ShowPage(TabletAccountUI.Page.Management); owner.GetComponentInChildren<CompanyGrowthUI>(true)?.Close(); });
        var income=owner.dashboard.GetComponent<DailyIncomeUI>();
        if (income!=null)
        { TabletKitStyle.Action("Record Settlement",records,"정산",.48f,0,.22f,.075f,income.OpenStatement); }
        TabletKitStyle.Action("Record Holdings",records,"보유 상세",.72f,0,.28f,.075f,()=>owner.ShowPage(TabletAccountUI.Page.Detail));
        owner.transferPanel.transform.SetAsLastSibling();
    }
    private void Add(string text,string icon,TabletAccountUI.Page page,int index)
    {
        // 기본 계좌 태블릿만 다섯 번째 탭(해금)을 가진다. 네 탭일 때의 간격은 그대로다.
        float step=.96f/(owner.second ? 4 : 5);
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
            bool active=owner.CurrentPage==pages[i] || pages[i]==TabletAccountUI.Page.Records && (owner.CurrentPage==TabletAccountUI.Page.Events || owner.CurrentPage==TabletAccountUI.Page.Detail);
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
        TabletKitStyle.Glyph("holdings",root,0,.01f,.035f,.05f);
        TabletKitStyle.Label("Valuation Caption",root,"주식 평가금",.045f,.01f,.40f,.05f,30).color=TabletKitStyle.Muted;
        Move(ui.valuation,0,.065f,.55f,.11f,62,true); ui.valuation.alignment=TextAlignmentOptions.MidlineLeft;
        Move(ui.profit,0,.18f,.57f,.065f,34,true);
        TabletKitStyle.Label("Cash Caption",root,"주문 가능 현금",.63f,.01f,.37f,.05f,29).color=TabletKitStyle.Muted;
        Move(ui.available,.63f,.065f,.37f,.075f,38,true);
        TabletKitStyle.Label("Total Caption",root,"총자산",.63f,.15f,.37f,.045f,27).color=TabletKitStyle.Muted;
        Move(ui.total,.63f,.20f,.37f,.06f,32,true);
        TabletKitStyle.Surface("Summary Divider",root,0,.29f,1,.0015f,TabletKitStyle.Line);
        Move(ui.count,0,.31f,.53f,.065f,34);
        Move(ui.sort,.68f,.31f,.32f,.065f); TabletControlStyle.Button(ui.sort); ui.sortLabel.fontSize=29;
        string[] headers={"종목 · 수량","현재가 · 평균가","평가금","손익 · 수익률"}; float[] xs={0,.36f,.58f,.78f}, widths={.34f,.20f,.18f,.22f};
        for (int i=0;i<4;i++) { TabletKitStyle.Label("Holding Column "+i,root,headers[i],xs[i],.40f,widths[i],.045f,27,i>0).color=TabletKitStyle.Muted; }
        Move(ui.scroll,0,.46f,1,.405f); ui.rowHeight=104;
        ui.content.gameObject.SetActive(true); ui.rowTemplate.gameObject.SetActive(false);
        foreach (var t in ui.rowTemplate.GetComponentsInChildren<TMP_Text>(true)) { TabletKitStyle.TextStyle(t,t==ui.rowTemplate.company ? 34 : 29,t!=ui.rowTemplate.company); }
        Move(ui.empty,0,.53f,1,.14f,32); ui.empty.color=TabletKitStyle.Muted;
        Move(ui.cost,0,.91f,.43f,.07f,27,true); ui.cost.color=TabletKitStyle.Muted;
        Move(owner.transferButton,.45f,.91f,.22f,.07f); TabletControlStyle.Button(owner.transferButton);
        Move(ui.detail,.69f,.91f,.31f,.07f); TabletControlStyle.Button(ui.detail);
        DeviceTerminalUI.Set(ui.detail.GetComponentInChildren<TextMeshProUGUI>(),"선택 종목 상세");
        if (owner.second)
        {
            Move(ui.cost,0,.875f,1,.045f,27,true);
            Move(owner.transferButton,0,.935f,.22f,.065f);
            Move(owner.orderButton,.24f,.935f,.28f,.065f); DeviceTerminalUI.Set(owner.orderButton.GetComponentInChildren<TextMeshProUGUI>(),"주문"); TabletControlStyle.Button(owner.orderButton);
            Move(ui.detail,.54f,.935f,.46f,.065f);
        }
    }
}
