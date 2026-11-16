using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public double MediationExitAt = -1, PublicReturnAt = -1;
    public int MediationExitRoom, MediationExitOpponent;
    public long MediationExitCause, MediationExitThread;
}
public sealed partial class InvestorSociety
{
    private void ConsiderMediationExit(TradeBot member, TradeBot mediator, SocialMessage message, SocialThread thread)
    {
        var mind = member.Social.Psychology;
        if (mind.MediationExitAt >= 0 || !Present(member, message.roomId) || member.Social.Stress < Config.Get("mediationStressMin") ||
            message.roomId > 0 && GetRoom(message.roomId)?.OwnerBotId == member.Social.Id)
        { return; }
        double pressure = Math.Max(0, Math.Min(1, (member.Social.Stress - Config.Get("mediationStressMin")) / (100 - Config.Get("mediationStressMin"))));
        double chance = Config.Get("mediationExitBaseChance") + pressure * (Config.Get("mediationExitMaxChance") - Config.Get("mediationExitBaseChance"));
        if (mind.SocialRng.Next() >= chance)
        { return; }
        int opponent = 0; double anger = -1;
        foreach (int id in thread.participants)
        {
            if (id == member.Social.Id || id == mediator.Social.Id)
            { continue; }
            if (mind.Relation(id).anger > anger) { opponent = id; anger = mind.Relation(id).anger; }
        }
        mind.MediationExitAt = SocialTime.Seconds + Config.Get("mediationExitDelayMinSec") + mind.SocialRng.Next() * (Config.Get("mediationExitDelayMaxSec") - Config.Get("mediationExitDelayMinSec"));
        mind.MediationExitRoom = message.roomId; mind.MediationExitOpponent = opponent; mind.MediationExitCause = message.id; mind.MediationExitThread = thread.id;
        ChatSocialAudit.Journal.Event(new SocialEvent { kind = "MEDIATION_OVERLOAD", id = message.id, room = message.roomId, threadId = thread.id, actor = member.Social.Id, target = mediator.Social.Id,
            time = SocialTime.GameMinute, value = member.Social.Stress, reason = "stress-exit-pending" });
    }
    private void TickMediationReaction(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        if (mind.PublicReturnAt >= 0 && SocialTime.GameMinute >= mind.PublicReturnAt)
        {
            mind.PublicReturnAt = -1;
            SocialSystemNotice?.Invoke(0, bot.Social.Nickname + " 님이 입장했습니다.", ChatSocialIds.Next(), ChatRoomNotice.Join);
        }
        if (mind.MediationExitAt < 0 || SocialTime.Seconds < mind.MediationExitAt)
        { return; }
        int room = mind.MediationExitRoom; long cause = mind.MediationExitCause;
        mind.MediationExitAt = -1;
        if (!Present(bot, room) || mind.MediationExitThread > 0 && exitedFight.Contains(mind.MediationExitThread))
        { return; }
        if (mind.MediationExitThread > 0) { exitedFight.Add(mind.MediationExitThread); }
        CloseSocialThread(FindSocialThread(mind.MediationExitThread), SocialEnd.EXIT);
        mind.Intentions.RemoveAll(intent => intent.roomId == room);
        if (room == 0)
        {
            mind.PublicReturnAt = SocialTime.GameMinute + Config.Get("mediationRejoinMinutes");
            SocialSystemNotice?.Invoke(room, bot.Social.Nickname + " 님이 퇴장했습니다.", cause, ChatRoomNotice.Leave);
        }
        else
        {
            mind.RejoinAt[room] = SocialTime.GameMinute + Config.Get("mediationRejoinMinutes");
            SocialDeparted?.Invoke(bot, room, "중재를 듣고 스트레스로 대화 중단", ChatRoomNotice.Leave);
            GetRoom(room)?.Members.Remove(mind.SocialId); mind.Room = 0;
            mind.ExitAt = -1; mind.ExitIsSympathy = false;
            if (room <= PlayerRoomCount) { VoluntaryExits++; }
        }
        ChatSocialAudit.Journal.Event(new SocialEvent { kind = "MEDIATION_STRESS_EXIT", id = cause, room = room, threadId = mind.MediationExitThread, actor = mind.SocialId,
            target = mind.MediationExitOpponent, time = SocialTime.GameMinute, value = bot.Social.Stress, reason = "overwhelmed-by-mediation" });
        OfferDepartureReaction(bot, room, mind.MediationExitOpponent, cause);
    }
    private void ReactToRejectedMediation(SocialMessage message, SocialThread thread, List<TradeBot> witnesses)
    {
        if (thread == null || !thread.Active || thread.kind != SocialThreadKind.ARGUMENT || !Attack(message.act) ||
            !socialMessageIndex.TryGetValue(message.replyToId, out var parent) || parent.act != SocialAct.MEDIATE)
        { return; }
        foreach (var observer in witnesses)
        {
            var mind = observer.Social.Psychology;
            if (observer.Social.Id == message.targetId || observer.Social.Id == message.senderId || !thread.participants.Contains(mind.SocialId) ||
                mind.Traits.Bravado < Config.Get("mediationTauntBravadoMin") || mind.Relation(message.senderId).anger < Config.Get("mediationTauntAngerMin") ||
                pendingReplies.ContainsKey(mind.SocialId) || mind.Intentions.Exists(intent => intent.threadId == thread.id))
            { continue; }
            if (mind.SocialRng.Next() >= Config.Get("mediationTauntChance"))
            { continue; }
            mind.Intentions.Add(new SocialIntent { motive = SocialMotive.WITNESSED_EVENT, forcedAct = SocialAct.MOCK, roomId = message.roomId, threadId = thread.id,
                replyToId = message.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
            break;
        }
    }
    private void OfferDepartureReaction(TradeBot departed, int room, int opponentId, long cause)
    {
        if (!SocialBots.TryGetValue(opponentId, out var opponent) || !Present(opponent, room) || !opponent.Social.Psychology.ActiveAt((float)SocialTime.Seconds))
        { return; }
        var mind = opponent.Social.Psychology;
        if (mind.Traits.Bravado < Config.Get("mediationTauntBravadoMin") || mind.Relation(departed.Social.Id).anger < Config.Get("mediationTauntAngerMin") ||
            DepartureReactionRoll(opponentId, cause) >= Config.Get("departureReactionChance"))
        { return; }
        Offer(opponent, room, SocialMotive.WITNESSED_EVENT, new SocialFacts { notice = "OPPONENT_LEFT", departedName = departed.Social.Nickname, eventId = cause }, SocialAct.REACT, cause: cause);
    }
    private static double DepartureReactionRoll(int botId, long eventId)
    {
        // An optional event reaction must not shift every later decision when the bot stays silent.
        uint seed = unchecked((uint)eventId ^ (uint)(eventId >> 32) ^ (uint)botId * 486187739u ^ 0x4a7e613bu);
        seed ^= seed >> 16; seed = unchecked(seed * 0x7feb352du); seed ^= seed >> 15; seed = unchecked(seed * 0x846ca68bu); seed ^= seed >> 16;
        return new SocialRandom(unchecked((int)seed)).Next();
    }
}
