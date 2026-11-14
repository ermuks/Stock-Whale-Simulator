using System;
using System.Collections.Generic;

public sealed partial class InvestorSociety
{
    private double nextCacheTrim;
    public int CachedThreadCount => socialThreads.Count;
    public int CachedExposureCount => threadExposure.Count;
    // These are working caches, not the durable audit log or an investor's salient memories.
    // Never discard a thread referenced by a visible message, queued reply, or pending departure.
    public void TrimSocialCaches(bool force = false)
    {
        if (!force && SocialTime.Seconds < nextCacheTrim)
        { return; }
        nextCacheTrim = SocialTime.Seconds + Config.Get("cacheTrimSeconds");
        var messages = new HashSet<long>(); var threads = new HashSet<long>(); var events = new HashSet<long>();
        for (int roomId = 0; roomId <= MaximumRoomId; roomId++)
        {
            var room = SocialRoom(roomId);
            if (room == null) { continue; }
            foreach (var message in room.recent)
            { messages.Add(message.id); threads.Add(message.threadId); events.Add(message.id); events.Add(message.facts?.eventId ?? 0); }
        }
        foreach (var wait in replyWaits.Values) { messages.Add(wait.message.id); threads.Add(wait.message.threadId); }
        foreach (var bot in socialBots)
        {
            var mind = bot.Social.Psychology;
            if (mind.ExitAt >= 0) { threads.Add(mind.ExitThread); }
            foreach (var intent in mind.Intentions) { threads.Add(intent.threadId); messages.Add(intent.replyToId); events.Add(intent.facts?.eventId ?? 0); }
        }
        int excess = ClosedThreads.Count - Config.Int("closedThreadCache");
        var removeThreads = new HashSet<long>();
        for (int i = 0; i < ClosedThreads.Count && excess > 0; i++)
        {
            var thread = ClosedThreads[i];
            if (threads.Contains(thread.id) || SocialTime.Seconds - thread.lastAt < Config.Get("cacheGraceSeconds")) { continue; }
            removeThreads.Add(thread.id); excess--;
        }
        ClosedThreads.RemoveAll(t => removeThreads.Contains(t.id));
        foreach (long id in removeThreads) { socialThreads.Remove(id); fightWitnesses.Remove(id); exitedFight.Remove(id); }
        for (int roomId = 0; roomId <= MaximumRoomId; roomId++)
        { SocialRoom(roomId)?.threads.RemoveAll(t => removeThreads.Contains(t.id)); }
        foreach (var bot in socialBots) { bot.Social.Psychology.SawUnmoderatedFight.ExceptWith(removeThreads); }
        var exposureKeys = new List<(long, int, string)>();
        foreach (var pair in threadExposure)
        {
            long id = pair.Key.Item1;
            if (id > 0 ? removeThreads.Contains(id) : !messages.Contains(-id)) { exposureKeys.Add(pair.Key); }
        }
        foreach (var key in exposureKeys) { threadExposure.Remove(key); }
        var pairs = new List<(int, int)>();
        foreach (var pair in pairCooldown) { if (pair.Value <= SocialTime.Seconds) { pairs.Add(pair.Key); } }
        foreach (var pair in pairs) { pairCooldown.Remove(pair); }
        pairs.Clear();
        foreach (var pair in warnings) { if (SocialTime.GameMinute - pair.Value.at >= Config.Get("warningRepeatMinutes")) { pairs.Add(pair.Key); } }
        foreach (var pair in pairs) { warnings.Remove(pair); }
        // Sympathy reservations already live on each departing bot; expired source messages cannot gain new witnesses.
        var departures = new HashSet<long>();
        foreach (var pair in departureCaps) { if (!events.Contains(pair.Key)) { departures.Add(pair.Key); } }
        foreach (long id in departures) { departureCaps.Remove(id); departureTimes.Remove(id); }
        sympathyConsidered.RemoveWhere(pair => departures.Contains(pair.Item1));
    }
}
