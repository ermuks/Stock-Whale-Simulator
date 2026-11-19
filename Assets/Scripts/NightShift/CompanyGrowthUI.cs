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
    private const int ProjectCount=7;
    private static readonly string[] ProjectIcons={"factory","sales","research","growth","robot","market","check"};
    /// <summary>새로 생긴 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly System.Collections.Generic.Dictionary<string,(string en,string ja)> Words=new()
    {
        ["브랜드 구축"]=("Build the brand","ブランド構築"), ["공정 자동화"]=("Automate production","工程の自動化"), ["해외 진출"]=("Go overseas","海外進出"), ["품질 관리"]=("Quality control","品質管理"),
        ["생산 ×{0}"]=("Output ×{0}","生産 ×{0}"), ["수요 +{0}%p"]=("Demand +{0}pp","需要 +{0}%p"), ["변동비 −{0}%"]=("Var. costs −{0}%","変動費 −{0}%"),
        ["불황 타격 −{0}%"]=("Slump hit −{0}%","不況の打撃 −{0}%"), ["고정비 −{0}%p"]=("Fixed costs −{0}pp","固定費 −{0}%p"), ["해외 매출 +{0}%"]=("Overseas sales +{0}%","海外売上 +{0}%"), ["평가 배수 {0}배"]=("Valuation ×{0}","評価倍率 {0}倍"),
        ["진행 중"]=("In progress","進行中"), ["{0}일"]=("{0} days","{0}日"), ["최대 단계"]=("Max level","最大段階"),
        ["동네 가게"]=("Corner shop","街の店"), ["소기업"]=("Small business","小企業"), ["중소기업"]=("Growing company","中小企業"), ["중견기업"]=("Established company","中堅企業"), ["대기업"]=("Major corporation","大企業"),
        ["등급 · {0}"]=("Rank · {0}","ランク · {0}"), ["다음 등급 {0}까지 {1}"]=("{1} to reach {0}","{0}まであと {1}"), ["최고 등급"]=("Top rank","最高ランク"),
        ["동시 투자 2건"]=("2 projects at once","同時投資 2件"), ["{0}부터 동시 투자 2건"]=("2 projects at once from {0}","{0}から同時投資 2件"),
        ["상장 준비"]=("IPO readiness","上場準備"), ["최종 목표"]=("Final goal","最終目標"), ["목표 달성 · {0}"]=("Goal reached · {0}","目標達成 · {0}"),
        ["사업 확장"]=("Expansions","事業拡張"), ["마감 시가총액"]=("Closing market cap","終値の時価総額"),
        ["{0}% · {1} 남음"]=("{0}% · {1} left","{0}% · 残り{1}"),
        ["진행 중인 투자 없음"]=("No active project","進行中の投資なし"), ["{0} · {1} 완료"]=("{0} · done {1}","{0} · {1} 完了"),
    };
    private static string T(string korean) => GameLocale.Code=="ko" || !Words.TryGetValue(korean,out var other) ? korean : GameLocale.Code=="ja" ? other.ja : other.en;
    /// <summary>투자 이름. 예전 셋은 번역 표에, 새 넷은 이 파일의 표에 있다.</summary>
    private static string ProjectLabel(CompanyProject type)
    { string name=CorporateEconomy.ProjectName(type); return Words.ContainsKey(name) ? T(name) : GameLocale.Text(name); }
    private TextMeshProUGUI title, representative, valuation, cash, earnings, profit, progress, state, result, allocation, rank;
    private readonly TextMeshProUGUI[] costs=new TextMeshProUGUI[ProjectCount], projectTitles=new TextMeshProUGUI[ProjectCount], criteria=new TextMeshProUGUI[4];
    private readonly Image[] checks=new Image[4];
    private readonly Button[] policies=new Button[3];
    private readonly Button[] projects=new Button[ProjectCount];
    // 카드 격자의 자리: 왼쪽 영역(너비 0.66)에 네 칸, 두 줄.
    private const float CardWidth=.155f, CardStep=.1675f, CardHeight=.265f, GridTop=.335f, RowStep=.285f;
    private readonly TextMeshProUGUI[] criterionValues=new TextMeshProUGUI[4];
    private readonly Image[] criterionFills=new Image[4];
    private const int MaxPips=5;
    private readonly Image[,] pips=new Image[ProjectCount,MaxPips];
    private readonly Image[] cardLines=new Image[ProjectCount], workTracks=new Image[ProjectCount], workFills=new Image[ProjectCount];
    private readonly TextMeshProUGUI[] levels=new TextMeshProUGUI[ProjectCount];
    /// <summary>게임 분을 짧게 적는다: 1일 4시간 / 1d 4h / 1日4時間.</summary>
    private static string Span(double minutes)
    {
        long total=(long)Math.Ceiling(Math.Max(0,minutes)), days=total/1440, hours=total%1440/60, rest=total%60; string code=GameLocale.Code;
        if (days>0) { return code=="en" ? days+"d "+hours+"h" : code=="ja" ? days+"日"+hours+"時間" : days+"일 "+hours+"시간"; }
        if (hours>0) { return code=="en" ? hours+"h "+rest+"m" : code=="ja" ? hours+"時間"+rest+"分" : hours+"시간 "+rest+"분"; }
        return code=="en" ? rest+"m" : code=="ja" ? rest+"分" : rest+"분";
    }
    private TextMeshProUGUI effect, advice;
    private readonly Image[] splitParts=new Image[3];
    private Button invest;
    private int selected;
    private Button fund, listing;
    private TextMeshProUGUI goalHeading;
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
        representative=Label("Representative","",.05f,.08f,.40f,.045f,26);
        // 기업 등급과 다음 등급까지 남은 평가액. 키우는 목표가 보이게 한다.
        rank=Label("Company Rank","",.46f,.08f,.54f,.045f,24); rank.alignment=TextAlignmentOptions.MidlineRight; rank.color=TabletKitStyle.Accent;
        string[] captions={"기업 평가액","기업 현금","일매출","순이익"};
        // 여기서 쓰는 숫자는 둘뿐이다: 얼마짜리 회사인지, 쓸 수 있는 돈이 얼마인지. 매출·순이익은 기업 정보 화면에 있다.
        for (int i=0;i<4;i++) { var caption=Label("Metric Caption "+i,captions[i],i*.25f,.16f,.23f,.045f,27); caption.color=Muted; caption.gameObject.SetActive(i<2); }
        valuation=Label("Appraisal","—",0,.215f,.23f,.065f,39);
        cash=Label("Cash","—",.25f,.215f,.23f,.065f,36);
        earnings=Label("Daily Earnings","—",.5f,.215f,.23f,.065f,32);
        profit=Label("Daily Profit","—",.75f,.215f,.25f,.065f,32);
        foreach (var value in new[]{valuation,cash,earnings,profit}) { TradingOrderView.Numeric(value); value.alignment=TextAlignmentOptions.MidlineLeft; value.fontSize=40; }
        earnings.gameObject.SetActive(false); profit.gameObject.SetActive(false); cash.color=TabletKitStyle.Accent;
        Surface("Company Summary Divider",0,.30f,1,.0015f,TabletKitStyle.Line);
        string[] policyNames={"배당","재투자","비축"};
        for (int i=0;i<3;i++)
        { int index=i; policies[i]=Button("Policy "+i,policyNames[i],i*.225f,.33f,.21f,.065f,()=> { CorporateEconomy.Instance.SetPolicy(stock,(CompanyPolicy)index); Refresh(); }); }
        // 이익을 나누는 몫을 띠로 보여 준다(배당·재투자·비축). 글자는 그 밑의 범례다.
        var split=Surface("Policy Split",0,.402f,.66f,.014f,TabletKitStyle.Line);
        Color[] splitColors={TabletKitStyle.Accent,new Color32(242,188,100,255),new Color32(110,129,142,255)};
        for (int i=0;i<3;i++) { splitParts[i]=TabletKitStyle.Surface("Policy Split "+i,split.transform,0,0,1,1,splitColors[i]); }
        // 결산 정책은 기업 정보 화면에서 고른다. 이 화면은 투자와 상장만 다룬다.
        split.gameObject.SetActive(false); foreach (var policy in policies) { policy.gameObject.SetActive(false); }
        allocation=Label("Allocation","",0,.418f,.66f,.03f,21); allocation.color=Muted; allocation.gameObject.SetActive(false);
        // 지금 권하는 정책과 그 까닭.
        advice=Label("Policy Advice","",.0f,.44f,.66f,.032f,19); advice.color=new Color32(61,220,132,255); advice.overflowMode=TextOverflowModes.Ellipsis; advice.gameObject.SetActive(false);
        // 투자 일곱 가지를 카드 격자로 보여 준다(네 칸 두 줄). 카드에는 아이콘·이름·단계 점·비용만 둔다.
        // 고른 카드의 다음 효과와 투자 단추는 마지막 칸에 있다.
        for (int i=0;i<ProjectCount;i++)
        {
            int index=i; float x=i%4*CardStep, y=GridTop+i/4*RowStep;
            var card=Button("Project "+i,"",x,y,CardWidth,CardHeight,()=> { selected=index; Refresh(); });
            projects[i]=card; card.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
            TabletKitStyle.Glyph(ProjectIcons[i],card.transform,.08f,.09f,.20f,.27f);
            projectTitles[i]=Child(card.transform,"Project Title",.08f,.42f,.88f,.20f,23);
            for (int step=0;step<MaxPips;step++) { pips[i,step]=TabletKitStyle.Surface("Level "+step,card.transform,.08f+step*.105f,.68f,.085f,.045f,TabletKitStyle.Line); }
            costs[i]=Child(card.transform,"Project Cost",.08f,.80f,.88f,.15f,19); costs[i].color=Muted;
            // 단계 숫자(Lv 2/5)와, 진행 중일 때 얼마나 됐는지 보여 주는 막대.
            levels[i]=Child(card.transform,"Project Level",.34f,.11f,.58f,.22f,22); levels[i].alignment=TextAlignmentOptions.MidlineRight;
            workTracks[i]=TabletKitStyle.Surface("Work Track",card.transform,.08f,.745f,.84f,.028f,TabletKitStyle.Line);
            workFills[i]=TabletKitStyle.Surface("Work Fill",workTracks[i].transform,0,0,1,1,TabletKitStyle.Accent);
            workTracks[i].raycastTarget=workFills[i].raycastTarget=false;
            cardLines[i]=TabletKitStyle.Surface("Card Line",card.transform,0,.965f,1,.035f,TabletKitStyle.Accent);
        }
        var action=Surface("Project Action",3*CardStep,GridTop+RowStep,CardWidth,CardHeight,TabletKitStyle.Panel).transform;
        effect=Child(action,"Project Effect",.06f,.10f,.88f,.42f,30); effect.textWrappingMode=TextWrappingModes.Normal; effect.alignment=TextAlignmentOptions.Center; effect.color=TabletKitStyle.Accent;
        invest=Button("Invest","투자",3*CardStep+.012f,GridTop+RowStep+.155f,CardWidth-.024f,.09f,()=>
        { CorporateEconomy.Instance.StartProject(stock,(CompanyProject)selected); Set(result,CorporateEconomy.Instance.GrowthResult); Refresh(); });
        // 등급 막대: 다음 등급까지 얼마나 왔는지를 글자 대신 막대로 보여 준다.
        var track=Surface("Rank Track",.70f,.128f,.30f,.006f,TabletKitStyle.Line);
        var fill=Surface("Rank Fill",0,0,1,1,TabletKitStyle.Accent); fill.transform.SetParent(track.transform,false); Place((RectTransform)fill.transform,0,0,1,1); progressFill=fill;
        Surface("IPO Divider",.69f,.33f,.0015f,.54f,TabletKitStyle.Line);
        goalHeading=Label("IPO Heading","",.72f,.33f,.28f,.06f,33);
        for (int i=0;i<4;i++)
        {
            // 조건 하나: 표시(됨/아직), 이름, 오른쪽에 숫자, 밑에 얼마나 찼는지 막대.
            float y=.415f+i*.092f;
            checks[i]=TabletKitStyle.Glyph("check",transform,.72f,y,.03f,.045f);
            criteria[i]=Label("IPO Criterion "+i,"",.765f,y,.13f,.045f,25);
            criterionValues[i]=Label("IPO Criterion Value "+i,"",.87f,y,.13f,.045f,22); criterionValues[i].alignment=TextAlignmentOptions.MidlineRight; TradingOrderView.Numeric(criterionValues[i]);
            var meter=Surface("IPO Criterion Track "+i,.765f,y+.052f,.235f,.010f,TabletKitStyle.Line);
            criterionFills[i]=TabletKitStyle.Surface("IPO Criterion Fill "+i,meter.transform,0,0,1,1,Ready);
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
        int level0=growth.Rank(rules); long nextValue=growth.NextRankValue(rules);
        Lit(rank,nextValue>0 ? T(CorporateEconomy.RankName(level0))+"   →   "+T(CorporateEconomy.RankName(level0+1)) : T(CorporateEconomy.RankName(level0)));
        long reached=level0>0 ? rules.rankValues[level0-1] : 0;
        float climbed=nextValue>0 ? Mathf.Clamp01((float)((growth.Appraisal-reached)/Math.Max(1m,nextValue-reached))) : 1f;
        ((RectTransform)progressFill.transform).anchorMax=new Vector2(climbed,1);
        Set(state,stock.IsListed ? "상장" : growth.RequestedDay>0 ? "상장 심사 대기" : "비상장");
        Set(valuation,Money(growth.Appraisal)); Set(cash,Money(data.Cash));
        Set(earnings,data.CloseFinished ? Money(data.Revenue) : "—");
        Set(profit,data.CloseFinished ? Money(data.Profit) : "—"); profit.color=InvestmentDashboard.ProfitColor(data.Profit);
        var businessRules=economy.Rules;
        double dividend=data.Policy==CompanyPolicy.Dividend ? businessRules.dividendPayout : data.Policy==CompanyPolicy.Investment ? businessRules.investmentPayout : businessRules.reservePayout;
        double reinvest=data.Policy==CompanyPolicy.Investment ? businessRules.growthInvestment : data.Policy==CompanyPolicy.Dividend ? businessRules.dividendInvestment : 0;
        Set(allocation,"배당 "+dividend.ToString("0%")+"  ·  재투자 "+reinvest.ToString("0%")+"  ·  비축 "+(1-dividend-reinvest).ToString("0%"));
        float cutA=Mathf.Clamp01((float)dividend), cutB=Mathf.Clamp01((float)(dividend+reinvest));
        splitParts[0].rectTransform.anchorMin=new Vector2(0,0); splitParts[0].rectTransform.anchorMax=new Vector2(cutA,1);
        splitParts[1].rectTransform.anchorMin=new Vector2(cutA,0); splitParts[1].rectTransform.anchorMax=new Vector2(cutB,1);
        splitParts[2].rectTransform.anchorMin=new Vector2(cutB,0); splitParts[2].rectTransform.anchorMax=new Vector2(1,1);
        var best=economy.Recommend(stock,out string why); string code=GameLocale.Code;
        string[] policyWords=code=="ja" ? new[]{"配当","再投資","内部留保"} : code=="en" ? new[]{"Dividend","Reinvest","Reserves"} : new[]{"배당","재투자","비축"};
        Lit(advice,(code=="ja" ? "おすすめ · " : code=="en" ? "Suggested · " : "추천 · ")+policyWords[(int)best]+"  —  "+why);
        for (int i=0;i<3;i++) { policies[i].interactable=manage; TabletKitStyle.Flat(policies[i].image,(int)data.Policy==i ? TabletKitStyle.Hover : TabletKitStyle.Panel); policies[i].GetComponentInChildren<TMP_Text>().color=(int)data.Policy==i ? TabletKitStyle.Accent : Muted; }
        int today=MarketDayController.Instance.Day; double nowMinute=MarketDayController.Instance.AbsoluteGameMinute;
        for (int i=0;i<ProjectCount;i++)
        {
            var type=(CompanyProject)i; int level=growth.Level(type); bool running=growth.Running(type);
            Lit(projectTitles[i],ProjectLabel(type));
            // 투자는 끝나는 날의 결산(17:00)에 마무리된다. 시작한 날 09:00부터 그때까지를 진행 구간으로 본다.
            int finishDay=growth.Project==i ? growth.CompleteDay : growth.SecondCompleteDay;
            double finishAt=(finishDay-1)*1440d+1020d, startAt=(finishDay-rules.projectDays-1)*1440d+540d;
            float worked=running ? Mathf.Clamp01((float)((nowMinute-startAt)/Math.Max(1d,finishAt-startAt))) : 0f;
            if (workTracks[i].gameObject.activeSelf!=running) { workTracks[i].gameObject.SetActive(running); }
            if (running) { workFills[i].rectTransform.anchorMax=new Vector2(worked,1f); }
            Lit(levels[i],"Lv "+level+" / "+rules.maximumLevel); levels[i].color=level>=rules.maximumLevel ? TabletKitStyle.Accent : level>0 ? new Color32(230,235,239,255) : Muted;
            Lit(costs[i],running ? string.Format(T("{0}% · {1} 남음"),Mathf.FloorToInt(worked*100f),Span(finishAt-nowMinute)) : level>=rules.maximumLevel ? T("최대 단계") : Money(growth.ProjectCost(data,type,rules)));
            // 지금 바로 시작할 수 있는(돈이 되고 자리가 빈) 투자는 값이 초록으로 켜진다.
            bool startable=manage && !running && level<rules.maximumLevel && growth.CanStart(data,type,today,rules) && data.Cash>=growth.ProjectCost(data,type,rules);
            costs[i].color=running ? TabletKitStyle.Accent : startable ? Ready : Muted;
            // 단계 점: 채운 점이 지금 단계, 흐린 점이 진행 중인 다음 단계.
            for (int step=0;step<MaxPips;step++)
            {
                pips[i,step].gameObject.SetActive(step<rules.maximumLevel);
                pips[i,step].color=step<level ? TabletKitStyle.Accent : running && step==level ? new Color(TabletKitStyle.Accent.r,TabletKitStyle.Accent.g,TabletKitStyle.Accent.b,.4f) : TabletKitStyle.Line;
            }
            TabletKitStyle.Flat(projects[i].image,i==selected ? TabletKitStyle.Hover : TabletKitStyle.Panel);
            cardLines[i].enabled=i==selected;
        }
        var chosen=(CompanyProject)selected; int chosenLevel=growth.Level(chosen), target=Math.Min(chosenLevel+1,rules.maximumLevel);
        // 다음 단계가 되면 어떻게 달라지는지 한 줄.
        string next=selected switch
        {
            0 => string.Format(T("생산 ×{0}"),(1+target*rules.capacityGain).ToString("0.00")),
            1 => string.Format(T("수요 +{0}%p"),(target*rules.salesGain*100).ToString("0")),
            2 => string.Format(T("변동비 −{0}%"),(target*rules.researchSaving*100).ToString("0")),
            3 => string.Format(T("불황 타격 −{0}%"),(Math.Min(1,target*rules.brandCushion)*100).ToString("0")),
            4 => string.Format(T("고정비 −{0}%p"),(target*rules.automationSaving*100).ToString("0.#")),
            5 => string.Format(T("해외 매출 +{0}%"),(target*rules.exportShare*100).ToString("0")),
            _ => string.Format(T("평가 배수 {0}배"),(rules.earningsMultiple+target*rules.qualityMultiple).ToString("0.#"))
        };
        Lit(effect,growth.Running(chosen) ? T("진행 중")+" · "+GameLocale.Day(growth.Project==selected ? growth.CompleteDay : growth.SecondCompleteDay) : chosenLevel>=rules.maximumLevel ? T("최대 단계") : next);
        invest.interactable=manage && growth.CanStart(data,chosen,today,rules) && data.Cash>=growth.ProjectCost(data,chosen,rules);
        TabletCall.Set(invest,invest.interactable);
        // 상장한 뒤에는 같은 자리에 최종 목표(엔딩 조건) 두 가지를 보여 준다.
        bool goal=stock.IsListed && stock==economy.PlayerCompany;
        Lit(goalHeading,!goal ? T("상장 준비") : growth.EndingDay>0 ? string.Format(T("목표 달성 · {0}"),GameLocale.Day(growth.EndingDay)) : T("최종 목표"));
        goalHeading.color=goal && growth.EndingDay>0 ? TabletKitStyle.Accent : new Color32(230,235,239,255);
        for (int i=0;i<4;i++) { bool shown=!goal || i<2; criteria[i].gameObject.SetActive(shown); checks[i].gameObject.SetActive(shown); criterionValues[i].gameObject.SetActive(shown); criterionFills[i].transform.parent.gameObject.SetActive(shown); }
        listing.gameObject.SetActive(!goal);
        if (goal)
        {
            decimal cap=stock.currentPrice*stock.issuedShares;
            Lit(criteria[0],T("사업 확장")); Lit(criterionValues[0],Math.Min(growth.CompletedProjects,rules.endingProjects)+" / "+rules.endingProjects);
            Lit(criteria[1],T("마감 시가총액")); Lit(criterionValues[1],Money(rules.endingMarketCap));
            Fill(0,growth.CompletedProjects/(float)Math.Max(1,rules.endingProjects)); Fill(1,(float)(cap/Math.Max(1m,rules.endingMarketCap)));
            criteria[0].color=growth.CompletedProjects>=rules.endingProjects ? Ready : Muted;
            criteria[1].color=growth.EndingDay>0 || cap>=rules.endingMarketCap ? Ready : Muted;
            for (int i=0;i<2;i++) { checks[i].sprite=TabletKitStyle.Icon(criteria[i].color==Ready ? "check" : "minus"); checks[i].color=criteria[i].color; criterionValues[i].color=criteria[i].color; }
            fund.interactable=amount.interactable=false;
            return;
        }
        Set(criteria[0],"상장 평가액"); Lit(criterionValues[0],Money(rules.listingValue));
        Set(criteria[1],"흑자 결산"); Lit(criterionValues[1],growth.ProfitableCloses+" / "+rules.listingProfitableCloses);
        Set(criteria[2],"사업 확장"); Lit(criterionValues[2],growth.CompletedProjects+" / "+rules.listingProjects);
        Set(criteria[3],"미지급 비용"); Lit(criterionValues[3],Money(data.Debt));
        Fill(0,(float)(growth.Appraisal/Math.Max(1m,rules.listingValue))); Fill(1,growth.ProfitableCloses/(float)Math.Max(1,rules.listingProfitableCloses));
        Fill(2,growth.CompletedProjects/(float)Math.Max(1,rules.listingProjects)); Fill(3,data.Debt==0 ? 1f : 0f);
        criteria[0].color=growth.Appraisal>=rules.listingValue ? Ready : Muted;
        criteria[1].color=growth.ProfitableCloses>=rules.listingProfitableCloses ? Ready : Muted;
        criteria[2].color=growth.CompletedProjects>=rules.listingProjects ? Ready : Muted;
        criteria[3].color=data.Debt==0 ? Ready : Muted;
        for (int i=0;i<4;i++) { checks[i].sprite=TabletKitStyle.Icon(criteria[i].color==Ready ? "check" : "minus"); checks[i].color=criteria[i].color; criterionValues[i].color=criteria[i].color; }
        fund.interactable=amount.interactable=manage && !stock.IsListed && owner.Account.GetHolding(stock)==stock.issuedShares;
        listing.interactable=manage && !stock.IsListed && growth.RequestedDay==0 && growth.Eligible(data,rules);
        TabletCall.Set(listing,listing.interactable);
    }
    void Update()
    {
        if (Time.unscaledTime<next)
        { return; }
        next=Time.unscaledTime+.5f; Refresh();
    }
    private static readonly Color Muted=new Color32(146,161,172,255), Ready=new Color32(98,191,176,255);
    private void Fill(int index,float amount) { criterionFills[index].rectTransform.anchorMax=new Vector2(Mathf.Clamp01(amount),1f); }
    private static string Money(decimal value) => DeviceTerminalUI.Money(value);
    private static void Set(TMP_Text text,string value) { DeviceTerminalUI.Set(text as TextMeshProUGUI,value); }
    private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
    {
        var go=new GameObject("Text :: "+name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
        var text=go.GetComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=size; text.enableAutoSizing=false; text.color=new Color32(230,235,239,255);
        text.alignment=TextAlignmentOptions.MidlineLeft; text.textWrappingMode=TextWrappingModes.NoWrap; text.raycastTarget=false; Set(text,value); return text;
    }
    /// <summary>카드나 칸 안에 넣는 글자.</summary>
    private TextMeshProUGUI Child(Transform parent,string name,float x,float y,float w,float h,float size)
    {
        var text=Label(name,"",0,0,1,1,size); text.transform.SetParent(parent,false); Place(text.rectTransform,x,y,w,h);
        text.overflowMode=TextOverflowModes.Ellipsis; return text;
    }
    /// <summary>이미 그 언어로 정한 글자를 넣는다.</summary>
    private static void Lit(TextMeshProUGUI label,string value)
    { if (!GameLocalizedText.Matches(label,value)) { GameLocalizedText.Set(label,value,true); } }
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
