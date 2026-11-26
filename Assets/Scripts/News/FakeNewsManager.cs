using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Fictional in-game rumors use the same news/order market. Player publication requires tablet 2.</summary>
public class FakeNewsManager : MonoBehaviour
{
    public static FakeNewsManager Instance { get; private set; }
    public float minSecondsAfterManipulation = 30f;
    public bool oneNewsPerManipulation = true;
    [Range(0, 1)] public float followupChance = .65f;
    [Min(30)] public float followupDelaySeconds = 120f;
    private readonly Dictionary<Stock, (float time, bool published)> manipulations = new();
    private readonly List<(Stock stock, NewsArticle article, float due)> followups = new();
    private readonly NewsPublicationClock playerClock = new();
    private float nextCheck;
    // 플레이어가 낮에 기사를 낸 날. 하루에 한 번만 낼 수 있다(밤에 맡기는 기사는 따로다).
    private int playerNewsDay = -1;
    /// <summary>오늘 낮의 한 번을 이미 썼는지.</summary>
    public bool PlayerUsedToday => MarketDayController.Instance != null && playerNewsDay == MarketDayController.Instance.Day;
    /// <summary>방금 돌려준 사유가 이미 지금 언어로 적힌 글인지(번역 표를 거치지 않아야 한다).</summary>
    public bool ReasonWritten { get; private set; }
    public double PlayerCooldown => playerClock.Remaining(Time.realtimeSinceStartupAsDouble);
    public bool PlayerUnlocked => PlayerAccounts.Instance != null &&
        PlayerAccounts.Instance.CanUse(PlayerAccounts.Instance.primary);
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    void OnEnable() { StockManager.OnStockDelisted += OnStockDelisted; }
    void OnDisable() { StockManager.OnStockDelisted -= OnStockDelisted; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
    void OnStockDelisted(Stock stock)
    {
        if (stock == null) { return; }
        manipulations.Remove(stock); followups.RemoveAll(item => item.stock == stock);
    }
    public void RecordManipulation(Stock stock)
    {
        if (stock == null) { return; }
        manipulations[stock] = (SessionClock.Time, false);
    }
    public bool CanPublish(Stock stock) => stock != null && manipulations.TryGetValue(stock, out var record) &&
        (!oneNewsPerManipulation || !record.published) && SessionClock.Time - record.time >= minSecondsAfterManipulation;
    public bool TryPublishFakeNews(Stock stock, bool isPump)
    {
        if (!CanPublish(stock) || NewsManager.Instance == null || !stock.AcceptsOrders) { return false; }
        var article = PublishRumor(stock, "contracts", isPump, Mathf.FloorToInt(SessionClock.Time));
        if (article == null) { return false; }
        manipulations[stock] = (manipulations[stock].time, true); return true;
    }
    public NewsArticle PreviewPlayer(Stock stock, string category, bool positive, int variant)
    {
        var manager = NewsManager.Instance;
        if (stock == null || manager == null) { return null; }
        string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
        return NewsWriting.Compose(manager.Writing, 0, stock.symbol, stock.stockName, title, positive ? .64 : -.64,
            DeviceMarketJournal.FullStamp(), SessionClock.Time, true, category, variant);
    }
    public bool TryPublishPlayer(Stock stock, string category, bool positive, int variant, out NewsArticle article, out string reason)
    {
        article = null; ReasonWritten = false;
        if (!PlayerUnlocked) { reason = "2번 계좌 해금 후 사용할 수 있습니다."; return false; }
        if (stock == null || !stock.IsListed || !stock.AcceptsOrders || !MarketDayController.CanTrade)
        { reason = "거래 중인 종목을 선택하세요."; return false; }
        int today = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
        if (playerNewsDay == today && today > 0)
        {
            reason = GameLocale.Code == "ja" ? "今日はもう記事を出しました。1日1回までです。" : GameLocale.Code == "en" ? "You already ran a story today. One per day." : "오늘은 이미 기사를 냈습니다. 하루에 한 번만 가능합니다.";
            ReasonWritten = true; return false;
        }
        if (PlayerCooldown > 0) { reason = "다음 발행까지 " + FormatCooldown(PlayerCooldown); return false; }
        if (NewsManager.Instance == null) { reason = "뉴스 연결을 준비하고 있습니다."; return false; }
        article = PublishRumor(stock, category, positive, variant);
        if (article == null) { reason = "기사 발행에 실패했습니다. 다시 확인하세요."; return false; }
        playerClock.TryConsume(Time.realtimeSinceStartupAsDouble); playerNewsDay = today;
        SteamAchievements.Unlock(SteamAchievements.FakeNews);
        reason = "발행 완료 · 재사용 대기 " + FormatCooldown(PlayerCooldown);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = -1,
            kind = "PLAYER_NEWS_PUBLISHED", reason = stock.symbol + " " + category, value = article.sentiment });
        return true;
    }
    /// <summary>밤에 맡겨 둔 기사를 개장 뒤에 낸다. 낮의 소문 기사와 같은 길이라 정정 기사가 따라붙을 수 있다.</summary>
    public NewsArticle PublishPlanted(Stock stock, bool positive)
    {
        if (stock == null || NewsManager.Instance == null || !stock.IsListed || !stock.AcceptsOrders) { return null; }
        var article = PublishRumor(stock, "contracts", positive, (int)(NewsWriting.Hash("PLANT:" + stock.symbol + ":" + Mathf.FloorToInt(SessionClock.Time)) % 1000));
        if (article != null)
        {
            ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = -1,
                kind = "PLAYER_NEWS_PUBLISHED", reason = stock.symbol + " planted", value = article.sentiment });
        }
        return article;
    }
    private NewsArticle PublishRumor(Stock stock, string category, bool positive, int variant)
    {
        var manager = NewsManager.Instance;
        string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
        var article = manager.Publish(stock, positive ? 1.08f : .92f, title, fabricated: true, category: category, variant: variant);
        if (article != null && NewsWriting.Hash(article.id + ":followup:" + stock.symbol) % 10000 < followupChance * 10000)
        {
            if (followups.Count >= 16) { followups.RemoveAt(0); }
            followups.Add((stock, article, SessionClock.Time + followupDelaySeconds + NewsWriting.Hash(article.id.ToString()) % 45));
        }
        return article;
    }
    void Update()
    {
        if (SessionClock.Time < nextCheck || !MarketDayController.CanTrade || NewsManager.Instance == null) { return; }
        nextCheck = SessionClock.Time + 1;
        for (int i = followups.Count - 1; i >= 0; i--)
        {
            var item = followups[i];
            if (SessionClock.Time < item.due) { continue; }
            followups.RemoveAt(i);
            if (item.stock == null || item.stock.IsDelisted) { continue; }
            NewsManager.Instance.Publish(item.stock, item.article.sentiment > 0 ? .94f : 1.06f,
                item.stock.stockName + " 관련 " + item.article.categoryLabel + " 주장, 확인 자료 없는 것으로 드러나",
                category: item.article.category, storyId: item.article.storyId, correction: true);
        }
    }
    public bool RestorePlayerCooldown(double remaining, long savedUtcTicks)
    {
        if (savedUtcTicks < 0 || savedUtcTicks > DateTime.MaxValue.Ticks) { return false; }
        double offline = savedUtcTicks == 0 ? 0 : Math.Max(0, (DateTime.UtcNow.Ticks - savedUtcTicks) / (double)TimeSpan.TicksPerSecond);
        return playerClock.Restore(remaining, offline, Time.realtimeSinceStartupAsDouble);
    }
    public static string FormatCooldown(double value)
    {
        return DeviceMarketJournal.Duration(Math.Max(0,value)*DeviceMarketJournal.GameSecondsPerRealSecond/60d);
    }
}
