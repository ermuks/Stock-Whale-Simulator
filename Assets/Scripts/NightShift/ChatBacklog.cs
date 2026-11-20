using System;
using System.Collections.Generic;
using UnityEngine;

// 장이 열리기 전에 이미 오가던 대화. 새 게임을 시작하면 공개 라운지가 텅 비어 있어서 방이 방금 만들어진 것처럼 보였다.
// 처음 한 번, 라운지에 아무 말도 없을 때, 개장 전 40분쯤에 걸쳐 오갔던 잡담 몇 토막을 미리 깔아 둔다.
//
// 표시 전용이다. 방의 최근 대화·목격·감정·기억·난수열 어디에도 넣지 않는다. 어떤 봇의 판단도 바꾸지 않는다.
// 종목이나 값 얘기는 하지 않는다(아직 장이 열리기 전이다). 대사는 언어마다 따로 쓴 파일(Chat/chat_backlog.ko/en/ja)에서 온다.
[Serializable] public sealed class ChatBacklogThread { public string[] lines; }
[Serializable] public sealed class ChatBacklogLocale { public int schemaVersion = 1; public ChatBacklogThread[] threads; }

public sealed partial class InvestorSociety
{
    /// <summary>미리 깔아 두는 대화의 토막 수와, 그 대화가 걸쳐 있는 시간(분). 가장 늦은 말은 지금보다 1~2분 앞이다.</summary>
    private const int BacklogThreads = 6, BacklogMinutes = 40;
    private bool backlogDone;
    /// <summary>확인용: 미리 깔아 둔 줄 수.</summary>
    public int BacklogLines { get; private set; }

    private void TickBacklog()
    {
        if (backlogDone || day == null || !SocialReady) { return; }
        backlogDone = true;
        // 이미 말이 있으면(이어하기, 또는 벌써 누가 말했으면) 깔지 않는다. 새로 시작한 첫날에만 한다.
        if (day.Day != 1 || day.LoungeMessageCount > 0) { return; }
        var asset = Resources.Load<TextAsset>("Chat/chat_backlog." + GameLocale.Code) ?? Resources.Load<TextAsset>("Chat/chat_backlog.ko");
        var locale = asset == null ? null : JsonUtility.FromJson<ChatBacklogLocale>(asset.text);
        if (locale == null || locale.schemaVersion != 1 || locale.threads == null || locale.threads.Length == 0) { return; }
        // 반말로 떠드는 잡담이다. 존댓말을 쓰는 봇, 개소리 봇, 세력 방장은 빼고 고른다.
        var speakers = new List<TradeBot>(); var odd = ResolveOddball(); var leader = NpcLeadingRoom.Instance == null ? null : NpcLeadingRoom.Instance.Leader;
        foreach (var bot in socialBots)
        { if (bot != null && bot != odd && bot != leader && bot.Social != null && bot.Social.Psychology.Traits.Politeness < Config.Int("styleFormalMin")) { speakers.Add(bot); } }
        if (speakers.Count < 6) { return; }
        var random = new System.Random(unchecked(Environment.TickCount * 31 + day.Day));
        var picked = new List<ChatBacklogThread>(locale.threads);
        for (int i = picked.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (picked[i], picked[j]) = (picked[j], picked[i]); }
        int count = Math.Min(BacklogThreads, picked.Count), now = day.ClockMinute;
        for (int i = 0; i < count; i++)
        {
            var thread = picked[i];
            if (thread == null || thread.lines == null) { continue; }
            // 토막마다 시간 간격을 두고, 토막 안에서는 한 줄씩 곧바로 잇는다. 같은 토막에서 같은 사람이 두 번 말하지 않는다.
            int at = now - 2 - (count - 1 - i) * (BacklogMinutes / count) - random.Next(0, 3);
            var used = new HashSet<TradeBot>();
            foreach (string line in thread.lines)
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }
                TradeBot speaker = null;
                for (int attempt = 0; attempt < 8 && (speaker == null || used.Contains(speaker)); attempt++) { speaker = speakers[random.Next(speakers.Count)]; }
                used.Add(speaker);
                day.PublishBacklog(speaker, line, Math.Max(0, at));
                BacklogLines++;
                if (random.Next(3) == 0) { at++; }
            }
        }
    }
}
