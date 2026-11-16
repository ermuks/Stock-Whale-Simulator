using System;
using System.Collections.Generic;

public readonly struct SocialTypingStatus
{
    public readonly int Count;
    public readonly string Nickname;
    public SocialTypingStatus(int count, string nickname) { Count = count; Nickname = nickname; }
    public string Text => Count == 0 ? "" : Count == 1 ? Nickname + "님이 채팅 입력 중..." : Count + "명이 입력 중...";
}

public sealed partial class InvestorSociety
{
    private sealed class TypingEntry
    {
        public TradeBot bot;
        public SocialIntent intent;
        public double ready;
    }
    private readonly List<TypingEntry> typing = new();
    public int TypingVersion { get; private set; }
    private bool IsTyping(TradeBot bot)
    {
        foreach (var entry in typing) { if (entry.bot == bot) { return true; } }
        return false;
    }
    public SocialTypingStatus ReadTyping(int room)
    {
        int count = 0; string name = null;
        foreach (var entry in typing)
        {
            if (entry.intent.roomId != room || entry.intent.expires < SocialTime.Seconds || !Present(entry.bot, room)) { continue; }
            count++; name = entry.bot.Social.Nickname;
        }
        return new SocialTypingStatus(count, count == 1 ? name : null);
    }
    private bool BeginTyping(TradeBot bot, SocialIntent intent)
    {
        if (!LiveConversationEnabled) { return false; }
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent)) { return true; }
        if (ReadTyping(intent.roomId).Count >= Config.Int("typingPerRoom"))
        { intent.due = Math.Max(intent.due, SocialTime.Seconds + .25); bot.Social.Psychology.Intentions.Add(intent); return true; }
        var random = PresentationRandom(bot, intent, 0x461b82a7u);
        double duration = Config.Get("typingMinSec") + random.Next() * (Config.Get("typingMaxSec") - Config.Get("typingMinSec"));
        typing.Add(new TypingEntry { bot = bot, intent = intent, ready = SocialTime.Seconds + duration }); TypingVersion++;
        return true;
    }
    private void FlushTyping()
    {
        if (!LiveConversationEnabled || typing.Count == 0) { return; }
        double now = SocialTime.Seconds;
        for (int i = 0; i < typing.Count;)
        {
            var entry = typing[i]; var intent = entry.intent;
            var stock = FindSocialStock(intent.facts?.symbol);
            if (intent.expires < now) { ExpireSocialIntent(entry.bot, intent); }
            if (intent.expires < now || !Present(entry.bot, intent.roomId) || !ValidateSocialIntent(entry.bot, intent) || stock != null && (!stock.IsListed || stock.IsDelisted))
            { typing.RemoveAt(i); TypingVersion++; continue; }
            if (entry.ready > now) { i++; continue; }
            var room = SocialRoom(intent.roomId);
            while (room.sent.Count > 0 && now - room.sent.Peek() >= Config.Get("roomWindowSec")) { room.sent.Dequeue(); }
            if (room.sent.Count >= Config.Int("roomMaxMsgPer10Sec")) { i++; continue; }
            typing.RemoveAt(i); TypingVersion++;
            // Portfolio facts and target availability are checked again at actual publication.
            GenerateSocialMessage(entry.bot, intent);
        }
    }
}
