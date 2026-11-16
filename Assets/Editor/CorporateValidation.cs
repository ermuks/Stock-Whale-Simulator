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
public static class CorporateValidation
{
    private static string output, language;
    private static bool hadLanguage, oldOptions, spawned, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, checks, errors, locale;
    private static float due;
    private static decimal closingCash, companyCash, closingCap;
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
        if (EditorApplication.timeSinceStartup-began>260) { Finish("Time budget exceeded"); return; }
        if (EditorApplication.isPlaying && !spawned) { spawned=true; new GameObject("Test :: Corporate Probe").AddComponent<CorporateProbe>(); }
    }
    private static void Log(string text,string stack,LogType type)
    { if (type==LogType.Error || type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),text+"\n"+stack+"\n"); } }
    private static void Check(bool success,string message)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(success ? "PASS " : "FAIL ")+message+"\n"); if (!success) { throw new Exception(message); } checks++; }
    private static CompanyDashboardUI View => tablet.GetComponentsInChildren<CompanyDashboardUI>(true).Single();
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime<due)
        { return; }
        try
        {
            var day=MarketDayController.Instance;
            var menu=PauseMenuController.Instance;
            if (phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName==null)
                { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase=1; due=Time.unscaledTime+2; return;
            }
            if (phase==1)
            {
                if (day==null || !day.Ready || day.Phase!=MarketPhase.Trading)
                { return; }
                Time.timeScale=1;
                stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock));
                tablet=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(t=>!t.second);
                foreach (var bot in day.Bots) { bot.SuspendSession(); }
                while (stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                Check(stock.business!=null&&stock.business.ControlInitialized,"opening ownership baseline exists before first player order");
                Check(stock.issuedShares==2000000,"fresh-game entry target has 2,000,000 issued shares");
                Check(!CorporateEconomy.Instance.SetPolicy(stock,CompanyPolicy.Investment),"no management rights without majority");
                var player=PlayerAccounts.Instance.primary; player.cash=stock.currentPrice*stock.issuedShares;
                int count=NewsManager.Instance.Articles.Count(a=>a.category=="corporate");
                player.BuyLimit(1,10,0,stock); CorporateEconomy.Instance.ObserveControl(stock);
                Check(!WorldDramaDirector.Instance.Controls(stock)&&NewsManager.Instance.Articles.Count(a=>a.category=="corporate")==count,"pending orders grant neither control nor acquisition news");
                while (stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                // Test counterparties post their actual shares via the existing matching path; no share minting.
                var sell=typeof(Trader).GetMethod("SellInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach (var account in Trader.Registry.ToArray())
                { if (!PlayerAccounts.IsPlayer(account) && account.GetHolding(stock)>0) { sell.Invoke(account,new object[]{stock,stock.currentPrice,account.GetHolding(stock),0}); } }
                Check(WorldDramaDirector.Instance.AcquireShares(null,stock,player),"acquisition places a finite, cash-backed order in the existing book");
                Check(PlayerAccounts.Instance.Owned(stock)==decimal.Floor(stock.issuedShares*.5m)+1&&WorldDramaDirector.Instance.Controls(stock),"only actual matched majority shares (half of issued + 1) establish control");
                decimal price=stock.currentPrice; CorporateEconomy.Instance.ObserveControl(stock);
                var news=NewsManager.Instance.Articles.First(a=>a.category=="corporate");
                acquisitionId=news.id;
                Check(news.symbol=="NM"&&news.sentiment>0&&news.credibility>=.98&&!news.fabricated&&news.body.Length>100,"confirmed acquisition becomes a substantial bullish article");
                Check(stock.currentPrice==price,"acquisition headline never directly sets market price");
                CorporateEconomy.Instance.ObserveControl(stock);
                Check(NewsManager.Instance.Articles.Count(a=>a.category=="corporate")==count+1,"control polling cannot spam duplicate acquisition news");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"issued shares conserved through takeover fills");
                tablet.ShowPage(TabletAccountUI.Page.Management); View.Refresh();
                View.GetComponentsInChildren<Button>().First(b=>b.name=="Button :: Policy 1").onClick.Invoke();
                Check(stock.business.Policy==CompanyPolicy.Investment,"existing tablet policy button sets persistent investment policy");
                View.GetComponentsInChildren<Button>().First(b=>b.name=="Button :: Policy 0").onClick.Invoke();
                DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                phase=10; due=Time.unscaledTime+8; return;
            }
            if (phase==10)
            {
                int readers=day.Bots.Count(b=>b.Social!=null && b.Social.Psychology.Companies.Read(stock.symbol).seen.Contains(acquisitionId));
                int bullish=day.Bots.Count(b=>b.Social!=null && b.Social.Psychology.Companies.Read(stock.symbol).seen.Contains(acquisitionId) && b.Social.Psychology.Companies.TradeScale(stock.symbol,true,SessionClock.Time)>1);
                Check(readers>0&&bullish>0,"existing news audience reads takeover and raises buy propensity: "+readers+" readers, "+bullish+" bullish");
                Time.timeScale=0; phase=2; due=Time.unscaledTime+.4f; return;
            }
            if (phase==2)
            {
                View.Refresh(); Audit(View.gameObject); Capture("before-first-close",1600,900);
                Check(View.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="—"),"unsettled figures distinguished from zero");
                closingCash=PlayerAccounts.Instance.primary.cash+PlayerAccounts.Instance.automated.cash;
                typeof(MarketDayController).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(day,day.tradingSeconds);
                Time.timeScale=1; phase=3; return;
            }
            if (phase==3)
            {
                if (menu.CurrentPage!=PauseMenuController.Page.Report)
                { return; }
                var data=stock.business; var report=DailyIncomeController.Instance.Ledger.Reports.Last();
                Check(data.LastDay==day.Day&&data.CloseFinished&&data.MarketCap==data.ClosePrice*data.CloseShares,"actual close settles business and snapshots capitalization");
                Check(data.PlayerDividend>0&&report.Dividends==CorporateEconomy.Instance.PlayerDividends(day.Day),"closing report includes shareholder dividends from all accounts");
                Check(PlayerAccounts.Instance.primary.cash+PlayerAccounts.Instance.automated.cash==closingCash+report.Dividends+report.Transfer,"player receives dividends exactly once, separately from base income");
                Check(SessionStore.Exists,"corporate close included in atomic end-of-day checkpoint");
                companyCash=data.Cash; closingCap=data.MarketCap; decimal playerCash=PlayerAccounts.Instance.primary.cash;
                string saved=SessionStore.Read(); data.Cash=1; data.Policy=CompanyPolicy.Reserve; data.MarketCap=1;
                SessionStore.Restore(saved); saved=null; stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                Check(stock.business.Cash==companyCash&&stock.business.MarketCap==closingCap&&stock.business.Policy==CompanyPolicy.Dividend,"real session restore retains treasury, cap and policy");
                var repeat=CorporateEconomy.Instance.Settle(day.Day); while (repeat.MoveNext()) { }
                Check(PlayerAccounts.Instance.primary.cash==playerCash&&stock.business.Cash==companyCash,"restored close cannot pay a second dividend");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"restored shareholder register conserved");
                menu.UI.Report(true); Capture("closing-report",1920,1080); Audit(menu.UI.Root);
                menu.UI.ReportCompanies(); Capture("company-statements",1600,900); Audit(menu.UI.Root);
                menu.ContinueAfterReport(); Time.timeScale=0;
                tablet.ShowPage(TabletAccountUI.Page.Management); DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                phase=4; due=Time.unscaledTime+.6f; return;
            }
            if (phase==4)
            {
                View.Refresh(); Canvas.ForceUpdateCanvases(); Audit(View.gameObject);
                Capture("company-"+GameLocale.Code,1600,900); Capture("company-"+GameLocale.Code,1920,1080);
                Check(GameLocale.MissingSources.Count==0,"all string table entries present: "+GameLocale.Code);
                string article=GameLocale.Text(NewsManager.Instance.Articles.First(a=>a.category=="corporate").body);
                File.WriteAllText(Path.Combine(output,"acquisition-"+GameLocale.Code+".txt"),article);
                if (++locale<languages.Length) { GameLocale.SetLanguage(languages[locale]); due=Time.unscaledTime+.5f; return; }
                decimal closing=stock.business.MarketCap; stock.currentPrice+=100;
                View.Refresh(); Check(stock.business.MarketCap==closing,"live price change leaves displayed last-close cap unchanged");
                phase=5; due=Time.unscaledTime+.2f; return;
            }
            if (phase==5)
            {
                GC.Collect(); LegacyFixture(); GC.Collect(); phase=6; due=Time.unscaledTime+.2f; return;
            }
            if (phase==6)
            {
                SessionStore.Restore(File.ReadAllText(Path.Combine(output,"legacy-session.json")));
                stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                Check(stock.issuedShares==2000000&&stock.business.CloseShares==2000000&&stock.business.MarketCap==closingCap,"pre-business save keeps saved issuance and last closing cap");
                Check(!stock.business.CloseFinished&&stock.business.LastDay==day.Day,"legacy financials marked unavailable without back-paying prior days");
                Check(stock.business.Cash==decimal.Floor(stock.realValue*stock.issuedShares*(decimal)CorporateEconomy.Instance.Rules.initialCashRatio),"legacy business initialized from restored values, not fresh-scene values");
                decimal cash=PlayerAccounts.Instance.primary.cash;
                var again=CorporateEconomy.Instance.Settle(day.Day); while (again.MoveNext()) { }
                Check(cash==PlayerAccounts.Instance.primary.cash,"legacy closing checkpoint cannot gain retroactive dividends");
                Check(errors==0,"no runtime errors"); Finish(null);
            }
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void LegacyFixture()
    {
        var envelope=JObject.Parse(SessionStore.Read()); var data=JObject.Parse((string)envelope["payload"]);
        foreach (JObject node in (JArray)data["nodes"])
        {
            if (node["fields"] is not JObject fields)
            { continue; }
            fields.Remove("Stock|business"); fields.Remove("DailyIncomeLedger+ClosingReport|dividends");
        }
        string payload=data.ToString(Formatting.None); envelope["payload"]=payload;
        using (var hash=SHA256.Create()) { envelope["sha256"]=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))); }
        File.WriteAllText(Path.Combine(output,"legacy-session.json"),envelope.ToString(Formatting.None));
    }
    private static void Audit(GameObject root)
    {
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>())
        { label.ForceMeshUpdate(); if (label.isTextTruncated || label.isTextOverflowing) { throw new Exception("Clipped: "+label.name+" / "+label.text+" / "+label.rectTransform.rect); } }
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
public sealed class CorporateProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { CorporateValidation.Step(); }
}
