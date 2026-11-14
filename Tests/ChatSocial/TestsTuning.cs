using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static partial class ChatSocialTests
{
    // Bounded search over ordinary tuning settings; acceptance ranges and all five seeds remain unchanged.
    private static void SearchTuning(int stage)
    {
        ChatSocialAssets.EnsureLoaded();
        var config = ChatSocialConfig.Current;
        var path = Path.Combine(root, "docs/chat-social-results/stage-" + stage, "search-trials-17.txt");
        using var log = new StreamWriter(path, true) { AutoFlush = true };
        double best = double.MaxValue;
        int trial = 0;
        foreach (double question in new[] { 5d, 6d, 4d })
        foreach (double chat in new[] { 4d, 5d })
        foreach (double target in new[] { .38d, .375d, .385d, .39d })
        {
            foreach (var change in new[] { ("temperMean", 55d), ("angerDecayPerHour", 4d),
                ("idleChance", .025d), ("initiateTargetChance", target),
                ("memoryMaxUses", 6d), ("argumentTurnsMin", 8d), ("argumentTurnsMax", 14d),
                ("memoryChance", 1d), ("exitGraceSec", 30d), ("angerStage1", 20d), ("angerStage2", 45d), ("replyBase.SMALLTALK", .8d), ("chatTurnsMax", chat),
                ("replyExpiry", 30d), ("replyDelayMax", 7d), ("questionTurnsMax", question),
                ("replyBase.MOCK", .9d), ("replyBase.REBUT", .9d) })
            {
                var entry = config.values.Single(v => v.key == change.Item1);
                log.WriteLine($"trial={trial} {entry.key} {entry.value} -> {change.Item2}"); entry.value = change.Item2;
            }
            foreach (var effect in new[] { ("DOUBT", 15d), ("REBUT", 15d) })
            {
                var weight = ChatSocialAssets.Acts.effects.Single(r => r.key == effect.Item1).weights.Single(w => w.key == "anger");
                log.WriteLine($"trial={trial} effects.{effect.Item1}.anger {weight.value} -> {effect.Item2}"); weight.value = effect.Item2;
            }
            foreach (var item in new[] { ("DOUBT", 13d), ("MOCK", 2d) })
            {
                var weight = ChatSocialAssets.Acts.motives.Single(r => r.key == "IDLE").weights.Single(w => w.key == item.Item1);
                log.WriteLine($"trial={trial} motives.IDLE.{item.Item1} {weight.value} -> {item.Item2}"); weight.value = item.Item2;
            }
            config.Validate();
            double error = 0; int voluntary = 0, sympathy = 0;
            foreach (int seed in new[] { 73129, 73130, 73131, 73132, 73133 })
            {
                var replay = SocialReplay(seed, stage);
                var room = replay.Messages.Where(m => m.roomId == 1).ToArray();
                var directed = room.Where(m => m.targetId > 0).ToArray(); var closed = replay.Society.ClosedThreads.Where(t => t.roomId == 1).ToArray();
                var rates = new Dictionary<int, double> {
                    [1] = directed.Length == 0 ? 0 : (double)directed.Count(m => room.Any(r => r.replyToId == m.id && r.senderId == m.targetId)) / directed.Length,
                    [2] = closed.Length == 0 ? 0 : closed.Average(t => t.turnCount), [3] = (double)room.Count(m => m.targetId != 0) / room.Length,
                    [4] = replay.Journal.Events.Count(e => e.kind == "ARGUMENT" && e.room == 1), [5] = replay.Society.VoluntaryExits,
                    [7] = (double)room.Count(m => m.memory) / room.Length, [8] = (double)replay.Society.TemplateBlocked / replay.Society.SocialAttempts };
                foreach (var rate in rates)
                {
                    double min = rate.Key == 8 ? 0 : config.Get("R" + rate.Key + "Min"), max = config.Get("R" + rate.Key + "Max");
                    error += Math.Max(0, min - rate.Value) / (max - min) + Math.Max(0, rate.Value - max) / (max - min);
                }
                voluntary += replay.Society.VoluntaryExits; sympathy += replay.Society.SympathyExits;
                log.WriteLine($"trial={trial} seed={seed} messages={room.Length} " + string.Join(" ", rates.Select(r => $"R{r.Key}={r.Value:F6}")));
            }
            double r6 = voluntary == 0 ? 0 : (double)sympathy / voluntary;
            error += Math.Max(0, config.Get("R6Min") - r6) + Math.Max(0, r6 - config.Get("R6Max"));
            log.WriteLine($"trial={trial} R6={r6:F6} error={error:F6}");
            if (error < best)
            {
                best = error;
                File.WriteAllText(Path.Combine(root, "Validation/ChatSocial/tuning-best.json"), UnityEngine.JsonUtility.ToJson(config));
            }
            Console.WriteLine($"Tuning {trial++}/24: question={question}, chat={chat}, target={target}, error={error:F4}, best={best:F4}");
            if (error == 0)
            {
                File.WriteAllText(Path.Combine(root, "Validation/ChatSocial/tuning-candidate.json"), UnityEngine.JsonUtility.ToJson(config));
                File.WriteAllText(Path.Combine(root, "Validation/ChatSocial/tuning-candidate-weights.json"), UnityEngine.JsonUtility.ToJson(ChatSocialAssets.Acts));
                Console.WriteLine("R criteria passed; candidate requires full T/R/S verification before adoption."); return;
            }
        }
        Console.WriteLine("No candidate passed. Failed configurations are retained only in the trial log.");
    }
}
