# Tablet kit 신규 C# 전문과 작업 전/후 비교
함수별 변경 의도는 `tablet-kit-changes.md`의 3절을 참고한다. 아래 diff는 이번 작업 직전 스냅샷 기준이다.

## 신규: Assets/Scripts/NightShift/TabletKitStyle.cs
```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared flat tablet presentation. Atlas cells are cached once; no domain state lives here.</summary>
public static class TabletKitStyle
{
    [Serializable] private sealed class Tokens { public Colors colors; }
    [Serializable] private sealed class Colors
    { public string background, panel, panelHover, divider, textPrimary, textSecondary, textMuted, accent, profitKR, lossKR, warning, disabled; }
    private static Tokens tokens;
    private static readonly Dictionary<string,Sprite> icons = new();
    private static readonly string[] names = { "home","assets","wallet","holdings","market","buy","sell","pending_order","company","factory","sales","research","auto_trade","robot","news","history","cash","coin_stack","growth","loss","check","close","lock","unlock","timer","calendar","refresh","settings","alert","info","chevron_right","chevron_down","plus","minus","search","price_tag","listing","dividend","reinvest","reserve","play","stop","pause","progress","complete","failed","bell","filter" };
    private static Colors Palette => (tokens ??= JsonUtility.FromJson<Tokens>(Resources.Load<TextAsset>("UI/TabletKit/DesignTokens").text)).colors;
    private static Color Parse(string value) { ColorUtility.TryParseHtmlString(value,out var color); return color; }
    public static Color Background => Parse(Palette.background);
    public static Color Panel => Parse(Palette.panel);
    public static Color Hover => Parse(Palette.panelHover);
    public static Color Line => Parse(Palette.divider);
    public static Color Ink => Parse(Palette.textPrimary);
    public static Color Muted => Parse(Palette.textSecondary);
    public static Color Accent => Parse(Palette.accent);
    public static Color Up => Parse(Palette.profitKR);
    public static Color Down => Parse(Palette.lossKR);
    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name,out var sprite))
        { return sprite; }
        int index = Array.IndexOf(names,name);
        var texture = Resources.Load<Texture2D>("UI/TabletKit/Icons");
        if (index < 0 || texture == null)
        { return null; }
        float cell = texture.width / 8f;
        sprite = Sprite.Create(texture,new Rect(index%8*cell,(5-index/8)*cell,cell,cell),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        sprite.name=name; sprite.hideFlags=HideFlags.DontSave; icons.Add(name,sprite); return sprite;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { tokens=null; icons.Clear(); }
    public static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=new Vector2(x,1-y-h); rect.anchorMax=new Vector2(x+w,1-y); rect.offsetMin=rect.offsetMax=Vector2.zero; rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity; }
    public static RectTransform Area(string name,Transform parent,float x,float y,float w,float h)
    {
        var rect=new GameObject("Area :: "+name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.gameObject.layer=parent.gameObject.layer; Place(rect,x,y,w,h); return rect;
    }
    public static Image Surface(string name,Transform parent,float x,float y,float w,float h,Color color)
    { var image=Area(name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.name="Image :: "+name; image.color=color; image.raycastTarget=false; return image; }
    public static Image Glyph(string name,Transform parent,float x,float y,float w,float h)
    { var image=Surface(name,parent,x,y,w,h,Ink); image.sprite=Icon(name); image.preserveAspect=true; return image; }
    public static void TextStyle(TMP_Text text,float size,bool numeric=false)
    {
        var font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (font!=null) { text.font=font; text.fontSharedMaterial=font.material; }
        if (numeric) { TradingOrderView.Numeric(text); }
        text.fontSize=size; text.enableAutoSizing=false; text.fontStyle=FontStyles.Normal; text.margin=Vector4.zero;
        text.textWrappingMode=TextWrappingModes.NoWrap; text.overflowMode=TextOverflowModes.Ellipsis; text.raycastTarget=false; text.color=Ink;
    }
    public static TextMeshProUGUI Label(string name,Transform parent,string value,float x,float y,float w,float h,float size=30,bool right=false)
    {
        var text=Area(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); text.name="Text :: "+name; TextStyle(text,size);
        text.alignment=right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft; DeviceTerminalUI.Set(text,value); return text;
    }
    public static Button Action(string name,Transform parent,string value,float x,float y,float w,float h,Action action,bool primary=false)
    {
        var image=Surface(name,parent,x,y,w,h,Panel); image.name="Button :: "+name; image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var label=Label(name,image.transform,value,.03f,0,.94f,1,30); label.alignment=TextAlignmentOptions.Center;
        TabletControlStyle.Button(button,primary); button.onClick.AddListener(()=>action()); return button;
    }
    public static void Flat(Image image,Color color)
    {
        if (image==null)
        { return; }
        image.sprite=null; image.type=Image.Type.Simple; image.color=color;
    }
}

```

## 신규: Assets/Scripts/NightShift/TabletKitLayout.cs
```csharp
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
        var button=TabletKitStyle.Action("Tablet "+page,owner.contentRoot.transform,text,.025f+index*.24f,.065f,.23f,.072f,()=>owner.ShowPage(page));
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

```

## 신규: Assets/Editor/TabletKitValidation.cs
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

/// <summary>One page/process, actual World Space raycasts, real order engine; isolated test fixtures only.</summary>
public static class TabletKitValidation
{
    private static string output,language;
    private static bool hadLanguage,oldOptions,spawned,finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase,checks,errors,locale,dueFrame,mode;
    private static TabletAccountUI tablet,second;
    private static Stock stock;
    private static Trader seller;
    private static readonly string[] languages={"ko","en","ja"};
    public static void Assets() { mode=0; Run(); }
    public static void Orders() { mode=1; Run(); }
    public static void Company() { mode=2; Run(); }
    public static void Auto() { mode=3; Run(); }
    public static void Save() { mode=4; Run(); }
    public static void Special() { mode=5; Run(); }
    public static void Cold() { mode=6; Run(); }
    private static void Run()
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output)) { throw new Exception("Use bounded runner"); }
        Directory.CreateDirectory(output); hadLanguage=PlayerPrefs.HasKey("DonDon.Language"); language=PlayerPrefs.GetString("DonDon.Language");
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log; EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>200) { Finish("time budget"); return; }
        if (EditorApplication.isPlaying&&!spawned) { spawned=true; new GameObject("Test :: Tablet Kit").AddComponent<TabletKitProbe>(); }
    }
    private static void Log(string message,string stack,LogType type)
    { if (type==LogType.Error||type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),message+"\n"+stack+"\n"); } }
    private static void Check(bool ok,string name)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(ok?"PASS ":"FAIL ")+name+"\n"); if (!ok) { throw new Exception(name); } checks++; }
    private static void Wait(int next) { phase=next; dueFrame=Time.frameCount+12; }
    public static void Step()
    {
        if (finished||!GameLocale.Ready||Time.frameCount<dueFrame)
        { return; }
        try
        {
            var day=MarketDayController.Instance;
            if (phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName==null)
                { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke(); Wait(1); return;
            }
            if (phase==1)
            {
                if (day==null||!day.Ready||day.Phase!=MarketPhase.Trading)
                { return; }
                var views=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include);
                tablet=views.Single(t=>!t.second); second=views.Single(t=>t.second);
                Check(tablet.orders.session!=second.orders.session&&tablet.account!=second.account,"separate account and order instances");
                Check(second.Locked&&second.lockRoot.GetComponentsInChildren<TMP_Text>().Length==0,"lock screen has icon only");
                for (int i=0;i<48;i++) { Check(TabletKitStyle.Icon(File.ReadAllLines("Design/TabletUIKit/Docs/IconIndex.csv")[i+1].Split(',')[1])!=null,"atlas cell "+i); }
                if (mode==4||mode==6) { SaveChecks(); return; }
                if (mode==0)
                {
                    second.account.cash=0; second.dashboard.Refresh(); Check(second.dashboard.total.text=="0원"&&second.dashboard.empty.gameObject.activeSelf,"empty zero balance is real zero");
                }
                Fixture();
                if (mode==1) { OrderChecks(); }
                if (mode==2)
                {
                    tablet.ShowPage(TabletAccountUI.Page.Management); CompanyChecks();
                    SessionStore.Restore(File.ReadAllText("Validation/Settings/CompanyGrowth02/session-v2.json"));
                    Check(CorporateEconomy.Instance.PlayerCompany.business.growth.CompletedProjects==2,"visual uses actual paid-growth DAY 5 checkpoint");
                }
                if (mode==3||mode==5) { AutoChecks(); tablet=second; }
                if (mode==5) { StockManager.Instance.SelectStock(0); }
                tablet.ShowPage(mode==0 ? TabletAccountUI.Page.Dashboard : mode==1 ? TabletAccountUI.Page.Orders : mode==2 ? TabletAccountUI.Page.Management : mode==5 ? TabletAccountUI.Page.Special : TabletAccountUI.Page.Automation);
                DeskViewController.Instance.Focus(mode==3||mode==5 ? DeskViewTarget.TabletTwo : DeskViewTarget.TabletOne);
                Wait(2); return;
            }
            if (phase==2)
            {
                if (GameLocale.Code!=languages[locale])
                { return; }
                Capture("page-"+mode+"-"+GameLocale.Code,1600,900); Capture("page-"+mode+"-"+GameLocale.Code,1920,1080);
                Audit(tablet.contentRoot);
                if (mode==3&&locale==0)
                { tablet.ShowPage(TabletAccountUI.Page.Dashboard); tablet.dashboard.Refresh(); Wait(3); return; }
                if (locale==0) { InteractionChecks(); }
                if (++locale<3) { GameLocale.SetLanguage(languages[locale]); Wait(2); return; }
                Check(errors==0,"no runtime exceptions"); Finish(null);
            }
            if (phase==3)
            {
                Capture("work-account-assets",1920,1080); Audit(tablet.contentRoot); InteractionChecks();
                locale=1; GameLocale.SetLanguage(languages[locale]); Wait(2);
            }
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }
    private static void Fixture()
    {
        seller=new GameObject("Test :: Seller").AddComponent<Trader>(); seller.cash=10000000;
        stock=new Stock("KIT","대만디스플레이",2350m,10000m); StockManager.Instance.stocks.Add(stock); seller.GrantShares(stock,10000m,2000m);
        tablet.events.records.ReconnectSession(); second.events.records.ReconnectSession();
        StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock));
        tablet.account.cash=245500m; seller.SellLimit(2350m,20m,0,stock); tablet.account.BuyLimit(2350m,20m,0,stock);
        tablet.orders.session.Prepare(stock);
        if (mode==0)
        {
            for (int i=0;i<7;i++)
            {
                var s=new Stock("KIT"+i,new[]{"국민금융","그린푸드","노보손해보험","미래바이오","덕상가주에이아이","두더리식품","대만디스플레이"}[i],1000m+i*100m,10000m);
                StockManager.Instance.stocks.Add(s); seller.GrantShares(s,10000m,800m); seller.SellLimit(s.currentPrice,10m+i,0,s); tablet.account.BuyLimit(s.currentPrice,10m+i,0,s);
                s.currentPrice+=i%2==0 ? 150m : -150m;
            }
        }
    }
    private static void OrderChecks()
    {
        var session=tablet.orders.session; tablet.account.cash=100000m;
        Check(session.Submit(stock,true,100m,100m,2,60),"split order accepted");
        Check(session.AvailableCash==90000m&&PlayerAccounts.Available(tablet.account)==90000m,"scheduled and resting cash reserved once across UI and domain");
        Check(session.Submit(stock,true,101m,10m,1,1),"second order while first pending");
        Check(!session.Submit(stock,true,1000m,1000m,1,1),"budget overflow rejected");
        var first=session.ActiveOrders[0]; var secondOrder=session.ActiveOrders[1];
        seller.SellLimit(101m,4m,0,stock);
        Check(secondOrder.Filled==4m&&secondOrder.UpdatedAt.StartsWith("DAY "),"partial fill has game timestamp");
        decimal cash=tablet.account.cash;
        Check(session.Cancel(secondOrder)&&session.ActiveOrders.Count==1,"individual cancel preserves other order");
        Check(tablet.account.cash==cash&&session.AvailableCash==cash-10000m,"cancel releases only remaining reserve, not spent cash");
        session.Cancel();
        Check(tablet.account.ReservedCash==0&&session.ScheduledBuyCost==0,"all actual/scheduled reservations returned");
        Check(session.Submit(stock,false,4000m,2m,1,1),"sell order escrow");
        Check(session.AvailableShares(stock)==tablet.account.GetOwnedVolume(stock)-2m,"reserved sell shares excluded");
        Check(session.Submit(stock,true,100m,10m,1,1),"buy alongside sell pending");
        tablet.orders.price.SetTextWithoutNotify("100"); tablet.orders.quantity.SetTextWithoutNotify("3");
    }
    private static void CompanyChecks()
    {
        var company=CorporateEconomy.Instance.PlayerCompany; var data=company.business; var g=data.growth; var rules=CorporateEconomy.Instance.GrowthRules;
        var view=tablet.GetComponentInChildren<CompanyGrowthUI>(true);
        var apply=view.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Button :: Apply IPO");
        void Gate(bool expected,string message) { view.Refresh(); Check(apply.interactable==expected,message); }
        g.Appraisal=rules.listingValue; g.ProfitableCloses=rules.listingProfitableCloses; g.CompletedProjects=rules.listingProjects; data.Debt=0;
        Check(g.Eligible(data,rules),"all four IPO gates pass");
        Gate(true,"IPO action enabled with all criteria");
        g.Appraisal--; Check(!g.Eligible(data,rules),"IPO appraisal gate"); Gate(false,"IPO button rejects insufficient appraisal"); g.Appraisal++;
        g.ProfitableCloses--; Check(!g.Eligible(data,rules),"IPO profit gate"); Gate(false,"IPO button rejects insufficient profitable closes"); g.ProfitableCloses++;
        g.CompletedProjects--; Check(!g.Eligible(data,rules),"IPO expansion gate"); Gate(false,"IPO button rejects insufficient completed projects"); g.CompletedProjects++;
        data.Debt=1; Check(!g.Eligible(data,rules),"IPO debt gate"); Gate(false,"IPO button rejects debt"); data.Debt=0;
        data.Cash=10000000m; decimal personal=tablet.account.cash, treasury=data.Cash;
        decimal cost=g.ProjectCost(data,CompanyProject.Capacity,rules);
        Check(CorporateEconomy.Instance.StartProject(company,CompanyProject.Capacity),"funded company project starts");
        Check(data.Cash==treasury-cost&&tablet.account.cash==personal,"project charged only company once");
        Check(!CorporateEconomy.Instance.StartProject(company,CompanyProject.Capacity)&&data.Cash==treasury-cost,"duplicate project cannot debit again");
    }
    private static void AutoChecks()
    {
        var accounts=PlayerAccounts.Instance; decimal other=tablet.account.Equity()-tablet.account.cash;
        tablet.account.cash=100000000m-other; accounts.EvaluateUnlock(); Check(!accounts.AutomatedUnlocked,"exact 100M remains locked");
        tablet.account.cash++; accounts.EvaluateUnlock(); Check(accounts.AutomatedUnlocked,"100M plus one unlocks");
        accounts.Transfer(tablet.account,100000m,out _); var op=TradingOperationController.Instance;
        Check(op.StartSpread(stock),"spread starts using work account"); Check(!op.StartSpread(stock),"duplicate start blocked");
        seller.SellLimit(op.SpreadBid,2m,0,stock);
        Check(op.SpreadFills.Count==1&&op.Bought==2m&&op.SpreadFills[0].Time.StartsWith("DAY "),"spread captures actual matching engine fill and game date");
        Check(second.account.ReservedCash>0&&tablet.account.ReservedCash==0,"spread reserves only work cash");
        op.CancelSpread(); Check(second.account.ReservedCash==0,"stop returns reservation"); Check(op.StartSpread(stock),"restart succeeds");
        seller.SellLimit(op.SpreadBid,1m,0,stock);
        Check(FakeNewsManager.FormatCooldown(600)=="05:20","600 real seconds displayed as 5h20 game time at default rate");
        var clock=new NewsPublicationClock();
        Check(clock.TryConsume(100)&&!clock.TryConsume(699.999)&&clock.TryConsume(700),"cooldown real-time boundary is exactly 600 seconds");
        Check(clock.Restore(500,120,10)&&clock.Remaining(10)==380&&clock.Remaining(15)==375,"cooldown reload accounts for real offline duration");
        Check(!clock.Restore(601,0,10)&&!clock.TryConsume(double.NaN),"cooldown rejects invalid saved times");
    }
    private static void InteractionChecks()
    {
        if (mode==0)
        {
            var ui=tablet.dashboard; Click(ui.sort); Check(ui.SortMode==1,"sort button follows actual raycast");
            var order=ui.Ordered.ToArray(); stock.currentPrice*=2; ui.Refresh(); Check(order.SequenceEqual(ui.Ordered),"price ticks preserve clicked sort order");
            Canvas.ForceUpdateCanvases(); var scroll=ui.scroll; var p=new PointerEventData(EventSystem.current) { position=Center(scroll.viewport),scrollDelta=new Vector2(0,-3) };
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits); p.pointerPressRaycast=hits[0]; p.pressPosition=p.position;
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.beginDragHandler); p.position+=new Vector2(0,120); p.delta=new Vector2(0,120);
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.dragHandler); ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            Check(scroll.verticalNormalizedPosition<.99f,"holdings responds to actual world-space drag"); scroll.verticalNormalizedPosition=1;
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.scrollHandler); Check(scroll.verticalNormalizedPosition<1,"holdings scroll handles wheel");
            ui.scroll.verticalNormalizedPosition=1;
        }
        if (mode==1)
        {
            var input=tablet.orders.price; input.Select(); input.ActivateInputField();
            Check(EventSystem.current.currentSelectedGameObject==input.gameObject,"price input owns keyboard focus");
            input.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            tablet.orders.price.text="100"; tablet.orders.quantity.text="3";
            typeof(DeviceTerminalUI).GetMethod("Order",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(tablet.orders,null);
            int count=tablet.orders.session.ActiveOrders.Count; Click(tablet.orders.review); Check(tablet.orders.session.ActiveOrders.Count==count+1,"actual primary button adds independent order");
            var cancel=tablet.contentRoot.GetComponentsInChildren<Button>().First(b=>b.name=="Button :: Cancel Pending Order"); Click(cancel);
            Check(tablet.orders.session.ActiveOrders.Count==count,"row cancel leaves other requests");
        }
        if (mode==3)
        {
            Click(tablet.orderButton);
            Check(tablet.orders.tradeView!=null&&tablet.orders.panel.activeInHierarchy&&tablet.orders.session.account==PlayerAccounts.Instance.automated,"work-account manual orders initialize after unlock");
            tablet.ShowPage(TabletAccountUI.Page.Automation);
        }
        if (mode==5)
        {
            var button=tablet.contentRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Publish Draft");
            int before=NewsManager.Instance.ArticleVersion; Click(button);
            Check(NewsManager.Instance.ArticleVersion==before+1&&FakeNewsManager.Instance.PlayerCooldown>598,"actual studio publishes one article and starts real cooldown");
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            Check(NewsManager.Instance.ArticleVersion==before+1,"repeat publish cannot bypass cooldown before UI refresh");
        }
    }
    private static Vector2 Center(RectTransform r) => RectTransformUtility.WorldToScreenPoint(Camera.main,r.TransformPoint(r.rect.center));
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); var p=new PointerEventData(EventSystem.current) {position=Center((RectTransform)button.transform)};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"raycast reaches "+button.name+"; hits="+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name))+"; active="+button.gameObject.activeInHierarchy);
        ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Audit(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        var problems=new List<string>();
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            if (text.GetComponentInParent<ScrollRect>()!=null||text.GetComponentInParent<TMP_InputField>()!=null)
            { continue; }
            text.ForceMeshUpdate(); if (text.isTextTruncated||text.isTextOverflowing) { problems.Add(text.name+" / "+text.text+" / "+text.rectTransform.rect); }
        }
        File.WriteAllLines(Path.Combine(output,"layout-"+GameLocale.Code+".txt"),problems);
        // Capture evidence before reporting layout failures; every reported failure must be corrected.
        Check(problems.Count==0,"visible fixed labels fit "+GameLocale.Code+": "+string.Join(" | ",problems));
    }
    private static void SaveChecks()
    {
        if (mode==6)
        {
            SessionStore.Restore(File.ReadAllText("Validation/Settings/TabletKitSave01/session-v2.json"));
            Check(tablet.events.records.Fills.Count>0&&tablet.events.records.Fills[0].Time.StartsWith("DAY "),"cold process restores dated journal");
            Check(tablet.events.records.Fills[0].Stock==CorporateEconomy.Instance.PlayerCompany,"cold restored journal uses actual stock identity");
            Check(errors==0,"cold restore no runtime errors"); Finish(null); return;
        }
        SessionStore.Restore(File.ReadAllText("Validation/Settings/CompanyListing02/session-v2.json"));
        var journal=tablet.events.records; var sample=CorporateEconomy.Instance.PlayerCompany;
        string stamp=DeviceMarketJournal.FullStamp(); journal.Fills.Insert(0,new DeviceMarketJournal.Fill {Stock=sample,Time=stamp,Price=sample.currentPrice,Quantity=2,Buy=true});
        SessionStore.Save(); string saved=File.ReadAllText(SessionStore.PathName); journal.Fills.Clear(); SessionStore.Restore(saved);
        Check(journal.Fills.Count>0&&journal.Fills[0].Time==stamp&&journal.Fills[0].Stock==CorporateEconomy.Instance.PlayerCompany,"journal game date and stock identity survive checkpoint reload");
        Check(errors==0,"save reload no runtime errors"); Finish(null);
    }
    private static void Capture(string name,int width,int height)
    {
        var camera=Camera.main; var overlays=new List<(Canvas canvas,Camera camera,float distance)>();
        var target=new RenderTexture(width,height,24); var prior=camera.targetTexture; var active=RenderTexture.active; Texture2D pixels=null;
        try
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode!=RenderMode.ScreenSpaceOverlay)
                { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+.1f;
            }
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=prior; RenderTexture.active=active; if (pixels!=null) { Object.Destroy(pixels); } target.Release(); Object.Destroy(target);
            foreach (var item in overlays) { item.canvas.renderMode=RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera=item.camera; item.canvas.planeDistance=item.distance; }
        }
    }
    private static void Finish(string failure)
    {
        if (finished)
        { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); } PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class TabletKitProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { TabletKitValidation.Step(); }
}

```

## 수정: Assets/Scripts/News/FakeNewsManager.cs
```diff
--- 작업 전/Assets/Scripts/News/FakeNewsManager.cs
+++ 수정본/Assets/Scripts/News/FakeNewsManager.cs
@@ -45,30 +45,30 @@
         manipulations[stock] = (manipulations[stock].time, true); return true;
     }
     public NewsArticle PreviewPlayer(Stock stock, string category, bool positive, int variant)
     {
         var manager = NewsManager.Instance;
         if (stock == null || manager == null) { return null; }
         string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
         return NewsWriting.Compose(manager.Writing, 0, stock.symbol, stock.stockName, title, positive ? .64 : -.64,
-            DeviceMarketJournal.Stamp(), SessionClock.Time, true, category, variant);
+            DeviceMarketJournal.FullStamp(), SessionClock.Time, true, category, variant);
     }
     public bool TryPublishPlayer(Stock stock, string category, bool positive, int variant, out NewsArticle article, out string reason)
     {
         article = null;
         if (!PlayerUnlocked) { reason = "2번 계좌 해금 후 사용할 수 있습니다."; return false; }
         if (stock == null || !stock.IsListed || !stock.AcceptsOrders || !MarketDayController.CanTrade)
         { reason = "거래 중인 종목을 선택하세요."; return false; }
         if (PlayerCooldown > 0) { reason = "다음 발행까지 " + FormatCooldown(PlayerCooldown); return false; }
         if (NewsManager.Instance == null) { reason = "뉴스 연결을 준비하고 있습니다."; return false; }
         article = PublishRumor(stock, category, positive, variant);
         if (article == null) { reason = "기사 발행에 실패했습니다. 다시 확인하세요."; return false; }
         playerClock.TryConsume(Time.realtimeSinceStartupAsDouble);
-        reason = "발행 완료 · 다음 발행까지 10:00";
+        reason = "발행 완료 · 재사용 대기 " + FormatCooldown(PlayerCooldown);
         ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = -1,
             kind = "PLAYER_NEWS_PUBLISHED", reason = stock.symbol + " " + category, value = article.sentiment });
         return true;
     }
     private NewsArticle PublishRumor(Stock stock, string category, bool positive, int variant)
     {
         var manager = NewsManager.Instance;
         string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
@@ -98,12 +98,11 @@
     public bool RestorePlayerCooldown(double remaining, long savedUtcTicks)
     {
         if (savedUtcTicks < 0 || savedUtcTicks > DateTime.MaxValue.Ticks) { return false; }
         double offline = savedUtcTicks == 0 ? 0 : Math.Max(0, (DateTime.UtcNow.Ticks - savedUtcTicks) / (double)TimeSpan.TicksPerSecond);
         return playerClock.Restore(remaining, offline, Time.realtimeSinceStartupAsDouble);
     }
     public static string FormatCooldown(double value)
     {
-        int seconds = (int)Math.Ceiling(Math.Max(0, value));
-        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
+        return DeviceMarketJournal.Duration(Math.Max(0,value)*DeviceMarketJournal.GameSecondsPerRealSecond/60d);
     }
 }

```

## 수정: Assets/Scripts/News/NewsDeskUI.cs
```diff
--- 작업 전/Assets/Scripts/News/NewsDeskUI.cs
+++ 수정본/Assets/Scripts/News/NewsDeskUI.cs
@@ -12,16 +12,17 @@
     private DeviceTerminalUI terminal;
     private TMP_FontAsset font;
     private RectTransform listContent, articleContent;
     private ScrollRect listScroll, articleScroll;
     private TextMeshProUGUI articleText, countText, empty, resultText, draftText, cooldownText;
     private Button all, owned, current, rumors, categoryButton, order, publish, studioButton, draftCategory, draftStock, positive, negative;
     private TMP_InputField search;
     private GameObject reading, studio, picker;
+    private readonly List<GameObject> readerChrome=new();
     private readonly List<NewsArticle> visible = new(40);
     private readonly List<Button> rows = new(40);
     private readonly List<TextMeshProUGUI> titles = new(40), meta = new(40);
     private readonly Dictionary<DeviceMarketJournal.Entry, NewsArticle> alerts = new();
     private readonly StringBuilder buffer = new();
     private NewsArticle selected, draft;
     private Stock lastStock, previewStock;
     private string category = "all", query = "", mode = "all";
@@ -84,17 +85,19 @@
             Panel(button.transform,"Article Row Divider",0,.99f,1,.005f,Line);
             rows.Add(button); titles.Add(title); meta.Add(details);
         }
         Button(transform,"News Management","기업 경영",.025f,.94f,.19f,.045f,() => navigation.ShowPage(TabletAccountUI.Page.Management));
         Button(transform,"News Growth","성장·권한",.225f,.94f,.19f,.045f,() => terminal.growth.onClick.Invoke());
         Button(transform,"Market Alerts","알림 기록",.435f,.94f,.19f,.045f,() => SetMode("alerts"));
         if (navigation.second)
         { studioButton = Button(transform,"News Studio","기사 만들기",.73f,.94f,.245f,.045f,OpenStudio); }
-        BuildStudio(); RefreshList();
+        BuildStudio();
+        foreach (Transform child in transform) { if (child.gameObject!=studio) { readerChrome.Add(child.gameObject); } }
+        RefreshList();
     }
     private void SetMode(string value) { mode = value; RefreshList(); }
     void OnEnable() { version = -1; next = 0; }
     private void ChooseCategory()
     {
         var catalog = NewsManager.Instance?.Writing;
         if (catalog == null) { return; }
         var choices = new string[catalog.narratives.Length + 1]; choices[0] = "전체 분야";
@@ -245,17 +248,17 @@
         if (stock == null || !stock.AcceptsOrders) { return; }
         StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock)); navigation.ShowPage(TabletAccountUI.Page.Orders);
     }
     private void BuildStudio()
     {
         studio = Area(transform,"News Studio",.025f,.17f,.95f,.75f).gameObject;
         Panel(studio.transform,"Studio Background",0,0,1,1,Surface);
         Label(studio.transform,"Studio Heading","가짜 뉴스 발행",0,0,.70f,.075f,40,Ink,true);
-        Button(studio.transform,"Close Studio","기사 목록",.79f,0,.21f,.065f,() => { studio.SetActive(false); reading.SetActive(true); });
+        Button(studio.transform,"Close Studio","기사 목록",.79f,0,.21f,.065f,() => navigation.ShowPage(TabletAccountUI.Page.Events));
         draftStock = Button(studio.transform,"Draft Stock","대상 종목",0,.10f,.46f,.07f,ChooseStock);
         draftCategory = Button(studio.transform,"Draft Theme","분야",.49f,.10f,.51f,.07f,() =>
         {
             var catalog = NewsManager.Instance.Writing; var options = new string[catalog.narratives.Length];
             for (int i = 0; i < options.Length; i++) { options[i] = catalog.narratives[i].label; }
             Choose(options,draftCategoryIndex,.50f,.31f,.475f,index => { draftCategoryIndex = index; RefreshDraft(); });
         });
         positive = Button(studio.transform,"Positive Draft","호재 방향",0,.20f,.24f,.06f,() => { optimistic = true; RefreshDraft(); });
@@ -265,21 +268,25 @@
         draftText = Label(previewContent,"Draft Text","",0,0,1,1,31,Ink); draftText.richText = true; draftText.overflowMode = TextOverflowModes.Overflow;
         draftText.textWrappingMode = TextWrappingModes.Normal; draftText.lineSpacing = 6;
         cooldownText = Label(studio.transform,"Publication Cooldown","",0,.80f,.65f,.055f,27,Muted);
         resultText = Label(studio.transform,"Publication Result","",0,.88f,.65f,.11f,28,Ink);
         publish = Button(studio.transform,"Publish Draft","이 초안 발행",.70f,.85f,.30f,.12f,Publish);
         TabletControlStyle.Emphasize(publish,true);
         studio.SetActive(false);
     }
-    private void OpenStudio()
+    public void OpenStudio()
     {
         if (!navigation.second || navigation.Locked) { return; }
-        reading.SetActive(false); studio.SetActive(true); RefreshDraft();
-    }
+        foreach (var element in readerChrome) { element.SetActive(false); }
+        Rect((RectTransform)studio.transform,.025f,.02f,.95f,.96f);
+        studio.SetActive(true); RefreshDraft();
+    }
+    public void OpenReader()
+    { foreach (var element in readerChrome) { element.SetActive(true); } studio.SetActive(false); RefreshList(); }
     private void ChooseStock()
     {
         var manager = StockManager.Instance;
         if (manager == null || manager.stocks.Count == 0) { return; }
         var stocks = manager.stocks.ToArray(); var options = new string[stocks.Length];
         for (int i = 0; i < stocks.Length; i++) { options[i] = stocks[i].stockName; }
         Choose(options,manager.stocks.IndexOf(manager.CurrentStock),.025f,.31f,.437f,index =>
         { int currentIndex = manager.stocks.IndexOf(stocks[index]); if (currentIndex >= 0) { manager.SelectStock(currentIndex); RefreshDraft(); } });
@@ -293,17 +300,17 @@
         draft = publisher.PreviewPlayer(previewStock,theme.key,optimistic,variant);
         Text(draftStock,"대상 · " + previewStock.stockName + " ▼"); Text(draftCategory,"분야 · " + theme.label + " ▼");
         TabletControlStyle.Emphasize(positive,optimistic); TabletControlStyle.Emphasize(negative,!optimistic);
         string text = "<b>" + Safe(draft.title) + "</b>\n\n" + Safe(draft.summary) + "\n\n" + Safe(draft.body);
         Set(draftText,text); var content = (RectTransform)draftText.transform.parent;
         float width = Math.Max(100,((RectTransform)content.parent).rect.width - 16);
         float height = draftText.GetPreferredValues(GameLocale.Text(text),width,0).y + 30; content.sizeDelta = new Vector2(0,height);
         Rect(draftText.rectTransform,0,0,1,1); draftText.rectTransform.offsetMax = new Vector2(-16,0);
-        if (Time.unscaledTime >= resultUntil) { Set(resultText,"공개되면 투자자마다 다르게 받아들입니다.\n후속 확인 보도가 나올 수 있습니다."); }
+        if (Time.unscaledTime >= resultUntil) { Set(resultText,""); }
     }
     private void Publish()
     {
         if (draft == null || !navigation.second || navigation.Locked) { return; }
         // Publish the previewed company, even if the user changes global selection before this frame.
         var publisher = FakeNewsManager.Instance;
         if (publisher == null) { return; }
         publisher.TryPublishPlayer(previewStock,draft.category,optimistic,variant,out var article,out string reason);
@@ -325,17 +332,17 @@
         if (version != news.ArticleVersion || stock != lastStock && mode == "current" || mode == "alerts" && alertVersion != terminal.records.Version) { RefreshList(); }
         lastStock = stock;
         int responses = CompanyNewsAudience.Instance == null ? 0 : CompanyNewsAudience.Instance.Version;
         if (responses != responseVersion) { responseVersion = responses; RenderArticle(); }
         if (!studio.activeSelf) { return; }
         if (stock != previewStock) { RefreshDraft(); }
         var publisher = FakeNewsManager.Instance;
         double remaining = publisher == null ? 0 : publisher.PlayerCooldown;
-        Set(cooldownText,remaining > 0 ? "다음 발행까지 " + FakeNewsManager.FormatCooldown(remaining) + " · 현실시간" : "발행 가능 · 발행 후 현실시간 10분 대기");
+        Set(cooldownText,remaining > 0 ? "재사용 대기 · " + FakeNewsManager.FormatCooldown(remaining) : "발행 가능");
         publish.interactable = publisher != null && publisher.PlayerUnlocked && remaining <= 0 && MarketDayController.CanTrade && previewStock?.AcceptsOrders == true;
         Text(publish,remaining > 0 ? "대기 " + FakeNewsManager.FormatCooldown(remaining) : "이 초안 발행");
     }
     private static Stock FindStock(string symbol)
     {
         if (StockManager.Instance == null) { return null; }
         foreach (var stock in StockManager.Instance.stocks) { if (stock.symbol == symbol && !stock.IsDelisted) { return stock; } }
         return null;

```

## 수정: Assets/Scripts/NightShift/CompanyGrowthUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/CompanyGrowthUI.cs
+++ 수정본/Assets/Scripts/NightShift/CompanyGrowthUI.cs
@@ -5,18 +5,20 @@
 using UnityEngine.UI;
 
 /// <summary>Existing tablet management canvas: real company funds, paid projects, and listing application.</summary>
 public sealed class CompanyGrowthUI : MonoBehaviour
 {
     private CompanyDashboardUI owner;
     private Stock stock;
     private TMP_FontAsset font;
-    private TextMeshProUGUI title, representative, valuation, cash, earnings, progress, state, result;
-    private readonly TextMeshProUGUI[] costs=new TextMeshProUGUI[3], criteria=new TextMeshProUGUI[3];
+    private TextMeshProUGUI title, representative, valuation, cash, earnings, profit, progress, state, result, allocation;
+    private readonly TextMeshProUGUI[] costs=new TextMeshProUGUI[3], criteria=new TextMeshProUGUI[4];
+    private readonly Image[] checks=new Image[4];
+    private readonly Button[] policies=new Button[3];
     private readonly Button[] projects=new Button[3];
     private Button fund, listing;
     private TMP_InputField amount;
     private Image progressFill;
     private float next;
     public Stock Company => stock;
     public static CompanyGrowthUI Install(CompanyDashboardUI parent)
     {
@@ -24,78 +26,114 @@
         Place((RectTransform)go.transform,0,0,1,1); go.GetComponent<Image>().color=new Color32(14,23,29,255);
         var view=go.AddComponent<CompanyGrowthUI>(); view.owner=parent; view.font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
         view.Build(); go.SetActive(false); return view;
     }
     public void Show(Stock company) { stock=company; gameObject.SetActive(true); transform.SetAsLastSibling(); Set(result,""); next=0; Refresh(); }
     public void Close() { gameObject.SetActive(false); owner.Refresh(); }
     private void Build()
     {
-        title=Label("Company","",.04f,.025f,.68f,.07f,44);
-        state=Label("Listing State","",.73f,.035f,.225f,.05f,26); state.alignment=TextAlignmentOptions.MidlineRight;
-        representative=Label("Representative","",.04f,.10f,.91f,.045f,25);
-        Label("Valuation Label","기업 평가액",.04f,.18f,.43f,.04f,26);
-        valuation=Label("Appraisal","—",.04f,.23f,.43f,.075f,44);
-        Label("Cash Label","기업 현금",.53f,.18f,.425f,.04f,26);
-        cash=Label("Cash","—",.53f,.23f,.425f,.075f,38); cash.alignment=TextAlignmentOptions.MidlineRight;
-        earnings=Label("Daily Earnings","",.04f,.325f,.91f,.05f,27);
+        GetComponent<Image>().color=TabletKitStyle.Background;
+        TabletKitStyle.Glyph("company",transform,.0f,.015f,.04f,.055f);
+        title=Label("Company","",.05f,.008f,.60f,.065f,42);
+        state=Label("Listing State","",.69f,.015f,.31f,.05f,28); state.alignment=TextAlignmentOptions.MidlineRight;
+        representative=Label("Representative","",.05f,.08f,.95f,.045f,26);
+        string[] captions={"기업 평가액","기업 현금","일매출","순이익"};
+        for (int i=0;i<4;i++) { Label("Metric Caption "+i,captions[i],i*.25f,.16f,.23f,.045f,27).color=Muted; }
+        valuation=Label("Appraisal","—",0,.215f,.23f,.065f,39);
+        cash=Label("Cash","—",.25f,.215f,.23f,.065f,36);
+        earnings=Label("Daily Earnings","—",.5f,.215f,.23f,.065f,32);
+        profit=Label("Daily Profit","—",.75f,.215f,.25f,.065f,32);
+        foreach (var value in new[]{valuation,cash,earnings,profit}) { TradingOrderView.Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight; }
+        Surface("Company Summary Divider",0,.30f,1,.0015f,TabletKitStyle.Line);
+        string[] policyNames={"배당","재투자","비축"};
+        for (int i=0;i<3;i++)
+        { int index=i; policies[i]=Button("Policy "+i,policyNames[i],i*.225f,.33f,.21f,.065f,()=> { CorporateEconomy.Instance.SetPolicy(stock,(CompanyPolicy)index); Refresh(); }); }
+        allocation=Label("Allocation","",0,.405f,.66f,.045f,26);
+        Surface("Projects Divider",0,.47f,.66f,.0015f,TabletKitStyle.Line);
         for (int i=0;i<3;i++)
         {
-            int index=i; float x=.04f+i*.315f;
-            projects[i]=Button("Project "+i,CorporateEconomy.ProjectName((CompanyProject)i),x,.41f,.285f,.075f,()=>
+            int index=i; float y=.49f+i*.105f;
+            TabletKitStyle.Glyph(new[]{"factory","sales","research"}[i],transform,0,y,.04f,.06f);
+            Label("Project Title "+i,CorporateEconomy.ProjectName((CompanyProject)i),.05f,y,.35f,.045f,29);
+            projects[i]=Button("Project "+i,"투자",.54f,y,.12f,.07f,()=>
             { CorporateEconomy.Instance.StartProject(stock,(CompanyProject)index); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
-            costs[i]=Label("Project Cost "+i,"",x,.49f,.285f,.12f,25);
+            costs[i]=Label("Project Cost "+i,"",.05f,y+.047f,.47f,.045f,25);
         }
-        progress=Label("Project Progress","",.04f,.625f,.91f,.045f,26);
-        var track=Surface("Project Track",.04f,.68f,.91f,.006f,new Color32(44,59,67,255));
+        progress=Label("Project Progress","",0,.815f,.66f,.04f,25);
+        var track=Surface("Project Track",0,.864f,.66f,.006f,TabletKitStyle.Line);
         var fill=Surface("Project Fill",0,0,1,1,new Color32(37,175,192,255)); fill.transform.SetParent(track.transform,false); Place((RectTransform)fill.transform,0,0,1,1); progressFill=fill;
-        for (int i=0;i<3;i++) { criteria[i]=Label("IPO Criterion "+i,"",.04f+i*.315f,.715f,.285f,.10f,25); }
-        amount=Input(.04f,.845f,.24f,.065f); amount.SetTextWithoutNotify("5000000");
-        fund=Button("Fund Company","출자",.295f,.845f,.15f,.065f,()=>
+        Surface("IPO Divider",.69f,.33f,.0015f,.54f,TabletKitStyle.Line);
+        Label("IPO Heading","상장 준비",.72f,.33f,.28f,.06f,33);
+        for (int i=0;i<4;i++)
+        {
+            checks[i]=TabletKitStyle.Glyph("check",transform,.72f,.415f+i*.085f,.03f,.045f);
+            criteria[i]=Label("IPO Criterion "+i,"",.765f,.405f+i*.09f,.235f,.085f,26);
+            criteria[i].textWrappingMode=TextWrappingModes.Normal;
+        }
+        amount=Input(0,.91f,.24f,.07f); amount.SetTextWithoutNotify("5000000");
+        fund=Button("Fund Company","출자",.255f,.91f,.12f,.07f,()=>
         { decimal.TryParse(amount.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value); CorporateEconomy.Instance.FundCompany(stock,owner.Account,value); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
-        listing=Button("Apply IPO","상장 신청",.50f,.845f,.235f,.065f,()=>
+        listing=Button("Apply IPO","상장 신청",.72f,.79f,.28f,.08f,()=>
         { CorporateEconomy.Instance.RequestListing(stock); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
-        Button("Back To Company","기업 정보",.75f,.845f,.205f,.065f,Close);
-        result=Label("Action Result","",.04f,.935f,.91f,.045f,25);
+        Button("Back To Company","기업 정보",.72f,.91f,.28f,.07f,Close);
+        result=Label("Action Result","",.395f,.91f,.29f,.07f,25); result.textWrappingMode=TextWrappingModes.Normal;
     }
     public void Refresh()
     {
         var economy=CorporateEconomy.Instance; var data=stock?.business; var growth=data?.growth;
-        if (economy==null || growth==null) { return; }
+        if (economy==null || growth==null)
+        { return; }
         var rules=economy.GrowthRules; bool manage=economy.CanManage(stock);
         Set(title,stock.stockName);
         string name=PlayerAccounts.IsPlayer(stock.Owner) ? "플레이어" : (stock.Owner as TradeBot)?.Social?.Nickname ?? "—";
-        Set(representative,"대표 · "+name);
+        decimal ratio=stock.issuedShares>0 ? PlayerAccounts.Instance.Owned(stock)/stock.issuedShares*100 : 0;
+        Set(representative,"대표 · "+name+"   ·   지분 "+ratio.ToString("0.##")+"%");
         Set(state,stock.IsListed ? "상장" : growth.RequestedDay>0 ? "상장 심사 대기" : "비상장");
         Set(valuation,Money(growth.Appraisal)); Set(cash,Money(data.Cash));
-        Set(earnings,data.CloseFinished ? "일매출 "+Money(data.Revenue)+"   순이익 "+Money(data.Profit) : "실적 —");
+        Set(earnings,data.CloseFinished ? Money(data.Revenue) : "—");
+        Set(profit,data.CloseFinished ? Money(data.Profit) : "—"); profit.color=InvestmentDashboard.ProfitColor(data.Profit);
+        var businessRules=economy.Rules;
+        double dividend=data.Policy==CompanyPolicy.Dividend ? businessRules.dividendPayout : data.Policy==CompanyPolicy.Investment ? businessRules.investmentPayout : businessRules.reservePayout;
+        double reinvest=data.Policy==CompanyPolicy.Investment ? businessRules.growthInvestment : data.Policy==CompanyPolicy.Dividend ? businessRules.dividendInvestment : 0;
+        Set(allocation,"배당 "+dividend.ToString("0%")+"  ·  재투자 "+reinvest.ToString("0%")+"  ·  비축 "+(1-dividend-reinvest).ToString("0%"));
+        for (int i=0;i<3;i++) { policies[i].interactable=manage; TabletKitStyle.Flat(policies[i].image,(int)data.Policy==i ? TabletKitStyle.Hover : TabletKitStyle.Background); policies[i].GetComponentInChildren<TMP_Text>().color=(int)data.Policy==i ? TabletKitStyle.Accent : Muted; }
         for (int i=0;i<3;i++)
         {
             var type=(CompanyProject)i; int level=growth.Level(type); decimal cost=growth.ProjectCost(data,type,rules);
             int target=Math.Min(level+1,rules.maximumLevel);
             string effect=i==0 ? "생산 ×"+(1+target*rules.capacityGain).ToString("0.00") : i==1 ? "수요 +"+(target*rules.salesGain*100).ToString("0")+"%p" : "변동비 −"+(target*rules.researchSaving*100).ToString("0")+"%";
-            Set(costs[i],"Lv. "+level+" / "+rules.maximumLevel+" · "+effect+"\n"+(level>=rules.maximumLevel ? "최대 단계" : Money(cost)+" · "+rules.projectDays+"일"));
+            Set(costs[i],"Lv."+level+" · "+GameLocale.Text(effect)+" · "+(level>=rules.maximumLevel ? "최대 단계" : Money(cost)+" · "+rules.projectDays+"일"));
             projects[i].interactable=manage && data.Debt==0 && growth.Project<0 && growth.LastProjectDay!=MarketDayController.Instance.Day && level<rules.maximumLevel && data.Cash>=cost;
+            projects[i].GetComponentInChildren<TMP_Text>().color=projects[i].interactable ? TabletKitStyle.Ink : Muted;
         }
         Set(progress,growth.Project>=0 ? CorporateEconomy.ProjectName((CompanyProject)growth.Project)+" · DAY "+growth.CompleteDay+" 완료" : "진행 중인 투자 없음");
         float fraction=growth.Project<0 ? 0 : Mathf.Clamp01((MarketDayController.Instance.Day-growth.LastProjectDay)/(float)Math.Max(1,growth.CompleteDay-growth.LastProjectDay));
         var rect=(RectTransform)progressFill.transform; rect.anchorMax=new Vector2(fraction,1);
         Set(criteria[0],"상장 평가액\n"+Money(rules.listingValue));
         Set(criteria[1],"흑자 결산\n"+growth.ProfitableCloses+" / "+rules.listingProfitableCloses);
         Set(criteria[2],"사업 확장\n"+growth.CompletedProjects+" / "+rules.listingProjects);
+        Set(criteria[3],"미지급 비용\n"+Money(data.Debt));
         criteria[0].color=growth.Appraisal>=rules.listingValue ? Ready : Muted;
         criteria[1].color=growth.ProfitableCloses>=rules.listingProfitableCloses ? Ready : Muted;
         criteria[2].color=growth.CompletedProjects>=rules.listingProjects ? Ready : Muted;
+        criteria[3].color=data.Debt==0 ? Ready : Muted;
+        for (int i=0;i<4;i++) { checks[i].sprite=TabletKitStyle.Icon(criteria[i].color==Ready ? "check" : "minus"); checks[i].color=criteria[i].color; }
         fund.interactable=amount.interactable=manage && !stock.IsListed && owner.Account.GetHolding(stock)==stock.issuedShares;
         listing.interactable=manage && !stock.IsListed && growth.RequestedDay==0 && growth.Eligible(data,rules);
         TabletControlStyle.Emphasize(listing,listing.interactable);
     }
-    void Update() { if (Time.unscaledTime<next) { return; } next=Time.unscaledTime+.5f; Refresh(); }
+    void Update()
+    {
+        if (Time.unscaledTime<next)
+        { return; }
+        next=Time.unscaledTime+.5f; Refresh();
+    }
     private static readonly Color Muted=new Color32(146,161,172,255), Ready=new Color32(98,191,176,255);
-    private static string Money(decimal value) => value.ToString("N0")+"원";
+    private static string Money(decimal value) => DeviceTerminalUI.Money(value);
     private static void Set(TMP_Text text,string value) { DeviceTerminalUI.Set(text as TextMeshProUGUI,value); }
     private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
     {
         var go=new GameObject("Text :: "+name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
         var text=go.GetComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=size; text.enableAutoSizing=false; text.color=new Color32(230,235,239,255);
         text.alignment=TextAlignmentOptions.MidlineLeft; text.textWrappingMode=TextWrappingModes.NoWrap; text.raycastTarget=false; Set(text,value); return text;
     }
     private Image Surface(string name,float x,float y,float w,float h,Color color)

```

## 수정: Assets/Scripts/NightShift/DailyIncomeUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/DailyIncomeUI.cs
+++ 수정본/Assets/Scripts/NightShift/DailyIncomeUI.cs
@@ -96,16 +96,18 @@
         view.panel.SetActive(false);
     }
     private void Initialize()
     {
         font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
         scale = ((RectTransform)transform).rect.width / 1600f;
         if (scale <= 0f) { scale = 1f; }
     }
+    public void OpenStatement()
+    { GetComponentInParent<TabletAccountUI>().ShowPage(TabletAccountUI.Page.Dashboard); Open(); }
     private void Open()
     {
         if (roomMode && DailyIncomeController.Instance != null)
         { feeInput.SetTextWithoutNotify(DailyIncomeController.Instance.Ledger.NextFee.ToString()); GameLocalizedText.Set(result,""); }
         if (roomMode && !panel.activeSelf) { originalSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
         panel.SetActive(true); panel.transform.SetAsLastSibling(); next = 0f; Refresh();
     }
     private void Close()

```

## 수정: Assets/Scripts/NightShift/DeviceMarketJournal.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/DeviceMarketJournal.cs
+++ 수정본/Assets/Scripts/NightShift/DeviceMarketJournal.cs
@@ -28,20 +28,35 @@
         public readonly decimal[] Buy = new decimal[60], Sell = new decimal[60];
     }
     public readonly List<Entry> Events = new(40);
     public readonly List<Fill> Fills = new(64);
     public int Version { get; private set; }
     private readonly Dictionary<Stock, Tape> tapes = new();
     private string lastFeed;
     private float next;
+    public static double GameNow => MarketDayController.Instance?.AbsoluteGameMinute ?? 0;
+    public static double GameSecondsPerRealSecond => 28800d / Math.Max(.1d,MarketDayController.Instance?.tradingSeconds ?? 900d);
+    public static string FullStamp() => MarketDayController.Instance==null ? "DAY — —:—" : "DAY "+MarketDayController.Instance.CalendarDay+" "+Stamp();
+    public static string Duration(double gameMinutes)
+    {
+        long minutes=(long)Math.Ceiling(Math.Max(0,gameMinutes));
+        return (minutes/60).ToString("00")+":"+(minutes%60).ToString("00");
+    }
+    public void ReconnectSession()
+    {
+        foreach (var stock in StockManager.Instance.stocks) { stock.OnMatchedTrade-=Observe; stock.OnMatchedTrade+=Observe; if (!tapes.ContainsKey(stock)) { tapes.Add(stock,new Tape()); } }
+    }
+    public void ClearHistory() { Fills.Clear(); Events.Clear(); Version++; }
     public static string Stamp()
     {
         var day = MarketDayController.Instance;
-        int minute = day == null ? 540 : day.ClockMinute;
+        if (day==null)
+        { return "—:—"; }
+        int minute = day.ClockMinute;
         return (minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2");
     }
     void OnEnable()
     {
         StockManager.OnStockListed += Register;
         StockManager.OnStockDelisted += Remove;
         NewsManager.OnNewsPublished += News;
         StockStatistics.OnVolumeSurge += Surge;
@@ -51,19 +66,20 @@
         if (StockManager.Instance != null)
         {
             foreach (var stock in StockManager.Instance.stocks)
             { Register(stock); }
         }
     }
     private void Register(Stock stock)
     {
-        if (stock == null || tapes.ContainsKey(stock))
+        if (stock == null)
         { return; }
-        tapes.Add(stock, new Tape());
+        if (!tapes.ContainsKey(stock)) { tapes.Add(stock,new Tape()); }
+        stock.OnMatchedTrade -= Observe;
         stock.OnMatchedTrade += Observe;
     }
     private void Remove(Stock stock)
     {
         stock.OnMatchedTrade -= Observe;
         tapes.Remove(stock);
     }
     private void Observe(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
@@ -77,17 +93,17 @@
         { tape.Sell[slot] += quantity; }
         else
         { tape.Buy[slot] += quantity; }
         var player = account != null ? account : StockManager.Instance.playerAccount;
         if (buyer == seller || !ReferenceEquals(buyer, player) && !ReferenceEquals(seller, player))
         { return; }
         bool isBuy = ReferenceEquals(buyer, player);
         decimal basis = ReferenceEquals(resting.owner, player) ? resting.costBasis : player.SubmittedSellCost;
-        Fills.Insert(0, new Fill { Stock = stock, Time = Stamp(), Buy = isBuy, Price = price, Quantity = quantity, Realized = isBuy ? 0m : (price - basis) * quantity });
+        Fills.Insert(0, new Fill { Stock = stock, Time = FullStamp(), Buy = isBuy, Price = price, Quantity = quantity, Realized = isBuy ? 0m : (price - basis) * quantity });
         if (Fills.Count > 64)
         { Fills.RemoveAt(64); }
         Version++;
     }
     public void Flow(Stock stock, out decimal volume, out decimal buy)
     {
         volume = buy = 0m;
         if (stock == null || !tapes.TryGetValue(stock, out var tape))
@@ -102,17 +118,17 @@
     }
     private void News(Stock stock, long id, string title)
     { Add(stock, id, title, "시장 뉴스", "보도 · 사실 여부 별도 확인", false); }
     private void Surge(Stock stock, float multiple)
     { Add(stock, 0, stock.stockName + " 거래량 급증 (" + multiple.ToString("F1") + "배)", "실제 체결 관측", "관측된 변화", true); }
     private void Add(Stock stock, long id, string title, string source, string certainty, bool urgent)
     {
         Flow(stock, out decimal volume, out decimal buy);
-        Events.Insert(0, new Entry { Stock = stock, Id = id, Title = title, Source = source, Certainty = certainty, Urgent = urgent, Time = Stamp(), Volume = volume, BuyVolume = buy,
+        Events.Insert(0, new Entry { Stock = stock, Id = id, Title = title, Source = source, Certainty = certainty, Urgent = urgent, Time = FullStamp(), Volume = volume, BuyVolume = buy,
             Article = id > 0 ? NewsManager.Instance?.FindArticle(id) : null });
         if (Events.Count > 40)
         { Events.RemoveAt(40); }
         Version++;
     }
     void Update()
     {
         if (Time.unscaledTime < next)

```

## 수정: Assets/Scripts/NightShift/DeviceOrderSession.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/DeviceOrderSession.cs
+++ 수정본/Assets/Scripts/NightShift/DeviceOrderSession.cs
@@ -15,16 +15,18 @@
         public decimal Filled { get; internal set; }
         public decimal Spent { get; internal set; }
         public decimal Budget { get; internal set; }
         public decimal Unsubmitted { get; internal set; }
         public decimal PendingVolume { get; internal set; }
         public int Parts { get; internal set; }
         public float Interval { get; internal set; }
         public string Status { get; internal set; }
+        public string AcceptedAt { get; internal set; }
+        public string UpdatedAt { get; internal set; }
         internal Trader Owner;
         internal int Part;
         internal float Next;
         internal readonly List<Order> Pending = new(4);
     }
     public readonly struct PendingEntry
     {
         public readonly Request Request;
@@ -35,32 +37,35 @@
         public decimal Remaining => Request != null ? Request.PendingVolume + Request.Unsubmitted : Order.volume;
         public PendingEntry(Request request) { Request = request; Stock = request.Stock; Order = null; }
         public PendingEntry(Stock stock, Order order) { Request = null; Stock = stock; Order = order; }
     }
     public Trader account;
     public Stock Target { get; private set; }
     public Request Latest { get; private set; }
     public IReadOnlyList<Request> ActiveOrders => active;
+    public IReadOnlyList<Request> History => history;
+    private readonly List<Request> history = new(64);
     public bool Active => active.Count > 0;
     public int Number { get; private set; }
+    public int Version { get; private set; }
     public decimal Budget => Latest?.Budget ?? 0m;
     public decimal Spent => Latest?.Spent ?? 0m;
     public decimal Limit => Latest?.Limit ?? 0m;
     public decimal Quantity => Latest?.Quantity ?? 0m;
     public decimal Filled => Latest?.Filled ?? 0m;
     public decimal Unsubmitted => Latest?.Unsubmitted ?? 0m;
     public decimal PendingVolume => Latest?.PendingVolume ?? 0m;
     public decimal Reserved => PendingVolume * Limit;
     public bool Buy => Latest?.Buy ?? true;
     public int Parts => Latest?.Parts ?? 1;
     public float Interval => Latest?.Interval ?? 1f;
     public string Status => Active ? "대기 " + active.Count + "건" : Latest?.Status ?? "미제출";
     public string Feedback { get; private set; } = "가격·수량을 고른 뒤 매수 또는 매도 버튼을 누르세요.";
-    public decimal AvailableCash => Math.Max(0m, PlayerAccounts.Available(account) - ScheduledBuyCost);
+    public decimal AvailableCash => Math.Max(0m,PlayerAccounts.Available(account)+PlayerAccounts.ScheduledCash(account)-ScheduledBuyCost);
     public decimal RemainingBudget => AvailableCash;
     public decimal ScheduledBuyCost
     {
         get
         {
             decimal value = 0m;
             foreach (var request in active)
             {
@@ -137,17 +142,19 @@
         if (!string.IsNullOrEmpty(error))
         { Feedback = error; return false; }
         var request = new Request
         {
             Stock = stock, Owner = account, Number = ++Number, Buy = buy, Limit = price,
             Quantity = quantity, Unsubmitted = quantity, Parts = parts, Interval = interval,
             Budget = AvailableCash, Next = SessionClock.Time, Status = "접수 · 체결 대기"
         };
-        Latest = request; active.Add(request);
+        request.AcceptedAt=request.UpdatedAt=DeviceMarketJournal.FullStamp();
+        Latest = request; active.Add(request); history.Insert(0,request); Version++;
+        if (history.Count>64) { history.RemoveAt(64); }
         subscriptions.TryGetValue(stock, out int count);
         if (count == 0) { stock.OnMatchedTrade += OnFill; }
         subscriptions[stock] = count + 1;
         // Reserve synchronously so consecutive clicks cannot spend the same cash.
         SendSlice(request); RefreshPending(request);
         return true;
     }
     void Update()
@@ -163,17 +170,17 @@
             if (request.Unsubmitted > 0m && SessionClock.Time >= request.Next)
             { SendSlice(request); }
             RefreshPending(request);
         }
     }
     private void SendSlice(Request request)
     {
         decimal quantity = decimal.Floor(request.Quantity / request.Parts) + (request.Part < request.Quantity % request.Parts ? 1m : 0m);
-        if (request.Buy && quantity * request.Limit > PlayerAccounts.Available(request.Owner))
+        if (request.Buy && quantity * request.Limit > AvailableCash+request.Unsubmitted*request.Limit)
         { Cancel(request, "다음 회차의 주문 가능 현금 부족"); return; }
         if (!request.Buy && quantity > request.Owner.GetHolding(request.Stock))
         { Cancel(request, "다음 회차의 매도 가능 수량 부족"); return; }
         request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
         before.Clear();
         foreach (var order in snapshot) { before.Add(order); }
         request.Unsubmitted -= quantity; request.Part++;
         submitting = request;
@@ -219,16 +226,17 @@
         Request request = null;
         if (resting != null) { tracked.TryGetValue(resting, out request); }
         if (request == null) { request = submitting; }
         if (request == null || request.Stock != stock)
         { return; }
         if (request.Buy && ReferenceEquals(buyer, request.Owner) || !request.Buy && ReferenceEquals(seller, request.Owner))
         {
             request.Filled += volume;
+            request.UpdatedAt=DeviceMarketJournal.FullStamp(); Version++;
             if (request.Buy) { request.Spent += price * volume; }
         }
     }
     public bool Cancel(Request request, string reason = "미체결 취소")
     {
         if (request == null || !active.Contains(request))
         { return false; }
         // A fill may have finished since the list was drawn; do not report it as a cancellation.
@@ -237,16 +245,17 @@
         { return false; }
         foreach (var order in request.Pending)
         {
             request.Stock.orderBook.CancelOrder(order, request.Stock);
             tracked.Remove(order);
         }
         request.Pending.Clear(); request.PendingVolume = request.Unsubmitted = 0m;
         request.Status = "미체결 취소됨";
+        request.UpdatedAt=DeviceMarketJournal.FullStamp(); Version++;
         Feedback = "#" + request.Number.ToString("D4") + " " + reason + " · 체결된 " + request.Filled.ToString("N0") + "주는 유지됩니다.";
         Remove(request); return true;
     }
     public void Cancel(string reason = "미체결 취소")
     {
         for (int i = active.Count - 1; i >= 0; i--) { Cancel(active[i], reason); }
     }
     private void Remove(Request request)

```

## 수정: Assets/Scripts/NightShift/DeviceTerminalUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/DeviceTerminalUI.cs
+++ 수정본/Assets/Scripts/NightShift/DeviceTerminalUI.cs
@@ -49,17 +49,17 @@
     private PendingOrderListUI pendingOrders;
     private readonly StringBuilder text = new(1024);
     private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(166, 177, 186, 255), Up = new Color32(239, 89, 100, 255), Down = new Color32(76, 150, 232, 255), Active = new Color32(23, 55, 62, 255), Neutral = new Color32(23, 33, 38, 255);
     public static void Set(TextMeshProUGUI target, string value)
     {
         if (target != null && !GameLocalizedText.Matches(target,value))
         { GameLocalizedText.Set(target,value); }
     }
-    public static string Money(decimal value) => PriceTickPolicy.FormatMoney(value) + "원";
+    public static string Money(decimal value) => PriceTickPolicy.FormatMoney(value) + (GameLocale.Code=="ko" ? "원" : " "+GameLocale.Text("원"));
     void Start()
     {
         mainTab.onClick.AddListener(Show);
         legacy.deskTab.onClick.AddListener(Hide);
         legacy.legacyTab.onClick.AddListener(Hide);
         if (journal)
         {
             visualGrowth?.Configure(openRooms,closeGrowth);
@@ -69,16 +69,18 @@
                 eventButtons[i].onClick.AddListener(() => SelectEvent(row));
             }
             eventOrder.onClick.AddListener(OpenOrder);
             growth.onClick.AddListener(ShowGrowth); closeGrowth.onClick.AddListener(HideGrowth); openRooms.onClick.AddListener(OpenRooms);
         }
         else
         {
             ConfigureQuickOrder();
+            if (float.TryParse(interval.text,NumberStyles.Float,CultureInfo.InvariantCulture,out var realInterval))
+            { interval.SetTextWithoutNotify((realInterval*DeviceMarketJournal.GameSecondsPerRealSecond).ToString("0",CultureInfo.InvariantCulture)); }
             buy.onClick.AddListener(Buy); sell.onClick.AddListener(Sell);
             split.onClick.AddListener(Split); review.onClick.AddListener(Review);
             submit.onClick.AddListener(Submit); back.onClick.AddListener(Back);
             cancel.onClick.AddListener(Cancel); prepare.onClick.AddListener(Prepare);
             price.onValueChanged.AddListener(Edited); quantity.onValueChanged.AddListener(Edited);
             parts.onValueChanged.AddListener(Edited); interval.onValueChanged.AddListener(Edited);
         }
         if (navigation == null) { Show(); }
@@ -186,16 +188,17 @@
         };
         quickButtons[0].onClick.AddListener(CurrentPrice);
         quickButtons[1].onClick.AddListener(() => SetQuantity(.25m));
         quickButtons[2].onClick.AddListener(() => SetQuantity(.5m));
         quickButtons[3].onClick.AddListener(() => SetQuantity(.75m));
         quickButtons[4].onClick.AddListener(() => SetQuantity(1m));
         tradeView = panel.AddComponent<TradingOrderView>();
         tradeView.Configure(this,pendingRoot,quickQuantityPanel,quickButtons);
+        if (navigation!=null && navigation.GetComponent<TabletKitLayout>()!=null) { tradeView.FitCanvas(); }
     }
     private Button QuickButton(string name,string caption,float x,float width)
     {
         var rect = new GameObject("Button :: Order " + name,typeof(RectTransform)).GetComponent<RectTransform>();
         rect.SetParent(quickQuantityPanel.transform,false); Layout(rect,x,0,width,1);
         rect.gameObject.layer = quickQuantityPanel.layer;
         var image = rect.gameObject.AddComponent<Image>(); image.color = Neutral; image.raycastTarget = true;
         var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
@@ -231,17 +234,18 @@
     {
         return decimal.TryParse(field.text, NumberStyles.None, CultureInfo.InvariantCulture, out decimal value) ? value : 0m;
     }
     private string Check(out decimal p, out decimal q, out int n, out float seconds)
     {
         p = Number(price); q = Number(quantity);
         decimal rawParts = Number(parts), rawInterval = Number(interval);
         n = splitting && rawParts <= 10m ? (int)rawParts : splitting ? 0 : 1;
-        seconds = splitting && rawInterval <= 60m ? (float)rawInterval : splitting ? 0f : 3f;
+        double realSeconds=(double)rawInterval/DeviceMarketJournal.GameSecondsPerRealSecond;
+        seconds = splitting && realSeconds <= 60d ? (float)realSeconds : splitting ? 0f : 3f;
         return session.Validate(selected, buying, p, q, n, seconds);
     }
     private void Review()
     {
         string error = Check(out decimal p, out decimal q, out int n, out float seconds);
         if (!string.IsNullOrEmpty(error))
         { tradeView.ShowError(error); return; }
         if (!splitting)

```

## 수정: Assets/Scripts/NightShift/InvestmentPositionRow.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/InvestmentPositionRow.cs
+++ 수정본/Assets/Scripts/NightShift/InvestmentPositionRow.cs
@@ -24,10 +24,19 @@
         DeviceTerminalUI.Set(average, "내 평균 " + (basis > 0m && qty > 0m ? DeviceTerminalUI.Money(basis / qty) : "—"));
         DeviceTerminalUI.Set(value, priced ? DeviceTerminalUI.Money(valuation) : "—");
         DeviceTerminalUI.Set(profit, priced ? InvestmentDashboard.Signed(pnl) + "원" : "—");
         DeviceTerminalUI.Set(percent, priced && basis > 0m ? InvestmentDashboard.Rate(pnl, basis) : "—");
         profit.color = percent.color = priced ? InvestmentDashboard.ProfitColor(pnl) : InvestmentDashboard.Ink;
         background.color = selected ? new Color32(30, 54, 59, 255) : Color.clear;
         selection.enabled = selected;
     }
-    private void Select() { dashboard.Select(Stock); }
+    private void Select()
+    {
+        dashboard.Select(Stock);
+        dashboard.navigation.ShowPage(Stock.IsListed ? TabletAccountUI.Page.Orders : TabletAccountUI.Page.Management);
+        if (!Stock.IsListed)
+        {
+            var company=dashboard.navigation.GetComponentInChildren<CompanyDashboardUI>(true);
+            company.ShowCompany(Stock); company.OpenGrowth();
+        }
+    }
 }

```

## 수정: Assets/Scripts/NightShift/PendingOrderListUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/PendingOrderListUI.cs
+++ 수정본/Assets/Scripts/NightShift/PendingOrderListUI.cs
@@ -17,16 +17,17 @@
     private TMP_FontAsset reading;
     private RectTransform content;
     private float scale=1f;
     private readonly List<DeviceOrderSession.PendingEntry> entries=new(16);
     private readonly List<Row> rows=new(8);
     private int visibleCount=-1;
     private const float RowHeight=88f;
     public int Count => entries.Count;
+    public void SetScale(float value) { scale=value; visibleCount=-1; }
     public void Configure(DeviceOrderSession orders,TextMeshProUGUI fontTemplate,float fontScale=1f)
     {
         session=orders; template=fontTemplate; scale=fontScale; reading=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
         title=Label("Pending Heading",transform,30,0,0,128,56,736,400); GameLocalizedText.Set(title,"미체결");
         count=Label("Pending Order Count",transform,30,136,0,72,56,736,400); TradingOrderView.Numeric(count); count.alignment=TextAlignmentOptions.MidlineRight;
         GameLocalizedText.Set(Label("Pending Company Header",transform,24,0,64,270,32,736,400),"미체결 종목");
         var price=Label("Pending Price Header",transform,24,286,64,170,32,736,400); GameLocalizedText.Set(price,"가격 · 원"); price.alignment=TextAlignmentOptions.MidlineRight;
         var volume=Label("Pending Quantity Header",transform,24,472,64,144,32,736,400); GameLocalizedText.Set(volume,"잔량 / 체결"); volume.alignment=TextAlignmentOptions.MidlineRight;
@@ -50,17 +51,17 @@
             visibleCount=entries.Count; content.sizeDelta=new Vector2(0,visibleCount*RowHeight*scale);
             for(int i=0;i<rows.Count;i++) { rows[i].Root.gameObject.SetActive(i<visibleCount); }
         }
         for(int i=0;i<entries.Count;i++)
         {
             var entry=entries[i]; var row=rows[i]; row.Entry=entry;
             DeviceTerminalUI.Set(row.Side,entry.Buy?"매수":"매도"); row.Side.color=entry.Buy?new Color32(239,89,100,255):new Color32(76,150,232,255);
             DeviceTerminalUI.Set(row.Name,entry.Stock.stockName);
-            DeviceTerminalUI.Set(row.Symbol,entry.Stock.symbol+(entry.Request!=null && entry.Request.Unsubmitted>0 ? " · 분할" : ""));
+            DeviceTerminalUI.Set(row.Symbol,entry.Request!=null ? entry.Request.AcceptedAt ?? "—" : entry.Stock.symbol);
             DeviceTerminalUI.Set(row.Price,TradingOrderView.Number(entry.Price));
             DeviceTerminalUI.Set(row.Remaining,TradingOrderView.Number(entry.Remaining));
             DeviceTerminalUI.Set(row.Filled,entry.Request==null?"—":TradingOrderView.Number(entry.Request.Filled));
         }
     }
     public void CancelAll()
     {
         session.CopyPendingOrders(entries);

```

## 수정: Assets/Scripts/NightShift/PlayerAccounts.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/PlayerAccounts.cs
+++ 수정본/Assets/Scripts/NightShift/PlayerAccounts.cs
@@ -41,17 +41,27 @@
         return Instance != null ? ReferenceEquals(owner, Instance.primary) || ReferenceEquals(owner, Instance.automated) : StockManager.Instance != null && ReferenceEquals(owner, StockManager.Instance.playerAccount);
     }
     public decimal Owned(Stock stock) => primary.GetOwnedVolume(stock) + automated.GetOwnedVolume(stock);
     public static decimal TaxDebt(Trader account)
     {
         bool primaryAccount = account != null && (Instance != null ? account == Instance.primary : StockManager.Instance != null && account == StockManager.Instance.playerAccount);
         return primaryAccount ? DailyIncomeController.Instance?.Ledger.UnpaidTax ?? 0m : 0m;
     }
-    public static decimal Available(Trader account) => account == null ? 0m : Math.Max(0m, account.cash - account.ReservedCash - TaxDebt(account));
+    public static decimal Available(Trader account)
+    {
+        if (account==null)
+        { return 0m; }
+        return Math.Max(0m,account.cash-account.ReservedCash-TaxDebt(account)-ScheduledCash(account));
+    }
+    public static decimal ScheduledCash(Trader account)
+    {
+        var session=Instance==null ? null : account==Instance.primary ? Instance.primaryOrders : account==Instance.automated ? Instance.automatedOrders : null;
+        return session?.ScheduledBuyCost ?? 0m;
+    }
     public bool Transfer(Trader from, decimal amount, out string reason)
     {
         if (!AutomatedUnlocked || !CanUse(from))
         { reason = "2번 계좌 해금 후 이체할 수 있습니다."; return false; }
         if (amount < 1m || amount != decimal.Floor(amount) || amount > Available(from))
         { reason = "예약금을 제외한 주문 가능 현금 안에서 입력하세요."; return false; }
         Trader to = from == primary ? automated : primary;
         from.cash -= amount; to.cash += amount;

```

## 수정: Assets/Scripts/NightShift/SpreadTradingUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/SpreadTradingUI.cs
+++ 수정본/Assets/Scripts/NightShift/SpreadTradingUI.cs
@@ -5,17 +5,18 @@
 /// <summary>두 번째 태블릿의 기존 Canvas와 패널에 스프레드 제어를 표시한다.</summary>
 public sealed class SpreadTradingUI : MonoBehaviour
 {
     private TabletAccountUI tablet;
     private TradingOperationController operation;
     private TMP_FontAsset font;
     private float scale, next;
     private TextMeshProUGUI stockName, currentPrice, state, range, bid, ask, cash, shares, buyRemaining, sellRemaining, bought, sold, realized;
-    private Button start, cancel;
+    private Button start, cancel, previous, following;
+    private TextMeshProUGUI elapsed, updated, tape;
     private Slider slider;
     private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
     private static readonly Color Line = new Color32(44,60,69,255), Accent = new Color32(37,175,192,255);
 
     public static SpreadTradingUI Install(TabletAccountUI tablet)
     {
         if (!tablet.second || tablet.orders == null || tablet.orders.legacy == null)
         { return null; }
@@ -39,63 +40,80 @@
     private void Build()
     {
         font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
         scale = ((RectTransform)transform).rect.width / 1600f;
         if (scale <= 0f) { scale = 1f; }
         var background = GetComponent<Image>();
         if (background != null)
         { background.sprite = null; background.type = Image.Type.Simple; background.color = new Color32(13,23,29,255); background.raycastTarget = false; }
-        Label("Title","스프레드",32,12,280,56,36);
-        state = Label("State","",336,12,584,56,27); state.color = Muted;
-        Action("Manual Orders","주문",1056,12,240,() => tablet.ShowPage(TabletAccountUI.Page.Orders));
-        Action("News","뉴스",1328,12,240,() => tablet.ShowPage(TabletAccountUI.Page.Events));
-        Rule(92);
-        stockName = Label("Stock","",32,112,880,64,42);
-        currentPrice = Number("Current Price",1056,112,472,64,42);
-        Label("Current Price Unit","원",1536,112,32,64,25).color = Muted;
-        cash = Metric("주문 가능 현금",32,208,736,"원");
-        shares = Metric("매도 가능 수량",832,208,736,"주");
-        Rule(272);
-        Label("Bid Label","매수 대기",32,296,320,48,28).color = Muted;
-        Label("Ask Label","매도 대기",832,296,320,48,28).color = Muted;
-        bid = Number("Bid",32,344,688,64,40);
-        ask = Number("Ask",832,344,688,64,40);
-        Label("Bid Unit","원",736,344,32,64,25).color = Muted;
-        Label("Ask Unit","원",1536,344,32,64,25).color = Muted;
-        buyRemaining = Metric("대기 수량",32,424,736,"주");
-        sellRemaining = Metric("대기 수량",832,424,736,"주");
-        Rule(488);
-        Label("Range Label","스프레드 범위",32,504,400,48,28);
-        range = Label("Range","",1056,504,512,48,30); range.alignment = TextAlignmentOptions.MidlineRight;
+        Label("Title","스프레드",32,8,512,56,40);
+        state = Label("State","",960,8,608,56,28); state.color = TabletKitStyle.Accent;
+        stockName = Label("Stock","",88,88,720,64,38);
+        previous = Action("Previous Stock","‹",32,88,48,() => Select(-1));
+        following = Action("Next Stock","›",832,88,48,() => Select(1));
+        currentPrice = Number("Current Price",32,168,800,56,32);
+        Label("Current Price Unit","원",848,168,32,56,25).color=Muted;
+        Label("Range Label","스프레드 범위",32,264,480,48,29);
+        range = Label("Range","",544,264,336,48,30); range.alignment=TextAlignmentOptions.MidlineRight;
         BuildSlider();
-        bought = Metric("매수 체결",32,656,736,"주");
-        sold = Metric("매도 체결",832,656,736,"주");
-        realized = Metric("실현손익",832,712,736,"원");
-        start = Action("Start Spread","매매 시작",32,792,736,StartTrading,true);
-        cancel = Action("Cancel Spread","매매 취소",832,792,736,CancelTrading);
+        Label("Bid Label","예상 매수가",32,432,400,40,27).color=Muted;
+        Label("Ask Label","예상 매도가",480,432,400,40,27).color=Muted;
+        bid = Number("Bid",32,480,400,56,40); ask = Number("Ask",480,480,400,56,40);
+        cash = Metric("주문 가능 현금",32,584,848,"원");
+        shares = Metric("매도 가능 수량",32,640,848,"주");
+        elapsed = Label("Elapsed","",32,720,848,40,27);
+        start = Action("Start Spread","매매 시작",32,792,408,StartTrading,true);
+        cancel = Action("Cancel Spread","매매 취소",472,792,408,CancelTrading);
+        var divider=Graphic("Auto Divider",transform,TabletKitStyle.Line);
+        TradingOrderView.Place(divider.rectTransform,transform,920,88,1,752,1600,880);
+        Label("Pending Title","미체결",960,88,608,56,34);
+        buyRemaining=Metric("매수 잔량",960,168,608,"주");
+        sellRemaining=Metric("매도 잔량",960,224,608,"주");
+        bought=Metric("매수 체결",960,304,608,"주");
+        sold=Metric("매도 체결",960,360,608,"주");
+        realized=Metric("실현손익",960,416,608,"원");
+        Label("Recent Fills","최근 체결",960,504,608,48,34);
+        tape=Label("Fill Tape","—",960,568,608,176,27); tape.textWrappingMode=TextWrappingModes.Normal;
+        updated=Label("Updated","",960,784,608,56,25); updated.color=Muted;
     }
+    private void Select(int delta)
+    {
+        var manager=StockManager.Instance;
+        if (operation.Active || manager==null || manager.stocks.Count==0)
+        { return; }
+        int index=manager.stocks.IndexOf(manager.CurrentStock);
+        manager.SelectStock((index+delta+manager.stocks.Count)%manager.stocks.Count); next=0;
+    }
+
     private void BuildSlider()
     {
         var root = new GameObject("Area :: Spread Range",typeof(RectTransform),typeof(Image),typeof(Slider));
         var rect = (RectTransform)root.transform;
-        TradingOrderView.Place(rect,transform,48,564,1504,56,1600,880);
+        TradingOrderView.Place(rect,transform,48,328,816,56,1600,880);
         var hit = root.GetComponent<Image>(); hit.color = Color.clear;
         slider = root.GetComponent<Slider>(); slider.minValue = 1; slider.maxValue = TradingOperationController.MaxSpreadTicks; slider.wholeNumbers = true;
         var track = Graphic("Range Track",root.transform,Line); Stretch(track.rectTransform,new Vector2(0,.42f),new Vector2(1,.58f));
         var fillArea = SliderArea("Fill",root.transform,.42f,.58f);
         var handleArea = SliderArea("Handle",root.transform,.2f,.8f);
         var fill = Graphic("Range Fill",fillArea,Accent); Stretch(fill.rectTransform,Vector2.zero,Vector2.one);
         var handle = Graphic("Range Handle",handleArea,Ink); handle.rectTransform.sizeDelta = new Vector2(18*scale,0);
         slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
         slider.direction = Slider.Direction.LeftToRight;
         slider.SetValueWithoutNotify(operation == null ? 2 : operation.SpreadTicks);
         slider.onValueChanged.AddListener(value => { operation?.SetSpreadTicks(Mathf.RoundToInt(value)); next = 0f; });
-        Label("Minimum Range","1틱",32,616,160,32,24).color = Muted;
-        var max = Label("Maximum Range",TradingOperationController.MaxSpreadTicks+"틱",1408,616,160,32,24); max.color = Muted; max.alignment = TextAlignmentOptions.MidlineRight;
+        Label("Minimum Range","1틱",32,392,160,32,24).color = Muted;
+        var max = Label("Maximum Range",TradingOperationController.MaxSpreadTicks+" 틱",720,392,160,32,24); max.color = Muted; max.alignment = TextAlignmentOptions.MidlineRight;
+    }
+    public void FitCanvas()
+    {
+        var rect=(RectTransform)transform;
+        float fitted=Mathf.Min(rect.rect.width/1600f,rect.rect.height/880f);
+        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) { text.fontSize*=fitted/scale; }
+        scale=fitted;
     }
     private static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
     { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
     private RectTransform SliderArea(string name,Transform parent,float bottom,float top)
     {
         var obj = new GameObject("Area :: Spread " + name,typeof(RectTransform)); obj.transform.SetParent(parent,false); obj.layer = gameObject.layer;
         var rect = (RectTransform)obj.transform; Stretch(rect,new Vector2(0,bottom),new Vector2(1,top)); return rect;
     }
@@ -144,22 +162,37 @@
         if (operation == null || Time.unscaledTime < next)
         { return; }
         next = Time.unscaledTime + .25f;
         var stock = operation.SpreadRunning ? operation.Target : StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
         DeviceTerminalUI.Set(stockName,stock == null ? "—" : stock.stockName);
         DeviceTerminalUI.Set(currentPrice,stock == null ? "—" : TradingOrderView.Number(stock.currentPrice));
         DeviceTerminalUI.Set(state,operation.SpreadRunning ? operation.Feedback : operation.CurrentMode == TradingOperationController.Mode.Standby ? "" : operation.Feedback);
         bool prices = TradingOperationController.SpreadPrices(stock == null ? 0m : stock.currentPrice,operation.SpreadTicks,out decimal buyPrice,out decimal sellPrice);
-        DeviceTerminalUI.Set(range,"± " + operation.SpreadTicks + "틱");
+        DeviceTerminalUI.Set(range,"± " + operation.SpreadTicks + " 틱");
         DeviceTerminalUI.Set(bid,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadBid : buyPrice) : "—");
         DeviceTerminalUI.Set(ask,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadAsk : sellPrice) : "—");
         DeviceTerminalUI.Set(cash,TradingOrderView.Number(operation.SpreadAvailableCash));
         DeviceTerminalUI.Set(shares,TradingOrderView.Number(operation.SpreadAvailableShares(stock)));
         DeviceTerminalUI.Set(buyRemaining,TradingOrderView.Number(operation.SpreadBuyRemaining));
         DeviceTerminalUI.Set(sellRemaining,TradingOrderView.Number(operation.SpreadSellRemaining));
         DeviceTerminalUI.Set(bought,TradingOrderView.Number(operation.Bought)); DeviceTerminalUI.Set(sold,TradingOrderView.Number(operation.Sold));
         DeviceTerminalUI.Set(realized,InvestmentDashboard.Signed(operation.SpreadRealized));
         realized.color = operation.SpreadRealized > 0m ? new Color32(239,89,100,255) : operation.SpreadRealized < 0m ? new Color32(76,150,232,255) : Ink;
+        previous.interactable=following.interactable=!operation.Active;
+        DeviceTerminalUI.Set(elapsed,operation.SpreadStartedGameMinute>0 ? "실행 경과 · "+DeviceMarketJournal.Duration(operation.SpreadElapsedGameMinutes) : "");
+        DeviceTerminalUI.Set(updated,operation.SpreadUpdatedStamp==null ? "" : "갱신 · "+operation.SpreadUpdatedStamp);
+        var buffer=new System.Text.StringBuilder();
+        int shown=0;
+        foreach (var fill in operation.SpreadFills)
+        {
+            if (++shown>3)
+            { break; }
+            buffer.Append(fill.Time).Append("  ").Append(fill.Buy ? "매수" : "매도").AppendLine();
+            buffer.Append(TradingOrderView.Number(fill.Price)).Append("원 · ").Append(TradingOrderView.Number(fill.Quantity)).Append("주").AppendLine();
+        }
+        DeviceTerminalUI.Set(tape,buffer.Length>0 ? buffer.ToString().TrimEnd() : "—");
         start.interactable = !operation.Active && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
         cancel.interactable = operation.SpreadRunning;
+        TabletControlStyle.Emphasize(start,!operation.SpreadRunning);
+        TabletControlStyle.Emphasize(cancel,operation.SpreadRunning);
     }
 }

```

## 수정: Assets/Scripts/NightShift/TabletAccountUI.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/TabletAccountUI.cs
+++ 수정본/Assets/Scripts/NightShift/TabletAccountUI.cs
@@ -4,17 +4,18 @@
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 
 /// <summary>Navigation inside the existing tablet Canvas. No camera or device pose changes.</summary>
 [DefaultExecutionOrder(300)]
 public sealed class TabletAccountUI : MonoBehaviour
 {
-    public enum Page { Dashboard, Events, Records, Detail, Orders, Automation, Management }
+    public enum Page { Dashboard, Events, Records, Detail, Orders, Automation, Management, Special }
+    private TabletKitLayout kit;
     public PlayerAccounts accounts;
     public Trader account;
     public bool second;
     public DeviceTerminalUI orders, events;
     public InvestmentDashboard dashboard;
     public HoldUIManager holdings;
     public GameObject contentRoot, lockRoot, recordsPanel, transferPanel;
     public GameObject[] screens;
@@ -42,16 +43,17 @@
         accounts.Unlocked += Unlock;
         DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
         lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
         events.newsReader = NewsDeskUI.Install(this);
         TabletControlStyle.Apply(this);
         if (second) { SpreadTradingUI.Install(this); }
         else { DailyIncomeUI.InstallTablet(this); }
         CompanyDashboardUI.Install(this);
+        kit=TabletKitLayout.Install(this);
         ShowPage(second ? Page.Automation : Page.Dashboard);
     }
     private void Unlock()
     {
         if (!second)
         { return; }
         lockRoot.SetActive(false); contentRoot.SetActive(true); ShowPage(Page.Automation);
     }
@@ -63,51 +65,59 @@
         { return; }
         if (page == Page.Automation && !second)
         { return; }
         if (page == Page.Detail && (dashboard.Selected == null || account.GetOwnedVolume(dashboard.Selected) <= 0m))
         { page = Page.Dashboard; }
         foreach (var screen in screens) { screen.SetActive(false); }
         transferPanel.SetActive(false);
         CurrentPage = page;
-        clock.gameObject.SetActive(page != Page.Orders && page != Page.Automation);
+        clock.gameObject.SetActive(true);
         if (page == Page.Dashboard) { dashboard.gameObject.SetActive(true); dashboard.Refresh(); }
-        else if (page == Page.Events) { events.ShowLocal(); }
+        else if (page == Page.Events) { events.ShowLocal(); events.newsReader.OpenReader(); }
         else if (page == Page.Orders) { orders.ShowLocal(); }
         else if (page == Page.Detail)
         { holdings.selectedOverride = dashboard.Selected; holdings.gameObject.SetActive(true); }
         else if (page == Page.Records) { recordsPanel.SetActive(true); seenVersion = -1; }
         else if (page == Page.Automation) { orders.legacy.ShowDesk(); }
-        else if (page == Page.Management) { events.legacy.ShowDesk(); }
+        else if (page == Page.Management) { events.legacy.ShowDesk(); GetComponentInChildren<CompanyDashboardUI>(true)?.OpenGrowth(); }
+        else if (page == Page.Special && second) { events.ShowLocal(); events.newsReader.OpenStudio(); }
         for (int i = 0; i < tabs.Length; i++)
         { tabs[i].image.color = (int)page == i ? new Color32(26,43,49,255) : Color.clear; tabLines[i].enabled = (int)page == i; }
         next = 0f;
+        kit?.RefreshTabs();
     }
     void Update()
     {
         if (Locked || Time.unscaledTime < next)
         { return; }
         next = Time.unscaledTime + .3f;
         var day = MarketDayController.Instance;
         DeviceTerminalUI.Set(clock,day == null ? "DAY —  —:—" : "DAY " + day.CalendarDay + "  " + DeviceMarketJournal.Stamp());
         transferButton.interactable = accounts.AutomatedUnlocked;
         tabs[3].interactable = dashboard.Selected != null && account.GetOwnedVolume(dashboard.Selected) > 0m;
-        if (CurrentPage != Page.Records || seenVersion == events.records.Version)
+        int version=events.records.Version+orders.session.Version;
+        if (CurrentPage != Page.Records || seenVersion == version)
         { return; }
-        seenVersion = events.records.Version;
+        seenVersion = version;
         text.Clear();
         foreach (var fill in events.records.Fills)
         {
             text.Append(fill.Time).Append("   ").Append(fill.Stock.stockName).Append("   ").Append(fill.Buy ? "매수" : "매도")
                 .Append("   ").Append(fill.Quantity.ToString("N0")).Append("주   ").Append(DeviceTerminalUI.Money(fill.Price));
             if (!fill.Buy) { text.Append("   실현 ").Append(InvestmentDashboard.Signed(fill.Realized)).Append("원"); }
             text.AppendLine().AppendLine();
         }
-        DeviceTerminalUI.Set(ledger,text.Length == 0 ? "아직 체결된 주문이 없습니다" : text.ToString());
-        ledgerContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Math.Max(120,events.records.Fills.Count * 110));
+        foreach (var request in orders.session.History)
+        {
+            text.Append(request.UpdatedAt ?? "—").Append("   ").Append(request.Stock.stockName).Append("   ").Append(request.Status)
+                .Append("   ").Append(request.Filled.ToString("N0")).Append(" / ").Append(request.Quantity.ToString("N0")).AppendLine("주").AppendLine();
+        }
+        DeviceTerminalUI.Set(ledger,text.Length == 0 ? "—" : text.ToString());
+        ledgerContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Math.Max(120,(events.records.Fills.Count+orders.session.History.Count) * 110));
     }
     private void OpenTransfer()
     {
         if (!accounts.AutomatedUnlocked)
         { return; }
         transferAmount.SetTextWithoutNotify("");
         DeviceTerminalUI.Set(transferTitle,second ? "2번 계좌 → 1번 계좌" : "1번 계좌 → 2번 계좌");
         DeviceTerminalUI.Set(transferStatus,"이체 가능 " + DeviceTerminalUI.Money(PlayerAccounts.Available(account)));

```

## 수정: Assets/Scripts/NightShift/TabletControlStyle.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/TabletControlStyle.cs
+++ 수정본/Assets/Scripts/NightShift/TabletControlStyle.cs
@@ -1,14 +1,14 @@
 using System.Collections.Generic;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 
-/// <summary>Generated bitmap surfaces for actions and editable fields; descriptions remain ordinary TMP text.</summary>
+/// <summary>Flat controls shared by the two tablets; decorative icons come from the supplied kit.</summary>
 public static class TabletControlStyle
 {
     private static Sprite primary, secondary, input;
     private static TMP_FontAsset readingFont;
     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
     private static void Reset() { primary = secondary = input = null; readingFont = null; }
     public static Sprite Primary => primary ??= Load("ActionPrimary-v1",new Rect(0,104,2172,524));
     public static Sprite Secondary => secondary ??= Load("ActionSecondary-v1",new Rect(0,104,2172,524));
@@ -28,35 +28,36 @@
         if (image == null || sprite == null) { return; }
         if (image.sprite != sprite) { image.sprite = sprite; }
         image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = false;
         image.pixelsPerUnitMultiplier = 4; image.color = Color.white;
     }
     public static void Emphasize(Button button, bool active)
     {
         if (button == null) { return; }
-        Sprite sprite = active ? Primary : Secondary;
-        if (button.image != null && (button.image.sprite != sprite || button.image.color != Color.white)) { Surface(button.image,sprite); }
+        TabletKitStyle.Flat(button.image,active ? TabletKitStyle.Accent : TabletKitStyle.Hover);
+        var text=button.GetComponentInChildren<TMP_Text>(true);
+        if (text!=null) { text.color=!button.interactable ? TabletKitStyle.Muted : active ? TabletKitStyle.Background : TabletKitStyle.Ink; }
     }
     public static void Button(Button button, bool primaryAction = false)
     {
         if (button == null) { return; }
-        Surface(button.image,primaryAction ? Primary : Secondary);
+        Emphasize(button,primaryAction);
         button.transition = Selectable.Transition.ColorTint;
         var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f,1.18f,1.18f,1);
         colors.pressedColor = new Color(.70f,.75f,.78f,1); colors.selectedColor = new Color(1.08f,1.08f,1.08f,1);
         colors.disabledColor = new Color(.62f,.65f,.68f,.45f); colors.fadeDuration = .10f; colors.colorMultiplier = 1;
         button.colors = colors;
         foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
-        { Font(label); label.fontStyle = FontStyles.Bold; label.raycastTarget = false; }
+        { Font(label); label.fontStyle = FontStyles.Normal; label.raycastTarget = false; }
     }
     public static void Input(TMP_InputField field)
     {
         if (field == null) { return; }
-        Surface(field.targetGraphic as Image,InputSurface); field.transition = Selectable.Transition.ColorTint;
+        TabletKitStyle.Flat(field.targetGraphic as Image,TabletKitStyle.Hover); field.transition = Selectable.Transition.ColorTint;
         var colors = field.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.9f,1.08f,1.12f,1);
         colors.selectedColor = new Color(.65f,1.20f,1.28f,1); colors.pressedColor = colors.selectedColor;
         colors.disabledColor = new Color(.65f,.65f,.65f,.45f); colors.fadeDuration = .12f; colors.colorMultiplier = 1; field.colors = colors;
         field.customCaretColor = true; field.caretColor = new Color32(111,219,232,255); field.caretWidth = 2;
         Font(field.textComponent); if (readingFont != null) { field.fontAsset = readingFont; }
         field.textComponent.fontStyle = FontStyles.Normal;
         if (field.placeholder is TMP_Text hint) { Font(hint); hint.color = new Color32(138,158,171,255); }
     }

```

## 수정: Assets/Scripts/NightShift/TradingOperationController.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/TradingOperationController.cs
+++ 수정본/Assets/Scripts/NightShift/TradingOperationController.cs
@@ -190,16 +190,22 @@
         }
         Feedback = $"{(buy ? "매수" : "매도")} {quantity:N0}주 · {price:N0}원 주문 / 체결은 호가 상황에 따라 달라집니다.";
         return true;
     }
     private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
     {
         if (!Active || (!submitting && !pending.Contains(resting)))
         { return; }
+        if (CurrentMode==Mode.Spread && buyer!=seller && (ReferenceEquals(buyer,account) || ReferenceEquals(seller,account)))
+        {
+            bool buy=ReferenceEquals(buyer,account);
+            SpreadFills.Insert(0,new DeviceMarketJournal.Fill { Stock=stock,Time=DeviceMarketJournal.FullStamp(),Buy=buy,Price=price,Quantity=volume,Realized=buy ? 0 : (price-(resting?.costBasis ?? account.SubmittedSellCost))*volume });
+            if (SpreadFills.Count>32) { SpreadFills.RemoveAt(32); }
+        }
         if (ReferenceEquals(buyer, account) && !ReferenceEquals(seller, account))
         { Bought += volume; Spent += price * volume; Inventory += volume; FillCount++; }
         else if (ReferenceEquals(seller, account) && !ReferenceEquals(buyer, account))
         {
             Sold += volume; Proceeds += price * volume; Inventory -= volume; FillCount++;
             if (CurrentMode == Mode.Spread)
             { SpreadRealized += (price - resting.costBasis) * volume; }
         }
@@ -212,16 +218,17 @@
             { Target.orderBook.CancelOrder(order, Target); }
         }
         pending.Clear();
     }
     public void Finish(string reason = "직접 종료")
     {
         if (!Active)
         { return; }
+        if (SpreadRunning) { SpreadEndedGameMinute=DeviceMarketJournal.GameNow; }
         CancelPending();
         Result = MarkedResult;
         CompletedSessions++;
         if (Result > 0m)
         { ProfitableSessions++; }
         string grade = Result > 0m ? "수익 기록" : Result < 0m ? "손실 기록" : "변동 없음";
         Debrief = $"{grade} · {reason}\n매수 {Bought:N0}주 / 매도 {Sold:N0}주 / 체결 {FillCount:N0}건\n작업 손익 {Result:+#,##0;-#,##0;0}원 (잔여 평가 포함)\n잔여 {Inventory:N0}주는 계좌에 남습니다.";
         CurrentMode = Mode.Review;

```

## 수정: Assets/Scripts/NightShift/TradingOperationController.Spread.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/TradingOperationController.Spread.cs
+++ 수정본/Assets/Scripts/NightShift/TradingOperationController.Spread.cs
@@ -12,16 +12,22 @@
     public decimal SpreadBid { get; private set; }
     public decimal SpreadAsk { get; private set; }
     public decimal SpreadRealized { get; private set; }
     public decimal SpreadBuyRemaining => Live(spreadBuy) ? spreadBuy.volume : 0m;
     public decimal SpreadSellRemaining => Live(spreadSell) ? spreadSell.volume : 0m;
     public decimal SpreadAvailableCash => account == null ? 0m : spreadSession != null && spreadSession.account == account ? spreadSession.AvailableCash : PlayerAccounts.Available(account);
     private Order spreadBuy, spreadSell;
     private float nextSpread;
+    public readonly System.Collections.Generic.List<DeviceMarketJournal.Fill> SpreadFills=new(32);
+    public double SpreadStartedGameMinute { get; private set; }
+    public double SpreadUpdatedGameMinute { get; private set; }
+    public double SpreadEndedGameMinute { get; private set; }
+    public string SpreadUpdatedStamp { get; private set; }
+    public double SpreadElapsedGameMinutes => Math.Max(0,(SpreadRunning ? DeviceMarketJournal.GameNow : SpreadEndedGameMinute)-SpreadStartedGameMinute);
 
     public decimal SpreadAvailableShares(Stock stock)
     {
         if (account == null || stock == null)
         { return 0m; }
         return spreadSession != null && spreadSession.account == account ? spreadSession.AvailableShares(stock) : account.GetHolding(stock);
     }
     public void SetSpreadTicks(int ticks)
@@ -60,30 +66,32 @@
         if (spreadSession == null && accounts != null)
         { spreadSession = account == accounts.automated ? accounts.automatedOrders : account == accounts.primary ? accounts.primaryOrders : null; }
         Unsubscribe();
         Target = stock;
         EntryPrice = stock.currentPrice;
         InitialInventory = Inventory = account.GetOwnedVolume(stock);
         Budget = SpreadAvailableCash;
         Spent = Proceeds = Bought = Sold = Result = SpreadRealized = 0m;
+        SpreadFills.Clear();
         FillCount = 0; Elapsed = 0f;
         pending.Clear(); spreadBuy = spreadSell = null;
         SpreadBid = bid; SpreadAsk = ask;
         CurrentMode = Mode.Spread;
+        SpreadStartedGameMinute=SpreadUpdatedGameMinute=DeviceMarketJournal.GameNow; SpreadEndedGameMinute=0;
         Target.OnMatchedTrade += OnFill;
         nextSpread = 0f;
         RefreshSpread();
         return true;
     }
     public void CancelSpread()
     {
         if (!SpreadRunning)
         { return; }
-        Finish("매매 취소");
+        SpreadEndedGameMinute=DeviceMarketJournal.GameNow; Finish("매매 취소");
         spreadBuy = spreadSell = null;
         Feedback = "매매 취소됨";
     }
     private bool Live(Order order) => order != null && Target != null && order.volume > 0m && Target.orderBook.IsActive(order);
     private void Retire(ref Order order)
     {
         if (order == null)
         { return; }
@@ -99,16 +107,17 @@
         if (Time.unscaledTime < nextSpread)
         { return; }
         nextSpread = Time.unscaledTime + Math.Max(.25f, spreadRefreshSeconds);
         if (!MarketDayController.CanTrade || !Target.AcceptsOrders || Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
         { CancelSpread(); Feedback = "거래 종료"; return; }
         if (!SpreadPrices(Target.currentPrice, SpreadTicks, out decimal bid, out decimal ask))
         { CancelSpread(); Feedback = "시세 대기"; return; }
         SpreadBid = bid; SpreadAsk = ask;
+        SpreadUpdatedGameMinute=DeviceMarketJournal.GameNow; SpreadUpdatedStamp=DeviceMarketJournal.FullStamp();
         // 먼저 이전 가격의 양쪽 호가를 회수한다. 새 호가가 자신의 옛 호가와 교차하지 않게 한다.
         if (!Live(spreadBuy) || spreadBuy.price != bid)
         { Retire(ref spreadBuy); }
         if (!Live(spreadSell) || spreadSell.price != ask)
         { Retire(ref spreadSell); }
         decimal buyCash = SpreadAvailableCash + (Live(spreadBuy) ? spreadBuy.price * spreadBuy.volume : 0m);
         decimal sellShares = SpreadAvailableShares(Target) + SpreadSellRemaining;
         MaintainQuote(ref spreadBuy, true, bid, decimal.Floor(buyCash / bid));

```

## 수정: Assets/Scripts/NightShift/TradingOrderView.cs
```diff
--- 작업 전/Assets/Scripts/NightShift/TradingOrderView.cs
+++ 수정본/Assets/Scripts/NightShift/TradingOrderView.cs
@@ -27,25 +27,25 @@
     public void Configure(DeviceTerminalUI terminal, GameObject pending, GameObject quick, Button[] quickButtons)
     {
         ui = terminal; reading = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
         reviewCaption=ui.review.GetComponentInChildren<TextMeshProUGUI>(true); submitCaption=ui.submit.GetComponentInChildren<TextMeshProUGUI>(true);
         scale = ((RectTransform)transform).rect.width / 1600f;
         if (scale <= 0f) { scale = 1f; }
         foreach (Transform child in transform) { child.gameObject.SetActive(false); }
         var background = GetComponent<Image>(); background.sprite = null; background.type = Image.Type.Simple; background.color = Background;
-        HeaderElement(ui.stockName,32,16,580,52,42,false);
-        HeaderElement(ui.ticker,32,72,580,32,26,false); ui.ticker.color = Muted;
+        HeaderElement(ui.stockName,32,16,580,52,38,false);
+        HeaderElement(ui.ticker,32,72,580,32,23,false); ui.ticker.color = Muted;
         HeaderElement(ui.currentPrice,640,8,400,72,44,true);
         Label("Price Unit",transform,"원",1052,8,40,72,26,1600,880).color = Muted;
         HeaderElement(ui.change,1104,8,464,72,28,true);
         Rule(transform,32,120,1536,1600,880);
 
-        var left = Area("Account And Pending",transform,32,144,736,704,1600,880);
-        var right = Area("Trade Grid",transform,832,144,736,704,1600,880);
+        var left = Area("Account And Pending",transform,832,144,736,704,1600,880);
+        var right = Area("Trade Grid",transform,32,144,736,704,1600,880);
         available = Metric(left,"주문 가능",0,"원"); reserved = Metric(left,"예약금",40,"원");
         Rule(left,0,92,736,736,704);
         owned = Metric(left,"보유",104,"주"); sellable = Metric(left,"매도 가능",144,"주");
         average = Metric(left,"평균가",184,"원"); profit = Metric(left,"평가손익",224,"원");
         Place((RectTransform)pending.transform,left,0,304,736,400,736,704);
         ui.cancel.transform.SetParent(pending.transform,false);
         Place((RectTransform)ui.cancel.transform,pending.transform,592,0,144,ControlHeight,736,400);
         Action(ui.cancel,"전체 취소");
@@ -115,24 +115,24 @@
         Place((RectTransform)field.transform,root,fieldX-72,0,104,ControlHeight,736,56); InputStyle(field);
         Label("Split " + unit,root,unit,fieldX+40,0,40,56,25,736,56).color=Muted;
     }
     private void HeaderElement(TextMeshProUGUI label,float x,float y,float w,float h,float size,bool numeric)
     { Place(label.rectTransform,transform,x,y,w,h,1600,880); TextStyle(label,size,numeric); label.alignment=numeric?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft; }
     private TextMeshProUGUI Metric(Transform parent,string caption,float y,string unit)
     {
         Label(caption+" Label",parent,caption,0,y,248,36,27,736,704).color=Muted;
-        var value=Label(caption+" Value",parent,"",264,y,416,36,30,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight;
+        var value=Label(caption+" Value",parent,"",264,y,416,36,26,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight;
         Label(caption+" Unit",parent,unit,696,y,40,36,25,736,704).color=Muted; return value;
     }
     private TextMeshProUGUI Forecast(Transform parent,string caption,float y,string unit)
     {
-        Label(caption+" Label",parent,caption,0,y,280,32,25,736,704).color=Muted;
-        var value=Label(caption+" Value",parent,"",288,y,376,32,27,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight; value.color=Muted;
-        Label(caption+" Unit",parent,unit,696,y,40,32,25,736,704).color=Muted; return value;
+        Label(caption+" Label",parent,caption,0,y,280,32,23,736,704).color=Muted;
+        var value=Label(caption+" Value",parent,"",288,y,376,32,23,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight; value.color=Muted;
+        Label(caption+" Unit",parent,unit,696,y,40,32,23,736,704).color=Muted; return value;
     }
     private void InputRow(Transform parent,TMP_InputField field,string caption,string unit,float y)
     {
         Label(caption+" Input Label",parent,caption,0,y,120,56,28,736,704).color=Muted;
         Place((RectTransform)field.transform,parent,144,y,536,ControlHeight,736,704); InputStyle(field);
         Label(caption+" Input Unit",parent,unit,696,y,40,56,26,736,704).color=Muted;
     }
     private void InputStyle(TMP_InputField field)
@@ -140,17 +140,17 @@
         TabletControlStyle.Input(field); Numeric(field.textComponent); field.fontAsset=field.textComponent.font;
         field.textComponent.fontSize=30*scale; field.textComponent.enableAutoSizing=false;
         field.textComponent.alignment=TextAlignmentOptions.MidlineRight; field.textComponent.margin=Vector4.zero;
         field.textComponent.fontStyle=FontStyles.Normal;
         field.textComponent.textWrappingMode=TextWrappingModes.NoWrap; field.richText=false;
         var viewport=field.textViewport; viewport.anchorMin=Vector2.zero; viewport.anchorMax=Vector2.one;
         viewport.offsetMin=new Vector2(16*scale,0); viewport.offsetMax=new Vector2(-16*scale,0);
         var text=field.textComponent.rectTransform; text.anchorMin=Vector2.zero; text.anchorMax=Vector2.one; text.offsetMin=text.offsetMax=Vector2.zero;
-        var image=field.targetGraphic as Image; if(image!=null) { image.color=new Color(.8f,.86f,.9f,1); }
+        var image=field.targetGraphic as Image; if(image!=null) { image.color=TabletKitStyle.Hover; }
     }
     private void Segment(Button button,string caption)
     {
         Action(button,caption); button.image.sprite=null; button.image.type=Image.Type.Simple;
         var colors=button.colors; colors.selectedColor=colors.normalColor=Color.white; button.colors=colors;
     }
     private void Action(Button button,string caption,bool primary=false)
     {
@@ -159,16 +159,26 @@
         TextStyle(label,28,false); label.fontStyle=FontStyles.Normal; label.alignment=TextAlignmentOptions.Center; GameLocalizedText.Set(label,caption);
     }
     public static void Numeric(TMP_Text label)
     {
         var font=Numerals;
         if(font!=null) { label.font=font; label.fontSharedMaterial=font.material; }
         label.fontStyle=FontStyles.Normal;
         label.fontFeatures.Clear();
+    }
+    public void FitCanvas()
+    {
+        var rect=(RectTransform)transform;
+        float fitted=Mathf.Min(rect.rect.width/1600f,rect.rect.height/880f);
+        float ratio=fitted/scale;
+        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) { text.fontSize*=ratio; }
+        foreach (var input in GetComponentsInChildren<TMP_InputField>(true)) { input.pointSize=input.textComponent.fontSize; }
+        GetComponentInChildren<PendingOrderListUI>(true)?.SetScale(fitted);
+        scale=fitted;
     }
     private void TextStyle(TextMeshProUGUI label,float size,bool numeric)
     {
         if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
         if(numeric) { Numeric(label); }
         label.fontSize=size*scale; label.fontStyle=FontStyles.Normal; label.enableAutoSizing=false;
         label.margin=Vector4.zero; label.textWrappingMode=TextWrappingModes.NoWrap;
         label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; label.color=Ink;
@@ -203,17 +213,17 @@
         DeviceTerminalUI.Set(reviewCaption,splitting?(buying?"분할 매수 확인":"분할 매도 확인"):(buying?"매수 주문":"매도 주문"));
         TabletControlStyle.Emphasize(ui.review,!splitting);
         DeviceTerminalUI.Set(afterCash,string.IsNullOrEmpty(error)?Number(ui.session.AvailableCash+(buying?-cost:cost)):"—");
         DeviceTerminalUI.Set(afterShares,string.IsNullOrEmpty(error)?Number(ui.session.account.GetOwnedVolume(stock)+(buying?quantity:-quantity)):"—");
         ShowError(error);
     }
     public void Confirm(Stock stock,bool buying,decimal price,decimal quantity,int parts,float seconds)
     {
-        string[] values={stock.stockName,Number(price),Number(quantity),Number(price*quantity),parts.ToString(),seconds.ToString("0",CultureInfo.InvariantCulture)};
+        string[] values={stock.stockName,Number(price),Number(quantity),Number(price*quantity),parts.ToString(),(seconds*DeviceMarketJournal.GameSecondsPerRealSecond).ToString("0",CultureInfo.InvariantCulture)};
         for(int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(confirmationValues[i],values[i]); }
         DeviceTerminalUI.Set(ui.confirmation,"");
         DeviceTerminalUI.Set(submitCaption,buying?"매수 주문":"매도 주문");
     }
     public void ShowError(string error) { DeviceTerminalUI.Set(ui.validation,ShortError(error)); }
     public static string ShortError(string error)
     {
         if(string.IsNullOrEmpty(error))

```

## 수정: Assets/Scripts/Settings/SessionStore.cs
```diff
--- 작업 전/Assets/Scripts/Settings/SessionStore.cs
+++ 수정본/Assets/Scripts/Settings/SessionStore.cs
@@ -24,17 +24,17 @@
     private const int Schema = 2, MaxNodes = 500000, MaxBytes = 128*1024*1024;
     private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
     private static readonly HashSet<string> RootNames = new(StringComparer.Ordinal)
     {
         "StockManager","StockStatistics","CandleHistoryManager","ExchangeWarehouse","MarketDayController","InvestorSociety","CorporateEconomy",
         "PlayerProgression","PlayerAccounts","DailyIncomeController","FakeNewsManager","NewsManager","ItCompanyManager",
         "StockNewsAttentionManager","StockInterestManager","MarketSentiment","DelistingManager","PsychologicalLevelManager",
         "LiquidityBotManager","TradeBotManager","InstitutionBootstrapper","WorldDramaDirector","MarketRhythmDirector",
-        "TradingOperationController","DeviceOrderSession","NpcLeadingRoom","CompanyNewsAudience","SocialContactController","PhoneCallController","IncumbentExchangeAI","PlayerRestController"
+        "TradingOperationController","DeviceOrderSession","DeviceMarketJournal","NpcLeadingRoom","CompanyNewsAudience","SocialContactController","PhoneCallController","IncumbentExchangeAI","PlayerRestController"
     };
     public static bool ValidationMode => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT"));
     public static string PathName => Path.Combine(ValidationMode ? Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT") : Application.persistentDataPath,"session-v2.json");
     public static bool Exists => File.Exists(PathName);
     public static bool CanSave => StockManager.Instance != null && StockManager.Instance.SetupConfirmed && MarketDayController.Instance != null &&
         MarketDayController.Instance.CheckpointReady && !MarketDayController.Instance.IntegrityFailed &&
         !StockManager.Instance.stocks.Any(stock=>stock.IsDelisting);
     private static bool Root(Component value) => value is Trader || RootNames.Contains(value.GetType().Name);
@@ -130,16 +130,21 @@
         typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
         InvestorSociety.Instance?.ReconnectSession();
         var day=MarketDayController.Instance;
         CorporateEconomy.Instance?.ReconnectSession();
         StockManager.Instance.ReconnectListedCompanies();
         day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
         foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
         foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
+        foreach (var journal in Object.FindObjectsByType<DeviceMarketJournal>(FindObjectsInactive.Include))
+        {
+            if (!((JArray)data["roots"]).Any(binding=>(string)binding["key"]==Key(journal))) { journal.ClearHistory(); }
+            journal.ReconnectSession();
+        }
         var roots=(JArray)data["roots"];
         var components=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).ToDictionary(Key,StringComparer.Ordinal);
         foreach (var binding in roots)
         {
             var component=components[(string)binding["key"]];
             component.enabled=(bool)binding["enabled"];
             if (component.gameObject.activeSelf!=(bool)binding["active"]) { component.gameObject.SetActive((bool)binding["active"]); }
         }

```

## NewsManager.cs

이번 작업 전 별도 스냅샷은 없으며 이번 변경은 기사 생성 두 경로에서 `DeviceMarketJournal.Stamp()`를 `DeviceMarketJournal.FullStamp()`로 교체한 두 부분이다. 이전 작업 전체와 Git HEAD를 비교하여 이번 변경이라고 제시하지 않는다.
