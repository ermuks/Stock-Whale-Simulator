using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 플레이어의 채팅과 종목 분석 AI.
//
// 플레이어가 채팅방에 글을 쓰면, 글에서 뜻을 읽고(PlayerTalk.Read) 그 방에 있는 봇들이 사람처럼 받아친다.
// 언어 모델은 쓰지 않는다. 낱말과 말꼬리, 종목 이름, 숫자를 규칙으로 읽는다: 부르기(님들·여러분), 인사, 웃음(ㅋㅋ·ㅎㅎ·^^), 종목 묻기(살까·팔까·어때),
// 오른다·내린다는 주장, 수익 자랑과 손실 한탄, 고맙다·미안하다, 시비, "내가 세력" 같은 허세, 그리고 "!분석 종목" 명령.
// 봇의 답은 그 봇의 기분(욕심·공포), 나에 대한 믿음과 적대심, 그 종목을 보는 자기 생각(믿는 값)에 따라 달라진다.
// 답글은 화면 표시용이다(방의 대화 기억·난수열에는 넣지 않는다). 대신 내 말은 봇의 마음에 남는다:
//  - 수익 자랑은 내 실제 계좌로 확인된다. 인증되면 믿음이 오르고 배 아픈 봇은 적대심이 오른다. 인증이 안 되면 믿음이 깎인다.
//  - "오른다"·"내린다"는 말은 기억됐다가 몇 분 뒤 맞았는지 따진다. 맞으면 믿음이, 틀리면 적대심이 쌓인다.
//  - 믿음이 쌓인 봇일수록 내가 말한 종목으로 눈을 돌린다(ChatInfluence와 같은 장치).
// 주문을 대신 내거나 봇의 믿는 값을 바꾸지는 않는다. 실제 시청자 채팅은 이 길을 지나지 않는다.
//
// 무슨 말을 해도 누군가는 받는다. 소식·시장·세력·수익을 물으면 봇은 자기가 보고 아는 것(공개된 기사, 시세, 자기 계좌)으로 답하고,
// 밥·잠·날씨 같은 시답잖은 얘기에는 그 갈래에 맞는 잡담으로 받는다. 봇이 던진 물음에 내가 답하면 물은 봇이 그 답을 받는다(PlayerChatTalk.cs).
//
// 종목 분석 AI: 새벽 관찰실에만 있다. "!분석 종목"이라고 쓰면(봇도 가끔 쓴다) 당일 시가·현재가·고가·저가와 매수 추천 점수를 카드로 올린다.
// 점수는 누구나 보는 시세·호가·거래량만으로 낸다. 진짜 가치는 읽지 않는다. 참고용이고 틀릴 수 있다. 봇은 AI 말대로 움직이지 않는다.

public enum PlayerTalkKind { Other, Analyze, Insult, Brag, Lament, AskBuy, AskSell, AskOpinion, AskPick, ClaimUp, ClaimDown, Whale, Thanks, Apology, Greeting, Bye, Laugh, Sad, CallAll,
    AskNews, AskMarket, AskHot, AskWhale, AskHoldings, AskProfit, AskWhy, AskHow, Mention, Agree, Disagree, Dunno, Surprise, Topic, Question, Statement }

public struct PlayerTalk
{
    public PlayerTalkKind Kind;
    public Stock Stock;
    /// <summary>글에 적힌 수익률(%). 없으면 NaN.</summary>
    public float Percent;
    public bool CallsAll, Laughs, Cries, Asks;
    /// <summary>물음표나 묻는 말꼬리는 없어도 묻는 낱말(뭐·왜·누가·what·何)이 들어 있다.</summary>
    public bool Wonders;
    /// <summary>밤사이·어제 일을 묻는다.</summary>
    public bool Night;
    /// <summary>"왜?"·"진짜?"처럼 바로 앞의 말을 되묻는 짧은 말.</summary>
    public bool AsksBack;
    /// <summary>잡담의 갈래(food, sleep, weather …). 잡담이 아니면 null.</summary>
    public string Topic;
    /// <summary>막말의 세기: 0 없음, 1 얕보는 말, 2 욕설, 3 선을 넘은 말(가족·죽으라는 말·성적인 욕).</summary>
    public int Rudeness;
    /// <summary>선을 넘은 말의 갈래: 가족을 건드렸는가, 죽으라고 했는가.</summary>
    public bool RudeFamily, RudeDeath;
    /// <summary>"!분석" 뒤에 적은 말(종목을 못 찾았을 때 되돌려 보여 준다).</summary>
    public string Argument;

    private static readonly Regex PercentPattern = new(@"([+\-]?\d+(?:\.\d+)?)\s*(?:%|퍼|프로|パー)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LaughPattern = new(@"ㅋ{2,}|ㅎ{2,}|\^\^|\^_\^|lol|lmao|haha|ｗ{2,}|w{3,}|草", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CryPattern = new(@"ㅠ{2,}|ㅜ{2,}|ㅠㅜ|ㅜㅠ|하\.{2,}|흑흑|;;|orz|泣", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] All = { "님들", "여러분", "저기요", "다들", "얘들아", "형들", "형님들", "누구", "아무나", "guys", "everyone", "anyone", "anybody", "y'all", "みんな", "皆さん", "すみません", "誰か" };
    private static readonly string[] Greet = { "안녕", "ㅎㅇ", "하이", "반갑", "반가워", "방가", "좋은아침", "굿모닝", "hello", "hi ", "hey", "good morning", "こんにちは", "おはよう", "こんばんは", "よろしく" };
    private static readonly string[] Farewell = { "잘자", "퇴근", "수고", "나간다", "이만", "빠이", "ㅂㅂ", "ㅂㅇ", "bye", "good night", "gn", "お疲れ", "おやすみ", "じゃあね" };
    private static readonly string[] Thank = { "감사", "고마워", "고맙", "ㄱㅅ", "땡큐", "thanks", "thank you", "thx", "ty", "ありがと", "感謝" };
    private static readonly string[] Sorry = { "미안", "죄송", "ㅈㅅ", "sorry", "my bad", "ごめん", "すまん" };
    private static readonly string[] Rude = { "바보", "멍청", "호구", "개미들", "닥쳐", "꺼져", "ㅂㅅ", "병신", "한심", "찐따", "루저", "흑우", "idiot", "stupid", "noob", "loser", "shut up", "dumb", "バカ", "馬鹿", "アホ", "雑魚", "黙れ" };
    // 욕설은 띄어쓰기·기호·숫자를 끼워 넣어도 알아듣도록, 글자만 남긴 글에서 찾는다.
    private static readonly string[] Swear = { "씨발", "시발", "씨바", "씨팔", "시팔", "쓰발", "씨빨", "ㅅㅂ", "ㅆㅂ", "씹", "좆", "졷", "병신", "븅신", "빙신", "ㅄ", "새끼", "새키", "섀끼", "쉐끼", "ㅅㄲ", "개놈", "개년", "년들", "련들", "년아", "련아", "미친놈", "미친년", "미친새", "또라이", "지랄", "ㅈㄹ", "등신", "머저리", "얼간이", "쪼다", "개돼지", "거지새", "아가리", "주둥이",
        "fuck", "fck", "shit", "bitch", "asshole", "bastard", "retard", "moron", "scum", "dumbass", "dipshit", "stfu", "クソ", "くそ", "カス", "ゴミ", "きもい", "キモい", "うざい", "ウザい", "ボケ", "クズ", "くず", "消えろ", "失せろ" };
    private static readonly string[] Vile = { "애미", "애비", "에미", "니엄마", "너희엄마", "니네엄마", "니아빠", "느금", "엄창", "뒤져", "뒤진", "뒤질", "뒈져", "뒈진", "디져", "죽어버려", "다죽어", "죽여버", "쳐죽", "암적", "쑤셔", "강간", "패죽", "찢어죽",
        "kys", "killyourself", "yourmom", "yourmother", "motherf", "godie", "dropdead", "hopeyoudie", "死ね", "殺す", "殺し", "くたばれ", "お前の母", "親の顔", "自殺" };
    /// <summary>글자만 남긴다(한글·자모·영문·가나·한자). 띄어쓰기나 기호로 끊어 쓴 욕을 이어 붙여 읽기 위해서다.</summary>
    private static string Bare(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        foreach (char raw in text)
        {
            char c = char.ToLowerInvariant(raw);
            if (c >= 'a' && c <= 'z' || c >= '\uAC00' && c <= '\uD7A3' || c >= '\u3131' && c <= '\u318E' || c >= '\u3040' && c <= '\u30FF' || c >= '\u4E00' && c <= '\u9FFF') { result.Append(c); }
        }
        return result.ToString();
    }
    private static readonly string[] FamilyWords = { "애미", "애비", "에미", "엄마", "아빠", "느금", "엄창", "부모", "yourmom", "yourmother", "motherf", "お前の母", "親の顔" };
    private static readonly string[] DeathWords = { "뒤져", "뒤질", "뒈져", "디져", "죽어버려", "다죽어", "죽여버", "쳐죽", "패죽", "찢어죽", "kys", "killyourself", "godie", "dropdead", "hopeyoudie", "死ね", "殺す", "殺し", "くたばれ", "自殺" };
    // 한글을 영문 자판 그대로 친 글("roqbdtls" = 개병신)도 알아듣는다: 두벌식 자판에서 글쇠를 자모로 바꾸고 글자로 모은다.
    private const string KeyConsonants = "rRseEfaqQtTdwWczxvg", Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private static readonly string[] KeyVowels = { "k", "o", "i", "O", "j", "p", "u", "P", "h", "hk", "ho", "hl", "y", "n", "nj", "np", "nl", "b", "m", "ml", "l" };
    private static readonly string[] KeyFinals = { "", "r", "R", "rt", "s", "sw", "sg", "e", "f", "fr", "fa", "fq", "ft", "fx", "fv", "fg", "a", "q", "qt", "t", "T", "d", "w", "c", "z", "x", "v", "g" };
    /// <summary>영문 자판으로 친 글을 한글로 바꾼다. 한글 글쇠가 아닌 것은 그대로 둔다.</summary>
    public static string FromKeys(string keys)
    {
        var result = new System.Text.StringBuilder(); int at = 0;
        int Vowel(int from, out int used)
        {
            used = 0; int found = -1;
            for (int i = 0; i < KeyVowels.Length; i++)
            { string v = KeyVowels[i]; if (v.Length > used && string.CompareOrdinal(keys, from, v, 0, v.Length) == 0) { used = v.Length; found = i; } }
            return found;
        }
        while (at < keys.Length)
        {
            int initial = KeyConsonants.IndexOf(keys[at]);
            if (initial < 0) { int lone = Vowel(at, out int w); if (lone >= 0) { result.Append((char)(0x314F + lone)); at += w; } else { result.Append(keys[at]); at++; } continue; }
            int vowel = at + 1 < keys.Length ? Vowel(at + 1, out int width) : -1;
            if (vowel < 0) { result.Append(Initials[initial]); at++; continue; }
            int next = at + 1 + System.Math.Max(1, KeyVowels[vowel].Length), final = 0, finalWidth = 0;
            // 받침: 뒤에 모음이 오면 그 자음은 다음 글자의 첫소리다.
            for (int i = 1; i < KeyFinals.Length; i++)
            {
                string f = KeyFinals[i];
                if (f.Length <= finalWidth || next + f.Length > keys.Length || string.CompareOrdinal(keys, next, f, 0, f.Length) != 0) { continue; }
                int after = next + f.Length;
                if (after < keys.Length && Vowel(after, out _) >= 0) { continue; }
                final = i; finalWidth = f.Length;
            }
            result.Append((char)(0xAC00 + (initial * 21 + vowel) * 28 + final)); at = next + finalWidth;
        }
        return result.ToString();
    }
    private static bool OnlyKeys(string text)
    {
        int letters = 0;
        foreach (char c in text) { if (c > 0x7F) { return false; } if (char.IsLetter(c)) { letters++; } }
        return letters >= 4;
    }
    /// <summary>막말의 세기(0~3).</summary>
    public static int RudenessOf(string text) => RudenessOf(text, out _, out _);
    public static int RudenessOf(string text, out bool family, out bool death)
    {
        family = death = false;
        string line = text ?? "", lower = line.ToLowerInvariant(), bare = Bare(lower);
        if (bare.Length == 0) { return 0; }
        int level = Has(bare, Vile) ? 3 : Has(bare, Swear) ? 2 : Has(lower, Rude) ? 1 : 0;
        // "자살"은 띄어 쓴 글에서만 찾는다. 글자만 이어 붙이면 "산삼전자 살까"가 걸린다.
        bool suicide = lower.Contains("자살") && !lower.Contains("전자살");
        if (suicide) { level = 3; }
        // 영문 자판으로 친 한글. 영어 낱말이 우연히 걸리지 않도록 욕설 이상만 본다.
        if (level == 0 && OnlyKeys(line))
        {
            string typed = Bare(FromKeys(line));
            level = Has(typed, Vile) ? 3 : Has(typed, Swear) ? 2 : 0;
            if (level > 0) { bare = typed; }
        }
        if (level >= 3) { family = Has(bare, FamilyWords); death = suicide || Has(bare, DeathWords); }
        return level;
    }
    private static readonly string[] BragWords = { "수익", "익절", "먹었", "먹음", "벌었", "벌음", "땄", "수익률", "플러스", "빨간불", "대박 났", "대박났", "대박 쳤", "떡상했", "profit", "made ", "gains", "up ", "banked", "利確", "儲か", "勝った", "爆益" };
    private static readonly string[] LossWords = { "물렸", "물림", "손절", "잃었", "잃음", "마이너스", "파란불", "깡통", "손실", "털렸", "반토막", "bagholding", "lost ", "loss", "down ", "rekt", "stopped out", "損切", "含み損", "負けた", "爆損" };
    private static readonly string[] BuyAsk = { "살까", "사도", "사야", "탈까", "타도", "들어가도", "들어갈까", "진입", "매수각", "줍줍", "담을까", "should i buy", "buy now", "get in", "worth buying", "買い?", "買う?", "買っていい", "入っていい", "買い時" };
    private static readonly string[] SellAsk = { "팔까", "팔아야", "던질까", "던져야", "손절할까", "익절할까", "정리할까", "매도각", "should i sell", "sell now", "take profit", "get out", "売り?", "売る?", "売っていい", "利確していい", "売り時" };
    private static readonly string[] OpinionAsk = { "어때", "어떰", "어떻게 봐", "어떻게 보", "어케 봄", "어떨까", "괜찮", "갈까", "오를까", "떨어질까", "전망", "의견", "what do you think", "thoughts", "how about", "any good", "どう思う", "どう?", "どうかな", "どうよ", "見通し" };
    private static readonly string[] PickAsk = { "뭐 살", "뭐 사", "뭘 사", "뭐 타", "추천", "종목 좀", "뭐가 좋", "어디 들어", "what to buy", "any picks", "recommend", "what should i buy", "何買", "おすすめ", "何がいい" };
    private static readonly string[] UpWords = { "오른다", "오를", "올라간다", "올라갈", "간다", "갈듯", "갈 듯", "갈거", "갈 거", "갈것", "갈 것", "쏜다", "쏠", "떡상", "상한가", "가즈아", "풀매수", "날아간다", "터진다", "급등", "올린다", "to the moon", "going up", "mooning", "pump", "rip", "sends", "上がる", "爆上げ", "ストップ高", "飛ぶ", "買い増し" };
    private static readonly string[] DownWords = { "떨어진다", "떨어질", "내려간다", "내려갈", "내릴", "빠진다", "빠질", "떡락", "하한가", "망했", "끝났", "폭락", "급락", "무너진다", "던져라", "going down", "dump", "crash", "tank", "it's over", "dead", "下がる", "暴落", "ストップ安", "終わった", "逃げろ" };
    private static readonly string[] WhaleWords = { "내가 세력", "나 세력", "제가 세력", "세력이다", "내가 올린", "내가 올릴", "작전 들어", "내가 쏜다", "i'm the whale", "i am the whale", "i'll pump", "i move this", "俺が仕手", "仕手だ", "俺が上げる" };

    // ── 금융 얘기를 묻는 말 ──
    private static readonly string[] NewsAsk = { "소식", "뉴스", "정보", "찌라시", "공시", "호재", "악재", "무슨 일", "무슨일", "뭔 일", "뭔일", "별일", "루머", "기사", "떴냐", "떴음", "news", "rumor", "rumour", "headline", "any info", "what happened", "anything happen", "what's going on", "whats going on", "what did i miss", "ニュース", "情報", "材料", "噂", "何かあった", "なんかあった", "何があった" };
    private static readonly string[] NightWords = { "밤에", "밤새", "밤사이", "밤 사이", "간밤", "어제", "새벽에", "자는 동안", "overnight", "last night", "yesterday", "while i slept", "昨夜", "夜の間", "昨日", "寝てる間", "夜中" };
    private static readonly string[] WhaleAsk = { "세력", "작전", "큰손", "고래", "기관", "주포", "누가 올", "누가 던", "누가 사", "누가 파", "매집", "whale", "whales", "big money", "big player", "manipulation", "manipulating", "who's buying", "who is buying", "who's selling", "who is selling", "仕手", "大口", "クジラ", "機関", "誰が買", "誰が売" };
    private static readonly string[] MarketAsk = { "장 어때", "오늘 장", "장 분위기", "분위기", "시장", "지수", "장 왜", "오늘 어때", "장 좋", "장 안 좋", "장이 좋", "장이 안", "market", "how's today", "hows today", "how is today", "相場", "地合い", "今日どう", "市場" };
    private static readonly string[] HotAsk = { "뭐 보고", "뭐 봄", "뭐 보는", "뭘 보", "뭐보", "뭐가 오", "뭐 오르", "뭐가 가", "뭐 감", "핫한", "뜨는", "급등주", "대장", "주도주", "뭐가 세", "뭐 빠", "뭐가 빠", "뭐 떨어", "뭐가 떨어", "what are you watching", "what are you looking at", "what's moving", "whats moving", "what's hot", "whats hot", "movers", "what's pumping", "what's running", "何見てる", "なに見てる", "何が上が", "何が動", "注目", "何が熱" };
    private static readonly string[] HoldAsk = { "뭐 들고", "뭘 들고", "뭐 갖고", "뭐 가지고", "뭐 샀", "뭘 샀", "뭐 탔", "뭐 담았", "보유 종목", "보유종목", "포트", "뭐 물렸", "what are you holding", "what do you hold", "what did you buy", "your bags", "portfolio", "positions", "何持って", "なに持って", "何買った", "ポートフォリオ", "ポジ" };
    private static readonly string[] ProfitAsk = { "수익 어때", "수익률 어때", "다들 수익", "다들 벌", "벌었냐", "벌었음?", "벌었어?", "벌고 있", "계좌 어때", "계좌 상태", "얼마 벌", "얼마 잃", "다들 손실", "다들 물렸", "잃었냐", "수익 났냐", "수익 났음?", "how's your pnl", "hows your pnl", "anyone green", "anyone up", "anyone red", "you guys up", "making money", "how much did you make", "how are you doing today", "みんな儲", "儲かってる", "勝ってる?", "負けてる?", "収支" };
    private static readonly string[] WhyWords = { "왜", "이유", "근거", "why", "how come", "reason", "なぜ", "なんで", "どうして", "理由", "根拠" };
    private static readonly string[] BackWords = { "왜", "이유", "근거", "진짜", "ㄹㅇ", "정말", "레알", "그래서", "어째서", "why", "really", "for real", "seriously", "how so", "and then", "so what", "なぜ", "なんで", "マジ", "本当", "ほんと", "それで", "根拠" };
    private static readonly string[] HowAsk = { "어떻게 해", "어떻게 하", "어케 해", "어케 함", "방법", "하는 법", "하는법", "뭔 뜻", "무슨 뜻", "뜻이 뭐", "알려줘", "알려 줘", "알려주", "가르쳐", "how do i", "how to", "what does", "explain", "teach me", "どうやって", "やり方", "って何", "教えて" };
    // ── 맞장구와 짧은 대꾸 ──
    private static readonly string[] AgreeWords = { "ㅇㅇ", "ㅇㅈ", "ㄹㅇ", "인정", "맞아", "맞네", "맞지", "맞음", "그치", "그니까", "그러게", "그렇지", "ㅇㅋ", "오키", "오케이", "굿", "좋아", "좋지", "콜", "당연", "yeah", "yep", "yes", "true", "agreed", "right", "ok", "okay", "sure", "fr", "exactly", "うん", "そう", "それな", "たしかに", "確かに", "おけ", "了解", "はい" };
    private static readonly string[] DisagreeWords = { "ㄴㄴ", "아닌데", "아니야", "아님", "아니", "노노", "별로", "싫어", "싫음", "안 해", "안해", "nope", "nah", "no", "no way", "disagree", "not really", "いや", "違う", "ちがう", "ないわ", "いいえ" };
    private static readonly string[] DunnoWords = { "몰라", "모름", "모르겠", "글쎄", "몰겠", "모르지", "몰루", "idk", "dunno", "no idea", "not sure", "who knows", "わからん", "分からん", "知らん", "さあ", "わかんない" };
    private static readonly string[] SurpriseWords = { "헐", "대박", "미쳤", "우와", "오오", "실화", "ㄷㄷ", "ㅁㅊ", "세상에", "와씨", "와 ", "wow", "omg", "holy", "wtf", "insane", "damn", "マジか", "やば", "うお", "えぐ", "すげ" };
    // ── 묻는 낱말 ──
    private static readonly string[] QuestionWords = { "뭐", "뭘", "왜", "어디", "언제", "누가", "누구", "어떻게", "어케", "몇", "얼마", "무슨", "뭔", "있냐", "없냐", "있나", "없나", "인가", "런가", "던가", "는가", "할까", "될까", "을까", "what", "why", "who", "how", "when", "where", "which", "anyone", "anybody", "any", "is it", "are you", "do you", "can i", "should i", "何", "なに", "なぜ", "なんで", "どう", "誰", "いつ", "どこ", "かな", "ですか", "ますか" };
    // ── 시답잖은 얘기의 갈래 ──
    private static readonly (string key, string[] words)[] Topics =
    {
        ("who", new[] { "봇이", "봇임", "봇이냐", "봇이지", "봇 아니", "사람이냐", "사람임", "사람 맞", "ai냐", "ai임", "몇 살", "몇살", "어디 살", "어디 사", "직업", "뭐 하는 사람", "누구세요", "누구임", "누구냐", "are you a bot", "are you bots", "are you real", "are you human", "how old", "where are you from", "what do you do", "ボット", "人間?", "何歳", "どこ住み", "何者" }),
        ("doing", new[] { "뭐해", "뭐함", "뭐 해", "뭐 함", "뭐하냐", "뭐 하냐", "뭐 하는 중", "뭐하는 중", "뭐하고", "what are you doing", "what are you up to", "wyd", "what's up", "whats up", "sup", "何してる", "なにしてる", "何してん" }),
        ("time", new[] { "몇 시", "몇시", "언제 열", "언제 닫", "장 언제", "언제 끝", "마감 언제", "개장 언제", "what time", "when does", "when is the", "market open", "market close", "何時", "いつ開", "いつ終", "引けは" }),
        ("food", new[] { "밥", "점심", "저녁", "아침 먹", "야식", "치킨", "라면", "배고", "뭐 먹", "뭐먹", "먹을까", "커피", "피자", "국밥", "삼겹", "떡볶이", "짜장", "햄버거", "간식", "lunch", "dinner", "breakfast", "hungry", "eat", "eating", "food", "coffee", "pizza", "burger", "snack", "ご飯", "昼飯", "夕飯", "腹減", "ラーメン", "コーヒー", "飯食", "何食" }),
        ("drink", new[] { "술", "맥주", "소주", "한잔", "한 잔", "막걸리", "beer", "drink", "drinks", "drunk", "whiskey", "酒", "ビール", "飲み", "ハイボール" }),
        ("sleep", new[] { "졸려", "졸리", "졸림", "피곤", "잠 와", "잠온", "잠와", "자고 싶", "못 잤", "못잤", "밤샘", "밤샜", "눈 감", "sleepy", "tired", "exhausted", "no sleep", "all nighter", "眠い", "眠たい", "疲れ", "徹夜", "寝不足" }),
        ("weather", new[] { "날씨", "비 오", "비온", "비 온", "눈 오", "눈온", "덥다", "더워", "춥다", "추워", "미세먼지", "장마", "weather", "raining", "rainy", "snowing", "cold out", "hot out", "freezing", "天気", "雨", "暑い", "寒い", "雪" }),
        ("bored", new[] { "심심", "노잼", "조용하", "조용~", "조용 ", "한산", "지루", "할 거 없", "할거 없", "잠잠", "bored", "boring", "quiet", "dead chat", "so slow", "暇", "退屈", "静か", "過疎" }),
        ("work", new[] { "회사", "출근", "퇴사", "월급", "사장", "부장", "팀장", "야근", "직장", "알바", "백수", "회의", "상사", "work", "job", "boss", "salary", "paycheck", "office", "meeting", "overtime", "会社", "出勤", "給料", "上司", "残業", "仕事", "無職", "会議" }),
        ("rich", new[] { "부자", "은퇴", "람보", "건물주", "집 사", "차 사", "경제적 자유", "졸업", "파이어", "일확천금", "rich", "retire", "lambo", "millionaire", "financial freedom", "金持ち", "引退", "億り人", "一攫千金" }),
        ("love", new[] { "여친", "남친", "결혼", "연애", "와이프", "마누라", "남편", "소개팅", "솔로", "썸", "girlfriend", "boyfriend", "wife", "husband", "married", "single", "dating", "彼女", "彼氏", "結婚", "嫁", "独身", "恋愛" }),
        ("fun", new[] { "게임", "영화", "넷플", "유튜브", "드라마", "축구", "야구", "노래", "웹툰", "만화", "game", "gaming", "movie", "netflix", "youtube", "football", "soccer", "baseball", "music", "ゲーム", "映画", "アニメ", "野球", "サッカー", "漫画" }),
        ("newbie", new[] { "뉴비", "초보", "주린이", "입문", "시작했", "처음 해", "처음이", "처음 왔", "오늘 처음", "newbie", "noobie", "new here", "beginner", "first time", "just started", "初心者", "初めて", "始めたばかり", "新参" }),
        ("praise", new[] { "고수", "잘한다", "잘하네", "대단", "멋지", "멋있", "존경", "천재", "똑똑", "형님", "genius", "pro", "respect", "nice one", "legend", "smart", "well played", "すごい", "天才", "さすが", "神", "尊敬" }),
        ("luck", new[] { "로또", "운이", "운빨", "기도", "제발", "물 떠놓고", "행운", "lottery", "luck", "lucky", "pray", "praying", "please god", "fingers crossed", "宝くじ", "運", "祈", "頼む", "神頼み" }),
        ("health", new[] { "아프", "아파", "감기", "병원", "두통", "허리", "몸살", "sick", "headache", "hospital", "my back", "a cold", "風邪", "病院", "頭痛", "腰が", "体調" }),
        ("home", new[] { "엄마", "아빠", "부모님", "집에", "애기", "애가", "강아지", "고양이", "댕댕", "냥이", "mom", "dad", "my kid", "kids", "dog", "cat", "at home", "親", "子供", "犬", "猫", "実家" }),
        ("angry", new[] { "짜증", "빡치", "빡침", "열받", "화나", "화남", "멘붕", "현타", "어이없", "annoyed", "annoying", "pissed", "tilted", "mad", "furious", "ムカつく", "イライラ", "萎え", "腹立つ" }),
        ("happy", new[] { "기분 좋", "기분좋", "행복", "신난", "신나", "개꿀", "꿀잼", "좋은 날", "feeling good", "happy", "lets go", "let's go", "great day", "嬉しい", "最高", "気分いい", "楽しい" }),
    };

    private static bool IsLatin(char c) => c >= 'a' && c <= 'z' || c >= '0' && c <= '9';
    /// <summary>낱말이 들어 있는가. 영문 낱말은 다른 낱말 속에 묻혀 있으면(show 속의 how) 치지 않는다. 글은 소문자여야 한다.</summary>
    private static bool HasWord(string lower, string[] words)
    {
        foreach (string word in words)
        {
            int at = lower.IndexOf(word, StringComparison.Ordinal);
            while (at >= 0)
            {
                int end = at + word.Length;
                bool left = !IsLatin(word[0]) || at == 0 || !IsLatin(lower[at - 1]);
                bool right = !IsLatin(word[word.Length - 1]) || end >= lower.Length || !IsLatin(lower[end]);
                if (left && right) { return true; }
                at = end < lower.Length ? lower.IndexOf(word, at + 1, StringComparison.Ordinal) : -1;
            }
        }
        return false;
    }
    private static string TopicOf(string lower)
    {
        foreach (var topic in Topics) { if (HasWord(lower, topic.words)) { return topic.key; } }
        return null;
    }

    private static bool Has(string text, string[] words)
    {
        foreach (string word in words) { if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) { return true; } }
        return false;
    }
    private static string Squeeze(string text) => (text ?? "").Replace(" ", "").Replace("　", "").ToLowerInvariant();

    /// <summary>
    /// 이름으로 종목을 찾는다. 화면에 보이는 이름(지금 언어), 원래 이름, 종목 기호 가운데 어느 것이든 된다.
    /// 다 쓰지 않아도 된다: "노보"처럼 앞머리 두 글자 이상이면 그 종목으로 알아듣는다. 여럿이 걸리면 가장 길게 겹치는 종목을 고른다.
    /// </summary>
    public static Stock FindStock(string words, bool allowShort)
    {
        var manager = StockManager.Instance; string text = Squeeze(words);
        if (manager == null || text.Length == 0) { return null; }
        Stock best = null; int bestLength = 0;
        foreach (var stock in manager.stocks)
        {
            if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
            foreach (string raw in new[] { GameLocale.Text(stock.stockName), stock.stockName, stock.symbol })
            {
                string name = Squeeze(raw);
                if (name.Length < 2) { continue; }
                // 이름이 통째로 들어 있으면 그만큼, 아니면 이름의 앞머리가 글 속의 낱말로 쓰였으면 그 길이만큼 겹친 것으로 친다.
                int length = text.Contains(name) ? name.Length + 100 : 0;
                if (length == 0 && allowShort)
                {
                    for (int size = Math.Min(name.Length - 1, 8); size >= 2; size--)
                    { if (text.Contains(name.Substring(0, size))) { length = size; break; } }
                }
                if (length > bestLength) { bestLength = length; best = stock; }
            }
            // 줄임말(산전, 그푸, Nobo, ノボ …)로 써도 알아듣는다.
            foreach (string nickname in StockNicknames.All(stock.symbol))
            {
                string name = Squeeze(nickname);
                if (name.Length >= 2 && Said(words, text, name) && name.Length + 50 > bestLength) { bestLength = name.Length + 50; best = stock; }
            }
        }
        return best;
    }

    /// <summary>줄임말이 글에 쓰였는가. 짧은 영문(LC, SSE)은 다른 낱말 속에 묻혀 있으면(welcome) 치지 않는다.</summary>
    private static bool Said(string raw, string squeezed, string name)
    {
        bool latin = true; foreach (char c in name) { if (c > 0x7F) { latin = false; break; } }
        if (!latin || name.Length >= 5) { return squeezed.Contains(name); }
        return Regex.IsMatch(raw ?? "", @"(?<![A-Za-z0-9])" + Regex.Escape(name) + @"(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
    /// <summary>글 한 줄에서 뜻을 읽는다. 여러 뜻이 겹치면 더 무거운 쪽을 고른다: 명령 > 시비 > 자랑·한탄 > 종목 묻기 > 소식·세력·시장 묻기 > 주장 > 인사치레 > 종목 얘기 > 잡담 > 맞장구 > 웃음 > 그 밖의 물음 > 그 밖의 말.</summary>
    public static PlayerTalk Read(string text)
    {
        var talk = new PlayerTalk { Percent = float.NaN, Kind = PlayerTalkKind.Other };
        string line = (text ?? "").Trim(); string lower = line.ToLowerInvariant();
        if (line.Length == 0) { return talk; }
        foreach (string command in new[] { "!분석", "！분석", "!analyze", "!analyse", "!analysis", "!分析", "！分析" })
        {
            if (!lower.StartsWith(command, StringComparison.Ordinal)) { continue; }
            talk.Kind = PlayerTalkKind.Analyze; talk.Argument = line.Substring(command.Length).Trim(); talk.Stock = FindStock(talk.Argument, true);
            return talk;
        }
        talk.CallsAll = Has(lower, All); talk.Laughs = LaughPattern.IsMatch(line); talk.Cries = CryPattern.IsMatch(line);
        talk.Asks = line.Contains("?") || line.Contains("？") || lower.EndsWith("냐") || lower.EndsWith("까") || lower.EndsWith("나요") || lower.EndsWith("ㅁ?");
        // 종목은 먼저 온전한 이름으로 찾고, 없으면 앞머리로 찾는다(세 글자 이상 겹칠 때만: "미래에는" 같은 말에 걸리지 않게).
        talk.Stock = FindStock(line, false);
        if (talk.Stock == null) { var loose = FindStock(line, true); if (loose != null && Overlap(line, loose) >= 3) { talk.Stock = loose; } }
        var match = PercentPattern.Match(line);
        if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float percent)) { talk.Percent = percent; }

        bool loss = Has(lower, LossWords) || !float.IsNaN(talk.Percent) && talk.Percent < 0f, gain = Has(lower, BragWords) || !float.IsNaN(talk.Percent) && talk.Percent > 0f && !talk.Asks;
        talk.Rudeness = RudenessOf(line, out talk.RudeFamily, out talk.RudeDeath);
        talk.Wonders = talk.Asks || HasWord(lower, QuestionWords);
        talk.Night = HasWord(lower, NightWords);
        bool brief = line.Length <= 14;
        talk.AsksBack = brief && talk.Stock == null && (HasWord(lower, WhyWords) || talk.Asks && HasWord(lower, BackWords));
        if (talk.Rudeness > 0) { talk.Kind = PlayerTalkKind.Insult; }
        else if (Has(lower, WhaleWords)) { talk.Kind = PlayerTalkKind.Whale; }
        else if (HasWord(lower, ProfitAsk)) { talk.Kind = PlayerTalkKind.AskProfit; }
        else if (loss && !talk.Asks) { talk.Kind = PlayerTalkKind.Lament; }
        else if (gain && !talk.Asks) { talk.Kind = PlayerTalkKind.Brag; }
        else if (Has(lower, PickAsk) && talk.Stock == null) { talk.Kind = PlayerTalkKind.AskPick; }
        else if (talk.Stock != null && Has(lower, SellAsk)) { talk.Kind = PlayerTalkKind.AskSell; }
        else if (talk.Stock != null && Has(lower, BuyAsk)) { talk.Kind = PlayerTalkKind.AskBuy; }
        else if (HasWord(lower, NewsAsk) && !Has(lower, Thank)) { talk.Kind = PlayerTalkKind.AskNews; }
        else if (HasWord(lower, WhaleAsk)) { talk.Kind = PlayerTalkKind.AskWhale; }
        else if (talk.Stock != null && HasWord(lower, WhyWords)) { talk.Kind = PlayerTalkKind.AskWhy; }
        else if (talk.Stock != null && (Has(lower, OpinionAsk) || talk.Asks)) { talk.Kind = PlayerTalkKind.AskOpinion; }
        else if (talk.Stock != null && Has(lower, DownWords)) { talk.Kind = PlayerTalkKind.ClaimDown; }
        else if (talk.Stock != null && Has(lower, UpWords)) { talk.Kind = PlayerTalkKind.ClaimUp; }
        else if (HasWord(lower, HoldAsk)) { talk.Kind = PlayerTalkKind.AskHoldings; }
        else if (HasWord(lower, HotAsk)) { talk.Kind = PlayerTalkKind.AskHot; }
        else if (HasWord(lower, MarketAsk)) { talk.Kind = PlayerTalkKind.AskMarket; }
        else if (Has(lower, Thank)) { talk.Kind = PlayerTalkKind.Thanks; }
        else if (Has(lower, Sorry)) { talk.Kind = PlayerTalkKind.Apology; }
        else if (Has(lower + " ", Greet)) { talk.Kind = PlayerTalkKind.Greeting; }
        else if (Has(lower, Farewell)) { talk.Kind = PlayerTalkKind.Bye; }
        // 종목 이름만 꺼낸 말("산삼전자 보는 중")은 묻는 말이 아니라 그 종목 얘기다.
        else if (talk.Stock != null) { talk.Kind = PlayerTalkKind.Mention; }
        else if (HasWord(lower, HowAsk)) { talk.Kind = PlayerTalkKind.AskHow; }
        else if ((talk.Topic = TopicOf(lower)) != null) { talk.Kind = PlayerTalkKind.Topic; }
        else if (brief && HasWord(lower, DunnoWords)) { talk.Kind = PlayerTalkKind.Dunno; }
        else if (brief && !talk.Asks && HasWord(lower, AgreeWords)) { talk.Kind = PlayerTalkKind.Agree; }
        else if (brief && !talk.Asks && HasWord(lower, DisagreeWords)) { talk.Kind = PlayerTalkKind.Disagree; }
        else if (brief && HasWord(lower + " ", SurpriseWords)) { talk.Kind = PlayerTalkKind.Surprise; }
        else if (talk.Cries) { talk.Kind = PlayerTalkKind.Sad; }
        else if (talk.Laughs) { talk.Kind = PlayerTalkKind.Laugh; }
        else if (talk.Wonders) { talk.Kind = PlayerTalkKind.Question; }
        else if (talk.CallsAll) { talk.Kind = PlayerTalkKind.CallAll; }
        else { talk.Kind = PlayerTalkKind.Statement; }
        return talk;
    }
    private static int Overlap(string words, Stock stock)
    {
        string text = Squeeze(words); int best = 0;
        foreach (string raw in new[] { GameLocale.Text(stock.stockName), stock.stockName, stock.symbol })
        {
            string name = Squeeze(raw);
            for (int size = Math.Min(name.Length, 12); size >= 2; size--) { if (text.Contains(name.Substring(0, size))) { best = Math.Max(best, size); break; } }
        }
        return best;
    }
}

[Serializable] public sealed class PlayerTalkGroup { public string key; public string[] texts; }
[Serializable] public sealed class PlayerTalkLocale { public int schemaVersion = 1; public PlayerTalkGroup[] groups; [NonSerialized] public Dictionary<string, string[]> index; }

/// <summary>플레이어의 말에 봇이 답하는 대사. 언어마다 따로 쓴 파일(Chat/player_talk.ko/en/ja)에서 온다.</summary>
public static class PlayerTalkAssets
{
    private static readonly string[] None = Array.Empty<string>();
    private static Dictionary<string, PlayerTalkLocale> locales;
    private static bool failed;
    public static bool Ready
    {
        get
        {
            if (locales == null && !failed)
            {
                var loaded = new Dictionary<string, PlayerTalkLocale>(StringComparer.Ordinal);
                foreach (string code in ChatHypeAssets.Languages)
                {
                    var asset = Resources.Load<TextAsset>("Chat/player_talk." + code);
                    var locale = asset == null ? null : JsonUtility.FromJson<PlayerTalkLocale>(asset.text);
                    if (locale == null || locale.groups == null) { continue; }
                    locale.index = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (var group in locale.groups) { if (group != null && !string.IsNullOrEmpty(group.key) && group.texts != null && group.texts.Length > 0) { locale.index[group.key] = group.texts; } }
                    loaded[code] = locale;
                }
                if (loaded.Count == 0) { failed = true; Debug.LogWarning("플레이어 채팅 답글을 끕니다: player_talk 파일이 없습니다."); } else { locales = loaded; }
            }
            return locales != null;
        }
    }
    /// <summary>"ask.bull.hostile"이 없으면 "ask.bull", 그것도 없으면 "ask"를 쓴다.</summary>
    public static string[] Lines(string key)
    {
        if (!Ready) { return None; }
        var locale = locales.TryGetValue(GameLocale.Code, out var current) ? current : locales.TryGetValue("ko", out current) ? current : null;
        if (locale == null) { foreach (var any in locales.Values) { locale = any; break; } }
        while (locale != null && key.Length > 0)
        {
            if (locale.index.TryGetValue(key, out var texts)) { return texts; }
            int cut = key.LastIndexOf('.');
            if (cut < 0) { break; }
            key = key.Substring(0, cut);
        }
        return None;
    }
}

public sealed partial class InvestorSociety
{
    public const string AnalystName = "종목 분석 AI";
    private const string PlayerTemplate = "PLAYER.CHAT";
    /// <summary>플레이어의 "오른다·내린다"를 이만큼(초) 뒤에 따진다. 그 사이 2% 넘게 그 방향으로 갔으면 맞은 것, 반대로 2% 넘게 갔으면 틀린 것.</summary>
    private const float CallMinSec = 90f, CallMaxSec = 150f; private const decimal CallMove = .02m;
    private sealed class TalkReply { public double due; public int room; public string key; public Stock stock; public long replyTo; public int onlyBot, notBot; public float percent; public double wait; public string said; }
    private sealed class PlayerCall { public Stock stock; public decimal price; public bool up; public double due; public int room; public long messageId; }
    private sealed class AnalysisJob { public double due; public int room; public Stock stock; public string argument; public long replyTo; }
    private readonly List<TalkReply> talkReplies = new();
    private readonly List<PlayerCall> playerCalls = new();
    private readonly List<AnalysisJob> analysisJobs = new();
    private readonly Queue<(string text, double at)> playerRecent = new();
    private double talkNextReply, analysisNextRequest = -1, analysisBotRest;
    /// <summary>확인용: 최근에 읽은 뜻과 한 답.</summary>
    public readonly Queue<string> PlayerTalkTrace = new(64);
    public int PlayerLines { get; private set; }
    public int PlayerReplies { get; private set; }
    public int AnalysesPosted { get; private set; }
    private void TraceTalk(string line) { PlayerTalkTrace.Enqueue(line); while (PlayerTalkTrace.Count > 60) { PlayerTalkTrace.Dequeue(); } }
    private static string TalkWord(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>플레이어가 이 방에 글을 쓸 수 있는가: 공개방, 들어가 있는 새벽 관찰실, 열어 둔 내 리딩방.</summary>
    public bool PlayerCanWrite(int room)
    {
        if (day == null || !SocialReady) { return false; }
        if (room == 0) { return true; }
        if (room == NpcLeadingRoom.RoomId) { return NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined; }
        return IsRoomOpen(room);
    }

    private float chatBarCheck;
    /// <summary>채팅 화면에 글 쓰는 칸과 새벽 관찰실 잠금 화면을 붙인다(화면이 만들어진 뒤 한 번).</summary>
    private void EnsureChatBar()
    {
        if (Time.unscaledTime < chatBarCheck) { return; }
        chatBarCheck = Time.unscaledTime + 2f;
        foreach (var chat in FindObjectsByType<TabletGroupChatUI>(FindObjectsInactive.Include))
        { if (chat.scroll != null && chat.GetComponent<PlayerChatBar>() == null) { chat.gameObject.AddComponent<PlayerChatBar>(); } }
    }

    // ── 올리기 ──
    private long PostPlayer(int room, string text, ChatProfitCard card)
    {
        var social = new SocialMessage { id = ChatSocialIds.Next(), senderId = 0, roomId = room, at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = SocialAct.REACT, templateKey = PlayerTemplate,
            text = text, facts = new SocialFacts(), profitCard = card, debug = new SocialMessageDebug { trigger = "PLAYER", weights = Array.Empty<SocialWeight>(), slots = new SocialFacts() } };
        if (room == 0) { return day.PublishPlayerMessage(text, PlayerTemplate, social); }
        var target = GetRoom(room);
        target?.Add(new BotChatMessage(target.Version + 1, null, day.Day, day.GameMinute, text, PlayerTemplate, ChatEvent.Agreement, new InvestorMessageContext(0, room, 0, 1, 0, false), social, fromPlayer: true));
        return social.id;
    }
    private long PostAnalyst(int room, string text, ChatAnalysisCard card, long replyTo)
    {
        var social = new SocialMessage { id = ChatSocialIds.Next(), senderId = 0, roomId = room, replyToId = replyTo, at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = SocialAct.REACT,
            templateKey = TabletGroupChatUI.AnalysisTemplate, text = text, facts = new SocialFacts { symbol = card?.symbol, stockName = card?.stockName }, analysisCard = card,
            debug = new SocialMessageDebug { trigger = "ANALYST", weights = Array.Empty<SocialWeight>(), slots = new SocialFacts() } };
        if (room == 0) { return day.PublishSpeaker(TalkWord(AnalystName, "Stock Analysis AI", "銘柄分析AI"), text, TabletGroupChatUI.AnalysisTemplate, social); }
        var target = GetRoom(room);
        target?.Add(new BotChatMessage(target.Version + 1, null, day.Day, day.GameMinute, text, TabletGroupChatUI.AnalysisTemplate, ChatEvent.Agreement, new InvestorMessageContext(0, room, 0, 1, 0, false), social,
            speaker: TalkWord(AnalystName, "Stock Analysis AI", "銘柄分析AI")));
        return social.id;
    }
    /// <summary>봇의 한 줄을 그 방에 올린다. 표시만 한다(방의 대화 기억·난수열에 넣지 않는다).</summary>
    private long PostBotLine(TradeBot bot, int room, string text, string key, Stock stock, long replyTo)
    {
        var message = new SocialMessage { id = ++hypeMessageId, roomId = room, senderId = bot.Social.Psychology.SocialId, at = SocialTime.GameMinute, realAt = SocialTime.Seconds,
            act = SocialAct.REACT, replyToId = replyTo, topicEvent = "PLAYER_TALK", templateKey = key, text = text,
            facts = new SocialFacts { symbol = stock?.symbol, stockName = stock?.stockName, at = SocialTime.GameMinute }, debug = new SocialMessageDebug { trigger = "PLAYER_TALK" } };
        bool was = breathDelivering; breathDelivering = true;
        try { DeliverSocialMessage(message); } finally { breathDelivering = was; }
        return message.id;
    }

    // ── 플레이어가 쓴 말 ──
    /// <summary>플레이어가 채팅에 한 줄을 썼다. 쓸 수 없는 방이면 false.</summary>
    public bool SubmitPlayerChat(int room, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || !PlayerCanWrite(room)) { return false; }
        if (text.Length > 80) { text = text.Substring(0, 80); }
        double now = SocialTime.Seconds;
        var talk = PlayerTalk.Read(text);
        var account = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        // 같은 말을 20초 안에 또 쓰면 도배다. 글은 올라가지만 봇들은 한 번 핀잔을 주고 무시한다.
        bool spam = false; foreach (var recent in playerRecent) { if (recent.text == text && now - recent.at < 20) { spam = true; break; } }
        playerRecent.Enqueue((text, now)); while (playerRecent.Count > 6) { playerRecent.Dequeue(); }

        // 수익 자랑·손실 한탄은 내 실제 계좌와 맞춰 본다. 맞으면 봇들과 같은 인증 카드가 붙는다.
        ChatProfitCard card = null; decimal actual = 0m; bool held = false;
        if (talk.Kind == PlayerTalkKind.Brag || talk.Kind == PlayerTalkKind.Lament)
        {
            var subject = talk.Stock ?? BestHolding(account, talk.Kind == PlayerTalkKind.Brag);
            if (subject != null && account != null && account.GetHolding(subject) >= 1m && account.GetAvgPrice(subject) > 0m)
            {
                held = true; talk.Stock = subject;
                decimal average = account.GetAvgPrice(subject), quantity = account.GetHolding(subject);
                actual = (subject.currentPrice / average - 1m) * 100m;
                if (talk.Kind == PlayerTalkKind.Brag ? actual > 0m : actual < 0m)
                {
                    card = new ChatProfitCard { kind = ChatProfitCardKind.Holding, symbol = subject.symbol, stockName = subject.stockName, amount = (subject.currentPrice - average) * quantity,
                        percent = actual, averageCost = average, price = subject.currentPrice, quantity = quantity, capturedAt = SocialTime.GameMinute };
                }
            }
        }
        long id = PostPlayer(room, text, card);
        PlayerLines++;
        TraceTalk("나: " + text + "  → " + talk.Kind + (talk.Stock != null ? " " + talk.Stock.symbol : "") + (float.IsNaN(talk.Percent) ? "" : " " + talk.Percent.ToString("0.#") + "%") + (card != null ? " 인증 " + actual.ToString("+0.0;-0.0") + "%" : "")
            + (talk.CallsAll ? " 부름" : "") + (talk.Laughs ? " 웃음" : "") + (spam ? " 도배" : ""));
        if (spam) { Plan(room, "spam", null, id, now + 1.2, 0, 1, .7f); return true; }

        // 짧은 말은 봇이 되받아 읊기도 한다("치킨? ㅋㅋ").
        talkSaid = text.Length <= 12 && text.IndexOf('{') < 0 && text.IndexOf('@') < 0 ? text : null;
        // 방금 나와 말을 섞은 봇이 있으면 그 봇이 먼저 받는다. 봇이 던진 물음에 내가 답했으면 물은 봇이 받는다.
        var partner = TalkPartnerIn(room, now); int near = partner == null ? 0 : partner.bot;
        if (HearPlayerAnswer(room, talk, id, now, ref near)) { return true; }
        if (partner != null && talk.AsksBack && talk.Kind != PlayerTalkKind.Insult && talk.Kind != PlayerTalkKind.Analyze)
        {
            // "왜?" "진짜?": 방금 말한 봇이 자기 말을 풀어 준다.
            Plan(room, "follow", partner.stock, id, now + 1.1, near, 1, 1f, sure: true);
            return true;
        }

        switch (talk.Kind)
        {
            case PlayerTalkKind.Analyze:
                if (room != NpcLeadingRoom.RoomId) { Plan(room, "aiOnly", null, id, now + 1.4, 0, 1, .9f, sure: true); break; }
                analysisJobs.Add(new AnalysisJob { due = now + 1.2 + HypeRandom.Next(), room = room, stock = talk.Stock, argument = talk.Argument, replyTo = id });
                break;
            case PlayerTalkKind.Insult:
            {
                // 막말은 셀수록, 되풀이할수록 반응이 커진다: 얕보는 말 < 욕설 < 선을 넘은 말(가족·죽으라는 말).
                int level = Mathf.Clamp(talk.Rudeness, 1, 3);
                while (rudeAt.Count > 0 && now - rudeAt.Peek() > 150) { rudeAt.Dequeue(); }
                rudeAt.Enqueue(now); bool again = rudeAt.Count >= 3;
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-2f * level, (HypeRandom.Next() < .5 ? 6f + mind.Traits.Grudge / 100f * 10f : 2f) * level, "플레이어의 막말"));
                string scold = level >= 3 ? (talk.RudeFamily ? "insult.family" : talk.RudeDeath ? "insult.death" : "insult.severe") : level == 2 ? "insult.swear" : "insult";
                Plan(room, scold, talk.Stock, id, now + 1.0, 0, level >= 3 ? HypeRandom.Range(3, 6) : level == 2 ? HypeRandom.Range(2, 5) : HypeRandom.Range(1, 4), 1f);
                // 계속 그러면 혀를 차거나 아예 무시하자는 말이 섞인다.
                if (again) { Plan(room, "insult.repeat", null, id, now + 2.2, 0, 2, .8f); }
                TraceTalk("    (막말 세기 " + level + (again ? ", 되풀이 " + rudeAt.Count + "번" : "") + ")");
            }
                break;
            case PlayerTalkKind.Brag:
                if (card != null)
                {
                    float gain = (float)actual;
                    // 인증된 수익: 클수록 믿음이 오른다. 그 종목에 물려 있거나 원한이 많은 봇은 배가 아프다.
                    Hearers(room, (bot, mind) =>
                    {
                        bool sore = bot.GetOwnedVolume(talk.Stock) > 0m && bot.GetAvgPrice(talk.Stock) > talk.Stock.currentPrice || mind.Traits.Grudge >= 70;
                        mind.ReactToPlayerTalk(Mathf.Clamp(gain * 1f, 2f, 25f) * (sore ? .3f : 1f), sore ? Mathf.Clamp(gain * .5f, 2f, 16f) : 0f, "플레이어의 수익 인증 " + gain.ToString("+0.0") + "%");
                    });
                    Plan(room, "brag.proof.praise", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 3), 1f, gain);
                    Plan(room, "brag.proof.envy", talk.Stock, id, now + 2.6, 0, gain >= 10f ? HypeRandom.Range(1, 3) : HypeRandom.Range(0, 2), .8f, gain);
                    Sway(room, talk.Stock, 1.2f);
                }
                else if (held)
                {
                    // 들고는 있는데 손실 중이다. 자랑이 거짓이다.
                    Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-5f, 1f, "플레이어의 거짓 자랑"));
                    Plan(room, "brag.loss", talk.Stock, id, now + 1.4, near, HypeRandom.Range(1, 3), 1f, (float)actual);
                }
                else
                {
                    Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-3f, 0f, "플레이어의 인증 없는 자랑"));
                    Plan(room, "brag.noproof", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), 1f);
                }
                break;
            case PlayerTalkKind.Lament:
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(card != null ? 2f : .5f, 0f, "플레이어의 손실 한탄"));
                Plan(room, "lament.comfort", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .9f, (float)actual, sure: true);
                Plan(room, "lament.mock", talk.Stock, id, now + 2.8, 0, HypeRandom.Range(0, 2), .7f, (float)actual);
                break;
            case PlayerTalkKind.AskBuy: case PlayerTalkKind.AskSell: case PlayerTalkKind.AskOpinion:
                Plan(room, talk.Kind == PlayerTalkKind.AskBuy ? "askBuy" : talk.Kind == PlayerTalkKind.AskSell ? "askSell" : "ask", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 4), .9f, sure: true);
                Sway(room, talk.Stock, .4f);
                break;
            case PlayerTalkKind.AskPick:
                Plan(room, "askPick", null, id, now + 1.4, near, HypeRandom.Range(1, 4), .9f, sure: true);
                break;
            case PlayerTalkKind.ClaimUp: case PlayerTalkKind.ClaimDown:
                bool up = talk.Kind == PlayerTalkKind.ClaimUp;
                Plan(room, up ? "claimUp" : "claimDown", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 4), .9f, sure: true);
                NotePlayerCall(room, talk.Stock, up, id, now);
                if (up) { Sway(room, talk.Stock, 1f); }
                break;
            case PlayerTalkKind.Whale:
                Plan(room, "whale", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 4), .95f, sure: true);
                if (talk.Stock != null) { Sway(room, talk.Stock, .8f); }
                break;
            // ── 소식·시장·세력·수익을 묻는 말: 봇은 자기가 보고 아는 것(공개된 기사, 시세, 자기 계좌)으로 답한다 ──
            case PlayerTalkKind.AskNews:
                Plan(room, talk.Night ? "newsNight" : "news", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .85f, sure: true);
                if (talk.Stock != null) { Sway(room, talk.Stock, .3f); }
                break;
            case PlayerTalkKind.AskMarket:
                Plan(room, "market", null, id, now + 1.3, near, HypeRandom.Range(1, 3), .85f, sure: true);
                break;
            case PlayerTalkKind.AskHot:
                Plan(room, "hot", null, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true);
                break;
            case PlayerTalkKind.AskWhale:
                Plan(room, "whaleTalk", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .85f, sure: true);
                break;
            case PlayerTalkKind.AskHoldings:
                Plan(room, "holdings", null, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true);
                break;
            case PlayerTalkKind.AskProfit:
                Plan(room, "profit", null, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true);
                break;
            case PlayerTalkKind.AskWhy:
                Plan(room, "why", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .85f, sure: true);
                Sway(room, talk.Stock, .3f);
                break;
            case PlayerTalkKind.AskHow:
                Plan(room, "how", null, id, now + 1.4, near, HypeRandom.Range(1, 3), .8f, sure: true);
                break;
            case PlayerTalkKind.Mention:
                Plan(room, "mention", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .8f, sure: true);
                Sway(room, talk.Stock, .5f);
                break;
            // ── 인사치레와 시답잖은 얘기 ──
            case PlayerTalkKind.Thanks:
                Plan(room, "thanks", null, id, now + 1.2, near, 1, 1f, sure: HypeRandom.Next() < .9);
                break;
            case PlayerTalkKind.Apology:
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(0f, -4f, "플레이어의 사과"));
                Plan(room, "apology", null, id, now + 1.2, near, 1, 1f, sure: HypeRandom.Next() < .9);
                break;
            case PlayerTalkKind.Greeting:
                Plan(room, "greet", null, id, now + 1.0, near, HypeRandom.Range(1, 3), .75f, sure: true);
                break;
            case PlayerTalkKind.Bye:
                Plan(room, "bye", null, id, now + 1.0, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.Sad:
                Plan(room, "sad", null, id, now + 1.3, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.Laugh:
                Plan(room, "laugh", null, id, now + 1.0, near, HypeRandom.Range(1, 3), .5f, sure: HypeRandom.Next() < .8);
                break;
            case PlayerTalkKind.CallAll:
                Plan(room, "all", null, id, now + 1.2, near, HypeRandom.Next() < .3 ? 2 : 1, .6f, sure: true);
                break;
            case PlayerTalkKind.Agree:
                Plan(room, "agree", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Disagree:
                Plan(room, "disagree", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Dunno:
                Plan(room, "dunno", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Surprise:
                Plan(room, "surprise", null, id, now + 1.1, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.Topic:
                Plan(room, "topic." + talk.Topic, null, id, now + 1.2, near, HypeRandom.Range(1, 3), .7f, sure: true);
                break;
            case PlayerTalkKind.Question:
                // 무슨 물음인지 몰라도 물었으면 누군가는 받는다.
                Plan(room, "question", null, id, now + 1.3, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            default:
                Plan(room, "statement", null, id, now + 1.4, near, HypeRandom.Range(1, 3), .45f, sure: HypeRandom.Next() < .85);
                break;
        }
        // 웃음·울음이 섞인 말에는 기분에 따라 한마디 더 붙기도 한다.
        if (talk.Kind != PlayerTalkKind.Laugh && talk.Kind != PlayerTalkKind.Insult && talk.Laughs) { Plan(room, "laugh", null, id, now + 2.4, 0, 1, .25f); }
        if (talk.Kind != PlayerTalkKind.Sad && talk.Kind != PlayerTalkKind.Lament && talk.Cries) { Plan(room, "sad", null, id, now + 2.4, 0, 1, .25f); }
        return true;
    }
    private static Stock BestHolding(Trader account, bool winner)
    {
        if (account == null || StockManager.Instance == null) { return null; }
        Stock best = null; decimal bestReturn = 0m;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || account.GetHolding(stock) < 1m || account.GetAvgPrice(stock) <= 0m) { continue; }
            decimal result = stock.currentPrice / account.GetAvgPrice(stock) - 1m;
            if (best == null || (winner ? result > bestReturn : result < bestReturn)) { best = stock; bestReturn = result; }
        }
        return best;
    }
    /// <summary>플레이어를 가리키는 멘션. 언어마다 다르다.</summary>
    public static string YouMention => GameLocale.Code == "ja" ? "@あなた" : GameLocale.Code == "en" ? "@You" : "@당신";
    public static readonly string[] YouMentions = { "@당신", "@You", "@あなた" };
    private readonly Queue<double> rudeAt = new();
    /// <summary>답글을 예약한다. count줄까지, 줄마다 chance의 확률로.</summary>
    private void Plan(int room, string key, Stock stock, long replyTo, double first, int onlyBot, int count, float chance, float percent = float.NaN, bool sure = false, int notBot = 0)
    {
        // onlyBot은 첫 줄을 맡을 봇이다(없으면 아무나). 둘째 줄부터는 다른 봇이 한다. sure면 첫 줄은 꼭 나온다.
        for (int i = 0; i < count; i++)
        {
            if (!(sure && i == 0) && HypeRandom.Next() >= chance) { continue; }
            talkReplies.Add(new TalkReply { due = first + i * (1.1 + HypeRandom.Next() * 1.6), room = room, key = key, stock = stock, replyTo = replyTo, onlyBot = i == 0 ? onlyBot : 0,
                notBot = i == 0 ? notBot : onlyBot != 0 ? onlyBot : notBot, percent = percent, said = talkSaid });
        }
        while (talkReplies.Count > 12) { talkReplies.RemoveAt(0); }
    }
    /// <summary>그 방에서 지금 채팅을 보고 있는 봇들.</summary>
    private void Hearers(int room, Action<TradeBot, InvestorPsychology> act)
    {
        float now = SessionClock.Time;
        foreach (var bot in socialBots)
        {
            if (bot.Social == null || !bot.isActiveAndEnabled || !Present(bot, room) || !bot.Social.Psychology.ActiveAt(now)) { continue; }
            act(bot, bot.Social.Psychology);
        }
    }
    /// <summary>내 말이 눈길을 끈다. 나를 믿는 봇일수록, 잘 휩쓸리는 봇일수록 내가 말한 종목을 들여다본다.</summary>
    private void Sway(int room, Stock stock, float weight)
    {
        if (stock == null || !stock.AcceptsOrders) { return; }
        float now = SessionClock.Time; int turned = 0;
        Hearers(room, (bot, mind) =>
        {
            float chance = (.04f + mind.Trust / 100f * .5f) * (.3f + mind.Traits.Suggestibility / 100f * .7f) * weight;
            if (mind.HasPanic || UnityEngine.Random.value >= chance) { return; }
            bot.Social.InfluencedStock = stock; bot.Social.InfluenceUntil = now + 30f + mind.Trust * .5f; turned++;
        });
        if (turned > 0) { TraceTalk("    (내 말에 눈 돌린 봇 " + turned + ", " + stock.symbol + ")"); }
    }

    // ── 봇의 답 ──
    private void TickPlayerChat()
    {
        if (day == null || !SocialReady) { return; }
        double now = SocialTime.Seconds;
        for (int i = 0; i < analysisJobs.Count;)
        {
            if (analysisJobs[i].due > now) { i++; continue; }
            var job = analysisJobs[i]; analysisJobs.RemoveAt(i); PostAnalysis(job, now);
        }
        if (now >= talkNextReply)
        {
            for (int i = 0; i < talkReplies.Count; i++)
            {
                var reply = talkReplies[i];
                if (reply.due > now || reply.wait > now) { continue; }
                // 말할 봇이 잠깐 없으면 버리지 않고 조금 뒤에 다시 본다. 20초가 지나면 때를 놓친 것이다.
                int spoke = now - reply.due < 20 ? SpeakReply(reply, now) : -1;
                if (spoke == 0) { reply.wait = now + 1.5; continue; }
                talkReplies.RemoveAt(i);
                if (spoke > 0) { talkNextReply = now + .9 + HypeRandom.Next() * .8; }
                break;
            }
        }
        ResolvePlayerCalls(now);
        TickAnalysisRequests(now);
    }
    /// <summary>1 말했다, 0 지금은 말할 봇이 없다(조금 뒤 다시 본다), -1 할 말이 없다(버린다).</summary>
    private int SpeakReply(TalkReply reply, double now)
    {
        if (!PlayerTalkAssets.Ready) { return -1; }
        var bot = PickTalkSpeaker(reply, now);
        if (bot == null) { return 0; }
        var mind = bot.Social.Psychology;
        // 이 봇의 마음: 나를 어떻게 보는가, 그 종목을 어떻게 보는가, 지금 기분이 어떤가.
        string stance = mind.Quadrant == InvestorQuadrant.Hostile || mind.Betrayal >= 40f ? "hostile" : mind.Trust >= 40f ? "friendly" : null;
        string mood = mind.Fear >= 55f ? "scared" : mind.Greed >= 60f ? "happy" : "dry";
        string key = reply.key, news = null; Stock stock = reply.stock; Stock pick = null; decimal mine = 0m;
        bool scold = key.StartsWith("insult", StringComparison.Ordinal);
        if (!scold && mind.Traits.Politeness >= Config.Int("styleFormalMin"))
        {
            // 존댓말을 쓰는 봇은 자기 말투로 짧게 받는다.
            key = stock != null ? "formal.stock" : AsksKey(key) ? "formal.ask" : "formal"; stance = null;
        }
        else if (stock != null && (key == "ask" || key == "askBuy" || key == "askSell" || key == "claimUp" || key == "claimDown" || key == "mention" || key == "answered.stock"))
        {
            decimal believed = bot.BelievedPrice(stock), price = stock.currentPrice;
            bool bull = believed > price * 1.03m, bear = believed < price * .97m;
            key += key == "claimUp" ? (bear ? ".doubt" : ".agree") : key == "claimDown" ? (bull ? ".doubt" : ".agree") : bull ? ".bull" : bear ? ".bear" : ".neutral";
        }
        else if (key == "laugh" || key == "sad") { key += "." + mood; }
        else if (key == "whale") { key += mind.Traits.Suggestibility >= 55 ? ".excited" : ".doubt"; }
        else if (key == "askPick")
        {
            // 이 봇이 지금 가장 싸다고 믿는 종목을 댄다. 없으면 모르겠다고 한다.
            decimal best = 1.03m;
            foreach (var candidate in StockManager.Instance.stocks)
            {
                if (candidate == null || !candidate.AcceptsOrders || candidate.currentPrice <= 0m) { continue; }
                decimal edge = bot.BelievedPrice(candidate) / candidate.currentPrice;
                if (edge > best) { best = edge; pick = candidate; }
            }
            if (pick == null) { key = "askPick.none"; }
        }
        else { ResolveTalkFacts(bot, ref key, ref stock, ref news, ref mine); }
        // 나를 싫어하는 봇은 잡담에 시큰둥하다. 자기가 물어 놓고 그러지는 않는다.
        if (stance == "hostile" && reply.onlyBot == 0 && SoftKey(key) && HypeRandom.Next() < .6) { key = "cold"; stance = null; }
        // 막말에 대한 핀잔은 나를 어떻게 보든 같다.
        if (stance != null && !scold) { key += "." + stance; }
        var subject = stock ?? pick;
        string text = PickHypeText(UsableLines(PlayerTalkAssets.Lines(key), subject != null, news != null, mine > 0m, !string.IsNullOrEmpty(reply.said)), Array.Empty<string>());
        if (text == null) { return -1; }
        var slots = subject != null ? ChartSlots(subject, subject.currentPrice, subject.currentPrice, mine) : HypeSlots(null, 0, 0m);
        if (pick != null) { slots["pick"] = GameLocale.Text(pick.stockName ?? pick.symbol ?? ""); }
        slots["ret"] = float.IsNaN(reply.percent) ? "" : Mathf.Abs(reply.percent).ToString("0");
        slots["news"] = news ?? ""; slots["said"] = reply.said ?? "";
        string line = ChatHypeAssets.Fill(text, slots);
        // 내 글에 다는 답에는 나를 가리키는 멘션을 붙인다(@당신 / @You / @あなた). AI 분석을 보고 하는 말은 내게 하는 말이 아니다.
        if (reply.replyTo > 0 && !key.StartsWith("aiReact", StringComparison.Ordinal)) { line = YouMention + " " + line; }
        PostBotLine(bot, reply.room, line, "talk." + key, subject, reply.replyTo);
        hypeSpokeAt[bot.Social.Id] = now; PlayerReplies++;
        if (reply.replyTo > 0 && !key.StartsWith("aiReact", StringComparison.Ordinal)) { NoteTalkPartner(reply.room, bot, subject, now); }
        // 말을 섞은 봇은 나와 조금 가까워진다(시비와 조롱은 빼고).
        if (!scold && !key.Contains("mock") && !key.Contains("hostile") && key != "cold") { mind.ReactToPlayerTalk(1.5f, 0f, "플레이어와 대화"); }
        TraceTalk("    > " + bot.Social.Nickname + ": " + line + "  [" + key + "]");
        return 1;
    }
    private void NotePlayerCall(int room, Stock stock, bool up, long id, double now)
    {
        if (stock == null || playerCalls.Count >= 3) { return; }
        playerCalls.Add(new PlayerCall { stock = stock, price = stock.currentPrice, up = up, due = now + CallMinSec + HypeRandom.Next() * (CallMaxSec - CallMinSec), room = room, messageId = id });
    }
    private void ResolvePlayerCalls(double now)
    {
        for (int i = playerCalls.Count - 1; i >= 0; i--)
        {
            var call = playerCalls[i];
            if (now < call.due) { continue; }
            playerCalls.RemoveAt(i);
            if (!call.stock.AcceptsOrders || call.price <= 0m) { continue; }
            decimal move = call.stock.currentPrice / call.price - 1m;
            bool right = call.up ? move >= CallMove : move <= -CallMove, wrong = call.up ? move <= -CallMove : move >= CallMove;
            if (!right && !wrong) { continue; }
            // 맞으면 믿음이, 틀리면 원망이 쌓인다. 그 말을 들었던 방의 봇들에게.
            Hearers(call.room, (bot, mind) => mind.ReactToPlayerTalk(right ? 5f + mind.Credulity * 6f : -2f, right ? 0f : 3f + mind.Traits.Grudge / 100f * 5f, right ? "플레이어의 예측 적중" : "플레이어의 예측 빗나감"));
            TraceTalk("    (예측 " + (right ? "적중" : "빗나감") + ": " + call.stock.symbol + " " + (call.up ? "오른다" : "내린다") + ", " + (move * 100m).ToString("+0.0;-0.0") + "%)");
            Plan(call.room, right ? "callRight" : "callWrong", call.stock, call.messageId, now + .5, 0, HypeRandom.Range(1, 3), .9f, (float)(move * 100m));
        }
    }

    // ── 종목 분석 AI ──
    /// <summary>
    /// 매수 추천 점수(0~100). 공개된 것만 본다: 최근 1분의 움직임, 당일 고가·저가 사이의 자리, 호가창의 벽, 거래가 평소보다 붙었는지.
    /// 오르는 중이고 받치는 벽이 있으면 높고, 고가 근처에서 힘이 빠지거나 매도벽에 눌리면 낮다.
    /// </summary>
    public ChatAnalysisCard Analyze(Stock stock, out string reason)
    {
        reason = "";
        if (stock == null || stock.currentPrice <= 0m) { return null; }
        decimal price = stock.currentPrice, open = price, high = price, low = price;
        if (StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyCandle(stock, out var candle) && candle.open > 0f)
        { open = (decimal)candle.open; high = Math.Max(price, (decimal)candle.high); low = Math.Min(price, (decimal)candle.low); }
        float momentum = 0f;
        if (chartTracks.TryGetValue(stock, out var track) && PriceAgo(track, SocialTime.Seconds, 60, out decimal before) && before > 0m) { momentum = (float)(price / before) - 1f; }
        float position = high > low ? (float)((price - low) / (high - low)) : .5f;
        float lean = ManipulationTools.Walls(stock).Lean;
        float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
        float score = 50f + 22f * (float)Math.Tanh(momentum * 25f) + 10f * lean;
        if (heat >= 3f) { score += momentum > 0f ? 8f : momentum < 0f ? -8f : 0f; }
        if (position > .9f) { score -= (position - .9f) * 140f * (momentum <= 0f ? 1f : .3f); }
        if (position < .15f) { score += (.15f - position) / .15f * 12f * (momentum >= 0f ? 1f : -.3f); }
        int final = Mathf.Clamp(Mathf.RoundToInt(score), 3, 97);
        // 가장 크게 작용한 까닭 하나를 말로.
        string move = (momentum * 100f).ToString("+0.0;-0.0");
        reason = Mathf.Abs(momentum) >= .01f
            ? (momentum > 0f ? TalkWord("최근 1분 " + move + "%로 오르는 중입니다.", "Up " + move + "% over the last minute.", "直近1分で" + move + "%上昇中です。")
                : TalkWord("최근 1분 " + move + "%로 밀리는 중입니다.", "Down " + move + "% over the last minute.", "直近1分で" + move + "%下落中です。"))
            : lean >= .4f ? TalkWord("아래에 큰 매수 잔량이 받치고 있습니다.", "A large bid is sitting underneath.", "下に大きな買い板があります。")
            : lean <= -.4f ? TalkWord("위에 큰 매도 잔량이 막고 있습니다.", "A large offer is capping it overhead.", "上に大きな売り板があります。")
            : position > .9f ? TalkWord("당일 고가 부근입니다.", "Trading near the day's high.", "本日の高値付近です。")
            : position < .15f ? TalkWord("당일 저가 부근입니다.", "Trading near the day's low.", "本日の安値付近です。")
            : TalkWord("뚜렷한 방향이 없습니다.", "No clear direction.", "はっきりした方向感はありません。");
        if (heat >= 3f) { reason += " " + TalkWord("거래가 평소보다 크게 붙었습니다.", "Volume is well above normal.", "出来高が普段よりかなり多いです。"); }
        return new ChatAnalysisCard { symbol = stock.symbol, stockName = stock.stockName, open = open, price = price, high = high, low = low, score = final, capturedAt = SocialTime.GameMinute };
    }
    private void PostAnalysis(AnalysisJob job, double now)
    {
        if (job.stock == null)
        {
            PostAnalyst(job.room, string.IsNullOrEmpty(job.argument)
                ? TalkWord("종목 이름을 함께 적어 주세요. 예: !분석 노보", "Add a stock name. Example: !analyze Nobo", "銘柄名を付けてください。例: !分析 ノボ")
                : TalkWord("'" + job.argument + "'에 맞는 종목을 찾지 못했습니다.", "No stock matches '" + job.argument + "'.", "「" + job.argument + "」に合う銘柄が見つかりません。"), null, job.replyTo);
            return;
        }
        var card = Analyze(job.stock, out string reason);
        if (card == null) { return; }
        string name = GameLocale.Text(job.stock.stockName ?? job.stock.symbol);
        string verdict = TabletGroupChatUI.AnalysisVerdict(card.score);
        long id = PostAnalyst(job.room, TalkWord(name + " 분석입니다. " + reason + " 지금은 '" + verdict + "' 구간으로 봅니다. 참고용입니다.",
            name + ". " + reason + " I read this as '" + verdict + "' for now. For reference only.", name + "の分析です。" + reason + " 現時点では「" + verdict + "」と見ます。参考情報です。"), card, job.replyTo);
        AnalysesPosted++;
        TraceTalk("    > AI: " + job.stock.symbol + " 점수 " + card.score + " (" + verdict + ") 시가 " + Money(card.open) + " 현재 " + Money(card.price) + " 고가 " + Money(card.high) + " 저가 " + Money(card.low) + " · " + reason);
        // 가끔 봇 하나가 결과에 한마디 한다. 그 말대로 사고팔지는 않는다.
        Plan(job.room, card.score >= 58 ? "aiReact.buy" : card.score < 42 ? "aiReact.sell" : "aiReact.hold", job.stock, id, now + 1.6, 0, 1, .55f);
    }
    /// <summary>새벽 관찰실의 봇들도 가끔 AI에게 분석을 시킨다.</summary>
    private void TickAnalysisRequests(double now)
    {
        if (day.Phase != MarketPhase.Trading || StockManager.Instance == null) { return; }
        if (analysisNextRequest < 0) { analysisNextRequest = now + 20 + HypeRandom.Next() * 30; }
        if (now < analysisNextRequest) { return; }
        analysisNextRequest = now + 55 + HypeRandom.Next() * 70;
        const int room = NpcLeadingRoom.RoomId;
        hypeSpeakers.Clear(); float clock = SessionClock.Time;
        foreach (var candidate in socialBots)
        { if (candidate.Social != null && candidate.isActiveAndEnabled && Present(candidate, room) && candidate.Social.Psychology.ActiveAt(clock) && !IsTyping(candidate)) { hypeSpeakers.Add(candidate); } }
        if (hypeSpeakers.Count == 0) { return; }
        var bot = hypeSpeakers[HypeRandom.Range(0, hypeSpeakers.Count)];
        // 눈길이 가 있는 종목(누가 말해서 보게 된 종목), 들고 있는 종목, 달아오른 종목 순으로 고른다.
        Stock stock = bot.Social.InfluencedStock != null && SessionClock.Time < bot.Social.InfluenceUntil && bot.Social.InfluencedStock.AcceptsOrders ? bot.Social.InfluencedStock : null; float hottest = 0f;
        if (stock == null)
        {
            foreach (var candidate in StockManager.Instance.stocks)
            {
                if (candidate == null || !candidate.AcceptsOrders) { continue; }
                float weight = (bot.GetOwnedVolume(candidate) > 0m ? 2f : 0f) + (StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(candidate)) + (float)HypeRandom.Next() * 2f;
                if (stock == null || weight > hottest) { hottest = weight; stock = candidate; }
            }
        }
        if (stock == null) { return; }
        string name = GameLocale.Text(stock.stockName ?? stock.symbol);
        // 봇도 이름을 줄여 쓰곤 한다.
        if (name.Length >= 4 && HypeRandom.Next() < .35) { name = name.Substring(0, Math.Max(2, name.Length / 2)); }
        long asked = PostBotLine(bot, room, TalkWord("!분석 ", "!analyze ", "!分析 ") + name, "talk.analyze", stock, 0);
        hypeSpokeAt[bot.Social.Id] = now;
        analysisJobs.Add(new AnalysisJob { due = now + 1.4 + HypeRandom.Next(), room = room, stock = stock, argument = name, replyTo = asked });
    }
}
