using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static partial class ChatSocialTests
{
    /// <summary>장 마감(17:00)부터 다음 날 개장(09:00)까지 장외 채팅을 실제 시간 간격으로 돌려 본다.</summary>
    private static void AfterHoursScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        foreach (var bot in society.SocialBots.Values) { bot.Social.Psychology.Room = 0; }
        var stocks = StockManager.Instance.stocks;
        // 노보손해보험: 980 → 1,050 (1,000원 돌파). 두덕리식품: −6%. 미래바이오: 보합. 국민금융: +5%.
        stocks[0].currentPrice = 1050m; stocks[1].currentPrice = 1410m; stocks[2].currentPrice = 2000m; stocks[3].currentPrice = 2625m;
        var references = new Dictionary<Stock, decimal> { [stocks[0]] = 980m, [stocks[1]] = 1500m, [stocks[2]] = 1998m, [stocks[3]] = 2500m };
        InvestorSociety.ReadMarketDay = stock => new InvestorSociety.MarketDay { reference = references[stock], high = stock.currentPrice, low = stock.currentPrice };
        int before = replay.Messages.Count; var rooms = new[] { 0 }; var lines = new List<string>();
        var hours = new Dictionary<long, int>();
        // 마감 후 1,687.5초가 게임 시간 900분(17:00~08:00), 개장 전 112.5초가 60분.
        for (double seconds = 0; seconds < 1800; seconds += .5)
        {
            double minute = seconds < 1687.5 ? 1020 + 900 * seconds / 1687.5 : 1920 + 60 * (seconds - 1687.5) / 112.5;
            society.SocialTime.Set(1, minute, 5000 + seconds);
            int count = replay.Messages.Count;
            society.AfterHoursAdvance(stocks, rooms);
            for (int i = count; i < replay.Messages.Count; i++)
            {
                var message = replay.Messages[i]; int clock = (int)minute % 1440; hours[message.id] = clock / 60;
                lines.Add($"{clock / 60:D2}:{clock % 60:D2}\t{message.templateKey}\t{society.SocialBots[message.senderId].Social.Nickname}\t{message.text}");
            }
        }
        InvestorSociety.ReadMarketDay = null;
        var said = replay.Messages.Skip(before).ToList(); var byId = said.ToDictionary(m => m.id);
        string Topic(SocialMessage m) => m.templateKey.Substring(0, m.templateKey.LastIndexOf('.'));
        Directory.CreateDirectory(Path.Combine(root, "Validation/ChatSocial"));
        File.WriteAllLines(Path.Combine(root, "Validation/ChatSocial/afterhours.tsv"), lines);
        Console.WriteLine($"After hours: {said.Count} messages, {said.Count(m => m.replyToId == 0)} openers, topics {string.Join(",", said.Select(Topic).Distinct().Select(t => t.Substring(6)))}");
        Check(said.Count >= 150, "After hours: chat continues from close to the next open");
        Check(said.All(m => !m.text.Contains("{") && !m.text.Contains("}")), "After hours: every slot is filled");
        Check(said.Where(m => m.replyToId != 0).All(m => byId.ContainsKey(m.replyToId) && Topic(byId[m.replyToId]) == Topic(m)), "After hours: every reply stays on the opener's topic");
        Check(said.Where(m => m.replyToId != 0).All(m => m.targetId == byId[m.replyToId].senderId && m.senderId != m.targetId), "After hours: replies address the person they answer");
        Check(said.Any(m => m.templateKey == "AFTER.BREAK.open" && m.text.Contains("노보손해보험") && m.text.Contains("1,000원") && m.text.Contains("2,000원")), "After hours: a round-number breakout names the level and the next one");
        Check(said.Where(m => m.templateKey.StartsWith("AFTER.DINNER.")).All(m => hours[m.id] >= 17 && hours[m.id] <= 21) && said.Any(m => m.templateKey == "AFTER.DINNER.open"), "After hours: dinner talk only happens in the evening");
        Check(said.Where(m => m.templateKey == "AFTER.BREAK.open" || m.templateKey == "AFTER.UP.open").All(m => hours[m.id] >= 12 ? !m.text.Contains("어제") : !m.text.Contains("내일")), "After hours: evening says today/tomorrow, morning says yesterday/today");
        Check(said.Where(m => m.templateKey.EndsWith(".holder")).All(m => society.SocialBots[m.senderId].GetOwnedVolume(stocks.First(s => s.symbol == m.facts.symbol)) > 0m), "After hours: only real holders speak as holders");
        Check(said.All(m => society.SocialBots[m.senderId].Social.Psychology.Traits.Politeness < ChatSocialConfig.Current.Int("styleFormalMin")), "After hours: casual lines are spoken by casual speakers");
        Check(said.Where(m => m.replyToId != 0).GroupBy(m => Topic(m) + ":" + (byId[m.replyToId].replyToId == 0 ? m.replyToId : byId[m.replyToId].replyToId)).All(g => g.Select(m => m.localizedPhrase).Distinct().Count() == g.Count()), "After hours: no line repeats inside one conversation");
        Check(said.All(m => !m.templateKey.StartsWith("AFTER.BREAK.") || m.facts.symbol != stocks[2].symbol), "After hours: a flat day is not called a breakout");
        Check(replay.AvailabilityErrors == 0, "After hours: speakers and targets are present");
    }
}
