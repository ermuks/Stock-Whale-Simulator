using System;
using System.Collections.Generic;

public enum SocialModeration { Warn, Mute, Kick }

public sealed partial class InvestorSociety
{
    private sealed class Sanction
    {
        public int room, actor, opponent;
        public long id, thread;
        public double at;
        public SocialModeration action;
        public bool paired, settled;
        public readonly List<int> friends = new();
    }
    private readonly List<Sanction> sanctions = new();
    private readonly Dictionary<(int, int), (double at, int count)> warnings = new();
    private readonly Dictionary<(int, int), double> muteExpirations = new();
    private readonly Dictionary<(int, string), double> roomDecayRemainders = new();
    private readonly List<(int, int)> expiredMutes = new();
    private sealed class RoomReaction
    {
        public long id;
        public double expires;
        public readonly SortedSet<int> witnesses = new();
    }
    private readonly Dictionary<(int, string), RoomReaction> roomReactions = new();
    private readonly List<(int, string)> expiredReactions = new();
    public Func<int, bool> CanModerateRoom;
    public event Action<int, string, long> SocialSystemNotice;

    public bool Moderate(int roomId, int botId, SocialModeration action, out string feedback)
    {
        feedback = "내 리딩방에 참여한 봇만 관리할 수 있습니다.";
        if (roomId < 1 || roomId > PlayerRoomCount || CanModerateRoom != null && !CanModerateRoom(roomId) ||
            !SocialBots.TryGetValue(botId, out var bot) || bot.Social.Psychology.Room != roomId || SocialRoom(roomId).banned.Contains(botId))
        { return false; }
        var mind = bot.Social.Psychology; var room = SocialRoom(roomId);
        double anger = mind.MaximumAnger(out int opponent);
        var thread = FindSocialThread(mind.ActiveThread);
        if (thread == null)
        {
            for (int i = room.threads.Count - 1; i >= 0; i--)
            {
                var recent = room.threads[i];
                if (SocialTime.Seconds - recent.lastAt > Config.Get("replyExpiry")) { continue; }
                if (recent.kind == SocialThreadKind.ARGUMENT && recent.participants.Contains(botId) && recent.endReason != SocialEnd.RESOLVED && recent.endReason != SocialEnd.PLAYER_ACTION)
                { thread = recent; break; }
            }
        }
        var previous = sanctions.FindLast(s => s.room == roomId && s.actor == opponent && s.opponent == botId && !s.paired && SocialTime.Seconds - s.at <= Config.Get("kickBothWindowSec"));
        bool recentAttack = false;
        foreach (var message in room.recent)
        {
            if (SocialTime.Seconds - message.realAt > Config.Get("replyExpiry") || !Attack(message.act)) { continue; }
            if (message.senderId == botId && message.targetId == opponent || message.senderId == opponent && message.targetId == botId)
            { recentAttack = true; break; }
        }
        bool fighting = InvestorPsychology.Stage(anger) >= 2 && (thread != null || mind.ExitAt >= 0 || previous != null || recentAttack);
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, room = roomId, actor = botId, target = opponent, kind = "MODERATION_CONTEXT", value = anger, reason = "fighting=" + fighting + ", thread=" + thread?.id + ", active=" + mind.ActiveThread });
        long id = ChatSocialIds.Next();
        var actionRecord = new Sanction { room = roomId, actor = botId, opponent = fighting ? opponent : 0, id = id, at = SocialTime.Seconds, thread = thread?.id ?? previous?.thread ?? 0, action = action };
        var witnesses = ModerationWitnesses(bot, roomId);
        if (thread != null) { thread.moderated = true; }
        if (action == SocialModeration.Warn)
        {
            warnings.TryGetValue((roomId, botId), out var warning);
            int repeats = warnings.ContainsKey((roomId, botId)) && SocialTime.GameMinute - warning.at < Config.Get("warningRepeatMinutes") ? warning.count + 1 : 0;
            warnings[(roomId, botId)] = (SocialTime.GameMinute, repeats);
            ChangeRelation(bot, opponent, "anger", Config.Get("warningAnger") * Math.Pow(Config.Get("warningRepeatScale"), repeats), "player-warning", id, roomId, false);
            if (mind.Traits.Temper >= Config.Get("warningTemperMin")) { mind.SocialDelta("trust", Config.Get("warningTrust"), "player-warning", id); }
            ChangeSocialRoom(roomId, "discipline", Config.Get("warningDiscipline"), "player-warning", id, witnesses);
            if (mind.SocialRng.Next() < Config.Get("warningResolveChance"))
            {
                CloseSocialThread(thread, SocialEnd.PLAYER_ACTION);
                if (opponent > 0 && SocialBots.TryGetValue(opponent, out var other) && Present(other, roomId))
                { Offer(bot, roomId, SocialMotive.EMOTION_THRESHOLD, forced: SocialAct.APOLOGIZE, target: opponent, cause: id); }
            }
            feedback = "방장님이 " + bot.Social.Nickname + "님에게 경고했습니다.";
        }
        else if (action == SocialModeration.Mute)
        {
            double until = SocialTime.GameMinute + Config.Get("muteMinutes");
            mind.MutedUntil[roomId] = until; muteExpirations[(roomId, botId)] = until;
            mind.Intentions.RemoveAll(i => i.roomId == roomId);
            mind.SocialDelta("trust", Config.Get("muteTrust"), "player-mute", id);
            ChangeSocialRoom(roomId, "discipline", Config.Get("muteDiscipline"), "player-mute", id, witnesses);
            CloseSocialThread(thread, SocialEnd.PLAYER_ACTION);
            feedback = "방장님이 " + bot.Social.Nickname + "님을 " + Config.Int("muteMinutes") + "분간 채팅 금지했습니다.";
        }
        else
        {
            string kind = fighting ? "Fighter" : mind.Betrayal >= Config.Get("kickBetrayalMin") ? "Betrayal" : "Arbitrary";
            ChangeSocialRoom(roomId, "discipline", Config.Get("kick" + kind + "Discipline"), "player-kick-" + kind, id, witnesses);
            if (kind == "Arbitrary") { ChangeSocialRoom(roomId, "chill", Config.Get("kickArbitraryChill"), "arbitrary-kick", id, witnesses); }
            while (room.kicks.Count > 0 && SocialTime.GameMinute - room.kicks.Peek() >= Config.Get("kickRateWindowMinutes")) { room.kicks.Dequeue(); }
            room.kicks.Enqueue(SocialTime.GameMinute);
            if (room.kicks.Count > Config.Int("kickRateLimit")) { ChangeSocialRoom(roomId, "chill", Config.Get("kickRateChill"), "repeated-kicks", id, witnesses); }
            foreach (var witness in witnesses)
            {
                var observer = witness.Social.Psychology;
                bool friend = observer.Relation(botId).affinity >= Config.Get("sympathyAffinity");
                if (friend) { actionRecord.friends.Add(observer.SocialId); }
                double delta = friend ? Config.Get("kickFriendTrust") : kind == "Arbitrary" ? 0 : Config.Get("kick" + kind + "Trust");
                observer.SocialDelta("trust", delta, "witness-player-kick", id);
                observer.Remember(new SocialMemory { type = SocialMemoryKind.SAW_KICK, actorId = -1, targetId = botId, eventId = id, at = SocialTime.GameMinute, salience = Config.Int("memorySawKickSalience") });
            }
            mind.SocialDelta("trust", Config.Get("kickSelfTrust"), "player-kick", id);
            mind.SocialDelta("betrayal", Config.Get("kickSelfBetrayal"), "player-kick", id);
            mind.Remember(new SocialMemory { type = SocialMemoryKind.KICKED, actorId = -1, targetId = botId, eventId = id, at = SocialTime.GameMinute, salience = Config.Int("memoryKickedSalience") });
            int members = 0; foreach (var member in socialBots) { if (member.Social.Psychology.Room == roomId) { members++; } }
            room.banned.Add(botId); CloseSocialThread(thread, SocialEnd.PLAYER_ACTION);
            SocialDeparted?.Invoke(bot, roomId, "방장에 의한 추방"); GetRoom(roomId).Members.Remove(botId); mind.Room = 0; mind.ExitAt = -1; mind.ExitIsSympathy = false; mind.Intentions.Clear();
            departureCaps[id] = (roomId, (int)Math.Floor(members * Config.Get("sympathyCapFraction")), 0);
            ScheduleSympathy(bot, roomId, witnesses, members, id);
            if (mind.SocialRng.Next() < Config.Get("kickWarnBase") + mind.Traits.Grudge / Config.Get("kickWarnGrudgeDivisor")) { SchedulePublicWarning(bot, roomId, id, true); }
            if (previous != null && previous.action == SocialModeration.Kick)
            {
                previous.paired = actionRecord.paired = true;
                ChangeSocialRoom(roomId, "discipline", Config.Get("kickBothDiscipline"), "both-fighters-kicked", id, witnesses);
            }
            feedback = "방장님이 " + bot.Social.Nickname + "님을 내보냈습니다.";
        }
        if (action != SocialModeration.Kick && previous != null && previous.action != SocialModeration.Kick) { previous.paired = actionRecord.paired = true; }
        if (mind.ExitAt >= 0 && mind.Relation(mind.ExitOpponent).anger < Config.Get("exitAngerMin")) { mind.ExitAt = -1; }
        sanctions.Add(actionRecord);
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, id = id, threadId = actionRecord.thread, room = roomId, actor = -1, target = botId, kind = "PLAYER_" + action.ToString().ToUpperInvariant(), reason = feedback });
        SocialSystemNotice?.Invoke(roomId, feedback, id);
        return true;
    }
    private List<TradeBot> ModerationWitnesses(TradeBot target, int roomId)
    {
        var result = new List<TradeBot>();
        foreach (var observer in socialBots)
        {
            var mind = observer.Social.Psychology;
            if (observer == target || !Present(observer, roomId) || !mind.ActiveAt((float)SocialTime.Seconds)) { continue; }
            double chance = WitnessChance?.Invoke(observer) ?? Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + mind.Activity * Config.Get("witnessActivityScale"));
            if (mind.SocialRng.Next() < chance) { result.Add(observer); }
        }
        return result;
    }
    private void ChangeSocialRoom(int roomId, string field, double delta, string cause, long id, List<TradeBot> witnesses = null)
    {
        var room = SocialRoom(roomId);
        double before = field == "discipline" ? room.discipline : room.chill;
        double after = Math.Round(Math.Max(0, Math.Min(100, before + delta)));
        if (field == "discipline") { room.discipline = after; } else { room.chill = after; }
        ChatSocialAudit.Journal.Change(SocialTime.GameMinute, roomId, "room." + field, before, after, cause, id);
        double threshold = Config.Get(field + "ReactThreshold");
        if (before < threshold && after >= threshold && witnesses != null)
        { roomReactions[(roomId, field)] = new RoomReaction { id = id, expires = SocialTime.Seconds + Config.Get("selfExpiry") }; }
        if (witnesses != null && roomReactions.TryGetValue((roomId, field), out var reaction))
        {
            foreach (var witness in witnesses)
            { reaction.witnesses.Add(witness.Social.Id); }
        }
    }
    partial void ApplyModerationReaction(TradeBot bot, SocialMessage message, List<TradeBot> witnesses)
    {
        string field = message.facts.notice == "DISCIPLINE" ? "discipline" : message.facts.notice == "CHILL" ? "chill" : null;
        if (field != null && roomReactions.TryGetValue((message.roomId, field), out var reaction) && reaction.id == message.facts.eventId)
        { roomReactions.Remove((message.roomId, field)); }
    }
    partial void TickSocialModeration()
    {
        expiredReactions.Clear();
        foreach (var pair in roomReactions)
        {
            var reaction = pair.Value;
            if (SocialTime.Seconds > reaction.expires) { expiredReactions.Add(pair.Key); continue; }
            foreach (int id in reaction.witnesses)
            {
                if (!SocialBots.TryGetValue(id, out var witness) || !Present(witness, pair.Key.Item1)) { continue; }
                if (lastAct.TryGetValue(id, out var last) && last == SocialAct.REACT && consecutiveAct.TryGetValue(id, out int count) && count >= Config.Int("maxConsecutiveAct")) { continue; }
                Offer(witness, pair.Key.Item1, SocialMotive.EMOTION_THRESHOLD, new SocialFacts { notice = pair.Key.Item2 == "discipline" ? "DISCIPLINE" : "CHILL", eventId = reaction.id }, SocialAct.REACT, cause: reaction.id);
                break;
            }
        }
        foreach (var key in expiredReactions) { roomReactions.Remove(key); }
        for (int roomId = 1; roomId <= PlayerRoomCount; roomId++)
        {
            if (roomDecayAt.TryGetValue(roomId, out double last))
            {
                foreach (string field in new[] { "discipline", "chill" })
                {
                    var key = (roomId, field); roomDecayRemainders.TryGetValue(key, out double carry);
                    double decay = carry + Math.Max(0, SocialTime.GameMinute - last) / 60d * Config.Get(field + "DecayPerHour");
                    double whole = Math.Floor(decay); roomDecayRemainders[key] = decay - whole;
                    if (whole > 0) { ChangeSocialRoom(roomId, field, -whole, "game-hour-decay", 0); }
                }
            }
            roomDecayAt[roomId] = SocialTime.GameMinute;
        }
        expiredMutes.Clear();
        foreach (var pair in muteExpirations)
        {
            if (SocialTime.GameMinute < pair.Value) { continue; }
            expiredMutes.Add(pair.Key);
            if (!SocialBots.TryGetValue(pair.Key.Item2, out var bot)) { continue; }
            var mind = bot.Social.Psychology; mind.MutedUntil.Remove(pair.Key.Item1);
            if (mind.Room == pair.Key.Item1 && mind.Traits.Grudge >= Config.Get("unmuteGrudge"))
            { Offer(bot, mind.Room, SocialMotive.EMOTION_THRESHOLD, new SocialFacts { notice = "UNMUTED" }, SocialAct.REACT); }
        }
        foreach (var key in expiredMutes) { muteExpirations.Remove(key); }
        foreach (var action in sanctions)
        {
            if (action.settled || SocialTime.Seconds - action.at <= Config.Get("kickBothWindowSec")) { continue; }
            action.settled = true;
            if (action.paired || action.opponent <= 0) { continue; }
            if (action.action == SocialModeration.Kick)
            {
                foreach (int id in action.friends)
                { if (SocialBots.TryGetValue(id, out var friend)) { friend.Social.Psychology.SocialDelta("trust", Config.Get("kickOneSidedFriendTrust") - Config.Get("kickFriendTrust"), "one-sided-kick", action.id); } }
            }
            else if (SocialBots.TryGetValue(action.actor, out var target))
            { target.Social.Psychology.SocialDelta("trust", Config.Get("oneSidedTrust"), "one-sided-sanction", action.id); }
        }
        sanctions.RemoveAll(s => s.settled && SocialTime.Seconds - s.at > Config.Get("pairCooldownSec"));
    }
}
