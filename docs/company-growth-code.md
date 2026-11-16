# 기업 성장 패치: 신규 C# 전문과 함수 비교

이번 작업 시작 시 보관한 `Validation/CompanyGrowth/before/`와 비교합니다. 이전 작업의 미커밋 변경을 이번 작업의 변경으로 계산하지 않습니다.

## 신규 파일: Assets/Scripts/NightShift/CompanyGrowth.cs

```csharp
using System;

public enum CompanyProject { Capacity, Sales, Research }

/// <summary>Paid projects, earnings-backed private valuation, and public-listing eligibility.</summary>
[Serializable]
public sealed class CompanyGrowth
{
    [Serializable] public sealed class Rules
    {
        public int version=1, maximumLevel=5, projectDays=2, listingProfitableCloses=3, listingProjects=2;
        public long founderShares=10000, founderValue=20000000, listingValue=50000000;
        public double projectCostRatio=.05, levelCostScale=.35, capacityGain=.25, salesGain=.05, researchSaving=.04;
        public double earningsMultiple=20, publicIssueRatio=.25, npcCashReserve=.25;
    }
    public int Version=1, Capacity, Sales, Research, Project=-1, CompleteDay, LastProjectDay, CompletedProjects;
    public int OperatingDays, ProfitableCloses, RequestedDay, ListedCloseDay, LastDecisionDay, Decisions;
    public bool FounderBusiness, ListingApproved;
    public decimal Capital, Contributions, Retained, Appraisal, StartingRevenue, ExpectedProfit, AverageProfit, PaidProject;
    public string LastDecision="";
    public static bool Valid(Rules r) => r!=null && r.version==1 && r.founderShares>=1 && r.founderValue>=r.founderShares && r.listingValue>0
        && r.maximumLevel>=1 && r.maximumLevel<=20 && r.projectDays>=1 && r.listingProfitableCloses>=1 && r.listingProjects>=1
        && Fraction(r.projectCostRatio) && r.projectCostRatio>0 && Fraction(r.levelCostScale) && Fraction(r.capacityGain)
        && Fraction(r.salesGain) && Fraction(r.researchSaving) && r.researchSaving*r.maximumLevel<.9
        && Fraction(r.publicIssueRatio) && r.publicIssueRatio>0 && Fraction(r.npcCashReserve) && r.earningsMultiple>=0 && r.earningsMultiple<=100;
    private static bool Fraction(double n) => !double.IsNaN(n) && !double.IsInfinity(n) && n>=0 && n<=1;
    public static CompanyGrowth Create(CompanyBusiness company,decimal value,bool founder,decimal expectedMargin=.25m)
    { return new CompanyGrowth { Capital=Math.Max(1,value), Appraisal=Math.Max(1,value), StartingRevenue=company.BaseRevenue,
        ExpectedProfit=company.BaseRevenue*expectedMargin, AverageProfit=company.BaseRevenue*expectedMargin, FounderBusiness=founder }; }
    public int Level(CompanyProject type) => type==CompanyProject.Capacity ? Capacity : type==CompanyProject.Sales ? Sales : Research;
    public decimal ProjectCost(CompanyBusiness company,CompanyProject type,Rules rules)
    { return decimal.Ceiling(company.ReferenceCapital*(decimal)rules.projectCostRatio*(1+Level(type)*(decimal)rules.levelCostScale)); }
    public bool Start(CompanyBusiness company,CompanyProject type,int day,Rules rules)
    {
        if (day<1 || Project>=0 || LastProjectDay==day || !Enum.IsDefined(typeof(CompanyProject),type) || Level(type)>=rules.maximumLevel || company.Debt>0)
        { return false; }
        decimal cost=ProjectCost(company,type,rules);
        if (company.Cash<cost) { return false; }
        company.Cash-=cost; PaidProject=cost; Project=(int)type; CompleteDay=day+rules.projectDays; LastProjectDay=day;
        return true;
    }
    public bool Complete(CompanyBusiness company,int day,Rules rules)
    {
        if (Project<0 || day<CompleteDay) { return false; }
        if (Project==0) { Capacity++; } else if (Project==1) { Sales++; } else { Research++; }
        CompletedProjects++; Project=-1; PaidProject=0;
        company.BaseRevenue=decimal.Floor(StartingRevenue*(1+Capacity*(decimal)rules.capacityGain));
        return true;
    }
    public void Close(CompanyBusiness company,Rules rules)
    {
        OperatingDays++; if (company.Profit>0) { ProfitableCloses++; }
        Retained+=company.Profit-company.Dividend;
        AverageProfit=OperatingDays==1 ? company.Profit : AverageProfit*.75m+company.Profit*.25m;
        Revalue(rules);
    }
    public void Revalue(Rules rules)
    { Appraisal=decimal.Floor(Math.Max(1,Capital+Contributions+Retained+(AverageProfit-ExpectedProfit)*(decimal)rules.earningsMultiple)); }
    public bool Eligible(CompanyBusiness company,Rules rules) => Appraisal>=rules.listingValue && ProfitableCloses>=rules.listingProfitableCloses
        && CompletedProjects>=rules.listingProjects && company.Debt==0;
    public bool Request(CompanyBusiness company,int day,Rules rules)
    {
        if (RequestedDay>0 || !Eligible(company,rules)) { return false; }
        RequestedDay=day; return true;
    }
}

```

## 신규 파일: Assets/Scripts/NightShift/CorporateEconomy.Growth.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CorporateEconomy
{
    public CompanyGrowth.Rules GrowthRules=new();
    private Stock founderCompany;
    public Stock PlayerCompany => founderCompany ?? StockManager.Instance?.playerAccount?.OwnedCompany;
    public string GrowthResult { get; private set; }="";
    public static CompanyGrowth.Rules LoadGrowthRules()
    {
        var file=Resources.Load<TextAsset>("Progression/CompanyGrowth");
        var rules=file==null ? new CompanyGrowth.Rules() : JsonUtility.FromJson<CompanyGrowth.Rules>(file.text);
        if (!CompanyGrowth.Valid(rules)) { throw new InvalidOperationException("Invalid CompanyGrowth configuration."); }
        return rules;
    }
    public IEnumerable<Stock> Companies()
    {
        foreach (var stock in StockManager.Instance.stocks) { yield return stock; }
        foreach (var stock in StockManager.Instance.UnlistedStocks) { yield return stock; }
    }
    public void InitializeCompanyWorld()
    {
        EnsureFounder();
        foreach (var stock in Companies()) { EnsureRepresentative(stock); ObserveControl(stock); }
    }
    private void EnsureFounder()
    {
        var manager=StockManager.Instance; var player=manager.playerAccount;
        if (founderCompany?.business?.growth?.FounderBusiness==true) { return; }
        var original=player.OwnedCompany;
        if (original!=null && player.GetOwnedVolume(original)>original.issuedShares*.5m) { founderCompany=original; }
        else
        {
            // Old retail saves had sold PLAYER to the exchange. Preserve those public shares and create the previously off-ledger private business.
            founderCompany=manager.RegisterPrivateCompany("PLAYER_BIZ",DailyIncomeController.Instance?.CompanyName ?? "내 기업",
                GrowthRules.founderValue/(decimal)GrowthRules.founderShares,GrowthRules.founderShares);
            founderCompany.Owner=player; founderCompany.OwnerId=player.OwnerId; player.OwnedCompany=founderCompany;
            if (player.GetOwnedVolume(founderCompany)==0) { player.GrantShares(founderCompany,founderCompany.issuedShares,founderCompany.currentPrice); }
            Ensure(founderCompany).LastDay=DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
        }
        Ensure(founderCompany).growth.FounderBusiness=true;
    }
    private void EnsureRepresentative(Stock stock)
    {
        if (stock.Owner!=null || stock.IsDelisted) { return; }
        var bot=TradeBot.Create<WhaleBot>("CEO_"+stock.symbol);
        if (bot==null) { return; }
        stock.Owner=bot; stock.OwnerId="CEO_"+stock.symbol; bot.BindCompany(stock,stock.OwnerId);
        bot.cash=0; bot.InitialCashAssigned=true;
    }
    public bool StartProject(Stock stock,CompanyProject type)
    {
        if (!CanManage(stock)) { GrowthResult="경영권 필요"; return false; }
        bool accepted=Ensure(stock).growth.Start(stock.business,type,MarketDayController.Instance.Day,GrowthRules);
        GrowthResult=accepted ? "투자 착수" : "진행 중·자금·단계 확인"; return accepted;
    }
    public bool CanManage(Stock stock) => stock!=null && !stock.IsDelisted && !stock.IsDelisting && StockManager.Instance.SetupConfirmed
        && WorldDramaDirector.Instance.Controls(stock) && MarketDayController.Instance.Phase!=MarketPhase.Closing;
    public bool FundCompany(Stock stock,Trader payer,decimal amount)
    {
        if (!CanManage(stock) || stock.IsListed || !PlayerAccounts.Instance.CanUse(payer) || payer.GetHolding(stock)!=stock.issuedShares)
        { GrowthResult="비상장 완전 소유 기업만"; return false; }
        if (amount<1 || amount!=decimal.Floor(amount) || PlayerAccounts.Available(payer)<amount)
        { GrowthResult="가용 현금 부족"; return false; }
        if (!payer.ContributePrivateCapital(stock,amount)) { GrowthResult="가용 현금 부족"; return false; }
        var data=Ensure(stock); data.Cash+=amount; data.growth.Contributions+=amount;
        data.growth.Revalue(GrowthRules); ApplyValuation(stock); GrowthResult="출자 완료"; return true;
    }
    public bool RequestListing(Stock stock)
    {
        if (!CanManage(stock) || stock.IsListed) { GrowthResult="상장 신청 불가"; return false; }
        bool requested=Ensure(stock).growth.Request(stock.business,MarketDayController.Instance.Day,GrowthRules);
        GrowthResult=requested ? "다음 개장 심사" : "상장 요건 확인"; return requested;
    }
    private void ManageRepresentative(Stock stock,int day)
    {
        var data=Ensure(stock); var growth=data.growth;
        if (growth.LastDecisionDay>=day || stock.IsDelisted || WorldDramaDirector.Instance.Controls(stock)) { return; }
        growth.LastDecisionDay=day; growth.Decisions++;
        var mind=(stock.Owner as TradeBot)?.Social?.Psychology;
        float greed=mind?.Greed ?? 60, fear=mind?.Fear ?? 25;
        decimal reserve=data.BaseRevenue*(decimal)GrowthRules.npcCashReserve;
        data.Policy=data.Debt>0 || data.Cash<reserve || fear>65 ? CompanyPolicy.Reserve : greed>60 ? CompanyPolicy.Investment : CompanyPolicy.Dividend;
        int preferred=(int)(CompanyBusiness.Hash(stock.symbol+":"+day)%3);
        if (fear>55) { preferred=2; } else if (greed>75) { preferred=0; }
        for (int i=0;i<3 && growth.Project<0;i++)
        {
            var type=(CompanyProject)((preferred+i)%3);
            if (data.Cash-growth.ProjectCost(data,type,GrowthRules)<reserve) { continue; }
            if (growth.Start(data,type,day,GrowthRules)) { growth.LastDecision=ProjectName(type); break; }
        }
        if (!stock.IsListed && growth.Eligible(data,GrowthRules)) { growth.Request(data,day,GrowthRules); }
    }
    public static string ProjectName(CompanyProject type) => type==CompanyProject.Capacity ? "설비 확장" : type==CompanyProject.Sales ? "영업 확대" : "기술 연구";
    private void ApplyValuation(Stock stock)
    {
        var growth=stock.business.growth;
        decimal value=growth.Appraisal/Math.Max(1,stock.issuedShares);
        stock.SetRealValue(Math.Max(1,value));
        if (!stock.IsListed) { stock.currentPrice=value; stock.SetFakeValue(value); }
    }
    private void PrepareBusinessDay(Stock stock,int day)
    {
        var data=Ensure(stock); var growth=data.growth;
        ManageRepresentative(stock,day);
        if (growth.Complete(data,day,GrowthRules))
        {
            growth.LastDecision="확장 완료";
            if (stock.IsListed) { PublishCompanyEvent(stock,"사업 확장 완료",.35,"투자가 완료되어 다음 실적에 반영됩니다."); }
        }
    }
    private void FinishBusinessDay(Stock stock)
    {
        var data=stock.business; data.growth.Close(data,GrowthRules);
        if (stock.IsListed) { data.growth.ListedCloseDay=data.LastDay; }
        else { data.MarketCap=0; }
        ApplyValuation(stock);
    }
    public IEnumerator ProcessListings(int day)
    {
        var pending=new List<Stock>(StockManager.Instance.UnlistedStocks);
        foreach (var stock in pending)
        {
            var data=Ensure(stock); var growth=data.growth;
            if (growth.RequestedDay<1 || growth.RequestedDay>=day || !growth.Eligible(data,GrowthRules) || stock.IsDelisted || stock.IsDelisting) { continue; }
            decimal price=PriceTickPolicy.FloorToTick(Math.Max(1,growth.Appraisal/stock.issuedShares));
            decimal issue=Math.Max(1,decimal.Floor(stock.issuedShares*(decimal)GrowthRules.publicIssueRatio)), funding=issue*price;
            var underwriter=ExchangeBot.Instance;
            if (underwriter==null || underwriter.cash-underwriter.ReservedCash<funding) { continue; }
            // Primary issue: cash moves from the actual underwriter to the company; founder shares are not sold or duplicated.
            if (!underwriter.SubscribeShares(stock,issue,price)) { continue; }
            data.Cash+=funding; growth.Contributions+=funding; stock.issuedShares+=issue;
            growth.Revalue(GrowthRules); stock.currentPrice=price; stock.orderBook=new OrderBook(price);
            growth.ListingApproved=true; stock.SetRealValue(growth.Appraisal/stock.issuedShares); stock.SetFakeValue(stock.realValue);
            if (StockManager.Instance.ListOwnerCompany(stock.symbol)==null) { throw new InvalidOperationException("Approved IPO failed: "+stock.symbol); }
            PublishCompanyEvent(stock,"신규 상장",.75,"상장 심사를 통과했습니다. 공모 자금은 기업에 유입되며 실제 호가에서 거래가 시작됩니다.");
            yield return null;
        }
    }
    private void PublishCompanyEvent(Stock stock,string headline,double sentiment,string body)
    {
        string title=stock.stockName+" · "+headline;
        var article=new NewsArticle { symbol=stock.symbol,company=stock.stockName,title=title,summary=body,body=body,
            category="corporate",categoryLabel="기업 공시",source="기업 공시",verification="공식 확인",time=DeviceMarketJournal.Stamp(),
            publishedAt=SessionClock.Time,sentiment=sentiment,credibility=.98 };
        NewsManager.Instance?.Publish(stock,1+(float)sentiment*.2f,title,authored:article);
    }
}

```

## 신규 파일: Assets/Scripts/NightShift/CompanyGrowthUI.cs

```csharp
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Existing tablet management canvas: real company funds, paid projects, and listing application.</summary>
public sealed class CompanyGrowthUI : MonoBehaviour
{
    private CompanyDashboardUI owner;
    private Stock stock;
    private TMP_FontAsset font;
    private TextMeshProUGUI title, representative, valuation, cash, earnings, progress, state, result;
    private readonly TextMeshProUGUI[] costs=new TextMeshProUGUI[3], criteria=new TextMeshProUGUI[3];
    private readonly Button[] projects=new Button[3];
    private Button fund, listing;
    private TMP_InputField amount;
    private Image progressFill;
    private float next;
    public Stock Company => stock;
    public static CompanyGrowthUI Install(CompanyDashboardUI parent)
    {
        var go=new GameObject("Panel :: Company Growth",typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent.transform,false);
        Place((RectTransform)go.transform,0,0,1,1); go.GetComponent<Image>().color=new Color32(14,23,29,255);
        var view=go.AddComponent<CompanyGrowthUI>(); view.owner=parent; view.font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        view.Build(); go.SetActive(false); return view;
    }
    public void Show(Stock company) { stock=company; gameObject.SetActive(true); transform.SetAsLastSibling(); Set(result,""); next=0; Refresh(); }
    public void Close() { gameObject.SetActive(false); owner.Refresh(); }
    private void Build()
    {
        title=Label("Company","",.04f,.025f,.68f,.07f,44);
        state=Label("Listing State","",.73f,.035f,.225f,.05f,26); state.alignment=TextAlignmentOptions.MidlineRight;
        representative=Label("Representative","",.04f,.10f,.91f,.045f,25);
        Label("Valuation Label","기업 평가액",.04f,.18f,.43f,.04f,26);
        valuation=Label("Appraisal","—",.04f,.23f,.43f,.075f,44);
        Label("Cash Label","기업 현금",.53f,.18f,.425f,.04f,26);
        cash=Label("Cash","—",.53f,.23f,.425f,.075f,38); cash.alignment=TextAlignmentOptions.MidlineRight;
        earnings=Label("Daily Earnings","",.04f,.325f,.91f,.05f,27);
        for (int i=0;i<3;i++)
        {
            int index=i; float x=.04f+i*.315f;
            projects[i]=Button("Project "+i,CorporateEconomy.ProjectName((CompanyProject)i),x,.41f,.285f,.075f,()=>
            { CorporateEconomy.Instance.StartProject(stock,(CompanyProject)index); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
            costs[i]=Label("Project Cost "+i,"",x,.49f,.285f,.12f,25);
        }
        progress=Label("Project Progress","",.04f,.625f,.91f,.045f,26);
        var track=Surface("Project Track",.04f,.68f,.91f,.006f,new Color32(44,59,67,255));
        var fill=Surface("Project Fill",0,0,1,1,new Color32(37,175,192,255)); fill.transform.SetParent(track.transform,false); Place((RectTransform)fill.transform,0,0,1,1); progressFill=fill;
        for (int i=0;i<3;i++) { criteria[i]=Label("IPO Criterion "+i,"",.04f+i*.315f,.715f,.285f,.10f,25); }
        amount=Input(.04f,.845f,.24f,.065f); amount.SetTextWithoutNotify("5000000");
        fund=Button("Fund Company","출자",.295f,.845f,.15f,.065f,()=>
        { decimal.TryParse(amount.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value); CorporateEconomy.Instance.FundCompany(stock,owner.Account,value); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
        listing=Button("Apply IPO","상장 신청",.50f,.845f,.235f,.065f,()=>
        { CorporateEconomy.Instance.RequestListing(stock); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
        Button("Back To Company","기업 정보",.75f,.845f,.205f,.065f,Close);
        result=Label("Action Result","",.04f,.935f,.91f,.045f,25);
    }
    public void Refresh()
    {
        var economy=CorporateEconomy.Instance; var data=stock?.business; var growth=data?.growth;
        if (economy==null || growth==null) { return; }
        var rules=economy.GrowthRules; bool manage=economy.CanManage(stock);
        Set(title,stock.stockName);
        string name=PlayerAccounts.IsPlayer(stock.Owner) ? "플레이어" : (stock.Owner as TradeBot)?.Social?.Nickname ?? "—";
        Set(representative,"대표 · "+name);
        Set(state,stock.IsListed ? "상장" : growth.RequestedDay>0 ? "상장 심사 대기" : "비상장");
        Set(valuation,Money(growth.Appraisal)); Set(cash,Money(data.Cash));
        Set(earnings,data.CloseFinished ? "일매출 "+Money(data.Revenue)+"   순이익 "+Money(data.Profit) : "실적 —");
        for (int i=0;i<3;i++)
        {
            var type=(CompanyProject)i; int level=growth.Level(type); decimal cost=growth.ProjectCost(data,type,rules);
            int target=Math.Min(level+1,rules.maximumLevel);
            string effect=i==0 ? "생산 ×"+(1+target*rules.capacityGain).ToString("0.00") : i==1 ? "수요 +"+(target*rules.salesGain*100).ToString("0")+"%p" : "변동비 −"+(target*rules.researchSaving*100).ToString("0")+"%";
            Set(costs[i],"Lv. "+level+" / "+rules.maximumLevel+" · "+effect+"\n"+(level>=rules.maximumLevel ? "최대 단계" : Money(cost)+" · "+rules.projectDays+"일"));
            projects[i].interactable=manage && data.Debt==0 && growth.Project<0 && growth.LastProjectDay!=MarketDayController.Instance.Day && level<rules.maximumLevel && data.Cash>=cost;
        }
        Set(progress,growth.Project>=0 ? CorporateEconomy.ProjectName((CompanyProject)growth.Project)+" · DAY "+growth.CompleteDay+" 완료" : "진행 중인 투자 없음");
        float fraction=growth.Project<0 ? 0 : Mathf.Clamp01((MarketDayController.Instance.Day-growth.LastProjectDay)/(float)Math.Max(1,growth.CompleteDay-growth.LastProjectDay));
        var rect=(RectTransform)progressFill.transform; rect.anchorMax=new Vector2(fraction,1);
        Set(criteria[0],"상장 평가액\n"+Money(rules.listingValue));
        Set(criteria[1],"흑자 결산\n"+growth.ProfitableCloses+" / "+rules.listingProfitableCloses);
        Set(criteria[2],"사업 확장\n"+growth.CompletedProjects+" / "+rules.listingProjects);
        criteria[0].color=growth.Appraisal>=rules.listingValue ? Ready : Muted;
        criteria[1].color=growth.ProfitableCloses>=rules.listingProfitableCloses ? Ready : Muted;
        criteria[2].color=growth.CompletedProjects>=rules.listingProjects ? Ready : Muted;
        fund.interactable=amount.interactable=manage && !stock.IsListed && owner.Account.GetHolding(stock)==stock.issuedShares;
        listing.interactable=manage && !stock.IsListed && growth.RequestedDay==0 && growth.Eligible(data,rules);
        TabletControlStyle.Emphasize(listing,listing.interactable);
    }
    void Update() { if (Time.unscaledTime<next) { return; } next=Time.unscaledTime+.5f; Refresh(); }
    private static readonly Color Muted=new Color32(146,161,172,255), Ready=new Color32(98,191,176,255);
    private static string Money(decimal value) => value.ToString("N0")+"원";
    private static void Set(TMP_Text text,string value) { DeviceTerminalUI.Set(text as TextMeshProUGUI,value); }
    private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
    {
        var go=new GameObject("Text :: "+name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
        var text=go.GetComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=size; text.enableAutoSizing=false; text.color=new Color32(230,235,239,255);
        text.alignment=TextAlignmentOptions.MidlineLeft; text.textWrappingMode=TextWrappingModes.NoWrap; text.raycastTarget=false; Set(text,value); return text;
    }
    private Image Surface(string name,float x,float y,float w,float h,Color color)
    {
        var go=new GameObject("Image :: "+name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
    }
    private Button Button(string name,string caption,float x,float y,float w,float h,Action action)
    {
        var image=Surface(name,x,y,w,h,Color.white); image.name="Button :: "+name; image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var text=Label(name,caption,0,0,1,1,28); text.transform.SetParent(image.transform,false); Place(text.rectTransform,0,0,1,1); text.alignment=TextAlignmentOptions.Center;
        TabletControlStyle.Button(button); button.onClick.AddListener(()=>action()); return button;
    }
    private TMP_InputField Input(float x,float y,float w,float h)
    {
        var image=Surface("Company Funding",x,y,w,h,Color.white); image.raycastTarget=true;
        image.gameObject.SetActive(false);
        var input=image.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic=image;
        var viewport=new GameObject("Area :: Funding Viewport",typeof(RectTransform),typeof(RectMask2D)); viewport.transform.SetParent(image.transform,false);
        var viewportRect=(RectTransform)viewport.transform; Place(viewportRect,.04f,0,.92f,1);
        var go=new GameObject("Text :: Funding Amount",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(viewport.transform,false);
        var text=go.GetComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=29; text.color=new Color32(230,235,239,255); text.raycastTarget=false;
        Place(text.rectTransform,0,0,1,1); text.alignment=TextAlignmentOptions.MidlineRight; text.textWrappingMode=TextWrappingModes.NoWrap;
        text.enableAutoSizing=false; text.overflowMode=TextOverflowModes.Overflow; text.richText=false;
        input.textViewport=viewportRect; input.textComponent=text; input.contentType=TMP_InputField.ContentType.IntegerNumber; input.characterLimit=15;
        input.lineType=TMP_InputField.LineType.SingleLine; input.richText=false;
        TabletControlStyle.Input(input); input.pointSize=29; image.gameObject.SetActive(true); return input;
    }
    private static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=new Vector2(x,1-y-h); rect.anchorMax=new Vector2(x+w,1-y); rect.offsetMin=rect.offsetMax=Vector2.zero; }
}

```

## 신규 파일: Assets/Editor/CompanyGrowthValidation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit bounded integration test. Fixtures and save files are isolated from the player session.</summary>
public static class CompanyGrowthValidation
{
    private static string output, language;
    private static bool hadLanguage, oldOptions, spawned, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, checks, errors, locale;
    private static float due;
    private static decimal closingCash, companyCash, closingCap, contribution;
    private static long acquisitionId;
    private static Stock stock;
    private static TabletAccountUI tablet;
    private static readonly string[] languages = { "ko", "en", "ja" };
    public static void Run()
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Use the bounded Settings runner."); }
        Directory.CreateDirectory(output);
        hadLanguage=PlayerPrefs.HasKey("DonDon.Language"); language=PlayerPrefs.GetString("DonDon.Language");
        File.WriteAllText(Path.Combine(output,"language-before.txt"),(hadLanguage ? "1\n" : "0\n")+language);
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>260) { Finish("Company growth time budget exceeded: phase "+phase+" / day "+MarketDayController.Instance?.Day+" / "+MarketDayController.Instance?.Phase); return; }
        if (EditorApplication.isPlaying && !spawned) { spawned=true; new GameObject("Test :: Corporate Probe").AddComponent<CompanyGrowthProbe>(); }
    }
    private static void Log(string text,string stack,LogType type)
    { if (type==LogType.Error || type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),text+"\n"+stack+"\n"); } }
    private static void Check(bool success,string message)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(success ? "PASS " : "FAIL ")+message+"\n"); if (!success) { throw new Exception(message); } checks++; }
    private static CompanyDashboardUI View => tablet.GetComponentsInChildren<CompanyDashboardUI>(true).Single();
    private static CompanyGrowthUI Growth => View.GetComponentInChildren<CompanyGrowthUI>(true);
    private static void Elapse(float value) => typeof(MarketDayController).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(MarketDayController.Instance,value);
    private static void NextMorning()
    {
        PauseMenuController.Instance.Sleep(); Elapse(MarketDayController.Instance.tradingSeconds/8f);
        phase=4; due=Time.unscaledTime+2; GC.Collect();
    }
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime<due) { return; }
        try
        {
            var day=MarketDayController.Instance; var menu=PauseMenuController.Instance; var economy=CorporateEconomy.Instance;
            if (phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName==null) { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase=1; due=Time.unscaledTime+2; return;
            }
            if (phase==1)
            {
                if (day==null || !day.Ready || day.Phase!=MarketPhase.Trading) { return; }
                stock=economy.PlayerCompany; var player=PlayerAccounts.Instance.primary;
                Check(!stock.IsListed&&!stock.AcceptsOrders&&!StockManager.Instance.stocks.Contains(stock),"founder starts private and absent from tradable stock list");
                Check(player.GetHolding(stock)==10000&&player.cash==50000000&&stock.business.growth.Appraisal==20000000,"fresh founder owns 100% of a 20m business plus 50m personal cash");
                player.BuyLimit(2000,1,0,stock);
                Check(player.ReservedCash==0&&!stock.orderBook.HasOrders&&player.cash==50000000,"private company rejects real trading path without reserving cash");
                var others=economy.Companies().Where(s=>s!=stock).ToArray();
                Check(others.All(s=>s.Owner is TradeBot)&&others.Select(s=>s.Owner).Distinct().Count()==others.Length,"every other company has its own representative bot");
                Check(!economy.StartProject(others[0],CompanyProject.Sales),"player cannot spend another firm's treasury without control");
                tablet=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(t=>!t.second);
                tablet.ShowPage(TabletAccountUI.Page.Management); View.ShowCompany(stock); View.OpenGrowth();
                DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                var input=Growth.GetComponentInChildren<TMP_InputField>(); input.SetTextWithoutNotify("40000000");
                decimal wealth=player.Equity(), basis=player.GetOwnedCost(stock), treasury=stock.business.Cash;
                Growth.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Fund Company").onClick.Invoke();
                Check(player.cash==10000000&&stock.business.Cash==treasury+40000000&&player.Equity()==wealth,"funding transfers actual cash without creating shareholder wealth");
                Check(player.GetOwnedCost(stock)==basis+40000000,"capital contribution added to acquisition cost, not unrealized trading profit");
                Growth.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Project 0").onClick.Invoke();
                Check(stock.business.growth.Project==0&&stock.business.Cash==treasury+39000000,"tablet capacity button starts a paid two-day project");
                Check(!economy.RequestListing(stock),"cash-rich business still needs completed expansion and profitable closes");
                Time.timeScale=0; phase=2; due=Time.unscaledTime+1; return;
            }
            if (phase==2)
            {
                if (GameLocale.Code!=languages[locale]) { return; }
                Growth.Refresh(); Audit(Growth.gameObject);
                Check(Growth.GetComponentInChildren<TMP_InputField>().text=="40000000","locale switch preserves entered capital amount: "+GameLocale.Code);
                Capture("growth-"+GameLocale.Code,1600,900); Capture("growth-"+GameLocale.Code,1920,1080);
                Check(GameLocale.MissingSources.Count==0,"all localized string entries available: "+GameLocale.Code);
                if (++locale<3) { GameLocale.SetLanguage(languages[locale]); due=Time.unscaledTime+.6f; return; }
                GameLocale.SetLanguage("ko"); phase=7; due=Time.unscaledTime+.6f; return;
            }
            if (phase==7)
            {
                if (GameLocale.Code!="ko") { return; }
                Growth.Close(); View.Refresh(); Audit(View.gameObject);
                Capture("private-company",1600,900);
                GC.Collect(); Time.timeScale=1; Elapse(day.tradingSeconds); phase=3; return;
            }
            if (phase==3)
            {
                if (menu.CurrentPage!=PauseMenuController.Page.Report) { return; }
                var data=stock.business; var growth=data.growth;
                Check(data.CloseFinished&&data.LastDay==day.Day&&data.MarketCap==0,"private closing results settled without fabricating exchange capitalization: day "+day.Day);
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid&&!day.IntegrityFailed,"private shareholder ledger conserved at close "+day.Day);
                Check(economy.Companies().Where(s=>!PlayerAccounts.IsPlayer(s.Owner)).All(s=>s.business.growth.Decisions>=day.Day),"representatives autonomously decide once per day "+day.Day);
                Check(DailyIncomeController.Instance.Ledger.Reports.Last().Dividends==economy.PlayerDividends(day.Day),"actual company dividend appears in closing report "+day.Day);
                File.AppendAllText(Path.Combine(output,"business-days.txt"),$"DAY {day.Day}: revenue={data.Revenue} profit={data.Profit} cash={data.Cash} value={growth.Appraisal} projects={growth.CompletedProjects}\n");
                if (day.Day==3)
                {
                    Check(growth.Capacity==1&&growth.CompletedProjects==1,"capacity finishes at real DAY 3 settlement");
                    Check(economy.StartProject(stock,CompanyProject.Sales),"sales project starts after actual first completion");
                }
                if (day.Day<5) { NextMorning(); return; }
                Check(growth.CompletedProjects==2&&growth.ProfitableCloses>=3&&growth.Eligible(data,economy.GrowthRules),"paid growth and actual financial results meet IPO criteria");
                companyCash=data.Cash; closingCap=growth.Appraisal;
                Check(SessionStore.Exists,"real closing checkpoint includes growth state");
                SessionStore.Restore(SessionStore.Read()); stock=economy.PlayerCompany;
                Check(stock.business.Cash==companyCash&&stock.business.growth.Appraisal==closingCap&&stock.business.growth.CompletedProjects==2,"restore preserves treasury, appraisal and completed projects");
                decimal cash=PlayerAccounts.Instance.primary.cash;
                var repeat=economy.Settle(day.Day); while(repeat.MoveNext()) { }
                Check(cash==PlayerAccounts.Instance.primary.cash&&companyCash==stock.business.Cash,"restored business cannot pay the same close twice");
                menu.ContinueAfterReport();
                Check(economy.RequestListing(stock),"qualified founder can apply for listing at night");
                decimal underwritingCash=ExchangeBot.Instance.cash; ExchangeBot.Instance.ApplyTradeCash(-underwritingCash);
                var pending=economy.ProcessListings(day.Day+1); while(pending.MoveNext()) { }
                Check(!stock.IsListed&&stock.issuedShares==10000,"unfunded underwriter cannot mint public shares");
                ExchangeBot.Instance.ApplyTradeCash(underwritingCash); contribution=stock.business.growth.Contributions;
                NextMorning(); return;
            }
            if (phase==4)
            {
                if (day.Phase!=MarketPhase.Trading) { return; }
                if (day.Day<=5) { Elapse(day.tradingSeconds); phase=3; return; }
                Check(stock.IsListed&&stock.AcceptsOrders&&StockManager.Instance.stocks.Contains(stock),"approved IPO appears in existing market on next opening");
                Check(stock.issuedShares==12500&&PlayerAccounts.Instance.Owned(stock)==10000,"primary issue creates 20% public float without selling founder shares");
                Check(stock.business.growth.Contributions>contribution&&StockIntegrityChecker.Instance.CheckStock(stock).isValid,"finite underwriting proceeds fund company with conserved share ledger");
                Check(NewsManager.Instance.Articles.Any(a=>a.symbol==stock.symbol&&a.category=="corporate"&&a.sentiment>0),"IPO publishes a positive corporate article through existing news system");
                phase=5; due=Time.unscaledTime+2; return;
            }
            if (phase==5)
            {
                foreach(var bot in day.Bots) { bot.SuspendSession(); }
                while(stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                var seller=Trader.Registry.First(t=>!PlayerAccounts.IsPlayer(t)&&t.GetHolding(stock)>=10);
                var buyer=day.Bots.First(b=>b!=seller&&!b.IsExchangeParticipant&&!b.IsCompanyOwner&&b.cash>=stock.currentPrice*10);
                var sell=typeof(Trader).GetMethod("SellInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                var buy=typeof(Trader).GetMethod("BuyInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                decimal held=buyer.GetOwnedVolume(stock), cash=buyer.cash, price=stock.currentPrice;
                sell.Invoke(seller,new object[]{stock,price,10m,0}); buy.Invoke(buyer,new object[]{stock,price,10m,0});
                Check(buyer.GetOwnedVolume(stock)==held+10&&buyer.cash==cash-price*10,"ordinary bot purchases newly listed shares through real matching and finite cash");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"IPO trading conserves total issued shares");
                tablet.ShowPage(TabletAccountUI.Page.Management); View.ShowCompany(stock); View.Refresh();
                Capture("listed-company",1920,1080); Audit(View.gameObject);
                Elapse(day.tradingSeconds); phase=6; return;
            }
            if (phase==6)
            {
                if (menu.CurrentPage!=PauseMenuController.Page.Report) { return; }
                Check(stock.business.MarketCap==stock.business.ClosePrice*12500&&stock.business.growth.ListedCloseDay==6,"first listed close snapshots capitalization using new issued-share total");
                companyCash=stock.business.Cash;
                SessionStore.Restore(SessionStore.Read()); stock=economy.PlayerCompany;
                Check(stock.IsListed&&stock.issuedShares==12500&&stock.business.Cash==companyCash,"listing, funding and share count survive checkpoint restore");
                // Simulate an older retail save: its public PLAYER remains public and no longer owned by player.
                var player=PlayerAccounts.Instance.primary; decimal oldIssued=stock.issuedShares;
                typeof(CorporateEconomy).GetField("founderCompany",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(economy,null);
                typeof(Trader).GetProperty("OwnedCompany").SetValue(player,null);
                economy.ReconnectSession(); var migrated=economy.PlayerCompany;
                Check(migrated.symbol=="PLAYER_BIZ"&&!migrated.IsListed&&player.GetOwnedVolume(migrated)==10000&&stock.issuedShares==oldIssued,"legacy migration creates private business without confiscating public shares");
                Check(migrated.business.LastDay==day.Day&&!migrated.business.CloseFinished,"legacy private business never back-pays a prior close");
                SessionStore.Save();
                // Remove only the runtime lookup scaffold. Restore must reconstruct the absent saved Stock by identity.
                StockManager.Instance.UnlistedStocks.Remove(migrated);
                var lookup=(Dictionary<string,Stock>)typeof(StockManager).GetField("stockById",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(StockManager.Instance); lookup.Remove(migrated.symbol);
                SessionStore.Restore(SessionStore.Read());
                Check(economy.PlayerCompany.symbol=="PLAYER_BIZ"&&economy.PlayerCompany!=migrated&&PlayerAccounts.Instance.primary.GetOwnedVolume(economy.PlayerCompany)==10000,"cold stock lookup restoration reconnects migrated private company and ownership");
                Check(errors==0,"no runtime errors"); Finish(null);
            }
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void Audit(GameObject root)
    {
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>())
        { if (label.GetComponentInParent<TMP_InputField>()!=null) { continue; } label.ForceMeshUpdate(); if (label.isTextTruncated || label.isTextOverflowing) { throw new Exception("Clipped: "+label.name+" / "+label.text+" / "+label.rectTransform.rect); } }
        Check(true,"active labels fit: "+root.name+" / "+GameLocale.Code);
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
        if (finished) { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); } PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class CompanyGrowthProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { CompanyGrowthValidation.Step(); }
}

```

## 신규 파일: Assets/Editor/CompanyGrowthContinuationValidation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit bounded integration test. Fixtures and save files are isolated from the player session.</summary>
public static class CompanyGrowthContinuationValidation
{
    private static string output, language;
    private static bool hadLanguage, oldOptions, spawned, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, checks, errors, locale;
    private static float due;
    private static decimal closingCash, companyCash, closingCap, contribution;
    private static long acquisitionId;
    private static Stock stock;
    private static TabletAccountUI tablet;
    private static readonly string[] languages = { "ko", "en", "ja" };
    private static int mode, dueFrame;
    public static void Listing() { mode=1; Run(); }
    public static void Restore() { mode=2; Run(); }
    public static void Visual() { mode=3; Run(); }
    private static void Run()
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Use the bounded Settings runner."); }
        Directory.CreateDirectory(output);
        hadLanguage=PlayerPrefs.HasKey("DonDon.Language"); language=PlayerPrefs.GetString("DonDon.Language");
        File.WriteAllText(Path.Combine(output,"language-before.txt"),(hadLanguage ? "1\n" : "0\n")+language);
        var original=File.ReadAllLines("Validation/Settings/CompanyGrowth01/language-before.txt"); hadLanguage=original[0]=="1"; language=original.Length>1 ? original[1] : "";
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>260) { Finish("Company growth time budget exceeded: phase "+phase+" / day "+MarketDayController.Instance?.Day+" / "+MarketDayController.Instance?.Phase); return; }
        if (EditorApplication.isPlaying && !spawned) { spawned=true; new GameObject("Test :: Corporate Probe").AddComponent<CompanyGrowthContinuationProbe>(); }
    }
    private static void Log(string text,string stack,LogType type)
    { if (type==LogType.Error || type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),text+"\n"+stack+"\n"); } }
    private static void Check(bool success,string message)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(success ? "PASS " : "FAIL ")+message+"\n"); if (!success) { throw new Exception(message); } checks++; }
    private static CompanyDashboardUI View => tablet.GetComponentsInChildren<CompanyDashboardUI>(true).Single();
    private static CompanyGrowthUI Growth => View.GetComponentInChildren<CompanyGrowthUI>(true);
    private static void Elapse(float value) => typeof(MarketDayController).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(MarketDayController.Instance,value);
    public static void Step()
    {
        if(finished || !GameLocale.Ready || Time.unscaledTime<due || Time.frameCount<dueFrame) { return; }
        try
        {
            var day=MarketDayController.Instance; var menu=PauseMenuController.Instance; var economy=CorporateEconomy.Instance;
            if(phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>(); if(setup?.businessName==null) { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase=1; due=Time.unscaledTime+2; return;
            }
            if(phase==1)
            {
                if(day==null || !day.Ready || day.Phase!=MarketPhase.Trading) { return; }
                string folder=mode==2 ? "CompanyListing02" : "CompanyGrowth02";
                SessionStore.Restore(File.ReadAllText("Validation/Settings/"+folder+"/session-v2.json"));
                stock=economy.PlayerCompany;
                Check(stock.business.growth.CompletedProjects==2&&stock.business.growth.ProfitableCloses>=3,"loaded actual paid projects and profitable closes");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"cold restore preserves exact shareholder ownership");
                if(mode==2) { phase=10; due=Time.unscaledTime+.2f; GC.Collect(); return; }
                Check(day.Day==5&&!stock.IsListed,"continued DAY 5 private company checkpoint");
                tablet=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(t=>!t.second);
                tablet.ShowPage(TabletAccountUI.Page.Management); View.ShowCompany(stock); View.OpenGrowth();
                Growth.GetComponentInChildren<TMP_InputField>().SetTextWithoutNotify("40000000");
                DeskViewController.Instance.Focus(DeskViewTarget.TabletOne); Time.timeScale=0;
                phase=2; due=Time.unscaledTime+1; dueFrame=Time.frameCount+8; GC.Collect(); return;
            }
            if(phase==2)
            {
                if(GameLocale.Code!=languages[locale]) { return; }
                Growth.Refresh(); Audit(Growth.gameObject);
                var input=Growth.GetComponentInChildren<TMP_InputField>(); input.textComponent.ForceMeshUpdate();
                string rendered=input.textComponent.text.Replace("\u200B","");
                Check(input.text=="40000000"&&rendered==input.text,"full input value remains rendered after locale change: "+GameLocale.Code+" / "+rendered);
                Capture("growth-"+GameLocale.Code,1600,900); Capture("growth-"+GameLocale.Code,1920,1080);
                if(++locale<3) { GameLocale.SetLanguage(languages[locale]); due=Time.unscaledTime+.7f; dueFrame=Time.frameCount+8; return; }
                GameLocale.SetLanguage("ko"); phase=3; due=Time.unscaledTime+.7f; return;
            }
            if(phase==3)
            {
                if(GameLocale.Code!="ko") { return; }
                if(mode==3) { Check(errors==0,"no runtime errors during visual inspection"); Finish(null); return; }
                Check(economy.RequestListing(stock),"player applies using actual earned eligibility");
                decimal cash=ExchangeBot.Instance.cash; ExchangeBot.Instance.ApplyTradeCash(-cash);
                var pending=economy.ProcessListings(day.Day+1); while(pending.MoveNext()) { }
                Check(!stock.IsListed&&stock.issuedShares==10000,"IPO waits when real underwriting cash is unavailable");
                ExchangeBot.Instance.ApplyTradeCash(cash); contribution=stock.business.growth.Contributions;
                menu.Sleep(); Time.timeScale=1; Elapse(day.tradingSeconds/8f);
                phase=4; due=Time.unscaledTime+2; GC.Collect(); return;
            }
            if(phase==4)
            {
                if(day.Phase!=MarketPhase.Trading) { return; }
                Check(day.Day==6&&stock.IsListed&&stock.AcceptsOrders,"approved founder IPO starts trading on DAY 6");
                Check(stock.issuedShares==12500&&PlayerAccounts.Instance.Owned(stock)==10000,"founder retains 10,000 shares and 80% after new public issue");
                Check(stock.business.growth.Contributions>contribution&&StockIntegrityChecker.Instance.CheckStock(stock).isValid,"offering proceeds reach company treasury without share duplication");
                Check(NewsManager.Instance.Articles.Any(a=>a.symbol==stock.symbol&&a.category=="corporate"&&a.sentiment>0),"positive IPO news uses real company and existing audience");
                phase=5; due=Time.unscaledTime+2; return;
            }
            if(phase==5)
            {
                foreach(var bot in day.Bots) { bot.SuspendSession(); }
                while(stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                var seller=Trader.Registry.First(t=>!PlayerAccounts.IsPlayer(t)&&t.GetHolding(stock)>=10);
                var buyer=day.Bots.First(b=>b!=seller&&!b.IsExchangeParticipant&&!b.IsCompanyOwner&&b.cash>=stock.currentPrice*10);
                var sell=typeof(Trader).GetMethod("SellInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                var buy=typeof(Trader).GetMethod("BuyInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                decimal held=buyer.GetOwnedVolume(stock), cash=buyer.cash, price=stock.currentPrice;
                sell.Invoke(seller,new object[]{stock,price,10m,0}); buy.Invoke(buyer,new object[]{stock,price,10m,0});
                Check(buyer.GetOwnedVolume(stock)==held+10&&buyer.cash==cash-price*10,"ordinary bot buys ten newly listed shares with finite cash through matching");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"issued shares conserved after matching");
                Growth.Close(); View.ShowCompany(stock); View.Refresh(); Capture("listed-company",1920,1080); Audit(View.gameObject);
                Elapse(day.tradingSeconds); phase=6; GC.Collect(); return;
            }
            if(phase==6)
            {
                if(menu.CurrentPage!=PauseMenuController.Page.Report) { return; }
                Check(stock.business.MarketCap==stock.business.ClosePrice*12500&&stock.business.growth.ListedCloseDay==6,"first exchange close uses exact closing price times post-IPO shares");
                Check(SessionStore.Exists,"post-IPO state saved in actual closing checkpoint");
                Check(errors==0,"no runtime errors"); Finish(null);
            }
            if(phase==10)
            {
                Check(day.Day==6&&stock.IsListed&&stock.issuedShares==12500&&stock.business.growth.ListedCloseDay==6,"cold restart retains completed IPO and first market-cap snapshot");
                Check(day.CheckpointReady,"restore has no leftover opening coroutine or phase handle");
                int history=CandleHistoryManager.Instance.GetLastCandles(stock,100).Count;
                StockManager.Instance.ReconnectListedCompanies();
                var callbacks=(Delegate)typeof(Stock).GetField("OnMatchedTrade",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stock);
                foreach(var name in new[]{"CandleHistoryManager","StockStatistics","ExchangeBot","MarketDayController","MarketRhythmDirector","WorldDramaDirector","PlayerProgression","NpcLeadingRoom"})
                { Check(callbacks.GetInvocationList().Count(d=>d.Target?.GetType().Name==name)==1,"cold IPO reconnect binds exactly once: "+name); }
                Check(history==CandleHistoryManager.Instance.GetLastCandles(stock,100).Count,"reconnecting runtime observers keeps saved candle history");
                decimal cash=PlayerAccounts.Instance.primary.cash, company=stock.business.Cash;
                var repeat=economy.Settle(6); while(repeat.MoveNext()) { }
                Check(cash==PlayerAccounts.Instance.primary.cash&&company==stock.business.Cash,"restored close cannot duplicate company dividends");
                var player=PlayerAccounts.Instance.primary; decimal issued=stock.issuedShares, owned=player.GetOwnedVolume(stock);
                typeof(CorporateEconomy).GetField("founderCompany",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(economy,null);
                typeof(Trader).GetProperty("OwnedCompany").SetValue(player,null);
                economy.ReconnectSession(); var migrated=economy.PlayerCompany;
                Check(migrated.symbol=="PLAYER_BIZ"&&!migrated.IsListed&&player.GetOwnedVolume(migrated)==10000&&stock.issuedShares==issued&&player.GetOwnedVolume(stock)==owned,"legacy migration preserves existing listed equity while creating separate private firm");
                Check(migrated.business.LastDay==day.Day&&!migrated.business.CloseFinished,"new legacy business skips already settled days");
                phase=11; due=Time.unscaledTime+.2f; GC.Collect(); return;
            }
            if(phase==11)
            {
                SessionStore.Save(); phase=12; due=Time.unscaledTime+.2f; GC.Collect(); return;
            }
            if(phase==12)
            {
                var migrated=economy.PlayerCompany; StockManager.Instance.UnlistedStocks.Remove(migrated);
                var lookup=(Dictionary<string,Stock>)typeof(StockManager).GetField("stockById",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(StockManager.Instance); lookup.Remove(migrated.symbol);
                SessionStore.Restore(SessionStore.Read());
                Check(economy.PlayerCompany.symbol=="PLAYER_BIZ"&&economy.PlayerCompany!=migrated&&PlayerAccounts.Instance.primary.GetOwnedVolume(economy.PlayerCompany)==10000,"missing stock scaffold reconstructed and connected to saved ownership");
                Check(StockIntegrityChecker.Instance.CheckStock(economy.PlayerCompany).isValid,"migrated private company share ledger remains exact");
                Check(errors==0,"no runtime errors"); Finish(null);
            }
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void Audit(GameObject root)
    {
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>())
        { if (label.GetComponentInParent<TMP_InputField>()!=null) { continue; } label.ForceMeshUpdate(); if (label.isTextTruncated || label.isTextOverflowing) { throw new Exception("Clipped: "+label.name+" / "+label.text+" / "+label.rectTransform.rect); } }
        Check(true,"active labels fit: "+root.name+" / "+GameLocale.Code);
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
        if (finished) { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); } PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class CompanyGrowthContinuationProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { CompanyGrowthContinuationValidation.Step(); }
}

```

## 수정 파일: Assets\Scripts\CandleHistoryManager.cs

```diff
--- before
+++ after
@@ -72,14 +72,15 @@
 
     public void RegisterStock(Stock stock)
     {
-        if (stock == null || candles.ContainsKey(stock))
+        if (stock==null) { return; }
+        stock.OnMatchedTrade -= OnMatched; stock.OnMatchedTrade += OnMatched;
+        stock.OnRiskChanged -= OnRiskChanged; stock.OnRiskChanged += OnRiskChanged;
+        if (candles.ContainsKey(stock))
         { return; }
 
         candles[stock] = new List<CandleData>(101);
         actualCompleted[stock] = 0;
         ResetForming(stock);
-        stock.OnMatchedTrade += OnMatched;
-        stock.OnRiskChanged += OnRiskChanged;
     }
 
     private void OnRiskChanged(Stock stock, StockRiskGrade before, StockRiskGrade after)

```

## 수정 파일: Assets\Scripts\NightShift\CompanyBusiness.cs

### BeginClose

원본:
```csharp
public bool BeginClose(int day, decimal price, decimal shares, decimal demand, Rules rules)
    {
        if (day <= LastDay || day < 1 || shares <= 0 || price < 0)
        { return false; }
        LastDay = day; CloseFinished = false; ClosePrice = price; CloseShares = shares; MarketCap = price * shares;
        OpeningCash = Cash; OpeningDebt = Debt; Dividend = Investment = PlayerDividend = 0;
        Demand = Math.Max(.25m, Math.Min(1.65m, demand));
        Revenue = decimal.Floor(BaseRevenue * Demand * Productivity);
        Costs = decimal.Ceiling(BaseRevenue * ((decimal)rules.variableCostRatio * Demand + (decimal)rules.fixedCostRatio) + Debt * .001m);
        Profit = Revenue - Costs; CumulativeProfit += Profit;
        decimal net = Cash + Profit - Debt;
        Cash = Math.Max(0, net); Debt = Math.Max(0, -net); NewsDemand *= .6m;
        return true;
    }
```

수정본:
```csharp
public bool BeginClose(int day, decimal price, decimal shares, decimal demand, Rules rules,decimal efficiency=1m)
    {
        if (day <= LastDay || day < 1 || shares <= 0 || price < 0)
        { return false; }
        LastDay = day; CloseFinished = false; ClosePrice = price; CloseShares = shares; MarketCap = price * shares;
        OpeningCash = Cash; OpeningDebt = Debt; Dividend = Investment = PlayerDividend = 0;
        Demand = Math.Max(.25m, Math.Min(1.65m, demand));
        Revenue = decimal.Floor(BaseRevenue * Demand * Productivity);
        Costs = decimal.Ceiling(BaseRevenue * ((decimal)rules.variableCostRatio * Demand * Math.Max(.1m,Math.Min(1,efficiency)) + (decimal)rules.fixedCostRatio) + Debt * .001m);
        Profit = Revenue - Costs; CumulativeProfit += Profit;
        decimal net = Cash + Profit - Debt;
        Cash = Math.Max(0, net); Debt = Math.Max(0, -net); NewsDemand *= .6m;
        return true;
    }
```

### FinishClose

원본:
```csharp
public void FinishClose(decimal paid, decimal playerPaid, Rules rules)
    {
        if (LastDay == 0 || CloseFinished) { throw new InvalidOperationException("Company close already completed or not started."); }
        if (paid < 0 || paid > DividendBudget(rules) || playerPaid < 0 || playerPaid > paid) { throw new InvalidOperationException("Invalid corporate distribution."); }
        Dividend = paid; PlayerDividend = playerPaid; Cash -= paid;
        double ratio = Policy == CompanyPolicy.Investment ? rules.growthInvestment : Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        Investment = Debt > 0 ? 0 : Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio));
        Cash -= Investment;
        Productivity = Math.Min((decimal)rules.maximumProductivity, Productivity + Investment / ReferenceCapital * (decimal)rules.productivityGain);
        CloseFinished = true;
    }
```

수정본:
```csharp
public void FinishClose(decimal paid, decimal playerPaid, Rules rules)
    {
        if (LastDay == 0 || CloseFinished) { throw new InvalidOperationException("Company close already completed or not started."); }
        if (paid < 0 || paid > DividendBudget(rules) || playerPaid < 0 || playerPaid > paid) { throw new InvalidOperationException("Invalid corporate distribution."); }
        Dividend = paid; PlayerDividend = playerPaid; Cash -= paid;
        double ratio = Policy == CompanyPolicy.Investment ? rules.growthInvestment : Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        decimal capacity = rules.productivityGain<=0 ? 0 : Math.Max(0,(decimal)rules.maximumProductivity-Productivity)*ReferenceCapital/(decimal)rules.productivityGain;
        Investment = Debt > 0 ? 0 : Math.Min(decimal.Floor(capacity),Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio)));
        Cash -= Investment;
        Productivity = Math.Min((decimal)rules.maximumProductivity, Productivity + Investment / ReferenceCapital * (decimal)rules.productivityGain);
        CloseFinished = true;
    }
```

## 수정 파일: Assets\Scripts\NightShift\CompanyDashboardUI.cs

### Build

원본:
```csharp
private void Build()
    {
        heading = Label("Company Name", "", .04f,.025f,.64f,.07f,46);
        symbol = Label("Symbol", "", .04f,.10f,.45f,.045f,25);
        Action("Previous Company", "‹", .78f,.035f,.075f,.07f,() => Select(-1));
        Action("Next Company", "›", .88f,.035f,.075f,.07f,() => Select(1));
        ownership = Label("Ownership", "", .52f,.11f,.435f,.045f,27); ownership.alignment = TextAlignmentOptions.MidlineRight;
        string[] captions = { "발행 주식 수", "마감 종가", "마감 시총", "내 지분", "매출", "운영비", "순이익", "내 배당금", "기업 현금", "미지급 비용", "재투자", "배당 비율" };
        for (int i = 0; i < captions.Length; i++)
        {
            float x = .04f + i % 4 * .235f, y = .21f + i / 4 * .16f;
            var label = Label(captions[i], captions[i], x,y,.215f,.043f,25); label.color = new Color32(153,168,179,255);
            values[i] = Label(captions[i] + " Value", "—", x,y+.052f,.215f,.065f,34);
            values[i].alignment = TextAlignmentOptions.MidlineRight; TradingOrderView.Numeric(values[i]);
            var divider = new GameObject("Image :: Company Divider",typeof(RectTransform),typeof(Image)); divider.transform.SetParent(transform,false);
            Place((RectTransform)divider.transform,x,y+.135f,.215f,.0015f); divider.GetComponent<Image>().color = new Color(1,1,1,.12f); divider.GetComponent<Image>().raycastTarget = false;
        }
        day = Label("Settlement Day", "", .04f,.685f,.91f,.04f,24);
        string[] names = { "배당", "재투자", "비축" };
        for (int i = 0; i < 3; i++)
        { int index = i; policies[i] = Action("Policy " + i,names[i],.04f+i*.315f,.755f,.285f,.075f,() => Policy(index)); }
        acquire = Action("Acquire Shares","지분 매입",.04f,.87f,.285f,.075f,Acquire);
        Action("Company News","관련 뉴스",.355f,.87f,.285f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        Action("Company Orders","거래",.67f,.87f,.285f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Orders));
        feedback = Label("Company Feedback","",.04f,.955f,.91f,.035f,23);
    }
```

수정본:
```csharp
private void Build()
    {
        heading = Label("Company Name", "", .04f,.025f,.64f,.07f,46);
        symbol = Label("Symbol", "", .04f,.10f,.45f,.045f,25);
        Action("My Company", "내 기업", .65f,.035f,.12f,.07f,() => { inspected=null; showOwn=true; Refresh(); });
        Action("Previous Company", "‹", .78f,.035f,.075f,.07f,() => Select(-1));
        Action("Next Company", "›", .88f,.035f,.075f,.07f,() => Select(1));
        ownership = Label("Ownership", "", .52f,.11f,.435f,.045f,27); ownership.alignment = TextAlignmentOptions.MidlineRight;
        string[] captions = { "발행 주식 수", "마감 종가", "마감 시총", "내 지분", "매출", "운영비", "순이익", "내 배당금", "기업 현금", "미지급 비용", "재투자", "배당 비율" };
        for (int i = 0; i < captions.Length; i++)
        {
            float x = .04f + i % 4 * .235f, y = .21f + i / 4 * .16f;
            var label = Label(captions[i], captions[i], x,y,.215f,.043f,25); label.color = new Color32(153,168,179,255);
            this.captions[i]=label;
            values[i] = Label(captions[i] + " Value", "—", x,y+.052f,.215f,.065f,34);
            values[i].alignment = TextAlignmentOptions.MidlineRight; TradingOrderView.Numeric(values[i]);
            var divider = new GameObject("Image :: Company Divider",typeof(RectTransform),typeof(Image)); divider.transform.SetParent(transform,false);
            Place((RectTransform)divider.transform,x,y+.135f,.215f,.0015f); divider.GetComponent<Image>().color = new Color(1,1,1,.12f); divider.GetComponent<Image>().raycastTarget = false;
        }
        day = Label("Settlement Day", "", .04f,.685f,.91f,.04f,24);
        string[] names = { "배당", "재투자", "비축" };
        for (int i = 0; i < 3; i++)
        { int index = i; policies[i] = Action("Policy " + i,names[i],.04f+i*.315f,.755f,.285f,.075f,() => Policy(index)); }
        Action("Company Growth","성장·상장",.04f,.87f,.215f,.075f,OpenGrowth);
        acquire = Action("Acquire Shares","지분 매입",.275f,.87f,.215f,.075f,Acquire);
        Action("Company News","관련 뉴스",.51f,.87f,.215f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        trade=Action("Company Orders","거래",.745f,.87f,.21f,.075f,() => { if (FocusedStock?.IsListed==true) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(FocusedStock)); tablet.ShowPage(TabletAccountUI.Page.Orders); } });
        feedback = Label("Company Feedback","",.04f,.955f,.91f,.035f,23);
    }
```

### Refresh

원본:
```csharp
public void Refresh()
    {
        var stock = StockManager.Instance?.CurrentStock;
        if (stock == null || CorporateEconomy.Instance == null)
        { return; }
        if (selected != stock) { selected = stock; DeviceTerminalUI.Set(feedback, ""); }
        DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.symbol);
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            acquire.interactable=false; DeviceTerminalUI.Set(ownership,"—"); DeviceTerminalUI.Set(day,"—"); return;
        }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal ratio = stock.issuedShares > 0 ? owned / stock.issuedShares * 100 : 0;
        DeviceTerminalUI.Set(ownership,ratio >= 100 ? "완전 소유" : ratio > 50 ? "경영권 확보" : "경영권 50% 초과");
        bool closed = data.CloseFinished, quoted = data.LastDay > 0;
        string Money(decimal n) => n.ToString("N0") + "원";
        var rules = CorporateEconomy.Instance.Rules;
        double payout = data.Policy == CompanyPolicy.Dividend ? rules.dividendPayout : data.Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        string[] row = { stock.issuedShares.ToString("N0")+"주", quoted ? Money(data.ClosePrice) : "—", quoted ? Money(data.MarketCap) : "—", ratio.ToString("0.00####")+"%",
            closed ? Money(data.Revenue) : "—", closed ? Money(data.Costs) : "—", closed ? Money(data.Profit) : "—", closed ? Money(data.PlayerDividend) : "—",
            Money(data.Cash), Money(data.Debt), closed ? Money(data.Investment) : "—", payout.ToString("0%") };
        for (int i = 0; i < values.Length; i++) { DeviceTerminalUI.Set(values[i],row[i]); }
        values[6].color = InvestmentDashboard.ProfitColor(data.Profit);
        DeviceTerminalUI.Set(day,closed ? "DAY " + data.LastDay + " · 17:00 결산" : "첫 기업 결산 전");
        for (int i = 0; i < policies.Length; i++)
        { policies[i].interactable = ratio > 50 && MarketDayController.Instance.Phase != MarketPhase.Closing; TabletControlStyle.Emphasize(policies[i],(int)data.Policy == i); }
        acquire.interactable = MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
    }
```

수정본:
```csharp
public void Refresh()
    {
        if (growthView!=null && growthView.gameObject.activeSelf) { return; }
        var stock = FocusedStock;
        if (stock == null || CorporateEconomy.Instance == null)
        { return; }
        if (selected != stock) { selected = stock; DeviceTerminalUI.Set(feedback, ""); }
        DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.IsListed ? stock.symbol : "비상장 · "+stock.symbol);
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            acquire.interactable=trade.interactable=false; DeviceTerminalUI.Set(ownership,"—"); DeviceTerminalUI.Set(day,"—"); return;
        }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal ratio = stock.issuedShares > 0 ? owned / stock.issuedShares * 100 : 0;
        DeviceTerminalUI.Set(ownership,ratio >= 100 ? "완전 소유" : ratio > 50 ? "경영권 확보" : "경영권 50% 초과");
        bool closed = data.CloseFinished, quoted = data.growth?.ListedCloseDay>0;
        DeviceTerminalUI.Set(captions[1],stock.IsListed ? "마감 종가" : "상장 상태");
        DeviceTerminalUI.Set(captions[2],stock.IsListed ? "마감 시총" : "기업 평가액");
        string Money(decimal n) => n.ToString("N0") + "원";
        var rules = CorporateEconomy.Instance.Rules;
        double payout = data.Policy == CompanyPolicy.Dividend ? rules.dividendPayout : data.Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        string[] row = { stock.issuedShares.ToString("N0")+"주", quoted ? Money(data.ClosePrice) : "—", quoted ? Money(data.MarketCap) : "—", ratio.ToString("0.00####")+"%",
            closed ? Money(data.Revenue) : "—", closed ? Money(data.Costs) : "—", closed ? Money(data.Profit) : "—", closed ? Money(data.PlayerDividend) : "—",
            Money(data.Cash), Money(data.Debt), closed ? Money(data.Investment) : "—", payout.ToString("0%") };
        if (!stock.IsListed) { row[1]="비상장"; row[2]=data.growth==null ? "—" : Money(data.growth.Appraisal); }
        for (int i = 0; i < values.Length; i++) { DeviceTerminalUI.Set(values[i],row[i]); }
        values[6].color = InvestmentDashboard.ProfitColor(data.Profit);
        DeviceTerminalUI.Set(day,closed ? "DAY " + data.LastDay + " · 17:00 결산" : "첫 기업 결산 전");
        for (int i = 0; i < policies.Length; i++)
        { policies[i].interactable = ratio > 50 && MarketDayController.Instance.Phase != MarketPhase.Closing; TabletControlStyle.Emphasize(policies[i],(int)data.Policy == i); }
        acquire.interactable = MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
        trade.interactable=stock.AcceptsOrders && MarketDayController.CanTrade;
    }
```

### Select

원본:
```csharp
private void Select(int delta)
    { var m = StockManager.Instance; if (m == null || m.stocks.Count == 0) { return; } m.SelectStock((m.stocks.IndexOf(m.CurrentStock)+delta+m.stocks.Count)%m.stocks.Count); next=0; }
```

수정본:
```csharp
private void Select(int delta)
    {
        var list=new System.Collections.Generic.List<Stock>(CorporateEconomy.Instance.Companies()); if (list.Count==0) { return; }
        inspected=list[(list.IndexOf(FocusedStock)+delta+list.Count)%list.Count]; showOwn=false;
        if (inspected.IsListed) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(inspected)); } next=0;
    }
```

### ShowCompany

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public void ShowCompany(Stock stock) { inspected=stock; showOwn=false; Refresh(); }
```

### OpenGrowth

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public void OpenGrowth()
    { if (growthView==null) { growthView=CompanyGrowthUI.Install(this); } growthView.Show(FocusedStock); }
```

## 수정 파일: Assets\Scripts\NightShift\CorporateEconomy.cs

### Awake

원본:
```csharp
void Awake() { Instance = this; Rules = LoadRules(); }
```

수정본:
```csharp
void Awake() { Instance = this; Rules = LoadRules(); GrowthRules=LoadGrowthRules(); }
```

### Ensure

원본:
```csharp
public CompanyBusiness Ensure(Stock stock)
    {
        if (stock.business == null)
        {
            string sector = stock.symbol;
            var writing = NewsManager.Instance?.Writing;
            if (writing?.companies != null)
            { foreach (var profile in writing.companies) { if (profile.symbol == stock.symbol) { sector = profile.sector; break; } } }
            stock.business = CompanyBusiness.Create(stock.realValue, stock.issuedShares, sector, Rules);
        }
        return stock.business;
    }
```

수정본:
```csharp
public CompanyBusiness Ensure(Stock stock)
    {
        if (stock.business == null)
        {
            string sector = stock.symbol;
            var writing = NewsManager.Instance?.Writing;
            if (writing?.companies != null)
            { foreach (var profile in writing.companies) { if (profile.symbol == stock.symbol) { sector = profile.sector; break; } } }
            stock.business = CompanyBusiness.Create(stock.realValue, stock.issuedShares, sector, Rules);
        }
        if (stock.business.growth==null)
        {
            stock.business.growth=CompanyGrowth.Create(stock.business,stock.realValue*stock.issuedShares,false,1-(decimal)Rules.variableCostRatio-(decimal)Rules.fixedCostRatio);
            stock.business.growth.ListedCloseDay=stock.IsListed ? stock.business.LastDay : 0;
        }
        return stock.business;
    }
```

### Update

원본:
```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + 1;
        foreach (var stock in StockManager.Instance.stocks) { ObserveControl(stock); }
    }
```

수정본:
```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + 1;
        foreach (var stock in Companies()) { EnsureRepresentative(stock); ObserveControl(stock); }
    }
```

### SetPolicy

원본:
```csharp
public bool SetPolicy(Stock stock, CompanyPolicy policy)
    {
        if (stock == null || !stock.IsListed || stock.IsDelisted || !Enum.IsDefined(typeof(CompanyPolicy),policy) || !WorldDramaDirector.Instance.Controls(stock) || MarketDayController.Instance.Phase == MarketPhase.Closing)
        { return false; }
        Ensure(stock).Policy = policy; return true;
    }
```

수정본:
```csharp
public bool SetPolicy(Stock stock, CompanyPolicy policy)
    {
        if (!CanManage(stock) || !Enum.IsDefined(typeof(CompanyPolicy),policy))
        { return false; }
        Ensure(stock).Policy = policy; return true;
    }
```

### ReconnectSession

원본:
```csharp
public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock.business != null)
            { continue; }
            // Pre-feature saves keep their actual issued shares and closing quote. Never back-pay old days.
            var data = Ensure(stock); var market = MarketDayController.Instance;
            if (market.ClosingPrices.TryGetValue(stock,out decimal price))
            {
                data.LastDay = DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
                data.ClosePrice = price; data.CloseShares = stock.issuedShares; data.MarketCap = price * stock.issuedShares;
            }
            data.ControlInitialized = true; data.PlayerControls = WorldDramaDirector.Instance.Controls(stock);
            data.AcquisitionAnnounced = data.PlayerControls;
        }
    }
```

수정본:
```csharp
public void ReconnectSession()
    {
        foreach (var stock in Companies())
        {
            if (stock.business != null)
            { Ensure(stock); continue; }
            // Pre-feature saves keep their actual issued shares and closing quote. Never back-pay old days.
            var data = Ensure(stock); var market = MarketDayController.Instance;
            if (market.ClosingPrices.TryGetValue(stock,out decimal price))
            {
                data.LastDay = DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
                data.ClosePrice = price; data.CloseShares = stock.issuedShares; data.MarketCap = price * stock.issuedShares;
            }
            data.ControlInitialized = true; data.PlayerControls = WorldDramaDirector.Instance.Controls(stock);
            data.AcquisitionAnnounced = data.PlayerControls;
        }
        EnsureFounder();
    }
```

### PlayerDividends

원본:
```csharp
public decimal PlayerDividends(int day)
    {
        decimal total = 0;
        foreach (var stock in StockManager.Instance.stocks)
        { if (stock.business?.LastDay == day) { total += stock.business.PlayerDividend; } }
        return total;
    }
```

수정본:
```csharp
public decimal PlayerDividends(int day)
    {
        decimal total = 0;
        foreach (var stock in Companies())
        { if (stock.business?.LastDay == day) { total += stock.business.PlayerDividend; } }
        return total;
    }
```

### Settle

원본:
```csharp
public IEnumerator Settle(int day)
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            ObserveControl(stock); var data = Ensure(stock);
            if (stock.IsDelisted || data.LastDay >= day)
            { continue; }
            payees.Clear(); payments.Clear(); decimal shares = 0;
            foreach (var owner in Trader.Registry)
            { if (owner != null) { payees.Add(owner); shares += owner.GetOwnedVolume(stock); } }
            // Closing already validates the share ledger; do not mint missing-holder dividends.
            if (shares + ExchangeWarehouse.Instance.GetHolding(stock) != stock.issuedShares) { throw new InvalidOperationException("Corporate shareholder ledger mismatch: " + stock.symbol); }
            if (!data.BeginClose(day, stock.currentPrice, stock.issuedShares, data.DemandFor(day, stock.symbol, Rules), Rules))
            { continue; }
            decimal budget = data.DividendBudget(Rules), paid = 0, playerPaid = 0;
            foreach (var owner in payees)
            {
                decimal payment = CompanyBusiness.SharePayment(budget, owner.GetOwnedVolume(stock), stock.issuedShares);
                payments.Add(payment); paid += payment;
                if (PlayerAccounts.IsPlayer(owner)) { playerPaid += payment; }
            }
            data.FinishClose(paid, playerPaid, Rules);
            for (int i = 0; i < payees.Count; i++)
            {
                if (payments[i] > 0) { payees[i].ApplyTradeCash(payments[i]); }
                if (i % 64 == 63) { yield return null; }
            }
            // Retained earnings change business value; market quotes still come only from matching.
            stock.SetRealValue(Math.Max(1, stock.realValue + (data.Profit - paid) / stock.issuedShares));
            yield return null;
        }
        payees.Clear(); payments.Clear();
    }
```

수정본:
```csharp
public IEnumerator Settle(int day)
    {
        foreach (var stock in Companies())
        {
            ObserveControl(stock); var data = Ensure(stock);
            if (stock.IsDelisted || data.LastDay >= day)
            { continue; }
            PrepareBusinessDay(stock,day);
            payees.Clear(); payments.Clear(); decimal shares = 0;
            foreach (var owner in Trader.Registry)
            {
                if (owner==null) { continue; }
                decimal owned=owner.GetOwnedVolume(stock);
                if (owned>0) { payees.Add(owner); shares+=owned; }
            }
            // Closing already validates the share ledger; do not mint missing-holder dividends.
            if (shares + ExchangeWarehouse.Instance.GetHolding(stock) != stock.issuedShares) { throw new InvalidOperationException("Corporate shareholder ledger mismatch: " + stock.symbol); }
            decimal demand=data.DemandFor(day,stock.symbol,Rules)+data.growth.Sales*(decimal)GrowthRules.salesGain;
            decimal efficiency=1-data.growth.Research*(decimal)GrowthRules.researchSaving;
            if (!data.BeginClose(day, stock.currentPrice, stock.issuedShares, demand, Rules,efficiency))
            { continue; }
            decimal budget = data.DividendBudget(Rules), paid = 0, playerPaid = 0;
            foreach (var owner in payees)
            {
                decimal payment = CompanyBusiness.SharePayment(budget, owner.GetOwnedVolume(stock), stock.issuedShares);
                payments.Add(payment); paid += payment;
                if (PlayerAccounts.IsPlayer(owner)) { playerPaid += payment; }
            }
            data.FinishClose(paid, playerPaid, Rules);
            float payoutFrameStart=Time.realtimeSinceStartup;
            for (int i = 0; i < payees.Count; i++)
            {
                if (payments[i] > 0) { payees[i].ApplyTradeCash(payments[i]); }
                if (i % 64 == 63 && Time.realtimeSinceStartup-payoutFrameStart>=.002f)
                { yield return null; payoutFrameStart=Time.realtimeSinceStartup; }
            }
            // Retained earnings change business value; market quotes still come only from matching.
            FinishBusinessDay(stock);
            yield return null;
        }
        payees.Clear(); payments.Clear();
    }
```

## 수정 파일: Assets\Scripts\NightShift\DailyIncomeController.cs

```diff
--- before
+++ after
@@ -121,7 +121,8 @@
             decimal realized = (PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit) - realizedAtOpen;
             decimal equity = account.Equity() + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
             decimal available = Math.Max(0m,account.cash - account.ReservedCash);
-            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity,available,CorporateEconomy.Instance?.PlayerDividends(day) ?? 0);
+            bool managed=CorporateEconomy.Instance?.PlayerCompany?.business?.growth?.FounderBusiness==true;
+            var report = Ledger.CloseDay(day,managed ? 0 : Rules.companyIncome,realized,equity,available,CorporateEconomy.Instance?.PlayerDividends(day) ?? 0);
             if (report == null)
             { yield break; }
             // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.

```

## 수정 파일: Assets\Scripts\TradeBot\ExchangeBot.cs

```diff
--- before
+++ after
@@ -194,6 +194,7 @@
 
     private void Register(Stock stock, bool listing)
     {
+        stock.OnMatchedTrade -= OnMatched; stock.OnMatchedTrade += OnMatched;
         if (byStock.ContainsKey(stock))
         { return; }
 
@@ -218,7 +219,6 @@
         }
 
         debugStocks.Add(new StockDebug { symbol = stock.symbol });
-        stock.OnMatchedTrade += OnMatched;
         if (listing)
         {
             decimal perLevel = decimal.Floor(state.InitialInventory * Ratio(initialListingSupplyRatio) / 50m);

```

## 수정 파일: Assets\Scripts\NightShift\InvestmentPositionRow.cs

```diff
--- before
+++ after
@@ -19,8 +19,8 @@
         bool priced = stock.currentPrice > 0m;
         decimal valuation = qty * stock.currentPrice, pnl = valuation - basis;
         DeviceTerminalUI.Set(company, stock.stockName);
-        DeviceTerminalUI.Set(quantity, qty.ToString("N0") + "주");
-        DeviceTerminalUI.Set(price, priced ? DeviceTerminalUI.Money(stock.currentPrice) : "—");
+        DeviceTerminalUI.Set(quantity, qty.ToString("N0") + "주"+(stock.IsListed ? "" : " · 비상장"));
+        DeviceTerminalUI.Set(price, priced ? (stock.IsListed ? "" : "평가 ")+DeviceTerminalUI.Money(stock.currentPrice) : "—");
         DeviceTerminalUI.Set(average, "내 평균 " + (basis > 0m && qty > 0m ? DeviceTerminalUI.Money(basis / qty) : "—"));
         DeviceTerminalUI.Set(value, priced ? DeviceTerminalUI.Money(valuation) : "—");
         DeviceTerminalUI.Set(profit, priced ? InvestmentDashboard.Signed(pnl) + "원" : "—");

```

## 수정 파일: Assets\Scripts\ItCompany\ItCompanyManager.cs

```diff
--- before
+++ after
@@ -83,7 +83,7 @@
         { return; }
 
         int before = ExchangeBot.Instance == null ? 0 : ExchangeBot.Instance.PublishedCompanyNode(stock);
-        if (applyRealValueOnNodeChange)
+        if (applyRealValueOnNodeChange && stock.business?.growth==null)
         {
             stock.SetRealValue(Math.Max(0.00000001m, baseValue * (decimal)item.CriticalValue));
         }

```

## 수정 파일: Assets\Scripts\MarketDay\MarketDayController.cs

### Start

원본:
```csharp
IEnumerator Start()
    {
        yield return null;
        yield return null;
        while (Initializers > 0 || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            yield return null;
        }

        if (StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        {
            Debug.LogError("하루 초기화: StockManager/ExchangeWarehouse 필요");
            yield break;
        }

        ExchangeBot.Instance?.InitializeAssets();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!subscribed.Contains(stock))
            {
                stock.OnTradeExecutedWithStock += OnMarketTrade;
                stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
                subscribed.Add(stock);
            }

            eventPrices[stock] = stock.currentPrice;
            if (!holders.ContainsKey(stock))
            {
                holders.Add(stock, new List<TradeBot>());
            }

            if (!recentSellers.ContainsKey(stock))
            {
                recentSellers.Add(stock, new List<TradeBot>(64));
            }
        }

        InitialDistributionComplete = true;
        for (int i = 0; i < Trader.Registry.Count; i++)
        {
            var trader = Trader.Registry[i];
            trader.BeginDailyBaseline();
            if (trader is TradeBot bot)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    UpdatePositionIndex(bot, stock);
                }
            }

            if (i % Mathf.Max(1, botsPerFrame) == 0)
            {
                yield return null;
            }
        }

        foreach (var stock in StockManager.Instance.stocks) { CorporateEconomy.Instance?.ObserveControl(stock); }
        Ready = true;
        nextPublish = SessionClock.Time + publishMin;
    }
```

수정본:
```csharp
IEnumerator Start()
    {
        yield return null;
        yield return null;
        while (Initializers > 0 || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            yield return null;
        }

        if (StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        {
            Debug.LogError("하루 초기화: StockManager/ExchangeWarehouse 필요");
            yield break;
        }

        ExchangeBot.Instance?.InitializeAssets();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!subscribed.Contains(stock))
            {
                stock.OnTradeExecutedWithStock += OnMarketTrade;
                stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
                subscribed.Add(stock);
            }

            eventPrices[stock] = stock.currentPrice;
            if (!holders.ContainsKey(stock))
            {
                holders.Add(stock, new List<TradeBot>());
            }

            if (!recentSellers.ContainsKey(stock))
            {
                recentSellers.Add(stock, new List<TradeBot>(64));
            }
        }

        InitialDistributionComplete = true;
        for (int i = 0; i < Trader.Registry.Count; i++)
        {
            var trader = Trader.Registry[i];
            trader.BeginDailyBaseline();
            if (trader is TradeBot bot)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    UpdatePositionIndex(bot, stock);
                }
            }

            if (i % Mathf.Max(1, botsPerFrame) == 0)
            {
                yield return null;
            }
        }

        CorporateEconomy.Instance?.InitializeCompanyWorld();
        Ready = true;
        nextPublish = SessionClock.Time + publishMin;
    }
```

### PrepareSessionRestore

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public void PrepareSessionRestore()
    {
        if (phaseWork!=null) { StopCoroutine(phaseWork); phaseWork=null; }
        foreach (var bot in Bots) { if (bot!=null) { bot.SuspendSession(); } }
    }
```

### AdvancePhase

원본:
```csharp
private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0)
                    {
                        yield return null;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                ClosingPrices[stock] = stock.currentPrice;
            }

            foreach (var trader in closingTraders)
            {
                if (trader != null && (trader.ReservedCash != 0m || trader.EscrowVolume != 0m))
                {
                    IntegrityFailed = true; Debug.LogError("마감 예약 자산 잔존: " + trader.name); phaseWork = null; yield break;
                }
            }
            if (CorporateEconomy.Instance != null) { yield return CorporateEconomy.Instance.Settle(Day); }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            elapsed = 0f; ReportPending = true;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            PlayerRestController.Instance?.NightPassedAwake();
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
        if (ReportPending)
        {
            PlayerRestController.Instance?.ObserveClock();
            PauseMenuController.Instance?.ReceiveClosingReport();
        }
    }
```

수정본:
```csharp
private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            if (CorporateEconomy.Instance!=null) { yield return CorporateEconomy.Instance.ProcessListings(Day); }
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            closingStocks.AddRange(StockManager.Instance.UnlistedStocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            float ledgerFrameStart=Time.realtimeSinceStartup;
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock) && !StockManager.Instance.UnlistedStocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0 && Time.realtimeSinceStartup-ledgerFrameStart>=.002f)
                    {
                        yield return null;
                        ledgerFrameStart=Time.realtimeSinceStartup;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                if (stock.IsListed) { ClosingPrices[stock] = stock.currentPrice; }
            }

            foreach (var trader in closingTraders)
            {
                if (trader != null && (trader.ReservedCash != 0m || trader.EscrowVolume != 0m))
                {
                    IntegrityFailed = true; Debug.LogError("마감 예약 자산 잔존: " + trader.name); phaseWork = null; yield break;
                }
            }
            if (CorporateEconomy.Instance != null) { yield return CorporateEconomy.Instance.Settle(Day); }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            elapsed = 0f; ReportPending = true;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            PlayerRestController.Instance?.NightPassedAwake();
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
        if (ReportPending)
        {
            PlayerRestController.Instance?.ObserveClock();
            PauseMenuController.Instance?.ReceiveClosingReport();
        }
    }
```

### OnListed

원본:
```csharp
void OnListed(Stock stock)
    {
        stock.OnTradeExecutedWithStock += OnMarketTrade;
        stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
        subscribed.Add(stock);
        eventPrices[stock] = stock.currentPrice;
        if (!holders.ContainsKey(stock))
        {
            holders[stock] = new List<TradeBot>();
        }

        if (!recentSellers.ContainsKey(stock))
        {
            recentSellers[stock] = new List<TradeBot>(64);
        }
    }
```

수정본:
```csharp
void OnListed(Stock stock)
    {
        stock.OnTradeExecutedWithStock -= OnMarketTrade;
        stock.OnTradeExecutedWithStock += OnMarketTrade;
        stock.OnMatchedTrade -= ObserveConversationTrade;
        stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
        if (!subscribed.Contains(stock)) { subscribed.Add(stock); }
        if (!eventPrices.ContainsKey(stock)) { eventPrices[stock] = stock.currentPrice; }
        if (!holders.ContainsKey(stock))
        {
            holders[stock] = new List<TradeBot>();
        }

        if (!recentSellers.ContainsKey(stock))
        {
            recentSellers[stock] = new List<TradeBot>(64);
        }
    }
```

## 수정 파일: Assets\Scripts\NightShift\MarketRhythmDirector.cs

```diff
--- before
+++ after
@@ -41,10 +41,11 @@
     }
     private void Register(Stock stock)
     {
-        if (stock == null || tapes.ContainsKey(stock))
+        if (stock==null) { return; }
+        stock.OnMatchedTrade -= Observe; stock.OnMatchedTrade += Observe;
+        if (tapes.ContainsKey(stock))
         { return; }
         tapes.Add(stock, new Tape { Rhythm = (Rhythm)random.Next(4), Remaining = 25f + (float)random.NextDouble() * 55f, Anchor = stock.currentPrice });
-        stock.OnMatchedTrade += Observe;
     }
     private void Remove(Stock stock)
     {

```

## 수정 파일: Assets\Scripts\NightShift\NpcLeadingRoom.cs

```diff
--- before
+++ after
@@ -116,8 +116,8 @@
     }
     private void Watch(Stock stock)
     {
-        if (stock != null && watched.Add(stock))
-        { stock.OnMatchedTrade += Trade; }
+        if (stock==null) { return; }
+        watched.Add(stock); stock.OnMatchedTrade -= Trade; stock.OnMatchedTrade += Trade;
     }
     private void Forget(Stock stock)
     {

```

## 수정 파일: Assets\Scripts\PlayerCompanySetupUI.cs

```diff
--- before
+++ after
@@ -77,9 +77,9 @@
         {
             if (startBalance != null)
             { GameLocalizedText.Set(startBalance,manager.economy.retailStartingCash.ToString("N0") + "원"); }
-            GameLocalizedText.Set(preview,"보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.");
+            GameLocalizedText.Set(preview,"내 기업 지분 100% · 비상장\n기업 성장 · 주식 투자 · 리딩방");
             if (retailPreview != null)
-            { GameLocalizedText.Set(retailPreview,"장 마감 회사 수익 +" + (DailyIncomeController.Instance?.Rules.companyIncome ?? 250000).ToString("N0") + "원"); }
+            { GameLocalizedText.Set(retailPreview,"기업 평가액 " + CorporateEconomy.LoadGrowthRules().founderValue.ToString("N0") + "원"); }
             return;
         }
         GameLocalizedText.Set(preview,data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원");

```

## 수정 파일: Assets\Scripts\NightShift\PlayerProgression.cs

```diff
--- before
+++ after
@@ -57,8 +57,8 @@
     }
     private void Register(Stock stock)
     {
-        if (stock != null && watched.Add(stock))
-        { stock.OnMatchedTrade += Trade; }
+        if (stock==null) { return; }
+        watched.Add(stock); stock.OnMatchedTrade -= Trade; stock.OnMatchedTrade += Trade;
     }
     private void Remove(Stock stock)
     {

```

## 수정 파일: Assets\Scripts\Settings\SessionStore.cs

### RestoreData

원본:
```csharp
private static void RestoreData(JObject data)
    {
        var graph=new Graph();
        graph.Restore(data);
        typeof(Trader).GetField("nextInitialAccountId",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Trader.Registry.Max(t=>t.InitialAccountId));
        SessionClock.Restore((float)data["clock"]);
        UnityEngine.Random.state=JsonUtility.FromJson<UnityEngine.Random.State>((string)data["random"]);
        FakeNewsManager.Instance?.RestorePlayerCooldown((double)data["newsCooldown"],(long)data["savedUtc"]);
        typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
        InvestorSociety.Instance?.ReconnectSession();
        var day=MarketDayController.Instance;
        CorporateEconomy.Instance?.ReconnectSession();
        day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
        foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
        foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
        var roots=(JArray)data["roots"];
        var components=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).ToDictionary(Key,StringComparer.Ordinal);
        foreach (var binding in roots)
        {
            var component=components[(string)binding["key"]];
            component.enabled=(bool)binding["enabled"];
            if (component.gameObject.activeSelf!=(bool)binding["active"]) { component.gameObject.SetActive((bool)binding["active"]); }
        }
        GameLocale.ProtectLiteral(DailyIncomeController.Instance?.CompanyName);
        foreach (var bot in MarketDayController.Instance.Bots) { if (bot?.Social != null) { GameLocale.ProtectLiteral(bot.Social.Nickname); } }
        foreach (var list in Object.FindObjectsByType<StockListUIManager>(FindObjectsInactive.Include)) { list.ReconnectSession(); }
        var market=StockManager.Instance; int selected=market.stocks.IndexOf(market.CurrentStock);
        market.CurrentStock=null;
        if (market.stocks.Count>0) { market.SelectStock(Math.Max(0,selected)); }
    }
```

수정본:
```csharp
private static void RestoreData(JObject data)
    {
        MarketDayController.Instance.PrepareSessionRestore();
        var graph=new Graph();
        graph.Restore(data);
        typeof(Trader).GetField("nextInitialAccountId",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Trader.Registry.Max(t=>t.InitialAccountId));
        SessionClock.Restore((float)data["clock"]);
        UnityEngine.Random.state=JsonUtility.FromJson<UnityEngine.Random.State>((string)data["random"]);
        FakeNewsManager.Instance?.RestorePlayerCooldown((double)data["newsCooldown"],(long)data["savedUtc"]);
        typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
        InvestorSociety.Instance?.ReconnectSession();
        var day=MarketDayController.Instance;
        CorporateEconomy.Instance?.ReconnectSession();
        StockManager.Instance.ReconnectListedCompanies();
        day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
        foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
        foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
        var roots=(JArray)data["roots"];
        var components=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).ToDictionary(Key,StringComparer.Ordinal);
        foreach (var binding in roots)
        {
            var component=components[(string)binding["key"]];
            component.enabled=(bool)binding["enabled"];
            if (component.gameObject.activeSelf!=(bool)binding["active"]) { component.gameObject.SetActive((bool)binding["active"]); }
        }
        GameLocale.ProtectLiteral(DailyIncomeController.Instance?.CompanyName);
        foreach (var bot in MarketDayController.Instance.Bots) { if (bot?.Social != null) { GameLocale.ProtectLiteral(bot.Social.Nickname); } }
        foreach (var list in Object.FindObjectsByType<StockListUIManager>(FindObjectsInactive.Include)) { list.ReconnectSession(); }
        var market=StockManager.Instance; int selected=market.stocks.IndexOf(market.CurrentStock);
        market.CurrentStock=null;
        if (market.stocks.Count>0) { market.SelectStock(Math.Max(0,selected)); }
    }
```

## 수정 파일: Assets\Scripts\StockInterestManager.cs

```diff
--- before
+++ after
@@ -291,12 +291,13 @@
     /// <summary>새 종목이 리스트에 추가될 때 호출 (StockManager에서)</summary>
     public void RegisterStock(Stock stock)
     {
-        if (stock == null || _interest.ContainsKey(stock))
+        if (stock==null) { return; }
+        stock.OnTradeExecutedWithStock -= OnStockTraded; stock.OnTradeExecutedWithStock += OnStockTraded;
+        if (_interest.ContainsKey(stock))
         { return; }
 
         _interest[stock] = Baseline;
         _lastPrice[stock] = stock.currentPrice;
-        stock.OnTradeExecutedWithStock += OnStockTraded;
         if (StockNewsAttentionManager.Instance != null)
         {
             StockNewsAttentionManager.Instance.RegisterStock(stock);

```

## 수정 파일: Assets\Scripts\StockManager.Economy.cs

### RegisterPrivateCompany

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public Stock RegisterPrivateCompany(string id,string name,decimal price,decimal shares)
    {
        var existing=FindStock(id); if (existing!=null) { return existing; }
        if (string.IsNullOrWhiteSpace(id) || stockById.Count>=64 || shares<1 || price<=0) { throw new InvalidOperationException("Invalid private company."); }
        var stock=new Stock(id,name,price,shares) { IsListed=false };
        stockById.Add(id,stock); UnlistedStocks.Add(stock); return stock;
    }
```

### ReconnectListedCompanies

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public void ReconnectListedCompanies()
    {
        foreach (var stock in stocks)
        {
            CandleHistoryManager.Instance?.RegisterStock(stock);
            StockInterestManager.Instance?.RegisterStock(stock);
            StockStatistics.Instance?.RegisterStock(stock);
            OnStockListed?.Invoke(stock);
        }
    }
```

### BuildEconomyStocks

원본:
```csharp
public void BuildEconomyStocks()
    {
        initialRandom = new System.Random(economy.seed);
        var businessRules = CorporateEconomy.LoadRules();
        foreach (var definition in economy.companies)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.stockId) || stockById.ContainsKey(definition.stockId))
            { continue; }

            // New-world entry target only. Restored Stock instances retain their saved issued shares.
            if (definition.stockId == businessRules.entryCompany && businessRules.entryIssuedShares > 0)
            { definition.issuedShares = businessRules.entryIssuedShares; }
            var stock = new Stock(definition.stockId, definition.name, PriceTickPolicy.RoundToTick(Math.Max(1m, definition.startPrice)), definition.issuedShares);
            stock.OwnerId = definition.ownerId;
            stock.IsListed = definition.initiallyListed;
            stockById.Add(stock.symbol, stock);
            if (stock.IsListed)
            {
                stocks.Add(stock);
            }
            else
            {
                UnlistedStocks.Add(stock);
            }
        }

        CurrentStock = stocks.Count > 0 ? stocks[0] : null;
    }
```

수정본:
```csharp
public void BuildEconomyStocks()
    {
        initialRandom = new System.Random(economy.seed);
        var businessRules = CorporateEconomy.LoadRules();
        var growthRules = CorporateEconomy.LoadGrowthRules();
        foreach (var definition in economy.companies)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.stockId) || stockById.ContainsKey(definition.stockId))
            { continue; }

            // New-world entry target only. Restored Stock instances retain their saved issued shares.
            if (definition.stockId == businessRules.entryCompany && businessRules.entryIssuedShares > 0)
            { definition.issuedShares = businessRules.entryIssuedShares; }
            var stock = new Stock(definition.stockId, definition.name, PriceTickPolicy.RoundToTick(Math.Max(1m, definition.startPrice)), definition.issuedShares);
            stock.OwnerId = definition.ownerId;
            stock.IsListed = definition.initiallyListed;
            if (definition==economy.companies[0] && economy.retailPlayerStart)
            {
                stock.issuedShares=growthRules.founderShares;
                stock.currentPrice=stock.realValue=stock.fakeValue=growthRules.founderValue/(decimal)growthRules.founderShares;
                stock.orderBook=new OrderBook(stock.currentPrice); stock.IsListed=false;
            }
            stockById.Add(stock.symbol, stock);
            if (stock.IsListed)
            {
                stocks.Add(stock);
            }
            else
            {
                UnlistedStocks.Add(stock);
            }
        }

        CurrentStock = stocks.Count > 0 ? stocks[0] : null;
    }
```

### PreviewPlayer

원본:
```csharp
public bool PreviewPlayer(string name, string sharesInput, string publicInput, out PlayerSetupPreview preview, out string reason)
    {
        preview = default;
        reason = "";
        if (EconomyInitialized || SetupInProgress)
        {
            reason = "이미 확정한 초기 설정입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length < economy.minimumNameLength || name.Length > economy.maximumNameLength || name.Contains("<") || name.Contains(">") || name.IndexOfAny(new[] { '\n', '\r', '\t' }) >= 0)
        {
            reason = "종목명은 앞뒤 공백·태그 없이 2~20자로 입력하세요.";
            return false;
        }

        foreach (var definition in economy.companies)
        {
            if (definition.stockId != economy.companies[0].stockId && string.Equals(definition.name, name, StringComparison.OrdinalIgnoreCase))
            {
                reason = "이미 사용 중인 종목 이름입니다.";
                return false;
            }
        }

        if (!long.TryParse(sharesInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long shares) || shares < economy.minimumShares || shares >= economy.maximumSharesExclusive)
        {
            reason = "발행량은 1,000,000 이상 1,000,000,000 미만 정수입니다.";
            return false;
        }

        if (!int.TryParse(publicInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percentage) || percentage < 10 || percentage > 90)
        {
            reason = "공개 비율은 10~90% 정수입니다.";
            return false;
        }

        decimal price = PriceTickPolicy.FloorToTick(economy.playerTargetMarketCap / (decimal)shares);
        if (price <= 0m)
        {
            reason = "최소 단가보다 작은 설정입니다.";
            return false;
        }

        long publicShares = (long)decimal.Floor(shares * percentage / 100m);
        decimal funding = publicShares * price, marketCap = price * shares;
        foreach (var definition in economy.companies)
        {
            if (definition.initiallyListed && definition != economy.companies[0])
            {
                marketCap += (decimal)definition.startPrice * definition.issuedShares;
            }
        }

        decimal exchangeCash = economy.marketCapExchangeCash ? marketCap * (decimal)Mathf.Clamp01(economy.exchangeCashRatio) : Math.Max(0L, economy.directExchangeCash);
        preview = new PlayerSetupPreview(name, shares, publicShares, price, funding, economy.playerBaseCash + funding, exchangeCash);
        if (exchangeCash < funding)
        {
            reason = $"거래소 현금 부족: {funding - exchangeCash:N0}원 추가 필요";
            return false;
        }

        return true;
    }
```

수정본:
```csharp
public bool PreviewPlayer(string name, string sharesInput, string publicInput, out PlayerSetupPreview preview, out string reason)
    {
        preview = default;
        reason = "";
        if (EconomyInitialized || SetupInProgress)
        {
            reason = "이미 확정한 초기 설정입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length < economy.minimumNameLength || name.Length > economy.maximumNameLength || name.Contains("<") || name.Contains(">") || name.IndexOfAny(new[] { '\n', '\r', '\t' }) >= 0)
        {
            reason = "종목명은 앞뒤 공백·태그 없이 2~20자로 입력하세요.";
            return false;
        }

        foreach (var definition in economy.companies)
        {
            if (definition.stockId != economy.companies[0].stockId && string.Equals(definition.name, name, StringComparison.OrdinalIgnoreCase))
            {
                reason = "이미 사용 중인 종목 이름입니다.";
                return false;
            }
        }

        if (economy.retailPlayerStart)
        {
            var rules=CorporateEconomy.LoadGrowthRules(); decimal cap=0;
            foreach (var item in stocks) { cap+=item.currentPrice*item.issuedShares; }
            preview=new PlayerSetupPreview(name,rules.founderShares,0,rules.founderValue/(decimal)rules.founderShares,0,economy.retailStartingCash,
                economy.marketCapExchangeCash ? cap*(decimal)economy.exchangeCashRatio : economy.directExchangeCash);
            return true;
        }

        if (!long.TryParse(sharesInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long shares) || shares < economy.minimumShares || shares >= economy.maximumSharesExclusive)
        {
            reason = "발행량은 1,000,000 이상 1,000,000,000 미만 정수입니다.";
            return false;
        }

        if (!int.TryParse(publicInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percentage) || percentage < 10 || percentage > 90)
        {
            reason = "공개 비율은 10~90% 정수입니다.";
            return false;
        }

        decimal price = PriceTickPolicy.FloorToTick(economy.playerTargetMarketCap / (decimal)shares);
        if (price <= 0m)
        {
            reason = "최소 단가보다 작은 설정입니다.";
            return false;
        }

        long publicShares = (long)decimal.Floor(shares * percentage / 100m);
        decimal funding = publicShares * price, marketCap = price * shares;
        foreach (var definition in economy.companies)
        {
            if (definition.initiallyListed && definition != economy.companies[0])
            {
                marketCap += (decimal)definition.startPrice * definition.issuedShares;
            }
        }

        decimal exchangeCash = economy.marketCapExchangeCash ? marketCap * (decimal)Mathf.Clamp01(economy.exchangeCashRatio) : Math.Max(0L, economy.directExchangeCash);
        preview = new PlayerSetupPreview(name, shares, publicShares, price, funding, economy.playerBaseCash + funding, exchangeCash);
        if (exchangeCash < funding)
        {
            reason = $"거래소 현금 부족: {funding - exchangeCash:N0}원 추가 필요";
            return false;
        }

        return true;
    }
```

### ConfirmPlayer

원본:
```csharp
public bool ConfirmPlayer(string name, string shares, string percentage, out string reason)
    {
        if (!PreviewPlayer(name, shares, percentage, out var preview, out reason))
        { return false; }

        EnsureOwnerAccounts();
        if (playerAccount == null || ownersById.Count != economy.companies.Length || ExchangeBot.Instance == null || ExchangeWarehouse.Instance == null)
        {
            reason = "초기 계좌 연결이 준비되지 않았습니다.";
            return false;
        }

        SetupInProgress = true;
        StartCoroutine(InitializeEconomy(preview));
        return true;
    }
```

수정본:
```csharp
public bool ConfirmPlayer(string name, string shares, string percentage, out string reason)
    {
        if (economy.retailPlayerStart && DailyIncomeController.Instance!=null) { name=DailyIncomeController.Instance.CompanyName; }
        if (!PreviewPlayer(name, shares, percentage, out var preview, out reason))
        { return false; }

        EnsureOwnerAccounts();
        if (playerAccount == null || ownersById.Count != economy.companies.Length || ExchangeBot.Instance == null || ExchangeWarehouse.Instance == null)
        {
            reason = "초기 계좌 연결이 준비되지 않았습니다.";
            return false;
        }

        SetupInProgress = true;
        StartCoroutine(InitializeEconomy(preview));
        return true;
    }
```

### InitializeEconomy

원본:
```csharp
private IEnumerator InitializeEconomy(PlayerSetupPreview preview)
    {
        yield return null;
        yield return null;
        while (MarketDayController.Initializers > 0)
        {
            yield return null;
        }

        using (InitialMarker.Auto())
        {
            var playerStock = playerAccount.OwnedCompany;
            playerStock.stockName = preview.Name;
            playerStock.issuedShares = preview.Shares;
            playerStock.currentPrice = playerStock.realValue = playerStock.fakeValue = preview.Price;
            playerStock.orderBook = new OrderBook(preview.Price);
            StockStatistics.Instance?.ResetInitialStock(playerStock);
            for (int i = 0; i < economy.companies.Length; i++)
            {
                var definition = economy.companies[i];
                var stock = FindStock(definition.stockId);
                var owner = stock.Owner;
                owner.cash = i == 0 ? economy.playerBaseCash : definition.initialCash;
                owner.InitialCashAssigned = true;
                owner.GrantShares(stock, i == 0 ? stock.issuedShares : decimal.Floor(stock.issuedShares * (decimal)Mathf.Clamp01(definition.ownerRatio)), stock.currentPrice);
                ExchangeWarehouse.Instance.InitializeStock(stock);
            }

            foreach (var trader in Trader.Registry)
            {
                if (trader is TradeBot bot)
                {
                    AssignInitialCash(bot);
                }
            }

            var exchange = ExchangeBot.Instance;
            exchange.useMarketCapCash = economy.marketCapExchangeCash;
            exchange.initialCashMarketCapRatio = economy.exchangeCashRatio;
            exchange.initialCashAmount = economy.directExchangeCash;
            exchange.InitializeAssets();
            exchange.ApplyTradeCash(-preview.Funding);
            playerAccount.ApplyTradeCash(preview.Funding);
            playerAccount.ForceRemoveHolding(playerStock, preview.PublicShares);
            exchange.GrantShares(playerStock, preview.PublicShares, preview.Price);
            foreach (var stock in UnlistedStocks)
            {
                exchange.GrantShares(stock, ExchangeWarehouse.Instance.TakeRemaining(stock), stock.currentPrice);
            }

            foreach (var stock in stocks)
            {
                DistributePublicShares(stock);
                exchange.ResetInitialInventory(stock);
            }

            if (economy.retailPlayerStart)
            {
                decimal transfer = playerAccount.cash - economy.retailStartingCash;
                playerAccount.ApplyTradeCash(-transfer);
                exchange.ApplyTradeCash(transfer);
                decimal shares = playerAccount.GetOwnedVolume(playerStock);
                playerAccount.ForceRemoveHolding(playerStock, shares);
                exchange.GrantShares(playerStock, shares, playerStock.currentPrice);
                exchange.ResetInitialInventory(playerStock);
            }

            EconomyInitialized = true;
            SetupInProgress = false;
            PlayerStartingEquity = playerAccount.Equity();
            initialAssets.Clear();
            foreach (var definition in economy.companies)
            {
                var stock = FindStock(definition.stockId);
                decimal general = 0m;
                foreach (var trader in Trader.Registry)
                {
                    if (!trader.IsCompanyOwner && !trader.IsExchangeParticipant)
                    {
                        general += trader.GetOwnedVolume(stock);
                    }
                }

                initialAssets.Add(new InitialAssetDebug { stockId = stock.symbol, ownerId = stock.OwnerId, listed = stock.IsListed, issued = (double)stock.issuedShares, ownerShares = (double)stock.Owner.GetOwnedVolume(stock), ownerCash = (double)stock.Owner.cash, exchangeShares = (double)exchange.GetOwnedVolume(stock), generalShares = (double)general });
            }

            foreach (var trader in Trader.Registry)
            {
                trader.BeginDailyBaseline();
            }

            foreach (var stock in stocks)
            {
                ItCompanyManager.Instance?.RegisterStock(stock);
                OnStockListed?.Invoke(stock);
            }

            OnInitialEconomyReady?.Invoke();
        }
    }
```

수정본:
```csharp
private IEnumerator InitializeEconomy(PlayerSetupPreview preview)
    {
        yield return null;
        yield return null;
        while (MarketDayController.Initializers > 0)
        {
            yield return null;
        }

        using (InitialMarker.Auto())
        {
            var playerStock = playerAccount.OwnedCompany;
            playerStock.stockName = preview.Name;
            playerStock.issuedShares = preview.Shares;
            playerStock.currentPrice = playerStock.realValue = playerStock.fakeValue = preview.Price;
            playerStock.orderBook = new OrderBook(preview.Price);
            if (playerStock.IsListed) { StockStatistics.Instance?.ResetInitialStock(playerStock); }
            for (int i = 0; i < economy.companies.Length; i++)
            {
                var definition = economy.companies[i];
                var stock = FindStock(definition.stockId);
                var owner = stock.Owner;
                owner.cash = i == 0 ? economy.playerBaseCash : definition.initialCash;
                owner.InitialCashAssigned = true;
                owner.GrantShares(stock, i == 0 ? stock.issuedShares : decimal.Floor(stock.issuedShares * (decimal)Mathf.Clamp01(definition.ownerRatio)), stock.currentPrice);
                ExchangeWarehouse.Instance.InitializeStock(stock);
            }

            foreach (var trader in Trader.Registry)
            {
                if (trader is TradeBot bot)
                {
                    AssignInitialCash(bot);
                }
            }

            var exchange = ExchangeBot.Instance;
            exchange.useMarketCapCash = economy.marketCapExchangeCash;
            exchange.initialCashMarketCapRatio = economy.exchangeCashRatio;
            exchange.initialCashAmount = economy.directExchangeCash;
            exchange.InitializeAssets();
            exchange.ApplyTradeCash(-preview.Funding);
            playerAccount.ApplyTradeCash(preview.Funding);
            playerAccount.ForceRemoveHolding(playerStock, preview.PublicShares);
            exchange.GrantShares(playerStock, preview.PublicShares, preview.Price);
            foreach (var stock in UnlistedStocks)
            {
                exchange.GrantShares(stock, ExchangeWarehouse.Instance.TakeRemaining(stock), stock.currentPrice);
            }

            foreach (var stock in stocks)
            {
                DistributePublicShares(stock);
                exchange.ResetInitialInventory(stock);
            }

            if (economy.retailPlayerStart)
            {
                decimal transfer = playerAccount.cash - economy.retailStartingCash;
                playerAccount.ApplyTradeCash(-transfer);
                exchange.ApplyTradeCash(transfer);
                // Founder keeps all private shares. Only liquid personal starting cash is normalized.
            }

            EconomyInitialized = true;
            SetupInProgress = false;
            PlayerStartingEquity = playerAccount.Equity();
            initialAssets.Clear();
            foreach (var definition in economy.companies)
            {
                var stock = FindStock(definition.stockId);
                decimal general = 0m;
                foreach (var trader in Trader.Registry)
                {
                    if (!trader.IsCompanyOwner && !trader.IsExchangeParticipant)
                    {
                        general += trader.GetOwnedVolume(stock);
                    }
                }

                initialAssets.Add(new InitialAssetDebug { stockId = stock.symbol, ownerId = stock.OwnerId, listed = stock.IsListed, issued = (double)stock.issuedShares, ownerShares = (double)stock.Owner.GetOwnedVolume(stock), ownerCash = (double)stock.Owner.cash, exchangeShares = (double)exchange.GetOwnedVolume(stock), generalShares = (double)general });
            }

            foreach (var trader in Trader.Registry)
            {
                trader.BeginDailyBaseline();
            }

            foreach (var stock in stocks)
            {
                ItCompanyManager.Instance?.RegisterStock(stock);
                OnStockListed?.Invoke(stock);
            }

            OnInitialEconomyReady?.Invoke();
        }
    }
```

### ListOwnerCompany

원본:
```csharp
public Stock ListOwnerCompany(string id)
    {
        var stock = FindStock(id);
        if (!EconomyInitialized || stock == null || stock.IsListed || stock.IsDelisting || !UnlistedStocks.Remove(stock))
        { return null; }

        stock.IsListed = true;
        stock.IsNewListing = true;
        stocks.Add(stock);
        DistributePublicShares(stock);
        CandleHistoryManager.Instance?.RegisterStock(stock);
        StockInterestManager.Instance?.RegisterStock(stock);
        StockStatistics.Instance?.RegisterStock(stock);
        PsychologicalLevelManager.Instance?.RegisterStock(stock);
        ItCompanyManager.Instance?.RegisterStock(stock);
        OnStockListed?.Invoke(stock);
        return stock;
    }
```

수정본:
```csharp
public Stock ListOwnerCompany(string id)
    {
        var stock = FindStock(id);
        if (!EconomyInitialized || stock == null || stock.IsListed || stock.IsDelisting || stock.business?.growth==null || !stock.business.growth.ListingApproved || !UnlistedStocks.Remove(stock))
        { return null; }

        stock.IsListed = true;
        stock.IsNewListing = true;
        stocks.Add(stock);
        // Public shares were purchased by the underwriter with finite cash; subsequent trades use the book.
        CandleHistoryManager.Instance?.RegisterStock(stock);
        StockInterestManager.Instance?.RegisterStock(stock);
        StockStatistics.Instance?.RegisterStock(stock);
        PsychologicalLevelManager.Instance?.RegisterStock(stock);
        ItCompanyManager.Instance?.RegisterStock(stock);
        OnStockListed?.Invoke(stock);
        return stock;
    }
```

## 수정 파일: Assets\Scripts\Settings\SystemMenuUI.cs

```diff
--- before
+++ after
@@ -147,7 +147,7 @@
     public void ReportCompanies()
     {
         Show("companies","기업 결산"); int i=0;
-        foreach (var stock in StockManager.Instance.stocks)
+        foreach (var stock in CorporateEconomy.Instance.Companies())
         {
             var data=stock.business; if (data==null || !data.CloseFinished) { continue; }
             if (i==companyRows.Count)

```

## 수정 파일: Assets\Scripts\Trader.cs

### ContributePrivateCapital

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public bool ContributePrivateCapital(Stock stock, decimal amount)
    {
        if (stock == null || stock.IsListed || GetHolding(stock) != stock.issuedShares || amount <= 0 || amount != decimal.Floor(amount) || PlayerAccounts.Available(this) < amount)
        { return false; }
        avgPrice[stock] += amount / stock.issuedShares;
        ApplyTradeCash(-amount);
        return true;
    }
```

### SubscribeShares

원본:
```csharp
(없음 — 신규 함수)
```

수정본:
```csharp
public bool SubscribeShares(Stock stock, decimal shares, decimal price)
    {
        if (stock == null || shares < 1 || shares != decimal.Floor(shares) || price < 1 || cash - ReservedCash < shares * price)
        { return false; }
        ApplyTradeCash(-shares * price);
        AddHolding(stock, shares, price);
        return true;
    }
```

## 수정 파일: Assets\Scripts\NightShift\WorldDramaDirector.cs

```diff
--- before
+++ after
@@ -39,9 +39,9 @@
     }
     private void Subscribe(Stock stock)
     {
-        if (subscribed.Contains(stock))
-        { return; }
-        subscribed.Add(stock); stock.OnMatchedTrade += Matched;
+        if (stock==null) { return; }
+        if (!subscribed.Contains(stock)) { subscribed.Add(stock); }
+        stock.OnMatchedTrade -= Matched; stock.OnMatchedTrade += Matched;
     }
     void Update()
     {

```
