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
    private TextMeshProUGUI title, representative, valuation, cash, earnings, profit, progress, state, result, allocation;
    private readonly TextMeshProUGUI[] costs=new TextMeshProUGUI[3], criteria=new TextMeshProUGUI[4];
    private readonly Image[] checks=new Image[4];
    private readonly Button[] policies=new Button[3];
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
        GetComponent<Image>().color=TabletKitStyle.Background;
        TabletKitStyle.Glyph("company",transform,.0f,.015f,.04f,.055f);
        title=Label("Company","",.05f,.008f,.60f,.065f,42);
        state=Label("Listing State","",.69f,.015f,.31f,.05f,28); state.alignment=TextAlignmentOptions.MidlineRight;
        representative=Label("Representative","",.05f,.08f,.95f,.045f,26);
        string[] captions={"기업 평가액","기업 현금","일매출","순이익"};
        for (int i=0;i<4;i++) { Label("Metric Caption "+i,captions[i],i*.25f,.16f,.23f,.045f,27).color=Muted; }
        valuation=Label("Appraisal","—",0,.215f,.23f,.065f,39);
        cash=Label("Cash","—",.25f,.215f,.23f,.065f,36);
        earnings=Label("Daily Earnings","—",.5f,.215f,.23f,.065f,32);
        profit=Label("Daily Profit","—",.75f,.215f,.25f,.065f,32);
        foreach (var value in new[]{valuation,cash,earnings,profit}) { TradingOrderView.Numeric(value); value.alignment=TextAlignmentOptions.MidlineLeft; value.fontSize=36; }
        Surface("Company Summary Divider",0,.30f,1,.0015f,TabletKitStyle.Line);
        string[] policyNames={"배당","재투자","비축"};
        for (int i=0;i<3;i++)
        { int index=i; policies[i]=Button("Policy "+i,policyNames[i],i*.225f,.33f,.21f,.065f,()=> { CorporateEconomy.Instance.SetPolicy(stock,(CompanyPolicy)index); Refresh(); }); }
        allocation=Label("Allocation","",0,.405f,.66f,.045f,26);
        Surface("Projects Divider",0,.47f,.66f,.0015f,TabletKitStyle.Line);
        for (int i=0;i<3;i++)
        {
            int index=i; float y=.49f+i*.105f;
            TabletKitStyle.Glyph(new[]{"factory","sales","research"}[i],transform,0,y,.04f,.06f);
            Label("Project Title "+i,CorporateEconomy.ProjectName((CompanyProject)i),.05f,y,.35f,.045f,29);
            projects[i]=Button("Project "+i,"투자",.54f,y,.12f,.07f,()=>
            { CorporateEconomy.Instance.StartProject(stock,(CompanyProject)index); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
            costs[i]=Label("Project Cost "+i,"",.05f,y+.047f,.47f,.045f,25);
        }
        progress=Label("Project Progress","",0,.815f,.66f,.04f,25);
        var track=Surface("Project Track",0,.864f,.66f,.006f,TabletKitStyle.Line);
        var fill=Surface("Project Fill",0,0,1,1,new Color32(37,175,192,255)); fill.transform.SetParent(track.transform,false); Place((RectTransform)fill.transform,0,0,1,1); progressFill=fill;
        Surface("IPO Divider",.69f,.33f,.0015f,.54f,TabletKitStyle.Line);
        Label("IPO Heading","상장 준비",.72f,.33f,.28f,.06f,33);
        for (int i=0;i<4;i++)
        {
            checks[i]=TabletKitStyle.Glyph("check",transform,.72f,.415f+i*.085f,.03f,.045f);
            criteria[i]=Label("IPO Criterion "+i,"",.765f,.405f+i*.09f,.235f,.085f,26);
            criteria[i].textWrappingMode=TextWrappingModes.Normal;
        }
        amount=Input(0,.91f,.24f,.07f); amount.SetTextWithoutNotify("5000000");
        fund=Button("Fund Company","출자",.255f,.91f,.12f,.07f,()=>
        { decimal.TryParse(amount.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value); CorporateEconomy.Instance.FundCompany(stock,owner.Account,value); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
        listing=Button("Apply IPO","상장 신청",.72f,.79f,.28f,.08f,()=>
        { CorporateEconomy.Instance.RequestListing(stock); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
        Button("Back To Company","기업 정보",.72f,.91f,.28f,.07f,Close);
        result=Label("Action Result","",.395f,.91f,.29f,.07f,25); result.textWrappingMode=TextWrappingModes.Normal;
    }
    public void Refresh()
    {
        var economy=CorporateEconomy.Instance; var data=stock?.business; var growth=data?.growth;
        if (economy==null || growth==null)
        { return; }
        var rules=economy.GrowthRules; bool manage=economy.CanManage(stock);
        Set(title,stock.stockName);
        string name=PlayerAccounts.IsPlayer(stock.Owner) ? "플레이어" : (stock.Owner as TradeBot)?.Social?.Nickname ?? "—";
        decimal ratio=stock.issuedShares>0 ? PlayerAccounts.Instance.Owned(stock)/stock.issuedShares*100 : 0;
        Set(representative,"대표 · "+name+"   ·   지분 "+ratio.ToString("0.##")+"%");
        Set(state,stock.IsListed ? "상장" : growth.RequestedDay>0 ? "상장 심사 대기" : "비상장");
        Set(valuation,Money(growth.Appraisal)); Set(cash,Money(data.Cash));
        Set(earnings,data.CloseFinished ? Money(data.Revenue) : "—");
        Set(profit,data.CloseFinished ? Money(data.Profit) : "—"); profit.color=InvestmentDashboard.ProfitColor(data.Profit);
        var businessRules=economy.Rules;
        double dividend=data.Policy==CompanyPolicy.Dividend ? businessRules.dividendPayout : data.Policy==CompanyPolicy.Investment ? businessRules.investmentPayout : businessRules.reservePayout;
        double reinvest=data.Policy==CompanyPolicy.Investment ? businessRules.growthInvestment : data.Policy==CompanyPolicy.Dividend ? businessRules.dividendInvestment : 0;
        Set(allocation,"배당 "+dividend.ToString("0%")+"  ·  재투자 "+reinvest.ToString("0%")+"  ·  비축 "+(1-dividend-reinvest).ToString("0%"));
        for (int i=0;i<3;i++) { policies[i].interactable=manage; TabletKitStyle.Flat(policies[i].image,(int)data.Policy==i ? TabletKitStyle.Hover : TabletKitStyle.Background); policies[i].GetComponentInChildren<TMP_Text>().color=(int)data.Policy==i ? TabletKitStyle.Accent : Muted; }
        for (int i=0;i<3;i++)
        {
            var type=(CompanyProject)i; int level=growth.Level(type); decimal cost=growth.ProjectCost(data,type,rules);
            int target=Math.Min(level+1,rules.maximumLevel);
            string effect=i==0 ? "생산 ×"+(1+target*rules.capacityGain).ToString("0.00") : i==1 ? "수요 +"+(target*rules.salesGain*100).ToString("0")+"%p" : "변동비 −"+(target*rules.researchSaving*100).ToString("0")+"%";
            Set(costs[i],"Lv."+level+" · "+GameLocale.Text(effect)+" · "+(level>=rules.maximumLevel ? "최대 단계" : Money(cost)+" · "+rules.projectDays+"일"));
            projects[i].interactable=manage && data.Debt==0 && growth.Project<0 && growth.LastProjectDay!=MarketDayController.Instance.Day && level<rules.maximumLevel && data.Cash>=cost;
            projects[i].GetComponentInChildren<TMP_Text>().color=projects[i].interactable ? TabletKitStyle.Ink : Muted;
        }
        Set(progress,growth.Project>=0 ? CorporateEconomy.ProjectName((CompanyProject)growth.Project)+" · "+GameLocale.Day(growth.CompleteDay)+" 완료" : "진행 중인 투자 없음");
        float fraction=growth.Project<0 ? 0 : Mathf.Clamp01((MarketDayController.Instance.Day-growth.LastProjectDay)/(float)Math.Max(1,growth.CompleteDay-growth.LastProjectDay));
        var rect=(RectTransform)progressFill.transform; rect.anchorMax=new Vector2(fraction,1);
        Set(criteria[0],"상장 평가액\n"+Money(rules.listingValue));
        Set(criteria[1],"흑자 결산\n"+growth.ProfitableCloses+" / "+rules.listingProfitableCloses);
        Set(criteria[2],"사업 확장\n"+growth.CompletedProjects+" / "+rules.listingProjects);
        Set(criteria[3],"미지급 비용\n"+Money(data.Debt));
        criteria[0].color=growth.Appraisal>=rules.listingValue ? Ready : Muted;
        criteria[1].color=growth.ProfitableCloses>=rules.listingProfitableCloses ? Ready : Muted;
        criteria[2].color=growth.CompletedProjects>=rules.listingProjects ? Ready : Muted;
        criteria[3].color=data.Debt==0 ? Ready : Muted;
        for (int i=0;i<4;i++) { checks[i].sprite=TabletKitStyle.Icon(criteria[i].color==Ready ? "check" : "minus"); checks[i].color=criteria[i].color; }
        fund.interactable=amount.interactable=manage && !stock.IsListed && owner.Account.GetHolding(stock)==stock.issuedShares;
        listing.interactable=manage && !stock.IsListed && growth.RequestedDay==0 && growth.Eligible(data,rules);
        TabletControlStyle.Emphasize(listing,listing.interactable);
    }
    void Update()
    {
        if (Time.unscaledTime<next)
        { return; }
        next=Time.unscaledTime+.5f; Refresh();
    }
    private static readonly Color Muted=new Color32(146,161,172,255), Ready=new Color32(98,191,176,255);
    private static string Money(decimal value) => DeviceTerminalUI.Money(value);
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
