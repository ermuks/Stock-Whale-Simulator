using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static partial class ChatSocialTests
{
    /// <summary>리딩방 인지·신뢰와, 그에 따라 달라지는 방 얘기.</summary>
    private static void RoomTalkScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        var bots = society.SocialBots.Values.OrderBy(b => b.Social.Id).ToList();
        foreach (var bot in bots) { bot.Social.Psychology.Room = 0; bot.isActiveAndEnabled = true; }
        var previousChance = society.WitnessChance; society.WitnessChance = _ => 1d;
        Check(Math.Abs(InvestorSociety.RoomTalkFreshness(0) - 1) < 1e-9 && Math.Abs(InvestorSociety.RoomTalkFreshness(20) - .5) < 1e-9 && InvestorSociety.RoomTalkFreshness(240) < .001,
            "Room talk: freshness is 1 at once, halves every 20 minutes and approaches 0");

        // 1) 홍보를 바로 본 사람과 4시간 뒤에 본 사람.
        society.SocialTime.Set(1, 600, 1000);
        society.NoteRoomTalk(ChatSocialIds.Next(), 0, 0);
        var early = bots.Take(10).Select(b => society.WitnessRoomTalk(b)).ToList();
        society.SocialTime.Set(1, 840, 1450);
        var late = bots.Skip(10).Select(b => society.WitnessRoomTalk(b)).ToList();
        Console.WriteLine($"Room talk: fresh gains {string.Join(" ", early.Select(g => g.ToString("0.0")))} | 4h-old gains max {late.Max():0.0000}");
        Check(early.All(g => g >= 0 && g <= 60) && early.Max() > 20 && early.Distinct().Count() > 5, "Room talk: a fresh promotion raises awareness by 0 to 60");
        Check(late.All(g => g >= 0 && g < .02), "Room talk: an old promotion raises awareness by almost nothing");
        Check(bots.All(b => b.Social.Psychology.RoomHearsay == 50), "Room talk: the host's own promotion does not move trust");
        Check(bots.Take(10).All(b => society.WitnessRoomTalk(b) == 0), "Room talk: the same message is not counted twice");

        // 2) 남이 한 좋은 말·나쁜 말은 인상을 움직인다.
        society.SocialTime.Set(1, 841, 1452);
        society.NoteRoomTalk(ChatSocialIds.Next(), bots[0].Social.Id, 1); society.WitnessRoomTalk(bots[1]); society.WitnessRoomTalk(bots[2]);
        double praised = bots[2].Social.Psychology.RoomHearsay;
        society.NoteRoomTalk(ChatSocialIds.Next(), bots[0].Social.Id, -1); society.WitnessRoomTalk(bots[2]);
        double speakerBefore = bots[0].Social.Psychology.RoomHearsay; society.WitnessRoomTalk(bots[0]);
        Check(bots[1].Social.Psychology.RoomHearsay > 50 && bots[1].Social.Psychology.RoomHearsay <= 56 && praised > 50 && bots[2].Social.Psychology.RoomHearsay < praised && bots[0].Social.Psychology.RoomHearsay == speakerBefore,
            "Room talk: others' praise raises and complaints lower the impression, never the speaker's own");

        // 3) 화자 유형.
        var probe = bots[3].Social.Psychology;
        string Profile(double awareness, double hearsay, int room, int joined)
        { probe.RoomAwareness = awareness; probe.RoomHearsay = hearsay; probe.Room = room; probe.JoinedPlayerRooms = joined; return InvestorSociety.RoomTalkProfile(probe); }
        Check(Profile(0, 50, 0, 0) == "A0" && Profile(24, 90, 0, 0) == "A0" && Profile(30, 50, 0, 0) == "A10" && Profile(30, 70, 0, 0) == "A1+" && Profile(59, 30, 0, 0) == "A1-" &&
            Profile(60, 50, 0, 0) == "A20" && Profile(90, 80, 0, 0) == "A2+" && Profile(90, 20, 0, 0) == "A2-", "Room talk: awareness and trust decide the outsider's voice");
        // 회원·전 회원은 그 방의 지시로 실제로 벌었는지 잃었는지로 갈린다. 소문으로 쌓인 인상만으로는 번 척도 잃은 척도 하지 않는다.
        Check(Profile(100, 90, 1, 1) == "MEM0" && Profile(100, 10, 0, 1) == "EX0", "Room talk: a member with no results claims neither gains nor losses");
        long fixture = 880000;
        void Result(TradeBot bot, bool gain)
        { fixture++; bot.Social.Psychology.Record(new InvestorExperience(fixture, fixture, 1, "S0", 100000m, gain ? 120000m : 80000m, 0, "room talk fixture"), new InvestorSocietySettings()); }
        Result(bots[3], true);
        bool winner = Profile(100, 10, 1, 1) == "MEM+" && Profile(100, 10, 0, 1) == "EX+";
        Result(bots[3], false); Result(bots[3], false);
        Check(winner && Profile(100, 90, 1, 1) == "MEM-" && Profile(100, 90, 0, 1) == "EX-", "Room talk: members and former members speak from what the room's calls actually did for them");
        // 직접 겪은 신뢰·배신감이 없는 사람으로 잰다.
        var clean = bots[4].Social.Psychology; clean.RoomAwareness = 0; clean.RoomHearsay = 50;
        bool neutral = Math.Abs(InvestorSociety.RoomInterest(clean) - 1f) < 1e-6;
        clean.RoomAwareness = 100; clean.RoomHearsay = 100;
        bool keen = Math.Abs(InvestorSociety.RoomInterest(clean) - 3f) < 1e-6;
        Check(neutral && keen, "Room talk: knowing and trusting the room raises interest in joining (1x unknown and neutral, 3x at most)");

        // 4) 홍보에 달리는 답과 그 뒤로 이어지는 말.
        // 마지막 값은 그 방의 지시로 얻은 결과: +1 벌었음, −1 잃었음, 0 없음.
        var kinds = new (double awareness, double hearsay, int room, int joined, int result)[] { (0, 50, 0, 0, 0), (40, 75, 0, 0, 0), (40, 50, 0, 0, 0), (40, 25, 0, 0, 0), (80, 75, 0, 0, 0), (80, 50, 0, 0, 0), (80, 25, 0, 0, 0),
            (100, 50, 1, 1, 1), (100, 50, 1, 1, 0), (100, 50, 1, 1, -1), (100, 50, 0, 1, 1), (100, 50, 0, 1, 0), (100, 50, 0, 1, -1) };
        // 방 얘기는 반말을 쓰는 사람만 한다. 유형은 말할 수 있는 사람들에게 고루 나눠 준다.
        var casual = bots.Where(b => b != bots[3] && b.Social.Psychology.Traits.Politeness < ChatSocialConfig.Current.Int("styleFormalMin")).ToList();
        Console.WriteLine($"Room talk: {casual.Count} of {bots.Count} bots speak casually");
        for (int i = 0; i < casual.Count; i++)
        {
            var mind = casual[i].Social.Psychology; var kind = kinds[i % kinds.Length];
            mind.RoomAwareness = kind.awareness; mind.RoomHearsay = kind.hearsay; mind.Room = kind.room; mind.JoinedPlayerRooms = kind.joined;
            if (kind.result != 0) { Result(casual[i], kind.result > 0); }
        }
        bots[3].isActiveAndEnabled = false;
        var said = new List<(SocialMessage message, string profile)>();
        Action<SocialMessage> record = m => { if (m.templateKey.StartsWith("ROOMTALK.")) { said.Add((m, InvestorSociety.RoomTalkProfile(society.SocialBots[m.senderId].Social.Psychology))); } };
        society.SocialMessagePublished += record;
        InvestorSociety.RoomTalkEnabled = true;
        var lines = new List<string>(); double clock = 2000;
        for (int round = 0; round < 40; round++)
        {
            long promotion = ChatSocialIds.Next();
            society.SocialTime.Set(1, 850 + round, clock);
            society.BeginRoomPromotionTalk(promotion);
            for (int step = 0; step < 120; step++) { clock += .5; society.SocialTime.Set(1, 850 + round, clock); society.TickRoomTalk(); }
        }
        // 누군가 스스로 꺼내는 말.
        int opened = 0;
        for (int i = 0; i < 30; i++)
        {
            clock += 30; society.SocialTime.Set(1, 900, clock);
            if (society.SpeakRoomTalk("OPEN", 0, 0, 0) != 0) { opened++; }
            for (int step = 0; step < 60; step++) { clock += .5; society.SocialTime.Set(1, 900, clock); society.TickRoomTalk(); }
        }
        InvestorSociety.RoomTalkEnabled = false; society.SocialMessagePublished -= record; society.WitnessChance = previousChance;
        var byId = said.ToDictionary(s => s.message.id, s => s.message);
        int Stance(string key) => key.EndsWith("+") ? 1 : key.EndsWith("-") ? -1 : 0;
        foreach (var item in said) { lines.Add($"{item.message.templateKey}\t{society.SocialBots[item.message.senderId].Social.Nickname}\t{item.message.text}"); }
        Directory.CreateDirectory(Path.Combine(root, "Validation/ChatSocial"));
        File.WriteAllLines(Path.Combine(root, "Validation/ChatSocial/roomtalk.tsv"), lines);
        var keys = said.Select(s => s.message.templateKey).Distinct().ToList();
        Console.WriteLine($"Room talk: {said.Count} messages, {keys.Count} of 64 voices used, {opened} self-started");
        Check(said.Count > 100 && keys.Count >= 55, "Room talk: promotions and openers draw varied replies");
        Check(said.All(s => s.message.templateKey.EndsWith("." + s.profile)), "Room talk: every line comes from the speaker's own awareness and trust");
        Check(said.Where(s => s.message.templateKey.StartsWith("ROOMTALK.PROMO.")).All(s => s.message.targetId == -1 && s.message.text.StartsWith("@방장 ")), "Room talk: replies to a promotion address the host");
        Check(said.Where(s => s.message.templateKey.StartsWith("ROOMTALK.REPLY_")).All(s => byId.TryGetValue(s.message.replyToId, out var parent) && s.message.targetId == parent.senderId && s.message.senderId != parent.senderId &&
            s.message.templateKey.StartsWith(Stance(parent.templateKey) > 0 ? "ROOMTALK.REPLY_POS." : Stance(parent.templateKey) < 0 ? "ROOMTALK.REPLY_NEG." : "ROOMTALK.REPLY_ASK.")), "Room talk: a follow-up answers what was actually said (praise, complaint or question)");
        Check(opened > 0 && said.Where(s => s.message.templateKey.StartsWith("ROOMTALK.OPEN.")).All(s => s.profile != "A0"), "Room talk: nobody brings up a room they have never heard of");
        Check(said.All(s => society.SocialBots[s.message.senderId].Social.Psychology.Traits.Politeness < ChatSocialConfig.Current.Int("styleFormalMin")), "Room talk: casual lines are spoken by casual speakers");
    }
}
