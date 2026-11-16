using System;
using System.Globalization;

public enum ChatProfitCardKind
{
    Holding,
    RealizedGain,
    RealizedLoss
}

/// <summary>
/// 채팅 메시지에 붙는 손익 인증 카드. 메시지를 올리는 순간 그 봇의 실제 계좌·체결 기록에서 찍어 고정한다.
/// 올린 뒤에는 바뀌지 않으며 메시지와 함께 저장된다. 말은 과장할 수 있어도 카드는 계좌 숫자다.
/// </summary>
[Serializable]
public sealed class ChatProfitCard
{
    public ChatProfitCardKind kind;
    public string symbol, stockName;
    /// <summary>price: 보유 중이면 찍은 순간의 현재가, 실현이면 평균 매도가. quantity가 0이면 가격·수량 줄을 숨긴다.</summary>
    public decimal amount, percent, averageCost, price, quantity;
    public double capturedAt;
    /// <summary>실현 카드에서, 판 뒤에도 그 종목을 아직 들고 있으면 true(일부 익절·일부 손절).</summary>
    public bool partial;
}

public sealed partial class InvestorSociety
{
    /// <summary>
    /// 손익을 말하는 메시지에만 카드를 붙인다. 수익은 자주, 손실은 가끔 공개한다(허세가 낮은 봇이 더 잘 올린다).
    /// 읽기만 하며 난수열을 소비하지 않는다. 같은 메시지는 언제나 같은 결과가 나온다.
    /// </summary>
    private ChatProfitCard CaptureProfitCard(TradeBot bot, SocialMessage message)
    {
        var facts = message.facts;
        // 방금 한 매도를 말하는 메시지인지(문장도 매도를 말하는 문장이어야 한다. "평가손익"을 말하는 문장에 실현 카드를 붙이지 않는다). 이 경우 아직 보유 중이어도 카드는 그 매도의 실현 손익이다.
        bool saleTalk = facts != null && (facts.sourceEvent == "ProfitRealized" || facts.sourceEvent == "LossRealized") && message.templateKey != null && (message.templateKey.Contains("ProfitRealized") || message.templateKey.Contains("LossRealized"));
        // 세력·종목 대표·거래소는 자기 지분과 평단을 채팅에 공개하지 않는다. 그 정보는 플레이어가 따로 알아내야 한다.
        if (bot != null && bot.KnowsRealValue)
        { return null; }

        if (bot == null || bot.Social == null || facts == null || string.IsNullOrEmpty(facts.symbol) || facts.profitKind == null && !saleTalk || facts.signal || message.memory)
        { return null; }

        bool spoke = message.act == SocialAct.BRAG || message.act == SocialAct.LAMENT || (message.act == SocialAct.PRAISE_PLAYER || message.act == SocialAct.WARN_PLAYER) && facts.directLead;
        if (!spoke && message.localizedValues != null)
        {
            foreach (var value in message.localizedValues)
            {
                if (value != null && (value.name == "amount" || value.name == "pct" || value.name == "gain" || value.name == "loss" || value.name == "return"))
                {
                    spoke = true;
                    break;
                }
            }
        }

        if (!spoke)
        { return null; }

        var stock = FindSocialStock(facts.symbol);
        var card = new ChatProfitCard { symbol = facts.symbol, stockName = stock != null ? stock.stockName : facts.stockName ?? facts.symbol, capturedAt = SocialTime.GameMinute };
        var mind = bot.Social.Psychology;
        if (facts.directLead)
        {
            // 리딩방 지시로 실현한 손익: 기록에 수량과 주당 가격이 없다. 손익금과 수익률만 싣는다.
            if (!decimal.TryParse(facts.amount, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out card.amount) ||
                !decimal.TryParse(facts.pct, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out card.percent))
            { return null; }

            card.kind = card.amount >= 0m ? ChatProfitCardKind.RealizedGain : ChatProfitCardKind.RealizedLoss;
        }
        else if (saleTalk && stock != null && bot.Social.Positions.TryGetValue(stock, out var sale) && sale.SellVolume >= 1m && sale.SellPrice > 0m)
        {
            // 이번 매도 묶음(최근 몇 분의 체결을 합친 값)의 실현 손익.
            decimal cost = sale.SellPrice * sale.SellVolume - sale.SellProfit;
            if (cost <= 0m)
            { return null; }

            card.quantity = sale.SellVolume;
            card.price = sale.SellPrice;
            card.averageCost = cost / sale.SellVolume;
            card.amount = sale.SellProfit;
            card.percent = card.amount / cost * 100m;
            card.kind = card.amount >= 0m ? ChatProfitCardKind.RealizedGain : ChatProfitCardKind.RealizedLoss;
            card.partial = bot.GetOwnedVolume(stock) >= 1m;
        }
        else if (facts.profitKind == "실현")
        {
            // 나눠 판 체결을 합산한다. 문장은 마지막 체결만 말하더라도 카드는 최근 매도 전체다.
            decimal quantity = 0m, proceeds = 0m, cost = 0m;
            double window = ChatSocialConfig.Current.Get("tradeFactMinutes");
            foreach (var fill in mind.SocialFills)
            {
                if (fill.bought || fill.symbol != facts.symbol || SocialTime.GameMinute - fill.at > window)
                { continue; }

                quantity += fill.quantity;
                proceeds += fill.price * fill.quantity;
                cost += fill.cost * fill.quantity;
            }

            if (quantity < 1m || cost <= 0m)
            { return null; }

            card.quantity = quantity;
            card.price = proceeds / quantity;
            card.averageCost = cost / quantity;
            card.amount = proceeds - cost;
            card.percent = card.amount / cost * 100m;
            card.kind = card.amount >= 0m ? ChatProfitCardKind.RealizedGain : ChatProfitCardKind.RealizedLoss;
        }
        else
        {
            if (stock == null)
            { return null; }

            decimal quantity = bot.GetOwnedVolume(stock), cost = bot.GetOwnedCost(stock);
            if (quantity < 1m || cost <= 0m)
            { return null; }

            card.kind = ChatProfitCardKind.Holding;
            card.quantity = quantity;
            card.price = stock.currentPrice;
            card.averageCost = cost / quantity;
            card.amount = quantity * stock.currentPrice - cost;
            card.percent = card.amount / cost * 100m;
        }

        if (card.amount == 0m)
        { return null; }

        // 공개 여부: 번 사람은 잘 올리고, 잃은 사람은 가끔만 올린다.
        int bravado = mind.Traits.Bravado;
        bool gain = card.amount > 0m;
        float chance = card.kind == ChatProfitCardKind.Holding
            ? gain ? 55f + bravado * .4f : 12f + (100 - bravado) * .2f
            : gain ? 65f + bravado * .3f : 10f + (100 - bravado) * .18f;
        if (CompanyBusiness.Hash("CARD:" + message.id + ":" + message.senderId) % 100 >= chance)
        { return null; }

        return card;
    }
}
