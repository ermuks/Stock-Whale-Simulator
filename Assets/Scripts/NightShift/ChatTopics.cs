using System;
using System.Collections.Generic;
using UnityEngine;

// 잡담의 갈래(주제).
//
// 플레이어는 주식과 상관없는 온갖 얘기를 한다: 고양이, 커피, 군대, 월세, 야근, MBTI, 생일, 택배 …
// 그 말이 어느 갈래인지 알아야 그 얘기에 맞는 답을 할 수 있다. 갈래를 모르면 답하지 않는다(엉뚱한 답보다 낫다).
//
// 갈래는 언어마다 따로 쓴 자료 파일에 적는다(Chat/chat_topics.ko / .en / .ja). 번역본이 아니다:
// 나라마다 하는 얘기가 다르므로(한국의 군대·전세·한강, 미국의 401k·팁·슈퍼볼, 일본의 NISA·優待·花粉症) 갈래의 목록부터 다르고,
// 말투도 그 나라 주식 커뮤니티의 것을 따른다. 같은 뜻의 갈래는 열쇠(key)를 같게 둔다(코드가 열쇠로 찾는 갈래가 있다: food, afk, newbie …).
// 갈래마다:
//   - words: 이 낱말이 들어 있으면 그 갈래다. 여러 갈래에 걸리면 가장 길게 맞은 쪽이 이긴다.
//   - tell: 그 얘기를 "하는" 글에 다는 답("고양이 키움" → "사진 없음? ㅋㅋ").
//   - ask: 그 얘기를 "묻는" 글에 다는 답("고양이 키우는 사람?" → "난 고양이 키움"). 없으면 tell을 쓴다.
//   - term: 낱말 풀이. "지정가가 뭐임"처럼 그 낱말의 뜻을 물을 때만 쓴다(tell에 풀이를 적는다). 갈래 고르기에는 끼지 않는다.
//   - weak: 기분을 말하는 갈래(무섭다, 우울하다, 피곤하다 …). 다른 얘깃거리가 같이 있으면 그쪽에 진다("전세 사기 무섭다"는 집 얘기다).
// 갈래를 늘리려면 그 언어의 파일에 한 덩어리를 더 적으면 된다. 코드는 고치지 않는다.
// 예전부터 있던 갈래(밥, 잠, 날씨 …)는 PlayerChat.cs의 표와 player_talk 파일에 있다.
[Serializable] public sealed class ChatTopic { public string key; public bool weak, term; public string[] words, tell, ask; }
[Serializable] public sealed class ChatTopicFile { public int schemaVersion = 2; public ChatTopic[] topics; }

public static class ChatTopics
{
    private static ChatTopic[] all;
    private static string loaded;
    private static Dictionary<string, ChatTopic> index;
    private static readonly ChatTopic[] None = Array.Empty<ChatTopic>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { all = null; index = null; loaded = null; }

    /// <summary>지금 언어의 갈래들. 언어가 바뀌면 그 언어의 파일을 다시 읽는다.</summary>
    public static ChatTopic[] All
    {
        get
        {
            string code = GameLocale.Code;
            if (all != null && loaded == code) { return all; }
            loaded = code;
            index = new Dictionary<string, ChatTopic>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Chat/chat_topics." + code);
            // 그 언어의 파일이 없으면 한국어 파일로 대신한다(낱말이 달라 거의 걸리지 않는다. 엉뚱한 답을 하느니 그게 낫다).
            if (asset == null) { asset = Resources.Load<TextAsset>("Chat/chat_topics.ko"); }
            var data = asset == null ? null : JsonUtility.FromJson<ChatTopicFile>(asset.text);
            if (data == null || data.schemaVersion != 2 || data.topics == null) { all = None; return all; }
            var list = new List<ChatTopic>();
            foreach (var topic in data.topics)
            {
                if (topic == null || string.IsNullOrEmpty(topic.key) || topic.words == null || topic.words.Length == 0 || index.ContainsKey(topic.key)) { continue; }
                // 낱말은 소문자로 맞춰 둔다(견줄 글도 소문자다).
                for (int i = 0; i < topic.words.Length; i++) { topic.words[i] = (topic.words[i] ?? "").ToLowerInvariant(); }
                index[topic.key] = topic; list.Add(topic);
            }
            all = list.ToArray();
            return all;
        }
    }
    /// <summary>그 갈래에 "묻는 글에 다는 답"이 따로 있는가.</summary>
    public static bool HasAsk(string key)
    {
        if (All.Length == 0 || key == null || !index.TryGetValue(key, out var topic)) { return false; }
        return topic.ask != null && topic.ask.Length > 0;
    }
    /// <summary>"topic.pet" 또는 "topic.pet.q"에 쓸 문장들. 이 파일의 갈래가 아니면 null.</summary>
    public static string[] Lines(string key)
    {
        if (All.Length == 0 || key == null || !key.StartsWith("topic.", StringComparison.Ordinal)) { return null; }
        string name = key.Substring(6); bool ask = false;
        if (name.EndsWith(".q", StringComparison.Ordinal)) { ask = true; name = name.Substring(0, name.Length - 2); }
        if (!index.TryGetValue(name, out var topic)) { return null; }
        if (ask && topic.ask != null && topic.ask.Length > 0) { return topic.ask; }
        return topic.tell != null && topic.tell.Length > 0 ? topic.tell : null;
    }
}
