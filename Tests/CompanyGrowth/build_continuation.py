"""Continue the real saved 5-day fixture in a fresh bounded Unity process."""
from pathlib import Path
s=Path('Assets/Editor/CompanyGrowthValidation.cs').read_text(encoding='utf-8-sig')
prefix=s[:s.index('    private static CompanyDashboardUI View')].replace('CompanyGrowthValidation','CompanyGrowthContinuationValidation').replace('CompanyGrowthProbe','CompanyGrowthContinuationProbe')
prefix=prefix.replace('    public static void Run()', '    private static int mode, dueFrame;\n    public static void Listing() { mode=1; Run(); }\n    public static void Restore() { mode=2; Run(); }\n    public static void Visual() { mode=3; Run(); }\n    private static void Run()')
prefix=prefix.replace('PlayerPrefs.SetString("DonDon.Language","ko");', 'var original=File.ReadAllLines("Validation/Settings/CompanyGrowth01/language-before.txt"); hadLanguage=original[0]=="1"; language=original.Length>1 ? original[1] : "";\n        PlayerPrefs.SetString("DonDon.Language","ko");')
suffix=s[s.index('    private static void Audit('):].replace('CompanyGrowthValidation','CompanyGrowthContinuationValidation').replace('CompanyGrowthProbe','CompanyGrowthContinuationProbe')
body=r'''    private static CompanyDashboardUI View => tablet.GetComponentsInChildren<CompanyDashboardUI>(true).Single();
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
'''
Path('Assets/Editor/CompanyGrowthContinuationValidation.cs').write_text(prefix+body+suffix,encoding='utf-8')
