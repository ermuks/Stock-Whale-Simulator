using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// 채팅 예약: 글을 만드는 일은 작업 스레드가 하고, 채팅방에 올리는 일만 메인 스레드가 한다.
//
//   메인 스레드 ── 맡긴다(Submit) ──▶ 작업 스레드: 문장을 고르고 짜 맞추고, 빈칸을 채우고, 가끔 오타를 내고, 치는 데 걸릴 시간을 잰다
//        ▲                                   │
//        └──── 다 된 것을 거둔다(TryTake) ◀───┘
//
//   1) 누가 말을 하기로 하면 메인 스레드는 재료(낱말 묶음, 빈칸에 넣을 값, 난수 씨앗)만 담아 맡긴다. 그 순간부터 그 봇은 "입력 중"이다.
//   2) 작업 스레드가 글을 완성한다. 게임 오브젝트는 건드리지 않는다(맡긴 재료만 쓴다).
//   3) 메인 스레드가 완성된 글을 거둬 "글 길이만큼 기다렸다가 올릴 목록"에 넣는다. 시간이 되면 채팅방에 올린다.
//
// 작업 스레드에서 쓰는 것은 문자열·배열·사전(맡길 때 복사해 둔 것)·자기 난수뿐이다. Stock·TradeBot 같은 것은 꼬리표로만 들고 다니다 돌려준다.
public sealed class ChatWork
{
    // ── 메인 스레드가 채운다 ──
    public int bot, room; public string key, topicEvent; public Stock stock; public long replyTo; public bool split; public object owner;
    /// <summary>맡긴 시각(사회 시계), 글을 읽고 치기 시작할 때까지의 뜸.</summary>
    public double submittedAt, startDelay;
    public int seed, generation;
    /// <summary>이미 정해진 글. 없으면 compose가 만든다.</summary>
    public string text;
    /// <summary>글을 만드는 함수. 작업 스레드에서 돈다: 넘겨받은 난수와 미리 담아 둔 재료만 써야 한다. null을 돌려주면 말하지 않는다.</summary>
    public Func<System.Random, string> compose;
    /// <summary>오타를 내면 안 되는 낱말들(종목 이름과 줄임말). 오타를 낼 수 없는 언어에서는 null.</summary>
    public string[] keep;
    public double typoChance, typoFixChance;
    /// <summary>글이 완성되어 올릴 목록에 들어갈 때 메인 스레드에서 한 번 부른다(확인용 기록 등).</summary>
    public Action<string> ready;
    // ── 작업 스레드가 채운다 ──
    public string result, fix, typoWrong, typoRight; public double typeSeconds, fixSeconds; public Exception error;
}

public static class ChatScheduler
{
    private static readonly ConcurrentQueue<ChatWork> inbox = new(), outbox = new();
    private static readonly AutoResetEvent wake = new(false);
    private static Thread worker;
    private static volatile bool running;
    private static int generation;
    /// <summary>맡겨 두고 아직 거두지 않은 일(메인 스레드만 만진다). 이 봇들은 "입력 중"이다.</summary>
    public static readonly List<ChatWork> Flying = new();
    /// <summary>확인용: 작업 스레드가 지금까지 완성한 글의 수.</summary>
    public static int Composed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Boot() { Reset(); Composed = 0; }

    /// <summary>새 장면·새 게임·불러오기: 맡겨 둔 일을 모두 버린다. 이미 돌고 있던 것의 결과도 받지 않는다.</summary>
    public static void Reset()
    {
        Interlocked.Increment(ref generation); Flying.Clear();
        while (inbox.TryDequeue(out _)) { }
        while (outbox.TryDequeue(out _)) { }
    }

    public static void Submit(ChatWork work)
    {
        work.generation = Volatile.Read(ref generation);
        Flying.Add(work); inbox.Enqueue(work);
        if (!running) { Begin(); }
        wake.Set();
    }

    /// <summary>완성된 글을 하나 거둔다(메인 스레드). 버려진 세대의 것은 건너뛴다.</summary>
    public static bool TryTake(out ChatWork work)
    {
        while (outbox.TryDequeue(out work))
        {
            if (work.generation != Volatile.Read(ref generation)) { continue; }
            Flying.Remove(work);
            return true;
        }
        return false;
    }

    private static void Begin()
    {
        running = true;
        Application.quitting -= Stop; Application.quitting += Stop;
        worker = new Thread(Loop) { IsBackground = true, Name = "Chat Scheduler", Priority = System.Threading.ThreadPriority.BelowNormal };
        worker.Start();
    }
    public static void Stop() { running = false; wake.Set(); }

    private static void Loop()
    {
        while (running)
        {
            while (running && inbox.TryDequeue(out var work))
            {
                if (work.generation != Volatile.Read(ref generation)) { continue; }
                try { Write(work); }
                catch (Exception error) { work.error = error; work.result = null; }
                outbox.Enqueue(work);
            }
            wake.WaitOne(250);
        }
    }

    /// <summary>글 하나를 완성한다(작업 스레드).</summary>
    private static void Write(ChatWork work)
    {
        var random = new System.Random(work.seed);
        string text = work.text ?? work.compose?.Invoke(random);
        if (string.IsNullOrEmpty(text)) { work.result = null; return; }
        // 아주 가끔 오타를 낸다. 그중 일부는 바로 뒤에 "*고친말"을 따로 올린다.
        if (work.keep != null && random.NextDouble() < work.typoChance)
        {
            string changed = ChatCompose.Typo(text, random.NextDouble, word => Kept(work.keep, word), out string wrong, out string right);
            if (changed != null)
            {
                text = changed; work.typoWrong = wrong; work.typoRight = right;
                if (random.NextDouble() < work.typoFixChance) { work.fix = "*" + right; work.fixSeconds = InvestorSociety.TypingSeconds(work.fix); }
            }
        }
        work.result = text; work.typeSeconds = InvestorSociety.TypingSeconds(text);
        Composed++;
    }
    private static bool Kept(string[] keep, string word)
    {
        foreach (string name in keep) { if (name.Length > 0 && (name.Contains(word) || word.Contains(name))) { return true; } }
        return false;
    }
}
