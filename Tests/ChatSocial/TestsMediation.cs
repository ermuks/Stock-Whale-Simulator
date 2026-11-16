using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static object InvokeSociety(InvestorSociety society, string method, params object[] arguments) =>
        typeof(InvestorSociety).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(society, arguments);
    private static void SpeechPersonality(TradeBot bot, bool serious, int polite, int temper, int sociability, int bravado)
    {
        var saved = JsonNode.Parse(JsonUtility.ToJson(bot.Social.Psychology.CaptureSocial()));
        saved["traits"]["serious"] = serious; saved["traits"]["politeness"] = polite; saved["traits"]["temper"] = temper;
        saved["traits"]["sociability"] = sociability; saved["traits"]["bravado"] = bravado;
        bot.Social.Psychology.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(saved.ToJsonString()));
    }
    private static void MediationScenarios()
    {
        ChatSocialAssets.Reload();
        var original = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var templates = ChatSocialAssets.Templates;
        var fixture = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original));
        void Setting(string key, double value) { fixture.values.Single(v => v.key == key).value = value; }
        Setting("thirdPartyChance", 1); Setting("mediationExitBaseChance", 1); Setting("mediationExitMaxChance", 1);
        Setting("mediationExitDelayMinSec", 8); Setting("mediationExitDelayMaxSec", 8);
        Setting("mediationTauntChance", 1); Setting("departureReactionChance", 1);
        Setting("mediationStage3Scale", 1); Setting("communityPlayfulChance", 1); Setting("communitySeriousChance", 0);
        var folder = Path.Combine(root, "Validation/ChatVoice"); Directory.CreateDirectory(folder);
        try
        {
            RunCase(true); RunCase(false);
            Setting("mediationBase", 0); ChatSocialAssets.Install(fixture, acts, templates);
            var publicFixture = CreateFight(9021, 0); var s = publicFixture.society; var a = publicFixture.a; var b = publicFixture.b; var c = publicFixture.c;
            a.Social.Stress = 100; b.Social.Stress = 10;
            var message = new SocialMessage { id = 81001, senderId = c.Social.Id, roomId = 0, act = SocialAct.MEDIATE, threadId = publicFixture.thread.id };
            int leaves = 0, returns = 0;
            s.SocialSystemNotice += (room, text, id, kind) => { if (room == 0 && kind == ChatRoomNotice.Leave) { leaves++; } if (room == 0 && kind == ChatRoomNotice.Join) { returns++; } };
            InvokeSociety(s, "Mediate", c, message, publicFixture.thread);
            s.SocialTime.Set(1, 610, a.Social.Psychology.MediationExitAt + 1);
            InvokeSociety(s, "TickMediationReaction", a);
            Check(!s.Present(a, 0) && a.isActiveAndEnabled && leaves == 1, "Public stress exit pauses lounge participation while the investor keeps trading");
            double returnAt = a.Social.Psychology.PublicReturnAt;
            s.SocialTime.Set(1, returnAt, s.SocialTime.Seconds + 70); InvokeSociety(s, "TickMediationReaction", a);
            Check(s.Present(a, 0) && returns == 1, "Public return resumes visibility once and emits a centered join notice");
            foreach (var kind in new[] { ChatRoomNotice.Join, ChatRoomNotice.Leave, ChatRoomNotice.Kick })
            {
                var notice = new BotChatMessage(1, null, 1, 600, "참여자 알림", "SYSTEM.ROOM", ChatEvent.Agreement, notice: kind);
                Check(notice.IsRoomNotice && notice.BotId == 0 && notice.Notice == kind, "Typed " + kind + " notice is distinguishable from ordinary bubble content");
            }
            var bulletin = new BotChatMessage(2, null, 1, 600, "시장 안내", "bulletin", ChatEvent.RiskChanged);
            Check(!bulletin.IsRoomNotice, "Ordinary market news is not mistaken for an entry/exit notice");
        }
        finally { ChatSocialAssets.Install(original, acts, templates); }

        void RunCase(bool accepted)
        {
            Setting("mediationBase", accepted ? 1 : 0); ChatSocialAssets.Install(fixture, acts, templates);
            var data = CreateFight(73129, 1); var society = data.society; var a = data.a; var b = data.b; var c = data.c; var thread = data.thread;
            var trace = new List<string>();
            society.SocialMessagePublished += m => trace.Add(society.SocialBots[m.senderId].Social.Nickname + ": " + m.text);
            int departures = 0;
            society.SocialDeparted += (bot, room, reason, kind) =>
            {
                if (kind != ChatRoomNotice.None) { trace.Add("── " + bot.Social.Nickname + " 님이 퇴장했습니다. ──"); }
                departures++;
            };
            a.Social.Stress = accepted ? 20 : 100; b.Social.Stress = 10;
            var source = society.CaptureFacts(a, StockManager.Instance.stocks[1]);
            SocialMessage Emit(TradeBot author, SocialAct act, int target, long reply = 0)
            {
                SocialMessage published = null;
                void Record(SocialMessage message) { if (message.senderId == author.Social.Id) { published = message; } }
                society.SocialMessagePublished += Record;
                author.Social.Psychology.Intentions.Clear();
                InvokeSociety(society, "GenerateSocialMessage", author, new SocialIntent { roomId = 1, motive = SocialMotive.REPLY_OBLIGATION, forcedAct = act, targetId = target,
                    threadId = thread.id, replyToId = reply, facts = source });
                society.SocialMessagePublished -= Record; return published;
            }
            var opening = Emit(a, SocialAct.INSULT, b.Social.Id);
            var answer = Emit(b, SocialAct.MOCK, a.Social.Id, opening.id);
            // Give this eligible, actually invited third participant a turn through the same production pipeline.
            c.isActiveAndEnabled = true;
            c.Social.Psychology.Relation(a.Social.Id).affinity = 70;
            InvokeSociety(society, "InviteThirdParty", thread, answer, new List<TradeBot> { c });
            Check(c.Social.Psychology.Intentions.Any(i => i.forcedAct == SocialAct.MEDIATE), "Calm sociable witness chooses mediation even when a friend is involved");
            double angerA = a.Social.Psychology.Relation(b.Social.Id).anger, angerB = b.Social.Psychology.Relation(a.Social.Id).anger;
            var mediated = Emit(c, SocialAct.MEDIATE, a.Social.Id);
            Check(mediated != null, "Mediator publishes an actual generated sentence in the ongoing argument");
            if (accepted)
            {
                Check(a.Social.Psychology.Relation(b.Social.Id).anger < angerA && b.Social.Psychology.Relation(a.Social.Id).anger < angerB && thread.endReason == SocialEnd.RESOLVED, "Accepted mediation reduces both directional anger gauges and resolves the argument");
                Check(a.Social.Psychology.MediationExitAt < 0 && b.Social.Psychology.MediationExitAt < 0 && departures == 0, "Low-stress participants do not exit merely because someone mediates");
                var late = society.SocialBots[4]; late.isActiveAndEnabled = true;
                SpeechPersonality(late, true, 80, 10, 100, 10);
                InvokeSociety(society, "InviteThirdParty", thread, mediated, new List<TradeBot> { late });
                Check(late.Social.Psychology.ActiveThread == 0 && !late.Social.Psychology.Intentions.Any(i => i.threadId == thread.id), "Resolved mediation cannot trap a late observer in a closed thread");
                trace.Add("[검증 상태] 양쪽 분노 하락 · 언쟁 종료");
            }
            else
            {
                Check(thread.Active && a.Social.Psychology.MediationExitAt > society.SocialTime.Seconds && b.Social.Psychology.MediationExitAt < 0, "Rejected mediation keeps the conversation open and schedules only the overloaded investor to leave");
                var refusal = Emit(a, SocialAct.REBUT, c.Social.Id, mediated.id);
                b.Social.Psychology.Intentions.Clear();
                Private<Dictionary<int, long>>(society, "pendingReplies").Remove(b.Social.Id);
                InvokeSociety(society, "ReactToRejectedMediation", refusal, thread, new List<TradeBot> { b });
                var taunt = b.Social.Psychology.Intentions.FirstOrDefault(i => i.forcedAct == SocialAct.MOCK);
                Check(taunt != null && taunt.targetId == a.Social.Id && taunt.replyToId == refusal.id, "An angry, boastful opponent who heard the refusal may taunt its author");
                if (taunt != null) { InvokeSociety(society, "GenerateSocialMessage", b, taunt); }
                double deadline = a.Social.Psychology.MediationExitAt;
                society.SocialTime.Set(1, 605, deadline - .1); InvokeSociety(society, "TickMediationReaction", a);
                Check(a.Social.Psychology.Room == 1 && departures == 0, "Stress departure leaves a brief window for refusal and a reply");
                society.SocialTime.Set(1, 606, deadline + .1); InvokeSociety(society, "TickMediationReaction", a);
                Check(a.Social.Psychology.Room == 0 && departures == 1 && !thread.Active && !a.Social.Psychology.Intentions.Any(i => i.roomId == 1), "Stress exit removes membership and stale room intentions through the existing departure hook");
                Check(society.VoluntaryExits == 1, "Stress-based private-room exit counts in R5 voluntary departures");
                var reaction = b.Social.Psychology.Intentions.FirstOrDefault(i => i.facts?.notice == "OPPONENT_LEFT");
                Check(reaction != null && reaction.facts.departedName == a.Social.Nickname, "Opponent reacts to the actual departure using the departed nickname");
                if (reaction != null) { InvokeSociety(society, "GenerateSocialMessage", b, reaction); }
                int before = departures; InvokeSociety(society, "TickMediationReaction", a);
                Check(departures == before && a.Social.Psychology.RejoinAt[1] > society.SocialTime.GameMinute, "Departure is emitted once and respects the rejoin cooldown");
                b.Social.Psychology.MediationExitAt = society.SocialTime.Seconds;
                b.Social.Psychology.MediationExitThread = thread.id; b.Social.Psychology.MediationExitRoom = 1;
                InvokeSociety(society, "TickMediationReaction", b);
                Check(b.Social.Psychology.Room == 1 && departures == before, "One argument cannot trigger cascading stress departures of both opponents");
            }
            string path = Path.Combine(folder, accepted ? "mediation-accepted.txt" : "mediation-rejected.txt");
            File.WriteAllLines(path, trace);
        }
    }
    private static (InvestorSociety society, TradeBot a, TradeBot b, TradeBot c, SocialThread thread) CreateFight(int seed, int room)
    {
        var replay = SocialReplay(seed, 8, steps: 0); var society = replay.Society;
        var a = society.SocialBots[1]; var b = society.SocialBots[2]; var c = society.SocialBots[3];
        foreach (var bot in society.SocialBots.Values) { bot.isActiveAndEnabled = false; bot.Social.Psychology.Room = room; bot.Social.Psychology.Intentions.Clear(); }
        a.isActiveAndEnabled = b.isActiveAndEnabled = true;
        SpeechPersonality(a, false, 10, 70, 50, 40); SpeechPersonality(b, false, 10, 70, 50, 90); SpeechPersonality(c, true, 90, 10, 100, 10);
        // Choose a common time in the opponent's real activity window; no market speed changes.
        int activeAt = Enumerable.Range(0, 180).First(t => b.Social.Psychology.ActiveAt(t) && b.Social.Psychology.ActiveAt(t + 12));
        society.SocialTime.Set(1, 600, activeAt);
        var thread = new SocialThread { id = 9001, roomId = room, kind = SocialThreadKind.ARGUMENT, maxTurns = 8, symbol = "S1" };
        thread.participants.Add(a.Social.Id); thread.participants.Add(b.Social.Id);
        if (room == 0) { thread.participants.Add(c.Social.Id); c.isActiveAndEnabled = true; }
        Private<Dictionary<long, SocialThread>>(society, "socialThreads").Add(thread.id, thread); society.SocialRoom(room).threads.Add(thread);
        a.Social.Psychology.ActiveThread = b.Social.Psychology.ActiveThread = thread.id;
        a.Social.Psychology.Relation(b.Social.Id).anger = 80; b.Social.Psychology.Relation(a.Social.Id).anger = 80;
        return (society, a, b, c, thread);
    }
}
