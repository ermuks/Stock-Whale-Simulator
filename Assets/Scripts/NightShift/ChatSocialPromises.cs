using System;

public enum SocialPromisedAction { None, Buy, Sell }
public sealed partial class InvestorPsychology
{
    public SocialPromisedAction PromisedAction { get; private set; }
    public string PromisedSymbol { get; private set; }
    public long PromiseMessage { get; private set; }
    public void PromiseTrade(SocialPromisedAction action, string symbol, long messageId)
    {
        if (action == SocialPromisedAction.None || string.IsNullOrEmpty(symbol)) { return; }
        PromisedAction = action; PromisedSymbol = symbol; PromiseMessage = messageId;
        ChatSocialAudit.Journal.Event(new SocialEvent { time = ChatSocialAudit.GameMinute, kind = "TRADE_PROMISE", actor = SocialId, id = messageId, reason = action + ":" + symbol });
    }
    public double ConsumePromisedBuyProbability(string symbol, double buyProbability)
    {
        double probability = Math.Max(0, Math.Min(1, buyProbability));
        if (PromisedAction == SocialPromisedAction.None || symbol != PromisedSymbol) { return probability; }
        double weight = ChatSocialConfig.Current.Get("promiseWeight");
        probability = PromisedAction == SocialPromisedAction.Buy ? Math.Min(1, probability * weight) : 1 - Math.Min(1, (1 - probability) * weight);
        ChatSocialAudit.Journal.Event(new SocialEvent { time = ChatSocialAudit.GameMinute, kind = "TRADE_PROMISE_DECISION", actor = SocialId, id = PromiseMessage, value = probability, reason = PromisedAction + ":" + symbol });
        ClearPromise(); return probability;
    }
    public void ClearPromise(string reason = null)
    {
        if (reason != null && PromisedAction != SocialPromisedAction.None)
        { ChatSocialAudit.Journal.Event(new SocialEvent { time = ChatSocialAudit.GameMinute, kind = "TRADE_PROMISE_CANCELED", actor = SocialId, id = PromiseMessage, reason = reason }); }
        PromisedAction = SocialPromisedAction.None; PromisedSymbol = null; PromiseMessage = 0;
    }
}
