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
