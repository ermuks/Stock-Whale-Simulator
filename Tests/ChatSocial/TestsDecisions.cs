using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static InvestorPsychology Psychology(float trust, float betrayal, int id = 1)
    {
        var mind = new InvestorPsychology(id, 73129);
        var snapshot = mind.CaptureSocial(); snapshot.trust = trust; snapshot.betrayal = betrayal; snapshot.room = 1;
        mind.RestoreSocial(snapshot); return mind;
    }
    private static void DecisionScenarios(int stage)
    {
        var amb = Psychology(80, 80); var indifferent = Psychology(10, 10); var loyal = Psychology(80, 10); var hostile = Psychology(10, 80);
        Check(Math.Abs(amb.Acceptance - .6166667) < .00001 && Math.Abs(indifferent.Acceptance - .05) < .00001 && amb.LeadingAmountScale == .5 && indifferent.LeadingAmountScale == .4, "S6 equal net trust has different quadrant acceptance and amount");
        Check(Math.Abs(loyal.Acceptance - .9) < .00001 && hostile.Acceptance == 0 && Psychology(180, 130).Quadrant == InvestorQuadrant.Ambivalent && Math.Abs(Psychology(180, 130).Acceptance - .6833333) < .00001, "actual overheat quadrant with capped formula inputs");
        var hysteresis = Psychology(55, 0); hysteresis.SocialDelta("trust", -5, "test", 0);
        Check(hysteresis.Quadrant == InvestorQuadrant.Loyal, "quadrant hysteresis keeps high at 50");
        hysteresis.SocialDelta("trust", -5, "test", 0);
        Check(hysteresis.Quadrant == InvestorQuadrant.Indifferent, "quadrant hysteresis switches low at 45");
        var settings = new InvestorSocietySettings();
        amb.Record(new InvestorExperience(100001, 1, 1, "NM", 10000, 12000, 0, assetBefore: 10000), settings);
        loyal.Record(new InvestorExperience(100002, 1, 1, "NM", 10000, 12000, 0, assetBefore: 10000), settings);
        Check(amb.Trust == 100 && loyal.Trust == 120 && amb.Betrayal == 60 && amb.Memories.Any(m => m.type == SocialMemoryKind.GAIN_BY_LEAD), "ambivalent realized gain halves trust, relieves betrayal, records significant investment");
        var ambLoss = Psychology(80, 80); var loyalLoss = Psychology(80, 10);
        ambLoss.Record(new InvestorExperience(100003, 1, 1, "NM", 10000, 8000, 0, assetBefore: 10000), settings);
        loyalLoss.Record(new InvestorExperience(100004, 1, 1, "NM", 10000, 8000, 0, assetBefore: 10000), settings);
        Check(Math.Abs((ambLoss.Betrayal - 80) / (loyalLoss.Betrayal - 10) - 1.5) < .00001, "ambivalent direct loss betrayal multiplier 1.5");
        var cappedLoss = Psychology(100, 80); var overheatedLoss = Psychology(180, 80);
        cappedLoss.Record(new InvestorExperience(100005, 1, 1, "NM", 10000, 9900, 0), settings);
        overheatedLoss.Record(new InvestorExperience(100006, 1, 1, "NM", 10000, 9900, 0), settings);
        Check(cappedLoss.Betrayal == overheatedLoss.Betrayal && overheatedLoss.Trust == 180, "D01 direct loss formula caps trust without erasing overheat");

        var fixture = SocialReplay(73129, stage, steps: 1); var society = fixture.Society;
        var stock = StockManager.Instance.stocks[0]; stock.currentPrice = 1000;
        var first = society.SocialBots[1]; var second = society.SocialBots[2];
        first.Social.Psychology.RestoreSocial(Psychology(80, 10, 1).CaptureSocial());
        second.Social.Psychology.RestoreSocial(Psychology(80, 80, 2).CaptureSocial());
        var third = society.SocialBots[3]; third.Social.Psychology.RestoreSocial(Psychology(10, 10, 3).CaptureSocial());
        var weightsMethod = typeof(InvestorSociety).GetMethod("BuildSocialWeights", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var facts = society.LeadingExperienceFacts(new InvestorExperience(200000, 1, 1, stock.symbol, 100, 120, 0));
        var intent = new SocialIntent { roomId = 1, targetId = -1, motive = SocialMotive.SELF_EVENT, facts = facts };
        var ambWeights = (System.Collections.Generic.List<SocialWeight>)weightsMethod.Invoke(society, new object[] { second, intent, facts });
        var indWeights = (System.Collections.Generic.List<SocialWeight>)weightsMethod.Invoke(society, new object[] { third, intent, facts });
        Check(ambWeights.Any(w => w.key == "DOUBT") && indWeights.All(w => w.key != "DOUBT" && w.key != "MOCK" && w.key != "PRAISE_PLAYER" && w.key != "WARN_PLAYER"), "S6 equal net trust produces different actual speech distributions");
        first.Positions.Clear(); second.Positions.Clear(); first.cash = second.cash = 1000000;
        var day = new MarketDayController(); day.InitializeReplay(73129);
        using (var orders = new LeadingRoomOrders(society))
        {
            var command = new LeadingRoomOrders.Command { Id = 200001, Room = 1, Stock = stock, Buy = 1000, Sell = 1400, IssuedDay = 1 };
            society.GetRoom(1).Command = command;
            orders.Consider(first, command, 0, 0); orders.Consider(second, command, 0, 0);
            var a = orders.Get(first); var b = orders.Get(second);
            Check(a.ExitTarget == 1400 && b.ExitTarget >= 1240 && b.ExitTarget <= 1320 && a.Goal > b.Goal && b.StopPrice > a.StopPrice, "S6 actual leading plans differ in target, quantity and stop width");
            orders.Tick(first, 0, 0); orders.Tick(second, 0, 0);
            foreach (var plan in new[] { a, b })
            {
                var order = plan.Pending.Single(); plan.Bot.Positions[stock] = (order.volume, order.price);
                stock.Matched(order.price, order.volume, plan.Bot, new Trader(), order);
                stock.orderBook.CancelOrder(order, stock);
            }
            stock.currentPrice = b.ExitTarget; orders.Tick(first, 4, 0); orders.Tick(second, 4, 0);
            Check(a.State == LeadingRoomOrders.Stage.AwaitSell && b.State == LeadingRoomOrders.Stage.Selling && b.Pending.Single().price == b.ExitTarget, "S6 ambivalent submits earlier actual sell order while loyal still waits");
            var sell = b.Pending.Single(); stock.Matched(sell.price, sell.volume, new Trader(), second, sell);
            Check(b.State == LeadingRoomOrders.Stage.Completed && b.Reported && second.Social.Psychology.Latest?.Cost == b.RealizedCost, "only matching callback settles leading result");
        }
        var fomo = Psychology(0, 0); ChatSocialAudit.GameMinute = 100;
        Check(fomo.TryFomo("NM", 100, 0) && fomo.HasFomo && fomo.FomoUntil == 120 && !fomo.TryFomo("DDP", 119, 0), "FOMO tag starts once with 20 minute expiry");
        fomo.ObserveFomoFill(new SocialTradeFact { symbol = "NM", bought = true, quantity = 5, price = 1000 });
        float fear = fomo.Fear; ChatSocialAudit.GameMinute = 121;
        fomo.ObserveFomoFill(new SocialTradeFact { symbol = "NM", bought = false, quantity = 5, price = 900 });
        Check(!fomo.HasFomo && fomo.Fear == fear + 15 && !fomo.TryFomo("NM", 179, 0) && fomo.TryFomo("NM", 180, 0), "FOMO actual loss after expiry raises fear; 60 minute cooldown retained");
        var encoded = JsonUtility.ToJson(amb.CaptureSocial()); var restored = Psychology(0, 0);
        Check(restored.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(encoded)) && restored.Traits.Temper == amb.Traits.Temper && restored.Memories.Count == amb.Memories.Count && restored.Trust == amb.Trust, "serialized snapshot roundtrip preserves private traits, memories and overheat");
        amb.Relation(4).anger = 70; var isolated = amb.CaptureSocial(); isolated.relations.Single(r => r.to == 4).anger = 1;
        Check(amb.Relation(4).anger == 70, "snapshot relationship rows do not alias live state");
        isolated.greed = float.NaN;
        Check(!amb.RestoreSocial(isolated) && amb.Relation(4).anger == 70, "non-finite snapshot rejected atomically");
        fomo.FomoLots["NM"] = new InvestorPsychology.FomoLot { symbol = "NM", quantity = 7, cost = 7000, realized = -30 };
        fomo.MutedUntil[1] = 220; fomo.RejoinAt[2] = 1440;
        var fomoRestore = Psychology(0, 0); var fomoJson = JsonUtility.ToJson(fomo.CaptureSocial());
        Check(fomoRestore.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(fomoJson)) && fomoRestore.FomoLots["NM"].cost == 7000 && fomoRestore.MutedUntil[1] == 220 && fomoRestore.RejoinAt[2] == 1440, "decimal FOMO lots and room cooldowns survive JSON roundtrip");
        var memoryBot = society.SocialBots[4]; var memoryMind = memoryBot.Social.Psychology;
        memoryMind.Memories.Clear();
        var notice = new SocialFacts { notice = "DISCIPLINE", eventId = 300002 };
        var noticeIntent = new SocialIntent { roomId = 1, facts = notice };
        var templatesMethod = typeof(InvestorSociety).GetMethod("EligibleTemplates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        System.Collections.Generic.List<SocialTemplate> ReadNotice() => (System.Collections.Generic.List<SocialTemplate>)templatesMethod.Invoke(society, new object[] { memoryBot, noticeIntent, notice, SocialAct.REACT });
        memoryMind.Remember(new SocialMemory { type = SocialMemoryKind.SAW_KICK, eventId = 300002, targetId = 2, at = 180 });
        Check(ReadNotice().All(t => t.variant != "memory"), "first witnessed kick cannot claim an earlier kick");
        var earlierKick = new SocialMemory { type = SocialMemoryKind.SAW_KICK, eventId = 300001, targetId = 1, at = 100 };
        memoryMind.Remember(earlierKick);
        Check(ReadNotice().Any(t => t.variant == "memory") && notice.memoryEventId == 300001, "repeat witnessed kick can cite the actual earlier memory");
        earlierKick.usedCount = ChatSocialConfig.Current.Int("memoryMaxUses");
        Check(ReadNotice().All(t => t.variant != "memory"), "exhausted memory cannot be cited again");
        File.WriteAllText(Path.Combine(root, "docs/chat-social-results/stage-" + stage, "S6-snapshot.json"), encoded);
    }
}
