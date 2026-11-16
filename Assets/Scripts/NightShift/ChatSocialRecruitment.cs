using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public int InvitedPlayerRooms;
    public readonly Dictionary<int, double> KnownPlayerRooms = new();
}

public sealed partial class InvestorSociety
{
    public bool CanDiscoverPlayerRoom(TradeBot bot, int roomId)
    {
        var room = GetRoom(roomId);
        if (bot?.Social == null || room == null || !room.PlayerOwned || roomId > AvailablePlayerRoomCount)
        { return false; }
        var mind = bot.Social.Psychology;
        if (!room.Social.firstMemberInvited)
        { return false; }
        return (mind.InvitedPlayerRooms & (1 << (roomId - 1))) != 0 ||
            mind.KnownPlayerRooms.TryGetValue(roomId, out double until) && SocialTime.GameMinute < until;
    }
    public bool ObserveRoomReferral(TradeBot witness, TradeBot author, SocialMessage message)
    {
        if (witness?.Social == null || author?.Social == null || witness == author || message == null || message.roomId != 0 ||
            message.senderId != author.Social.Id || message.act != SocialAct.PRAISE_PLAYER || message.facts == null || !message.facts.directLead || !message.facts.profitable)
        { return false; }
        int roomId = author.Social.Psychology.Room;
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || roomId > AvailablePlayerRoomCount || !room.Social.firstMemberInvited || !room.Members.Contains(author.Social.Id))
        { return false; }
        bool actualProfit = false;
        foreach (var experience in author.Social.Psychology.Experiences)
        {
            if (experience.EventId == message.facts.eventId && experience.Room == roomId && experience.CommandId > 0 && experience.ReturnPercent > 0)
            { actualProfit = true; break; }
        }
        if (!actualProfit) { return false; }
        witness.Social.Psychology.KnownPlayerRooms[roomId] = SocialTime.GameMinute + Config.Get("roomReferralMinutes");
        ChatSocialAudit.Journal.Event(new SocialEvent { id = message.id, time = SocialTime.GameMinute, actor = witness.Social.Id, target = author.Social.Id,
            room = roomId, kind = "ROOM_REFERRAL_HEARD", reason = "회원의 실제 수익 후기 목격" });
        return true;
    }
    public int InvitedRoomMask
    {
        get
        {
            int mask = 0;
            for (int i = 1; i <= AvailablePlayerRoomCount; i++) { if (GetRoom(i).Social.firstMemberInvited) { mask |= 1 << (i - 1); } }
            return mask;
        }
    }
    public void RestoreInvitedRooms(int mask)
    {
        for (int i = 1; i <= PlayerRoomCount; i++) { GetRoom(i).Social.firstMemberInvited = i <= AvailablePlayerRoomCount && (mask & (1 << (i - 1))) != 0; }
    }
}
