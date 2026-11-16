using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void NewsSocietyScenarios()
    {
        var catalog = JsonUtility.FromJson<NewsWritingCatalog>(File.ReadAllText(Path.Combine(root,"Assets/Resources/News/news_writing.json")));
        Check(catalog.narratives.Length == 9 && catalog.companies.Length >= 8,"nine news subjects and explicit fictional company links load from production data");
        Check(NewsWriting.Classify(catalog,"첫 대형 수주 계약 체결 - 실적 기대감 상승")=="contracts" && NewsWriting.Classify(catalog,"유동성 우려 - 자금 조달 실패")=="finance","classification follows the headline's main event before secondary consequences");
        var bodies = Enumerable.Range(1,80).Select(id => NewsWriting.Compose(catalog,id,"NM","노보손해보험","신규 계약 체결",.5,"09:00",0)).ToArray();
        Check(bodies.Select(a => a.body).Distinct().Count() >= 30 && bodies.All(a => !a.body.Contains("{")),"same headline has varied contextual bodies without unresolved placeholders");
        Check(bodies[0].body == NewsWriting.Compose(catalog,1,"NM","노보손해보험","신규 계약 체결",.5,"09:00",0).body,"article generation is deterministic and independent of market RNG");
        foreach (var category in catalog.narratives)
        {
            string headline = NewsWriting.RumorTitle(catalog,"NM","노보손해보험",category.key,false,4);
            var preview = NewsWriting.Compose(catalog,0,"NM","노보손해보험",headline,-.5,"미리보기",0,true,category.key,4);
            var published = NewsWriting.Compose(catalog,900,"NM","노보손해보험",headline,-.5,"10:00",60,true,category.key,4);
            Check(preview.body == published.body && preview.title == published.title && preview.credibility < .5,"preview remains the exact published draft: " + category.key);
        }
        var rumor = NewsWriting.Compose(catalog,101,"NM","노보손해보험","계약설",.5,"10:00",0,true,"contracts");
        var correction = NewsWriting.Compose(catalog,102,"NM","노보손해보험","계약설 근거 확인 안돼",-.5,"10:02",120,false,"contracts",0,101,true);
        Check(correction.storyId == rumor.id && correction.credibility > rumor.credibility && correction.verification == "후속 확인","correction retains the original story and stronger public evidence");
        Check(NewsWriting.Matches(rumor,"노보","contracts","NM",true,true,true) && !NewsWriting.Matches(rumor,"노보","contracts","DDP",false,true,false),"combined search, category, selected stock, holdings and unconfirmed filters use actual article fields");
        Check(!NewsWriting.Matches(rumor,"","all",null,true,false,false) && !NewsWriting.Matches(correction,"","all",null,false,false,true),"empty holdings and corrected news are excluded by their respective filters");

        var fan = new CompanyOpinionBook(73129,1); var skeptical = new CompanyOpinionBook(73129,2);
        fan.Read("NM").affinity = 80; skeptical.Read("NM").affinity = 0;
        fan.Read("NM").skepticism = skeptical.Read("NM").skepticism = 20;
        fan.Observe("NM",1,-1,.32,false,.7,true,0); skeptical.Observe("NM",1,-1,.32,false,.7,true,0);
        Check(fan.Read("NM").impulse > 0 && skeptical.Read("NM").impulse < 0 && fan.Stance("NM",-1,false) == "DEFEND","fans can defend a company against an unsubstantiated negative story while neutral readers worry");
        double affinity = fan.Read("NM").affinity;
        for (int id = 2; id <= 8; id++) { fan.Observe("NM",id,-1,.95,id==8,.7,true,id); }
        Check(fan.Read("NM").affinity < affinity - 20 && fan.Read("NM").disappointments == 7 && fan.Read("NM").skepticism == 28,"repeated reliable bad news erodes attachment; corrections also raise skepticism");
        affinity = fan.Read("NM").affinity;
        Check(!fan.Observe("NM",8,-1,.95,true,.7,true,9) && fan.Read("NM").affinity == affinity && !fan.Hear("NM",8,-1,9),"one article cannot be counted again through reading or chat");
        var listener = new CompanyOpinionBook(73129,3); affinity = listener.Read("NM").affinity;
        Check(listener.Hear("NM",8,-1,9) && listener.Read("NM").affinity == affinity-1 && !listener.Hear("NM",8,-1,10),"hearsay is weaker than firsthand evidence and repeated hearsay is bounded");
        Check(listener.Read("DDP").lastNews == 0 && listener.TradeScale("UNSEEN",true,10) == 1,"unobserved companies receive no news impulse");
        double active = Math.Abs(skeptical.TradeScale("NM",true,0)-1);
        Check(Math.Abs(skeptical.TradeScale("NM",true,900)-1) < active && fan.TradeScale("NM",false,8) <= 1.35 && fan.TradeScale("NM",true,8) >= .65,"news trading bias decays and order sizing stays bounded");
        var whole = new CompanyOpinionBook(42,5); var split = new CompanyOpinionBook(42,5);
        affinity = whole.Read("NM").affinity;
        whole.Realized("NM",500,1000,10);
        for (int i=0; i<10; i++) { split.Realized("NM",50,100,10+i); }
        Check(whole.Read("NM").affinity == split.Read("NM").affinity && whole.Read("NM").affinity > affinity,"actual realized profits improve attachment without multiplying split-fill rewards");
        whole.Realized("NM",-500,1000,70);
        Check(whole.Read("NM").affinity == affinity,"actual losses can reverse earlier favorable experience");
        whole.Realized("NM",5,double.NaN,71);
        Check(CompanyOpinionBook.Valid(whole.Capture()) && !whole.Observe("NM",9,double.NaN,1,false,.5,false,2),"invalid price math cannot poison company memory");
        var snapshot = fan.Capture(); var restored = new CompanyOpinionBook(7,7);
        Check(restored.Restore(snapshot) && !restored.Observe("NM",8,-1,.9,false,.5,false,9),"company memory and seen-event protection survive snapshot restore");
        snapshot[0].affinity = 99; snapshot[0].seen.Clear();
        Check(restored.Read("NM").affinity != 99 && restored.Read("NM").seen.Count > 0,"restored opinion data is not aliased to a mutable save object");
        Check(restored.Restore(null) && restored.Capture().Length == 0,"old saves without company opinions remain valid");
        var population = Enumerable.Range(1,200).Select(id => new CompanyOpinionBook(73129,id)).ToArray();
        for (int i=1;i<=120;i++)
        {
            foreach (var book in population)
            { if (NewsWriting.Hash(i+":"+book.Read("NM").conviction) % 4 == 0) { book.Observe("NM",i,i%2==0?1:-1,.7,false,.5,false,i*30); } }
        }
        Check(population.All(b => CompanyOpinionBook.Valid(b.Capture())) && population.Max(b=>b.Read("NM").affinity)-population.Min(b=>b.Read("NM").affinity)>30,"one-hour mixed evidence model preserves diverse bounded opinions");

        var clock = new NewsPublicationClock();
        Check(clock.TryConsume(100) && !clock.TryConsume(100) && !clock.TryConsume(699.999) && clock.TryConsume(700),"publication opens exactly after 600 real seconds, never on repeated submit");
        Check(!clock.TryConsume(double.NaN) && clock.Remaining(701)==599,"invalid timestamps cannot consume or clear a cooldown");
        Check(clock.Restore(500,120,10) && clock.Remaining(10)==380 && clock.Remaining(15)==375,"restored cooldown subtracts offline time and advances by real seconds");
        Check(!clock.Restore(601,0,10) && !clock.Restore(50,0,double.NaN) && clock.Restore(100,200,10) && clock.TryConsume(10),"invalid cooldown saves are rejected; elapsed offline cooldown is ready");

        ChatSocialAssets.Reload();
        Check(ChatSocialAssets.Templates.templates.Count(t => t.key.StartsWith("company-news.")) == 216,"company news dialogue covers six stances, styles and intensities");
        var fixture = SocialReplay(73129,8,steps:1); var society = fixture.Society;
        var stock = StockManager.Instance.stocks[0]; var bot = society.SocialBots[1];
        bot.Social.Psychology.Companies.Read(stock.symbol).affinity = 80;
        bot.Social.Psychology.Companies.Observe(stock.symbol,701,-1,.32,false,.8,true,10);
        Time.time=10; society.SocialTime.Set(1,550,10);
        var intent = new SocialIntent { roomId=1,motive=SocialMotive.SELF_EVENT,forcedAct=SocialAct.REACT,
            facts=new SocialFacts { symbol=stock.symbol,notice="COMPANY_NEWS_DEFEND",companyNewsId=701,newsObserver=bot.Social.Id,newsHeadline="신규 계약설",companySentiment=-1,eventId=800001 } };
        SocialMessage message=null; society.SocialMessagePublished += item => message=item;
        var generate=typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.Instance|BindingFlags.NonPublic);
        generate.Invoke(society,new object[]{bot,intent});
        Check(message != null && message.templateKey.StartsWith("company-news.") && !message.text.Contains("{"),"actual social pipeline emits a company-specific stance with guarded slots");
        message=null; bot.Social.Psychology.Companies.Observe(stock.symbol,702,-1,.95,true,.8,true,11);
        generate.Invoke(society,new object[]{bot,intent});
        Check(message==null,"superseded article reactions are dropped during typing instead of contradicting newer evidence");
    }
}
