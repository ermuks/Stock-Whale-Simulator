using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>실제 플레이어 체결로 얻는 첫 운영 권한. 시세·봇 심리·계좌를 직접 변경하지 않는다.</summary>
public sealed class PlayerProgression : MonoBehaviour
{
    [Serializable]
    public sealed class Rules
    {
        public int schemaVersion = 1;
        public long leadingTurnover = 100000, leadingRealizedProfit = 5000;
    }
    [Serializable]
    public sealed class Snapshot
    {
        public int version = 1;
        public string worldId, turnover, realizedProfit;
        public bool roomsOpened;
    }
    public static PlayerProgression Instance { get; private set; }
    public Rules rules = new();
    public decimal Turnover { get; private set; }
    public decimal RealizedProfit { get; private set; }
    public bool CanOperateRooms { get; private set; }
    public bool Eligible => Turnover >= rules.leadingTurnover && RealizedProfit >= rules.leadingRealizedProfit;
    public string WorldId { get; private set; }
    public string LockReason => "운영 권한 필요 · 실제 거래대금 " + Turnover.ToString("N0") + "/" + rules.leadingTurnover.ToString("N0") + "원, 실현손익 " + RealizedProfit.ToString("N0") + "/" + rules.leadingRealizedProfit.ToString("N0") + "원";
    private readonly HashSet<Stock> watched = new();
    void Awake()
    {
        Instance = this; WorldId = Guid.NewGuid().ToString("N");
        var data = Resources.Load<TextAsset>("Progression/PlayerProgression");
        if (data != null)
        {
            var candidate = JsonUtility.FromJson<Rules>(data.text);
            if (candidate != null && candidate.schemaVersion == 1 && candidate.leadingTurnover > 0 && candidate.leadingRealizedProfit > 0)
            { rules = candidate; }
        }
    }
    void OnEnable()
    { StockManager.OnStockListed += Register; StockManager.OnStockDelisted += Remove; }
    void Start()
    {
        if (StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks)
        { Register(stock); }
    }
    private void Register(Stock stock)
    {
        if (stock != null && watched.Add(stock))
        { stock.OnMatchedTrade += Trade; }
    }
    private void Remove(Stock stock)
    {
        if (watched.Remove(stock))
        { stock.OnMatchedTrade -= Trade; }
    }
    private void Trade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool playerBuy = PlayerAccounts.IsPlayer(buyer), playerSell = PlayerAccounts.IsPlayer(seller);
        if (playerBuy == playerSell || resting == null || volume <= 0m)
        { return; }
        Turnover += price * volume;
        if (playerSell && seller is Trader player)
        {
            decimal basis = ReferenceEquals(resting.owner, player) ? resting.costBasis : player.SubmittedSellCost;
            RealizedProfit += (price - basis) * volume;
        }
    }
    public bool OpenLeadingRooms()
    {
        if (CanOperateRooms)
        { return true; }
        if (!Eligible)
        { return false; }
        CanOperateRooms = true;
        MarketEventFeed.Instance?.Notify("리딩방 운영을 시작했습니다. 참가자는 실제 수익과 경험으로 당신을 판단합니다.");
        return true;
    }
    public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms });
    }
    public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        return true;
    }
    void OnDisable()
    {
        StockManager.OnStockListed -= Register; StockManager.OnStockDelisted -= Remove;
        foreach (var stock in watched)
        { stock.OnMatchedTrade -= Trade; }
        watched.Clear();
    }
    void OnDestroy()
    {
        if (Instance == this)
        { Instance = null; }
    }
}
