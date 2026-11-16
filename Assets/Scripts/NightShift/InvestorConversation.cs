using UnityEngine;

/// <summary>검증된 개인 원장과 방금 읽은 발언으로 문장을 구성한다. 감정 수치만으로 수익 경험을 지어내지 않는다.</summary>
public static class InvestorConversation
{
    private static readonly System.Collections.Generic.List<(decimal price, decimal volume)> bids = new(1), asks = new(1);
    private static readonly string[] greetings =
    {
        "잠깐 들렀어요. 어떤 얘기 중인가요?", "안녕하세요. 오늘은 먼저 좀 지켜볼게요.",
        "자리 비었다고 해서 들어왔어요.", "혼자 보다가 들어왔습니다. 반갑습니다.",
        "지금 들어왔는데 다들 뭐 보고 있어요?", "얘기 좀 읽으면서 시장 보려고요."
    };
    public static string Greeting(TradeBot bot) => greetings[(bot.Social.Id + bot.Social.Utterances++) % greetings.Length];
    public static string MissedForecast(TradeBot bot, InvestorForecastObservation observation)
    {
        string gain = observation.ReturnPercent.ToString("+0.0") + "%";
        if (bot.Social.Articulation < .3f)
        { return observation.Symbol + " 안 따라갔는데 목표 찍었네… " + gain + " 놓침. 이번 건 인정."; }
        return observation.Symbol + $" {observation.Buy:N0}원 매수 지시는 거절했는데, 장 끝나기 전에 {observation.Sell:N0}원까지 갔네요. {gain} 기회였어요. 직접 번 건 아니지만 다시 보게 됩니다.";
    }
    public static string Testimony(TradeBot bot, InvestorExperience experience, bool reply)
    {
        var social = bot.Social;
        string result = experience.ReturnPercent.ToString("+0.0;-0.0;0") + "%";
        string subject = experience.Symbol + " 지시 이후 " + experience.Reason + " 결과 " + result;
        if (reply)
        {
            if (social.Articulation < .3f)
            { return "난 " + result + "였는데. 같은 경험은 아닌 듯."; }
            return "그 얘기와 제 경험은 달랐어요. " + subject + "였습니다. 사람마다 진입과 체결이 달라요.";
        }
        if (experience.ReturnPercent > 0m)
        {
            if (social.Articulation < .3f)
            { return experience.Symbol + " " + result + " 실현. 이번엔 도움 됐음."; }
            return subject + ". " + experience.Room + "번 방에서 도움을 받았지만 다음에도 같을지는 모르겠습니다.";
        }
        if (social.Articulation < .3f)
        { return experience.Symbol + " " + result + " 실현… 지시 따라갔다가 물렸네."; }
        return subject + ". 지시만 믿으면 안 되겠어요. 체결과 위험은 직접 확인하세요.";
    }
    public static bool ShouldDisagree(InvestorPsychology mind, InvestorMessageContext context, double roll)
    {
        if (!mind.Latest.HasValue || context.Sentiment == 0f || context.Hops > 2)
        { return false; }
        var experience = mind.Latest.Value;
        bool opposite = experience.ReturnPercent > 0m && context.Sentiment < 0f || experience.ReturnPercent < 0m && context.Sentiment > 0f;
        return opposite && roll < Mathf.Clamp01(.15f + Mathf.Abs((float)experience.ReturnPercent) * .018f + mind.Activity * .25f);
    }
    public static string Waiting(TradeBot bot, LeadingRoomOrders.Plan plan, int variant)
    {
        bool active = plan != null && plan.State != LeadingRoomOrders.Stage.Canceled && plan.State != LeadingRoomOrders.Stage.Completed && plan.State != LeadingRoomOrders.Stage.Rejected;
        if (active && plan.Command?.Stock != null && variant % 4 != 0)
        { return MarketRemark(bot,plan.Command.Stock,variant); }
        if (active && plan.Remaining > 0m)
        { return plan.Command.Stock.stockName + " " + plan.Remaining.ToString("N0") + "주 보유. " + plan.Command.Sell.ToString("N0") + "원 매도 조건 기다리는 중."; }
        if (active && plan.State == LeadingRoomOrders.Stage.Deferred)
        { return "아직 결정 못 했어요. 자금이랑 호가부터 좀 볼게요."; }
        if (active && plan.State == LeadingRoomOrders.Stage.AwaitBuy)
        { return plan.Command.Stock.stockName + " " + plan.Command.Buy.ToString("N0") + "원 매수 조건까지 기다릴게요."; }
        bool busy = MarketDayController.Instance != null && MarketDayController.Instance.ConversationFocus.HasActivity(SessionClock.Time);
        if (variant % (busy ? 10 : 4) != 0 && StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
        { return MarketRemark(bot, variant); }
        return BotMind.Compose(bot, ChatEvent.SmallTalk, null, "잠깐 쉬었다 볼까요.");
    }
    private static string MarketRemark(TradeBot bot, int variant)
    {
        var stocks = StockManager.Instance.stocks;
        var focus = MarketDayController.Instance?.ConversationFocus;
        var stock = focus?.Choose((variant & int.MaxValue)/(double)(int.MaxValue+1L),SessionClock.Time) ?? stocks[variant % stocks.Count];
        return MarketRemark(bot,stock,variant);
    }
    public static string MarketRemark(TradeBot bot,Stock stock,int variant)
    {
        decimal shares = bot.GetOwnedVolume(stock), basis = bot.GetOwnedAverage(stock);
        string name = stock.stockName;
        if (shares > 0m && basis > 0m && variant % 5 == 0)
        {
            decimal rate = (stock.currentPrice / basis - 1m) * 100m;
            return variant % 2 == 0 ? name + " 제 평단은 " + basis.ToString("N0") + "원. 평가손익 " + rate.ToString("+0.0;-0.0;0") + "%네요."
                : name + " " + shares.ToString("N0") + "주 들고 있어요. " + (rate < 0m ? "손실 중이라 더 신중하게 보려고요." : "평가이익이 있어도 아직 판 건 아니에요.");
        }
        if (variant % 5 == 1 && bot.Social.Positions.TryGetValue(stock, out var memory) && memory.SellVolume > 0m && SessionClock.Time - memory.SellTime < 180f)
        { return name + " 최근 매도 결과는 " + memory.SellProfit.ToString("+0;-0;0") + "원. " + (memory.SellProfit < 0m ? "좀 쉬면서 생각해 볼게요." : "이번 결과를 다음에도 기대하진 않으려고요."); }
        var focus = MarketDayController.Instance?.ConversationFocus;
        var tape = focus == null ? default : focus.Read(stock,SessionClock.Time);
        if (tape.Volume > 0m)
        {
            string volume = tape.Volume.ToString("N0"), rate = tape.ReturnPercent.ToString("+0.0;-0.0;0.0");
            switch (variant % 6)
            {
                case 0: return name + " 최근 1분 " + rate + "%. " + volume + "주 체결됐네.";
                case 1:
                    if (tape.PreviousVolume > 0m && tape.Volume >= tape.PreviousVolume * 1.4m)
                    { return name + " 거래량이 앞선 1분의 " + (tape.Volume/tape.PreviousVolume).ToString("0.0") + "배. 지금 " + stock.currentPrice.ToString("N0") + "원."; }
                    return name + " 최근 1분에 " + volume + "주. 체결가 " + stock.currentPrice.ToString("N0") + "원 보고 있어요.";
                case 2: return name + " 최근 1분 거래대금 " + tape.Turnover.ToString("N0") + "원. " + (tape.ReturnPercent < 0m ? "체결은 이어지는데 가격은 밀리네요." : tape.ReturnPercent > 0m ? "최근 체결 구간에서 가격도 올라왔네요." : "거래는 있는데 가격은 제자리네요.");
                case 3: return name + " " + stock.currentPrice.ToString("N0") + "원. " + (tape.ReturnPercent < 0m ? "하락 중인데 이 거래량 누가 받아주나." : tape.ReturnPercent > 0m ? "올라오긴 했는데 여기서 더 따라갈까 고민 중." : "위아래 어느 쪽으로 움직일지 보고 있어요.");
            }
        }
        if (variant % 3 != 2)
        {
            stock.orderBook.GetTopBids(bids, 1); stock.orderBook.GetTopAsks(asks, 1);
            if (bids.Count == 0 || asks.Count == 0)
            { return name + " " + (asks.Count == 0 ? "매도" : "매수") + " 호가가 지금 비어 있어요. 급하게 넣긴 어렵겠네요."; }
            return name + " 매수 " + bids[0].price.ToString("N0") + " / 매도 " + asks[0].price.ToString("N0") + "원. 간격 " + (asks[0].price-bids[0].price).ToString("N0") + "원.";
        }
        if (stock.IsTradingHalted)
        { return name + " 거래 멈췄네요. 재개까지 주문은 기다려야겠어요."; }
        return name + " " + stock.currentPrice.ToString("N0") + "원 보고 있어요. " + (bot.Social.Articulation < .3f ? "일단 관망." : "다음 체결이 어느 가격에 나오는지 보려고요.");
    }
}
