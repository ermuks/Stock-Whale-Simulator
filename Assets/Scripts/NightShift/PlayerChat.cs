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
    AskNews, AskMarket, AskHot, AskWhale, AskHoldings, AskProfit, AskWhy, AskHow, Mention, Agree, Disagree, Dunno, Surprise, Topic, Question, Statement, AskNick,
    // 받아 줄 수 없는 말. Noise: 뜻이 없는 글(숫자·기호·자판 두드림). Lewd: 음란한 말.
    Noise, Lewd,
    // AskWhich: 종목 없이 "지금 사도 됨?"(무엇을?). AskOutlook: 종목 없이 "더 오를까"·"바닥인가". AskHolders: "그푸 들고 있는 사람?". AskPrice: "산전 지금 얼마임".
    // AskTerm: "지정가가 뭐임"(낱말 풀이. Topic에 풀이의 열쇠가 든다).
    AskWhich, AskOutlook, AskHolders, AskPrice, AskTerm,
    // AskCompany: "산삼전자 뭐하는 회사임"(업종으로 답한다). AskCompare: "산전이랑 그푸 중에 뭐 삼?"(Other에 둘째 종목이 든다).
    AskCompany, AskCompare }

public struct PlayerTalk
{
    public PlayerTalkKind Kind;
    public Stock Stock;
    /// <summary>글에 종목이 둘 나왔을 때의 둘째 종목("산전 vs 그푸"). 없으면 null.</summary>
    public Stock Other;
    /// <summary>글에 적힌 수익률(%). 없으면 NaN.</summary>
    public float Percent;
    public bool CallsAll, Laughs, Cries, Asks;
    /// <summary>물음표나 묻는 말꼬리는 없어도 묻는 낱말(뭐·왜·누가·what·何)이 들어 있다.</summary>
    public bool Wonders;
    /// <summary>밤사이·어제 일을 묻는다.</summary>
    public bool Night;
    /// <summary>위로하는 말, 축하하는 말, 칭찬하는 말이 들어 있는가(호감에 쓴다).</summary>
    public bool Comforts, Congrats, Praises;
    /// <summary>사라고(팔라고) 권하는 말이 들어 있는가.</summary>
    public bool TellsBuy, TellsSell;
    /// <summary>자리를 비웠다가 돌아왔다는 인사인가("밥 먹고 옴").</summary>
    public bool Returned;
    /// <summary>맞장구·반대·모름의 말뿐인가("ㅇㅇ", "ㄴㄴ 아님"). 딴 말이 붙어 있으면("ㅇㅇ 아직 들고 있음") 봇의 물음에 대한 예·아니오로 읽지 않는다.</summary>
    public bool BareNod;
    /// <summary>사겠다(팔겠다)는 말인가("그럼 살게", "ㅇㅋ 그거 삼"). 종목을 대지 않았으면 방금 얘기하던 종목이다.</summary>
    public bool WillBuy, WillSell;
    /// <summary>"님들은?"·"너는?"처럼 방금 내가 한 얘기를 그대로 되묻는 말인가.</summary>
    public bool AsksSame;
    /// <summary>샀다고(팔았다고) 알리는 말이 들어 있는가("산전 샀다", "그푸 다 던짐").</summary>
    public bool Bought, Sold;
    /// <summary>"왜?"·"진짜?"처럼 바로 앞의 말을 되묻는 짧은 말.</summary>
    public bool AsksBack;
    /// <summary>되묻는 말의 갈래: "why"(왜?), "doubt"(진짜? 거짓말), "explain"(무슨 말임? 그래서?). 되묻는 말이 아니면 null.</summary>
    public string BackKind;
    /// <summary>"그렇구나"·"알겠음"처럼 알아들었다는 말(맞장구로 읽히지만 "예"라는 대답은 아니다).</summary>
    public bool Acks;
    /// <summary>잡담의 갈래(food, sleep, weather …). 잡담이 아니면 null.</summary>
    public string Topic;
    /// <summary>글에 드러난 매매 버릇: "short"(단타), "long"(장투). 없으면 null.</summary>
    public string Style;
    /// <summary>막말의 세기: 0 없음, 1 얕보는 말, 2 욕설, 3 선을 넘은 말(가족·죽으라는 말·성적인 욕).</summary>
    public int Rudeness;
    /// <summary>선을 넘은 말의 갈래: 가족을 건드렸는가, 죽으라고 했는가.</summary>
    public bool RudeFamily, RudeDeath;
    /// <summary>"!분석" 뒤에 적은 말(종목을 못 찾았을 때 되돌려 보여 준다).</summary>
    public string Argument;

    // 영어·일본어에서 실제로 쓰는 말을 표마다 더 보탠다(위의 표들은 한국어를 먼저 적고 번역을 곁들인 것이라 그 나라 말투가 빠져 있었다).
    // 영어는 미국 주식 커뮤니티·디스코드, 일본어는 掲示板·株クラ의 말투를 따른다. 열쇠는 아래 표의 이름, "topic."으로 시작하면 갈래 표의 갈래다.
    private static readonly Dictionary<string, string[]> More = new(StringComparer.Ordinal)
    {
        ["Greet"] = new[] { "yo", "gm", "morning", "sup guys", "hiya", "howdy", "wassup", "おは", "おっは", "こんちゃ", "こんにちわ", "ども", "どうも", "よろ", "初めまして", "はじめまして", "ちわ", "うっす", "おっす" },
        ["ReturnWords"] = new[] { "back", "im back", "i'm back", "returned", "ただいま", "戻った", "戻りました", "戻ってきた", "帰ってきた", "復帰" },
        ["Farewell"] = new[] { "gtg", "g2g", "later", "laters", "peace", "im out", "i'm out", "logging off", "heading out", "ttyl", "good night", "nighty", "off to bed", "calling it a day", "落ちる", "落ちます", "寝る", "寝ます", "おち", "ノシ", "おつ", "乙", "また明日", "お先", "おつかれ", "じゃあの", "ばいばい", "バイバイ", "ほな" },
        ["Thank"] = new[] { "cheers", "appreciate it", "appreciated", "tysm", "thank u", "あざす", "あざ", "あざっす", "サンクス", "助かる", "助かった", "どうも", "サンキュー", "thx" },
        ["Sorry"] = new[] { "my b", "mb", "apologies", "sry", "すみません", "すまない", "申し訳", "わるい", "悪かった", "ごめんなさい", "めんご" },
        ["Rude"] = new[] { "clown", "moron", "trash", "garbage", "pathetic", "cringe", "bagholders", "regards", "smooth brain", "アホか", "無能", "養分ども", "イナゴども", "ゴミ", "カス", "低能", "情弱", "乞食" },
        ["BuyTellWords"] = new[] { "buy the dip", "load the boat", "ape in", "all in on", "back up the truck", "get in here", "buy buy", "scoop", "買っとけ", "買い場", "全力買い", "拾え", "拾っとけ", "乗っとけ", "買いだ", "買い推奨", "仕込め", "仕込んどけ", "インしろ" },
        ["SellTellWords"] = new[] { "take profits", "cash out", "get out", "bail", "dump your bags", "sell sell", "cut it", "売っとけ", "逃げとけ", "利確しろ", "損切りしろ", "降りろ", "手仕舞え", "撤退しろ", "全部売れ" },
        ["ComfortWords"] = new[] { "it'll be ok", "itll be ok", "hang tight", "we ride together", "stay strong", "you'll be fine", "keep your head up", "chin up", "どんまい", "ドンマイ", "元気だせ", "元気出せ", "気にすんな", "きっと戻る", "耐えろ", "耐えよう", "がんばれ", "頑張れ", "ファイト", "なんとかなる" },
        ["CongratsWords"] = new[] { "gz", "gratz", "nice one", "well done", "good for you", "lucky you", "ナイス", "ないす", "おめ", "おめです", "やったな", "いいな", "裏山", "うらやま" },
        ["PraiseWords"] = new[] { "goat", "legend", "king", "big brain", "galaxy brain", "good call", "great call", "you called it", "smart money", "神", "有能", "天才か", "やるな", "うまいな", "上手い", "プロかよ", "師匠", "先生" },
        ["BragWords"] = new[] { "printing", "printed", "tendies", "in the green", "im green", "i'm green", "took profit", "took profits", "cashed out", "locked in gains", "secured the bag", "up on the day", "killed it", "勝った", "勝ってる", "勝ち", "利確した", "プラス", "プラテン", "含み益", "儲けた", "儲かった", "爆益", "億った", "含み益に" },
        ["LossWords"] = new[] { "bagholder", "holding bags", "holding the bag", "underwater", "in the red", "im red", "i'm red", "got wrecked", "got wiped", "blew up", "blown up", "wiped out", "lost everything", "lost it all", "got stopped out", "liquidated", "margin called", "cut my losses", "負けて", "負けた", "負け", "マイナス", "含み損", "塩漬け", "溶かした", "溶けた", "やられた", "損した", "大損", "爆損", "損切りした", "退場", "焼かれた", "捕まった", "高値掴み" },
        ["BuyAsk"] = new[] { "should i buy", "should i get in", "good entry", "good time to buy", "too late to buy", "worth a buy", "buy here", "is it a buy", "can i buy", "ok to buy", "should i ape", "buy the dip?", "買うべき", "買った方が", "入るべき", "買い時", "今から買", "買っても", "インしていい", "乗っていい", "拾っていい", "買い?" },
        ["SellAsk"] = new[] { "should i sell", "should i take profit", "time to sell", "sell here", "is it a sell", "should i cut", "should i bail", "hold or sell", "sell or hold", "売るべき", "売った方が", "損切りすべき", "利確すべき", "損切りした方が", "利確した方が", "売り時", "逃げるべき", "降りるべき", "手放す" },
        ["OpinionAsk"] = new[] { "thoughts on", "thoughts?", "opinions on", "what about", "good or bad", "bullish or bearish", "is it good", "is it bad", "any good", "price target", "pt?", "worth it", "legit", "どう思う", "どうなの", "どうなん", "どうだろ", "アリ?", "あり?", "ありかな", "目標株価", "どこまで", "上がる?", "下がる?", "いける?", "行ける?" },
        ["PickAsk"] = new[] { "what should i buy", "what do i buy", "what to buy", "what to get", "what do i get", "any picks", "any plays", "any tips", "hot picks", "what's the play", "whats the play", "whats the move", "what's the move", "give me a ticker", "name a stock", "something to buy", "何買えば", "何を買えば", "何買う", "何買ったら", "おすすめ", "オススメ", "推奨銘柄", "いい銘柄", "何がいい", "狙い目" },
        ["UpWords"] = new[] { "mooning", "moon", "ripping", "sending it", "squeeze", "squeezing", "breakout", "breaking out", "pumping", "gonna run", "will run", "running", "going parabolic", "lfg", "ストップ高", "s高", "爆上げ", "ぶち上げ", "上がる", "上げる", "噴く", "噴いた", "飛ぶぞ", "いくぞ", "行くぞ", "来るぞ", "くるぞ", "ぶっ飛ぶ", "急騰", "全力" },
        ["DownWords"] = new[] { "dumping", "tanking", "crashing", "bleeding", "rug", "rugged", "going to zero", "falling knife", "its over for", "cooked", "done for", "ストップ安", "s安", "暴落", "ナイアガラ", "下がる", "下げる", "崩れる", "終わった", "終わり", "オワコン", "死んだ", "お通夜", "急落", "逃げろ", "寄り天" },
        ["WhaleWords"] = new[] { "i am the whale", "im the whale", "i'm the whale", "i move this", "i'll pump it", "ill pump it", "watch me pump", "i control this", "俺が仕手", "仕手は俺", "俺が上げる", "俺が動かす", "俺が買い上げる" },
        ["NewsAsk"] = new[] { "any news", "any catalyst", "catalyst", "what's the news", "whats the news", "why the move", "what did i miss", "any rumors", "pr out", "filing", "ニュース", "材料", "何かあった", "なんかあった", "何があった", "ir出た", "ir来た", "決算", "噂", "リーク", "適時開示" },
        ["NightWords"] = new[] { "overnight", "last night", "after hours", "premarket", "while i was asleep", "while i slept", "yesterday", "昨夜", "夜の間", "昨日", "寝てる間", "夜中", "夜間", "pts" },
        ["WhaleAsk"] = new[] { "whale", "whales", "big money", "smart money", "institutions", "market maker", "who is buying", "who's buying", "who is selling", "who's selling", "who is pumping", "manipulation", "manipulated", "仕手", "大口", "機関", "クジラ", "誰が買", "誰が売", "誰が上げ", "買い集め", "操作" },
        ["MarketAsk"] = new[] { "how's the market", "hows the market", "market today", "how are we looking", "how we looking", "green or red", "market mood", "market sentiment", "地合い", "相場", "今日どう", "市場", "今日の雰囲気", "全体どう" },
        ["HotAsk"] = new[] { "what's moving", "whats moving", "what's hot", "whats hot", "what's running", "whats running", "what's pumping", "whats pumping", "top movers", "anything moving", "what are you watching", "what you watching", "watchlist", "何が動いて", "何見てる", "なに見てる", "何が上がって", "注目", "何が熱い", "何が来てる", "監視銘柄" },
        ["HoldAsk"] = new[] { "what are you holding", "what you holding", "what are u holding", "whatcha holding", "holding anything", "what did you buy", "what are you in", "your positions", "your bags", "portfolio", "positions?", "何持ってる", "なに持ってる", "何買った", "保有銘柄", "ポジ", "ポートフォリオ", "何握ってる", "持ち株" },
        ["BackFill"] = new[] { "だろ", "やろ", "でしょ", "かよ", "じゃん", "なの", "だよ", "です", "ます", "って", "は", "の", "か", "ね", "よ", "tho", "though", "bro", "man", "lol", "you", "it", "is", "that", "this", "the", "a" },
        ["ProfitAsk"] = new[] { "you up", "you down", "you green", "you red", "are you up", "are you down", "anyone up", "anyone down", "anyone green", "anyone red", "how much are you up", "how much are you down", "how's your pnl", "hows your pnl", "how is your account", "making money", "you green", "you red", "勝ってる?", "負けてる?", "みんな儲", "儲かってる", "収支", "含み損どれ", "いくら負け", "いくら勝", "どれくらい負け", "どれくらい勝", "プラス?", "マイナス?" },
        ["WhyWords"] = new[] { "why", "how come", "reason", "なぜ", "なんで", "どうして", "何で", "理由", "根拠", "なんでや" },
        ["BackWords"] = new[] { "why", "really", "for real", "fr", "seriously", "srsly", "how so", "and then", "so what", "wdym", "no way", "cap", "なぜ", "なんで", "マジ", "まじ", "本当", "ほんと", "ほんま", "それで", "根拠", "ガチ" },
        ["DoubtWords"] = new[] { "source", "proof", "no way", "cap", "thats cap", "bs", "bullshit", "liar", "lies", "i doubt it", "doubt", "explain", "wdym", "what do you mean", "elaborate", "ソース", "嘘", "うそ", "ウソ", "まさか", "ガセ", "どういうこと", "説明", "証拠", "で?", "だから?", "つまり?" },
        ["ExplainWords"] = new[] { "explain", "what do you mean", "wdym", "elaborate", "so what", "and then", "meaning?", "tldr", "tl;dr", "どういうこと", "説明", "それで", "で?", "だから?", "つまり", "要するに", "わかるように" },
        ["DisbeliefWords"] = new[] { "really", "for real", "seriously", "srsly", "no way", "cap", "bs", "bullshit", "liar", "lies", "doubt", "嘘", "うそ", "ウソ", "まさか", "マジ", "まじ", "本当", "ほんと", "ほんま", "ガセ", "ガチ" },
        ["HowAsk"] = new[] { "how do i", "how to", "how does", "how can i", "what does", "teach me", "show me how", "walk me through", "newbie question", "noob question", "どうやって", "やり方", "教えて", "おしえて", "方法", "仕方", "どうすれば", "使い方" },
        ["AgreeWords"] = new[] { "yeah", "yea", "yep", "yup", "ya", "true", "facts", "fax", "agreed", "exactly", "same", "me too", "fair", "fair enough", "makes sense", "i see", "got it", "gotcha", "ah ok", "oh ok", "bet", "based", "うん", "うむ", "せやな", "そうだね", "そうだな", "それな", "たしかに", "確かに", "わかる", "分かる", "だよね", "だよな", "なるほど", "そうなんだ", "へえ", "ふーん", "おk", "おけ", "りょ", "了解", "はい", "同意", "同じく" },
        ["AckWords"] = new[] { "i see", "got it", "gotcha", "makes sense", "fair enough", "ah ok", "oh ok", "noted", "なるほど", "そうなんだ", "へえ", "ふーん", "そっか", "そうか", "了解", "りょ", "把握" },
        ["YesWords"] = new[] { "yeah", "yea", "yep", "yup", "ya", "yes", "true", "facts", "うん", "うむ", "はい", "せや", "そう", "その通り" },
        ["DisagreeWords"] = new[] { "nah", "nope", "no", "no way", "not really", "disagree", "wrong", "cap", "hard no", "i doubt it", "doubt it", "いや", "いやいや", "違う", "ちがう", "ちゃう", "ないわ", "ないない", "そうかな", "微妙", "それはない", "そうは思わ" },
        ["DunnoWords"] = new[] { "idk", "dunno", "no idea", "no clue", "not sure", "who knows", "beats me", "shrug", "わからん", "分からん", "わかんない", "知らん", "しらん", "さあ", "さぁ", "不明", "どうだろ", "謎" },
        ["SurpriseWords"] = new[] { "wow", "woah", "whoa", "omg", "holy", "wtf", "damn", "dang", "insane", "crazy", "no shot", "sheesh", "jesus", "bro what", "マジか", "まじか", "やば", "ヤバ", "うお", "うわ", "えっ", "えぐ", "エグ", "すげ", "すご", "ひえ", "嘘やろ", "なんだと", "ファッ" },
        ["SighWords"] = new[] { "ugh", "sigh", "oof", "welp", "bruh", "rip", "fml", "pain", "im done", "i'm done", "its over", "it's over", "gg", "f", "cooked", "damn it", "はあ", "はぁ", "あーあ", "やれやれ", "ちくしょう", "オワタ", "おわた", "終わった", "もうだめ", "もうダメ", "詰んだ", "つら", "つらい", "しんど", "萎えた" },
        ["QuestionWords"] = new[] { "what", "whats", "why", "who", "how", "hows", "when", "wen", "where", "which", "anyone", "anybody", "any", "is it", "are you", "do you", "can i", "should i", "何", "なに", "なん", "なぜ", "なんで", "どう", "誰", "だれ", "いつ", "どこ", "どれ", "どっち", "いくら", "かな", "ですか", "ますか", "だろうか" },
        ["Expletives"] = new[] { "fucking", "fuckin", "fuck", "fck", "fk", "shit", "damn", "goddamn", "bloody", "クソ", "くそ", "糞", "ちくしょう", "畜生" },
        ["AimedWords"] = new[] { "you", "u r", "ur", "you're", "youre", "yall", "y'all", "お前", "おまえ", "おめえ", "てめ", "貴様", "あんた", "お前ら", "君ら" },
        ["SelfWords"] = new[] { "i'm", "im ", "i am", "i was", "i feel", "me ", "my ", "俺", "おれ", "私", "わたし", "自分", "僕", "わい", "ワイ" },
        ["OutlookAsk"] = new[] { "is it the bottom", "is this the bottom", "the bottom", "is this the top", "the top", "will it go up", "will it drop", "will it recover", "going up tomorrow", "tomorrow green", "tomorrow red", "why is everything", "why are we red", "why are we down", "why is it all", "when does it go up", "wen moon", "dead cat", "bounce", "bull or bear", "底かな", "底打", "ここが底", "天井", "上がるかな", "下がるかな", "明日どう", "明日上が", "明日下が", "まだ下が", "まだ上が", "全部下が", "なんで下が", "なんで上が", "反発", "リバ", "見通し", "いつ上が" },
        ["HolderAsk"] = new[] { "who holds", "who's holding", "who is holding", "anyone holding", "anyone in", "anyone own", "anyone bagholding", "who bought", "who's in", "holders", "持ってる人", "ホルダー", "買った人", "握ってる人", "捕まってる人", "誰か持って", "持ってるやつ" },
        ["PriceAsk"] = new[] { "how much is", "price of", "what's the price", "whats the price", "what is the price", "what's it at", "whats it at", "where is it trading", "current price", "いくら", "株価", "今いくら", "値段", "何円" },
        ["BoughtWords"] = new[] { "i bought", "just bought", "bought some", "bought more", "i'm in", "im in", "got in", "loaded up", "aped in", "added more", "picked up", "買った", "買いました", "入った", "インした", "拾った", "仕込んだ", "買い増した", "全力した", "乗った" },
        ["SoldWords"] = new[] { "i sold", "just sold", "sold all", "sold my", "i'm out", "im out", "got out", "cashed out", "dumped", "took profit", "trimmed", "売った", "売りました", "降りた", "利確した", "損切りした", "手仕舞った", "撤退した", "全部売", "逃げた" },
        ["DefineWords"] = new[] { "what is", "what's", "whats", "what does", "what are", "what do you mean by", "meaning", "mean?", "define", "eli5", "って何", "ってなに", "とは", "って何?", "の意味", "意味", "ってどういう" },
        ["CompanyAsk"] = new[] { "what does", "what do they do", "what kind of company", "what sector", "what industry", "who are they", "what is this company", "何の会社", "どんな会社", "何してる会社", "何やってる会社", "業種", "何屋" },
        ["CompareWords"] = new[] { "vs", "versus", " or ", "which one", "better than", "どっち", "それとも", "どちら", "と比べ" },
        ["BlameWords"] = new[] { "your fault", "because of you", "thanks to you i lost", "you made me", "you owe me", "お前のせい", "君のせい", "責任取", "責任とれ", "どうしてくれる" },
        ["StockWords"] = new[] { "stock", "stocks", "ticker", "tickers", "shares", "pick", "picks", "play", "plays", "to buy", "i buy", "銘柄", "株", "買い", "買う" },
        ["NotReturnWords"] = new[] { "cash", "chance", "odds", "battery", "discount", "confident", "tax", "現金", "確率", "バッテリー", "割引", "税", "充電" },
        ["Cocky"] = new[] { "ez", "easy", "too easy", "ez money", "light work", "skill", "skill issue", "told you", "follow me", "trust me bro", "余裕", "簡単", "天才", "俺を信じろ", "ほらね", "ついてこい", "楽勝", "チョロい" },
        ["Humble"] = new[] { "lucky", "got lucky", "barely", "phew", "somehow", "fluke", "運が", "なんとか", "たまたま", "まぐれ", "ギリギリ" },
        ["Hurting"] = new[] { "ruined", "wrecked", "help", "i'm done", "im done", "what do i do", "hurts", "pain", "cooked", "終わった", "助けて", "つらい", "どうしよう", "きつい", "しんどい", "死にたい", "無理" },
        ["WantsAdvice"] = new[] { "should i", "what do i do", "sell or hold", "hold or sell", "cut it", "hold or", "advice", "どうしよう", "売るべき", "損切り", "利確", "ナンピン", "持つべき", "どうすれば", "アドバイス" },
        ["NotYet"] = new[] { "not yet", "haven't", "havent", "nothing yet", "skipped", "nope", "starving", "まだ", "食べてない", "食ってない", "抜いた", "これから" },
        ["Ate"] = new[] { "ate", "had lunch", "had dinner", "had breakfast", "eaten", "just ate", "食べた", "食った", "済ませた" },
        ["Hungry"] = new[] { "hungry", "starving", "腹減", "お腹すい", "おなかすい", "空腹" },
        ["NewHere"] = new[] { "just started", "new", "today", "first", "beginner", "this week", "yesterday", "noob", "newbie", "初めて", "初心者", "今日", "始めたばかり", "昨日", "今週", "最近" },
        ["LongAgo"] = new[] { "year", "years", "long time", "a while", "ages", "decade", "veteran", "年", "長い", "だいぶ", "古参", "ベテラン" },
        ["Nothing"] = new[] { "nothing", "none", "cash", "didn't buy", "didnt buy", "no positions", "flat", "ない", "なし", "現金", "ノーポジ", "買ってない", "様子見" },
        ["ShortStyle"] = new[] { "scalp", "scalper", "scalping", "day trade", "day trader", "day trading", "short term", "in and out", "デイトレ", "スキャ", "短期", "スキャルピング" },
        ["LongStyle"] = new[] { "long term", "long-term", "hold forever", "investor", "buy and hold", "hodl", "diamond hands", "長期", "ガチホ", "塩漬け", "握力" },
        // ── 갈래 표(코드에 적힌 것)에 보태는 낱말 ──
        ["topic.lag"] = new[] { "lag", "lagging", "laggy", "lags", "stutter", "stuttering", "frame drop", "fps", "freezing up", "ラグ", "ラグい", "重い", "カクつ", "カクカク", "落ちる?" },
        ["topic.game"] = new[] { "what game", "this game", "what is this game", "the game", "何のゲーム", "このゲーム", "なんのゲーム" },
        ["topic.who"] = new[] { "are you a bot", "are you bots", "you bots", "are you real", "are you human", "real people", "are you ai", "how old", "where are you from", "where you from", "what do you do", "who are you", "ボット", "bot?", "人間?", "中の人", "何歳", "どこ住み", "何者", "誰?", "ai?" },
        ["topic.doing"] = new[] { "what are you doing", "what are you up to", "wyd", "what's up", "whats up", "sup", "whatcha doing", "何してる", "なにしてる", "何してん", "なにしてん", "何やってる" },
        ["topic.time"] = new[] { "what time", "when does", "when is the", "when do we open", "when do we close", "market open", "market close", "何時", "いつ開", "いつ終", "引けは", "寄りは" },
        ["topic.food"] = new[] { "lunch", "dinner", "breakfast", "hungry", "starving", "eat", "eating", "ate", "food", "pizza", "burger", "snack", "tacos", "ramen", "takeout", "doordash", "what's for lunch", "ご飯", "ごはん", "昼飯", "夕飯", "朝飯", "腹減", "ラーメン", "飯食", "何食", "昼何", "弁当", "コンビニ飯", "牛丼", "カレー", "食べた", "食った", "食べたい" },
        ["topic.drink"] = new[] { "beer", "drink", "drinks", "drunk", "whiskey", "wine", "hungover", "hangover", "happy hour", "need a drink", "酒", "ビール", "飲み", "飲みたい", "ハイボール", "二日酔い", "晩酌", "ストゼロ", "居酒屋" },
        ["topic.sleep"] = new[] { "sleepy", "tired", "exhausted", "no sleep", "all nighter", "cant sleep", "can't sleep", "insomnia", "need a nap", "slept", "眠い", "眠たい", "徹夜", "寝不足", "眠れ", "寝れ", "寝落ち", "寝た" },
        ["topic.weather"] = new[] { "weather", "raining", "rainy", "snowing", "cold out", "hot out", "freezing", "its hot", "it's hot", "its cold", "it's cold", "humid", "天気", "雨", "暑い", "寒い", "雪", "台風", "蒸し暑" },
        ["topic.bored"] = new[] { "bored", "boring", "dead chat", "so slow", "nothing to do", "slow day", "暇", "ひま", "退屈", "過疎", "つまらん", "やることない" },
        ["topic.work"] = new[] { "at work", "work sucks", "from work", "off work", "work today", "hate work", "my job", "job", "boss", "office", "meeting", "at work", "my manager", "coworker", "quit my job", "9 to 5", "会社", "出勤", "上司", "仕事", "会議", "社畜", "職場", "在宅", "リモート" },
        ["topic.rich"] = new[] { "rich", "retire", "lambo", "millionaire", "financial freedom", "quit my job when", "if i win", "金持ち", "引退", "億り人", "一攫千金", "宝くじ当たったら", "億あったら" },
        ["topic.love"] = new[] { "girlfriend", "boyfriend", "gf", "bf", "single", "dating", "tinder", "my ex", "彼女", "彼氏", "独身", "恋愛", "婚活", "マッチングアプリ", "元カノ", "元カレ" },
        ["topic.fun"] = new[] { "game", "gaming", "movie", "netflix", "youtube", "football", "soccer", "baseball", "music", "ゲーム", "映画", "アニメ", "野球", "サッカー", "漫画", "配信" },
        ["topic.newbie"] = new[] { "newbie", "noob", "new here", "beginner", "first time", "first day", "just started", "im new", "i'm new", "初心者", "初めて", "始めたばかり", "新参", "今日から" },
        ["topic.praise"] = new[] { "genius", "pro", "respect", "nice one", "legend", "smart", "well played", "goat", "すごい", "天才", "さすが", "神", "尊敬", "有能" },
        ["topic.luck"] = new[] { "lottery", "luck", "lucky", "pray", "praying", "please god", "fingers crossed", "manifesting", "宝くじ", "運", "祈", "頼む", "神頼み", "お祈り" },
        ["topic.health"] = new[] { "sick", "headache", "hospital", "my back", "a cold", "the flu", "doctor", "feel sick", "風邪", "病院", "頭痛", "腰が", "体調", "花粉", "熱が" },
        ["topic.home"] = new[] { "mom", "dad", "my kid", "kids", "at home", "my parents", "親", "子供", "実家", "家で", "家族" },
        ["topic.angry"] = new[] { "annoyed", "annoying", "pissed", "tilted", "mad", "furious", "rage", "so angry", "ムカつく", "イライラ", "萎え", "腹立つ", "キレそう", "ふざけんな" },
        ["topic.happy"] = new[] { "feeling good", "happy", "lets go", "let's go", "great day", "love it", "good day", "嬉しい", "最高", "気分いい", "楽しい", "よっしゃ", "やった" },
    };
    // ── 언어 ──
    // 한국어·영어·일본어는 낱말만 다른 것이 아니라 묻는 법, 맞장구치는 법, 글의 길이가 다르다. 그래서 읽는 규칙을 언어별로 따로 둔다.
    // 낱말 표는 한 군데에 적되 글자로 갈라 쓴다: 한글이 든 낱말은 한국어에서만, 가나·한자가 든 낱말은 일본어에서만 본다.
    // 로마자 낱말은 영어의 것이지만 한국어·일본어 채팅에도 섞어 쓰므로(ok, lol, mbti) 세 언어 모두에서 본다. 영어에서는 로마자 낱말만 본다.
    /// <summary>지금 언어: 0 한국어, 1 영어, 2 일본어.</summary>
    public static int Lang => GameLocale.Code == "en" ? 1 : GameLocale.Code == "ja" ? 2 : 0;
    private static string[][] Lex(string name, string[] words)
    {
        var ko = new List<string>(); var en = new List<string>(); var ja = new List<string>();
        var every = new List<string>(words);
        if (name != null && More.TryGetValue(name, out var more)) { foreach (string word in more) { if (!every.Contains(word)) { every.Add(word); } } }
        foreach (string word in every)
        {
            bool hangul = false, kana = false, latin = false;
            foreach (char c in word)
            {
                if (c >= '\uAC00' && c <= '\uD7A3' || c >= '\u3131' && c <= '\u318E') { hangul = true; }
                else if (c >= '\u3040' && c <= '\u30FF' || c >= '\u4E00' && c <= '\u9FFF' || c >= '\uFF00' && c <= '\uFFEF') { kana = true; }
                else if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') { latin = true; }
            }
            if (hangul) { ko.Add(word); }
            else if (kana) { ja.Add(word); }
            else { en.Add(word); ko.Add(word); ja.Add(word); if (!latin) { continue; } }
        }
        return new[] { ko.ToArray(), en.ToArray(), ja.ToArray() };
    }
    private static (string key, string[] words)[][] topicsLex;
    /// <summary>시답잖은 얘기의 갈래 표(지금 언어의 낱말만 남긴 것).</summary>
    private static (string key, string[] words)[] LangTopics
    {
        get
        {
            if (topicsLex == null)
            {
                var built = new (string key, string[] words)[3][];
                for (int lang = 0; lang < 3; lang++)
                {
                    var list = new List<(string key, string[] words)>();
                    foreach (var topic in Topics) { var words = Lex("topic." + topic.key, topic.words)[lang]; if (words.Length > 0) { list.Add((topic.key, words)); } }
                    built[lang] = list.ToArray();
                }
                topicsLex = built;
            }
            return topicsLex[Lang];
        }
    }
    // 영어: 묻는 말은 물음표가 없어도 첫머리로 안다("what do i buy", "is it over").
    private static readonly string[] EnAskLeads = { "what", "whats", "what's", "why", "how", "hows", "how's", "who", "whos", "who's", "when", "wen", "where", "which", "is", "are", "am", "was", "were", "do", "does", "did", "can", "could", "should", "would", "will", "has", "have", "anyone", "anybody", "any" };
    // 일본어: 묻는 말꼬리.
    private static readonly string[] JaAskEnds = { "か", "かな", "かね", "かい", "よね", "でしょ", "だろ", "だろう", "っけ", "なの", "るの", "たの", "いの", "んの", "ないの", "どう", "なに", "何", "誰", "いつ", "どこ" };
    private static bool StartsWord(string lower, string[] words)
    {
        foreach (string word in words)
        {
            if (!lower.StartsWith(word, StringComparison.Ordinal)) { continue; }
            if (lower.Length == word.Length || !IsLatin(lower[word.Length])) { return true; }
        }
        return false;
    }
    /// <summary>
    /// 짧은 대꾸(맞장구·반대·모름·놀람)의 낱말이 쓰였는가. 영어에서는 글의 첫머리에 올 때만 친다:
    /// "right"·"no"·"sure"·"ok"는 문장 속에서는 딴 뜻이다("right now it sucks", "no idea what to buy").
    /// </summary>
    private static bool Nod(string lower, string[] words) => Lang == 1 ? StartsWord(lower, words) && lower.Trim().Split(' ').Length <= 3 : HasWord(lower, words);
    /// <summary>되묻는 말·부르는 말을 걷어 내고 남은 것의 크기. 한국어·일본어는 글자 수, 영어는 낱말 수(두 글자 이상).</summary>
    private static int Leftover(string rest)
    {
        int count = 0;
        if (Lang == 1)
        {
            foreach (string token in rest.Split(' ', ',', '.', '?', '!', '\'')) { int letters = 0; foreach (char c in token) { if (c >= 'a' && c <= 'z') { letters++; } } if (letters >= 2) { count++; } }
            return count;
        }
        foreach (char c in rest) { if (IsHangul(c) || c >= 'a' && c <= 'z' || c >= '\u3040' && c <= '\u30FF' || c >= '\u4E00' && c <= '\u9FFF') { count++; } }
        return count;
    }
    /// <summary>"짧은 말"의 길이(글자). 영어는 같은 말이 훨씬 길다("ㅇㅇ 맞음" = "yeah thats right").</summary>
    private static int BriefLength => Lang == 1 ? 30 : Lang == 2 ? 12 : 14;
    private static readonly Regex PercentPattern = new(@"([+\-]?\d+(?:\.\d+)?)\s*(?:%|퍼|프로|パー)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LaughPattern = new(@"ㅋ{2,}|ㅎ{2,}|\^\^|\^_\^|웃기|웃겨|웃김|웃프|lol|lmao|lmfao|rofl|haha|hehe|kek|ｗ{2,}|w{3,}|草|笑|ワロ|わろ", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EnGainPattern = new(@"\b(made|banked|gained|printed|up)\s+\$?\d", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EnLossPattern = new(@"\b(lost|down|blew)\s+\$?\d", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CryPattern = new(@"ㅠ{2,}|ㅜ{2,}|ㅠㅜ|ㅜㅠ|하\.{2,}|흑흑|;;|orz|泣", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string[] All => AllLex[Lang];
    private static readonly string[][] AllLex = Lex("All", new[] { "님들", "여러분", "저기요", "다들", "얘들아", "형들", "형님들", "누구", "아무나", "guys", "everyone", "anyone", "anybody", "y'all", "みんな", "皆さん", "すみません", "誰か" });
    // "하이"는 따로 선 말일 때만 인사다("하이고", "하이닉스"는 아니다). 견줄 글 끝에는 빈칸을 하나 붙여 둔다.
    private static string[] Greet => GreetLex[Lang];
    private static readonly string[][] GreetLex = Lex("Greet", new[] { "안녕", "ㅎㅇ", "하이 ", "하이요", "하이루", "하이하이", "하이~", "하이!", "하이ㅎ", "하이ㅋ", "반갑", "반가워", "방가", "좋은아침", "굿모닝", 
        "hello", "hi", "hey", "good morning", "i'm back", "im back", "こんにちは", "おはよう", "こんばんは", "よろしく", "ただいま" });
    // 자리를 비웠다가 돌아왔다는 말("밥 먹고 옴", "다녀왔음"). 다른 얘깃거리가 있으면("헬스 다녀옴") 그 얘기가 먼저다.
    private static string[] ReturnWords => ReturnWordsLex[Lang];
    private static readonly string[][] ReturnWordsLex = Lex("ReturnWords", new[] { "나 왔", "저 왔", "다녀왔", "돌아왔", "복귀함", "복귀했", "고 옴", "다녀옴", "돌아옴", "들어옴", "갔다 옴", "방금 옴", "i'm back", "im back", "ただいま", "戻った" });
    // "퇴근하고 싶다", "이만돈"은 작별이 아니다. 간다는 뜻이 분명한 말만 적는다.
    private static string[] Farewell => FarewellLex[Lang];
    private static readonly string[][] FarewellLex = Lex("Farewell", new[] { "안녕히", "잘자", "잘 자", "퇴근함", "퇴근한다", "퇴근합니다", "퇴근이다", "퇴근 ㄱ", "수고", "나간다", "나 간다", "나 감", "저 갑니다", "자러 감", "자러 간", "자러감", "먼저 감", "먼저 간", "그럼 이만", "전 이만", "난 이만", "이만 자", "이만 갑", "이만 간", "이만 들어",
        "들어가 볼게", "들어가볼게", "내일 봐", "내일 보자", "굿나잇", "굿밤", "오늘은 여기까지", "여기까지 할게", "빠이", "ㅂㅂ", "ㅂㅇ", "bye", "good night", "gn", "cya", "see you", "see ya", "お疲れ", "おやすみ", "じゃあね", "またね" });
    private static string[] Thank => ThankLex[Lang];
    private static readonly string[][] ThankLex = Lex("Thank", new[] { "감사", "고마워", "고맙", "ㄱㅅ", "땡큐", "thanks", "thank you", "thx", "ty", "ありがと", "感謝" });
    // 자기를 낮추는 말("나 바보인가", "내가 멍청했지")은 남을 얕보는 말이 아니다.
    private static string[] SelfWords => SelfWordsLex[Lang];
    private static readonly string[][] SelfWordsLex = Lex("SelfWords", new[] { "나 ", "난 ", "내가", "나는", "나도", "나만", "제가", "저는", "저도", "i'm", "im ", "i am", "i was", "俺", "私", "自分" });
    // 돈 얘기가 아닌 "먹었다·땄다·손절": 나이·욕·겁을 먹고, 자격증을 따고, 친구를 손절한다.
    private static string[] NotGainWords => NotGainWordsLex[Lang];
    private static readonly string[][] NotGainWordsLex = Lex("NotGainWords", new[] { "나이 먹", "나이먹", "나이를 먹", "욕 먹", "욕먹", "욕을 먹", "겁 먹", "겁먹", "겁을 먹", "마음 먹", "마음먹", "애먹", "애 먹", "더위 먹", "골탕", "약 먹", "밥 먹", "자격증", "면허", "메달", "학점", "장학금" });
    private static string[] NotLossWords => NotLossWordsLex[Lang];
    private static readonly string[][] NotLossWordsLex = Lex("NotLossWords", new[] { "친구 손절", "친구를 손절", "친구랑 손절", "걔 손절", "걔랑 손절", "사람 손절", "지인 손절", "인간관계", "지갑 잃", "길 잃", "길을 잃", "입맛 잃", "입맛을 잃", "정신 잃", "lost my wallet", "lost my keys", "lost my phone" });
    // 금융 얘기처럼 들리지만 아닌 말. 소식·세력·시장을 묻는 말인지 볼 때는 이것들을 지우고 본다("택시 기사", "기관지염", "시장 가서 떡볶이").
    private static string[] FalseFriends => FalseFriendsLex[Lang];
    private static readonly string[][] FalseFriendsLex = Lex("FalseFriends", new[] { "택시 기사", "택시기사", "버스 기사", "버스기사", "기사님", "기사 아저씨", "정보처리기사", "정보처리", "개인정보", "신상정보", "기사 시험", "기사 자격", "기관지", "기관사", "소화기관", "대장 내시경", "대장내시경", "대장암", "대장균", "골목대장",
        "재래시장", "전통시장", "수산시장", "벼룩시장", "장 끝나고", "장 마치고", "여기 분위기", "분위기 좋", "뭐 사고 싶", "뭐 사 먹", "뭐 사먹", "뭐 사줄", "시장가", "시장 가서", "시장에서", "시장 갔", "시장 가는", "시장님", "방 분위기", "분위기 메이커", "분위기 좋은", "술 분위기", "무슨 일 하", "무슨일 하", "뭔 일 하", "작전 타임", "고래고래", "고래밥", "좋은 소식 있", "기쁜 소식" });
    // 주식을 두고 하는 말임을 알려 주는 낱말. 이게 있으면 "추천 좀"은 종목 추천이다(없고 다른 얘깃거리가 있으면 그 얘기다: "점심 메뉴 추천 좀").
    private static string[] StockWords => StockWordsLex[Lang];
    private static readonly string[][] StockWordsLex = Lex("StockWords", new[] { "종목", "주식", "매수", "뭐 살", "뭐 사", "뭘 사", "뭐 타", "뭐 담", "stock", "ticker", "pick", "to buy", "i buy", "銘柄", "株" });
    // 수익률이 아닌 퍼센트("현금 100%", "확률 50%").
    private static string[] NotReturnWords => NotReturnWordsLex[Lang];
    private static readonly string[][] NotReturnWordsLex = Lex("NotReturnWords", new[] { "현금", "비중", "확률", "배터리", "충전", "할인", "cash", "chance", "battery", "discount", "現金", "確率", "バッテリー" });
    private static string[] OutlookAsk => OutlookAskLex[Lang];
    private static readonly string[][] OutlookAskLex = Lex("OutlookAsk", new[] { "더 오를", "더 오르", "더 떨어질", "더 떨어지", "더 빠질", "더 빠지", "더 갈까", "더 가나", "바닥인", "바닥임", "바닥 맞", "고점인", "고점임", "고점 맞", "저점", "반등", "하락장", "상승장", "왜 이렇게 빠", "왜 이리 빠", "왜 다 떨어", "왜 다 빠", "왜 안 오", "왜 떨어",
        "언제 오르", "언제 오름", "언제 올라", "내일 오를", "내일 오르", "내일은 어", "내일 장 어", "내일 어떨", "전망", "다 떨어지", "다 빠지", "is it the bottom", "is this the top", "the bottom", "the top", "going up tomorrow", "will it go up", "will it drop", "why is everything", "outlook", "底か", "天井か", "明日上が", "まだ上が", "まだ下が", "見通し" });
    private static string[] HolderAsk => HolderAskLex[Lang];
    private static readonly string[][] HolderAskLex = Lex("HolderAsk", new[] { "들고 있는 사람", "들고있는 사람", "들고 있는 분", "물린 사람", "물린 분", "산 사람", "탄 사람", "가진 사람", "있는 사람", "누가 삼", "누가 샀", "누가 들", "누가 탔", "보유자", "홀더", "주주", "who holds", "who's holding", "who is holding", "anyone holding", "anyone in", "holders", "持ってる人", "ホルダー", "買った人" });
    private static string[] PriceAsk => PriceAskLex[Lang];
    private static readonly string[][] PriceAskLex = Lex("PriceAsk", new[] { "얼마임", "얼마야", "얼마냐", "얼마에요", "얼마예요", "얼마지", "지금 얼마", "현재가", "가격이", "가격 얼마", "가격 좀", "시세", "how much is", "price of", "what's the price", "whats the price", "いくら", "株価" });
    private static string[] BoughtWords => BoughtWordsLex[Lang];
    private static readonly string[][] BoughtWordsLex = Lex("BoughtWords", new[] { "샀다", "샀음", "샀어", "샀네", "샀습", "매수함", "매수했", "매수 완", "탔다", "탔음", "탔어", "담았", "풀매수 했", "풀매수했", "들어갔", "진입했", "진입함", "i bought", "just bought", "i'm in", "im in", "買った", "入った" });
    private static string[] SoldWords => SoldWordsLex[Lang];
    private static readonly string[][] SoldWordsLex = Lex("SoldWords", new[] { "팔았", "매도함", "매도했", "매도 완", "던졌", "던짐", "정리함", "정리했", "익절함", "익절했", "손절함", "손절했", "다 뺐", "i sold", "just sold", "i'm out", "im out", "売った", "降りた" });
    private static string[] CompanyAsk => CompanyAskLex[Lang];
    private static readonly string[][] CompanyAskLex = Lex("CompanyAsk", new[] { "뭐하는 회사", "뭐 하는 회사", "무슨 회사", "어떤 회사", "뭐하는 곳", "뭐 하는 곳", "뭐하는 데", "뭐 하는 데", "뭐 파는", "뭐 만드", "뭘 만드", "뭘 파는", "업종", "무슨 종목", "what does", "what kind of company", "what sector", "何の会社", "どんな会社", "業種" });
    private static string[] CompareWords => CompareWordsLex[Lang];
    private static readonly string[][] CompareWordsLex = Lex("CompareWords", new[] { "vs", "중에", "둘 중", "아니면", "이랑", "랑", "하고", " or ", "どっち", "それとも" });
    // 낱말의 뜻을 묻는 말꼬리("지정가가 뭐임", "매집이 무슨 뜻").
    private static string[] DefineWords => DefineWordsLex[Lang];
    private static readonly string[][] DefineWordsLex = Lex("DefineWords", new[] { "뭐임", "뭐야", "뭔데", "뭐지", "뭐냐", "뭐예요", "뭐에요", "뭐죠", "뭔가요", "뭐하는 거", "뭐 하는 거", "무슨 뜻", "뭔 뜻", "뜻이", "뜻 좀", "뜻 알", "이란", "what is", "what's", "whats", "what does", "what are", "meaning", "って何", "とは", "意味" });
    // 바로 앞 말을 못 믿겠다거나 풀어 달라는 말("거짓말", "근거 있음?", "무슨 말임?").
    private static string[] DoubtWords => DoubtWordsLex[Lang];
    private static readonly string[][] DoubtWordsLex = Lex("DoubtWords", new[] { "근거", "출처", "누가 그", "어디서 들", "거짓말", "구라", "뻥", "설마", "말이 되", "무슨 말", "뭔 말", "무슨 소리", "뭔 소리", "다시 말", "설명", "결론", "요약", "이해 안", "이해가 안", "source", "proof", "no way", "explain", "what do you mean", "ソース", "嘘", "まさか", "どういうこと", "説明" });
    private static string[] ExplainWords => ExplainWordsLex[Lang];
    private static readonly string[][] ExplainWordsLex = Lex("ExplainWords", new[] { "무슨 말", "뭔 말", "무슨 소리", "뭔 소리", "다시 말", "설명", "결론", "요약", "이해 안", "이해가 안", "그래서", "explain", "what do you mean", "so what", "and then", "どういうこと", "説明", "それで" });
    private static string[] DisbeliefWords => DisbeliefWordsLex[Lang];
    private static readonly string[][] DisbeliefWordsLex = Lex("DisbeliefWords", new[] { "거짓말", "구라", "뻥", "설마", "말이 되", "진짜", "ㄹㅇ", "정말", "레알", "really", "for real", "seriously", "no way", "嘘", "まさか", "マジ", "本当", "ほんと" });
    private static string[] AckWords => AckWordsLex[Lang];
    private static readonly string[][] AckWordsLex = Lex("AckWords", new[] { "그렇구나", "그렇군", "그렇네", "그럴 수도", "그럴수도", "그런가", "글쿤", "알겠", "이해함", "이해했", "아하", "오호", "ㅇㅎ", "i see", "got it", "なるほど", "そうなんだ" });
    private static string[] YesWords => YesWordsLex[Lang];
    private static readonly string[][] YesWordsLex = Lex("YesWords", new[] { "ㅇㅇ", "응", "맞아", "맞음", "ㅇㅈ", "인정", "yes", "yeah", "yep", "うん", "はい" });
    // 한숨과 푸념.
    private static string[] SighWords => SighWordsLex[Lang];
    private static readonly string[][] SighWordsLex = Lex("SighWords", new[] { "에휴", "휴", "하아", "하", "젠장", "아오", "아놔", "노답", "답 없", "답이 없", "한숨", "어이가 없", "망함", "망했", "ㅈ됨", "조졌", "끝", "끝났", "sigh", "ugh", "damn it", "はあ", "やれやれ", "ちくしょう" });
    private static string[] ErrandTopics => ErrandTopicsLex[Lang];
    private static readonly string[][] ErrandTopicsLex = Lex("ErrandTopics", new[] { "food", "toilet", "smoke", "coffee", "afk", "delivery", "drink" });
    private static readonly string[] MoneyTopics = { "crypto", "usstock", "economy", "sector", "newbie", "praise", "opinion",
        "positions", "whales", "headlines", "rumor", "dip", "bags", "squeeze", "earnings", "fed", "bullbear", "fundamentals", "ath", "sideways", "halted", "memestock", "overnight", "fomo", "bloodbath",
        "nikkei", "fukumizon", "taijou", "kessan", "ipo", "inago", "nisa", "yutai", "boj" };
    // 남 탓("님 때문에 물렸음", "책임져")은 한탄이 아니라 따지는 말이다.
    private static string[] BlameWords => BlameWordsLex[Lang];
    private static readonly string[][] BlameWordsLex = Lex("BlameWords", new[] { "님 때문", "너 때문", "니 때문", "책임져", "책임 져", "your fault", "お前のせい", "責任取" });
    private static string[] ShouldWords => ShouldWordsLex[Lang];
    private static readonly string[][] ShouldWordsLex = Lex("ShouldWords", new[] { "should i", "can i" });
    private static string[] Sorry => SorryLex[Lang];
    private static readonly string[][] SorryLex = Lex("Sorry", new[] { "미안", "죄송", "ㅈㅅ", "sorry", "my bad", "ごめん", "すまん" });
    private static string[] Rude => RudeLex[Lang];
    private static readonly string[][] RudeLex = Lex("Rude", new[] { "바보", "멍청", "호구", "개미들", "닥쳐", "꺼져", "ㅂㅅ", "병신", "한심", "찐따", "루저", "흑우", "idiot", "stupid", "noob", "loser", "shut up", "dumb", "バカ", "馬鹿", "アホ", "雑魚", "黙れ" });
    private static string[] BuyTellWords => BuyTellWordsLex[Lang];
    private static readonly string[][] BuyTellWordsLex = Lex("BuyTellWords", new[] { "매수 ㄱ", "매수ㄱ", "ㄱㄱ", "사자", "삽시다", "탑승해", "탑승 ㄱ", "타자", "사라", "사세요", "사셈", "사셔", "사 둬", "사둬", "타라", "타세요", "타셈", "담아", "담으", "매수 추천", "매수추천", "추천함", "추천한다", "추천합니다", "강추", "풀매수", "들어가라", "들어가세요", "줍줍해", "주워라", "buy it", "buy this", "get in now", "load up", "買え", "買っとけ", "買い推奨", "乗れ" });
    private static string[] SellTellWords => SellTellWordsLex[Lang];
    private static readonly string[][] SellTellWordsLex = Lex("SellTellWords", new[] { "팔아라", "파세요", "팔아", "파셈", "던져라", "던지세요", "손절해", "정리해", "빼라", "빼세요", "매도 추천", "매도추천", "sell it", "sell this", "get out now", "dump it", "売れ", "売っとけ", "逃げろ" });
    private static string[] ComfortWords => ComfortWordsLex[Lang];
    private static readonly string[][] ComfortWordsLex = Lex("ComfortWords", new[] { "힘내", "힘 내", "괜찮아", "괜찮을", "토닥", "화이팅", "파이팅", "응원", "버텨", "버티자", "잘 될", "잘될", "기운 내", "기운내", "너무 걱정", "다 지나", "hang in", "cheer up", "be ok", "you got this", "stay strong", "頑張", "大丈夫", "元気出", "応援" });
    private static string[] CongratsWords => CongratsWordsLex[Lang];
    private static readonly string[][] CongratsWordsLex = Lex("CongratsWords", new[] { "축하", "ㅊㅋ", "ㅊㅊ", "부럽", "잘됐", "잘 됐", "좋겠다", "congrats", "grats", "happy for you", "jealous", "おめでと", "うらやま", "よかったね" });
    private static string[] PraiseWords => PraiseWordsLex[Lang];
    private static readonly string[][] PraiseWordsLex = Lex("PraiseWords", new[] { "대단", "고수", "잘하네", "잘한다", "멋지", "멋있", "천재", "존경", "배우고", "한 수", "역시", "nice call", "well played", "genius", "respect", "impressive", "さすが", "すごい", "天才", "尊敬", "うまい" });
    // 음란한 말. 받아 주지 않는다(싸늘하게 한마디 하거나 못 본 척한다).
    private static string[] LewdWords => LewdWordsLex[Lang];
    private static readonly string[][] LewdWordsLex = Lex("LewdWords", new[] { "따먹", "보지", "자지", "섹스", "쎅스", "ㅅㅅ", "야동", "야스", "딸치", "딸딸", "자위", "꼴린", "꼴려", "빨통", "젖꼭", "쑤시", "박고 싶", "떡치", "떡 치", "원나잇", "성욕", "sex", "porn", "boobs", "horny", "nudes", "エロ", "セックス", "おっぱい", "ちんこ", "まんこ" });
    /// <summary>음란한 말인가.</summary>
    public static bool IsLewd(string text) => Has((text ?? "").ToLowerInvariant(), LewdWords);
    // 자음만으로 뜻이 통하는 줄임말. 이것들은 잡음이 아니다.
    private static string[] JamoWords => JamoWordsLex[Lang];
    private static readonly string[][] JamoWordsLex = Lex("JamoWords", new[] { "ㅎㄷㄷ", "ㅇㅇ", "ㄴㄴ", "ㄱㄱ", "ㅇㅋ", "ㅎㅇ", "ㅂㅂ", "ㅂㅇ", "ㅅㅂ", "ㅆㅂ", "ㄷㄷ", "ㄹㅇ", "ㅇㅈ", "ㅊㅋ", "ㅊㅊ", "ㄱㅅ", "ㅈㅅ", "ㅁㄹ", "ㅇㄷ", "ㅈㄴ", "ㄱㅇㄷ", "ㅎㅇㅌ", "ㄹㅈㄷ", "ㅁㅊ", "ㅋ", "ㅎ", "ㅠ", "ㅜ" });
    /// <summary>
    /// 뜻이 없는 글인가: 숫자·기호뿐이거나(123123, :), ...), 자판을 그냥 두드린 자음·모음뿐인 글(ㅁㄴㅇㄹ).
    /// "ㅋㅋ", "ㅇㅇ", "ㄱㄱ"처럼 자음만으로 통하는 말은 잡음이 아니다.
    /// </summary>
    public static bool IsNoise(string text)
    {
        string line = (text ?? "").Trim();
        if (line.Length == 0) { return true; }
        if (line.Length >= 4 && line.Trim(line[0]).Length == 0 && "ㅋㅎㅠㅜwｗ草笑.?!~".IndexOf(line[0]) < 0) { return true; }
        bool letters = false;
        foreach (string word in JamoWords) { if (line.Contains(word)) { letters = true; line = line.Replace(word, ""); } }
        bool jamo = false, rest = false, latin = line.Length >= 3, vowel = false;
        foreach (char c in line)
        {
            char low = char.ToLowerInvariant(c);
            if (low < 'a' || low > 'z') { latin = false; } else if ("aeiouy".IndexOf(low) >= 0) { vowel = true; }
        }
        // 영문 자판을 그냥 두드린 글(asdf, qwer, zxcv, sdfg): 모음이 없거나 자판 한 줄을 훑은 글.
        if (latin)
        {
            string keys = line.ToLowerInvariant();
            if (!vowel && keys.Length >= 5 && keys != "hmmmm") { return true; }
            foreach (string row in new[] { "asdf", "qwer", "zxcv", "hjkl", "wasd", "uiop" }) { if (keys.Contains(row)) { return true; } }
        }
        foreach (char c in line)
        {
            if (char.IsWhiteSpace(c)) { continue; }
            if (c >= 0x3131 && c <= 0x3163) { jamo = true; continue; }
            if (char.IsLetter(c)) { return false; }
            rest = true;
        }
        // 글자가 하나도 없다. 줄임말을 걷어 내고 남은 것이 없으면 줄임말만 쓴 글이니 잡음이 아니다. 숫자·기호·낱자가 남았으면 잡음이다.
        // 줄임말에 물음표·마침표만 붙은 글("ㄹㅇ?", "ㅇㅇ..")은 줄임말로 한 말이다.
        return jamo || rest && !letters;
    }
    // 감탄사로도 쓰는 욕. 사람을 가리키는 말(너, 니가, 놈 …) 없이 이것만 있으면 막말로 치지 않는다. "새끼"·"병신"처럼 사람을 부르는 욕은 여기 넣지 않는다.
    private static string[] Expletives => ExpletivesLex[Lang];
    private static readonly string[][] ExpletivesLex = Lex("Expletives", new[] { "씨발", "시발", "씨바", "씨팔", "시팔", "쓰발", "씨빨", "ㅅㅂ", "ㅆㅂ", "씹", "좆", "졷", "fucking", "fuck", "fck", "shit", "クソ", "くそ" });
    private static string[] AimedWords => AimedWordsLex[Lang];
    private static readonly string[][] AimedWordsLex = Lex("AimedWords", new[] { "너 ", "넌 ", "너는", "너가", "너도", "너나", "니가", "네가", "너네", "니네", "니들", "너희", "님들", "놈", "년", "@", "you", "u r ", "お前", "おまえ", "てめ", "貴様" });
    // 욕설은 띄어쓰기·기호·숫자를 끼워 넣어도 알아듣도록, 글자만 남긴 글에서 찾는다.
    private static string[] Swear => SwearLex[Lang];
    private static readonly string[][] SwearLex = Lex("Swear", new[] { "씨발", "시발", "씨바", "씨팔", "시팔", "쓰발", "씨빨", "ㅅㅂ", "ㅆㅂ", "씹", "좆", "졷", "병신", "븅신", "빙신", "ㅄ", "새끼", "새키", "섀끼", "쉐끼", "ㅅㄲ", "개놈", "개년", "년들", "련들", "년아", "련아", "미친놈", "미친년", "미친새", "또라이", "지랄", "ㅈㄹ", "등신", "머저리", "얼간이", "쪼다", "개돼지", "거지새", "아가리", "주둥이",
        "fuck", "fck", "shit", "bitch", "asshole", "bastard", "retard", "moron", "scum", "dumbass", "dipshit", "stfu", "クソ", "くそ", "カス", "ゴミ", "きもい", "キモい", "うざい", "ウザい", "ボケ", "クズ", "くず", "消えろ", "失せろ" });
    private static string[] Vile => VileLex[Lang];
    private static readonly string[][] VileLex = Lex("Vile", new[] { "애미", "애비", "에미", "니엄마", "너희엄마", "니네엄마", "니아빠", "느금", "엄창", "뒤져", "뒤진", "뒤질", "뒈져", "뒈진", "디져", "죽어버려", "다죽어", "죽여버", "쳐죽", "암적", "쑤셔", "강간", "패죽", "찢어죽",
        "kys", "killyourself", "yourmom", "yourmother", "motherf", "godie", "dropdead", "hopeyoudie", "死ね", "殺す", "殺し", "くたばれ", "お前の母", "親の顔", "自殺" });
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
    private static string[] FamilyWords => FamilyWordsLex[Lang];
    private static readonly string[][] FamilyWordsLex = Lex("FamilyWords", new[] { "애미", "애비", "에미", "엄마", "아빠", "느금", "엄창", "부모", "yourmom", "yourmother", "motherf", "お前の母", "親の顔" });
    private static string[] DeathWords => DeathWordsLex[Lang];
    private static readonly string[][] DeathWordsLex = Lex("DeathWords", new[] { "뒤져", "뒤질", "뒈져", "디져", "죽어버려", "다죽어", "죽여버", "쳐죽", "패죽", "찢어죽", "kys", "killyourself", "godie", "dropdead", "hopeyoudie", "死ね", "殺す", "殺し", "くたばれ", "自殺" });
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
        // 감탄사로 뱉은 욕("와 씨발 개쏜다", "쏜다 ㅅㅂ")은 누구를 욕한 것이 아니다. 그 감탄사를 빼고 남은 말로 다시 본다.
        if (level == 2 && !Has(lower, AimedWords))
        {
            string rest = lower;
            foreach (string word in Expletives) { rest = rest.Replace(word, " "); }
            string restBare = Bare(rest);
            level = Has(restBare, Vile) ? 3 : Has(restBare, Swear) ? 2 : Has(rest, Rude) ? 1 : 0;
        }
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
    private static string[] BragWords => BragWordsLex[Lang];
    private static readonly string[][] BragWordsLex = Lex("BragWords", new[] { "수익", "익절", "먹었", "먹음", "벌었", "벌음", "땄", "수익률", "플러스", "빨간불", "대박 났", "대박났", "대박 쳤", "떡상했", "profit", "made money", "made bank", "i made", "gains", "i'm up", "im up", "up big", "banked", "利確", "儲か", "勝った", "爆益" });
    private static string[] LossWords => LossWordsLex[Lang];
    private static readonly string[][] LossWordsLex = Lex("LossWords", new[] { "물렸", "물림", "손절", "잃었", "잃음", "마이너스", "파란불", "깡통", "손실", "털렸", "반토막", "bagholding", "i lost", "lost money", "lost it all", "loss", "i'm down", "im down", "down bad", "down big", "rekt", "stopped out", "損切", "含み損", "負けた", "爆損" });
    private static string[] BuyAsk => BuyAskLex[Lang];
    private static readonly string[][] BuyAskLex = Lex("BuyAsk", new[] { "살까", "사도", "사야", "탈까", "타도", "들어가도", "들어갈까", "진입", "매수각", "줍줍", "담을까", "should i buy", "buy now", "get in", "worth buying", "買い?", "買う?", "買っていい", "入っていい", "買い時" });
    private static string[] SellAsk => SellAskLex[Lang];
    private static readonly string[][] SellAskLex = Lex("SellAsk", new[] { "팔까", "팔아야", "던질까", "던져야", "손절할까", "익절할까", "정리할까", "매도각", "should i sell", "sell now", "take profit", "get out", "売り?", "売る?", "売っていい", "利確していい", "売り時" });
    private static string[] OpinionAsk => OpinionAskLex[Lang];
    private static readonly string[][] OpinionAskLex = Lex("OpinionAsk", new[] { "어때", "어떰", "어떻게 봐", "어떻게 보", "어케 봄", "어떨까", "괜찮", "갈까", "오를까", "떨어질까", "전망", "의견", "목표가", "얼마까지", "언제 오", "언제 가", "언제 감", "what do you think", "thoughts", "how about", "any good", "どう思う", "どう?", "どうかな", "どうよ", "見通し" });
    private static string[] PickAsk => PickAskLex[Lang];
    private static readonly string[][] PickAskLex = Lex("PickAsk", new[] { "뭐 살", "뭐 사", "뭘 사", "뭐 타", "추천", "종목 좀", "뭐가 좋", "어디 들어", "what to buy", "any picks", "recommend", "what should i buy", "何買", "おすすめ", "何がいい" });
    private static string[] UpWords => UpWordsLex[Lang];
    private static readonly string[][] UpWordsLex = Lex("UpWords", new[] { "오른다", "오를", "올라간다", "올라갈", "간다", "갈듯", "갈 듯", "갈거", "갈 거", "갈것", "갈 것", "쏜다", "쏠", "떡상", "상한가", "가즈아", "가자", "가보자", "ㄱㄱ", "풀매수", "날아간다", "터진다", "급등", "올린다", "to the moon", "going up", "mooning", "pump", "rip", "sends", "上がる", "爆上げ", "ストップ高", "飛ぶ", "買い増し" });
    private static string[] DownWords => DownWordsLex[Lang];
    private static readonly string[][] DownWordsLex = Lex("DownWords", new[] { "떨어진다", "떨어질", "내려간다", "내려갈", "내릴", "빠진다", "빠질", "떡락", "하한가", "망했", "끝났", "폭락", "급락", "무너진다", "던져라", "going down", "dump", "crash", "tank", "it's over", "dead", "下がる", "暴落", "ストップ安", "終わった", "逃げろ" });
    private static string[] WhaleWords => WhaleWordsLex[Lang];
    private static readonly string[][] WhaleWordsLex = Lex("WhaleWords", new[] { "내가 세력", "나 세력", "제가 세력", "세력이다", "내가 올린", "내가 올릴", "내가 올려", "올려줄게", "올려 줄게", "내가 띄", "작전 들어", "내가 쏜다", "i'm the whale", "i am the whale", "i'll pump", "i move this", "俺が仕手", "仕手だ", "俺が上げる" });

    // ── 금융 얘기를 묻는 말 ──
    private static string[] NewsAsk => NewsAskLex[Lang];
    private static readonly string[][] NewsAskLex = Lex("NewsAsk", new[] { "소식", "뉴스", "정보", "찌라시", "공시", "호재", "악재", "무슨 일", "무슨일", "뭔 일", "뭔일", "별일", "루머", "기사", "떴냐", "떴음", "news", "rumor", "rumour", "headline", "any info", "what happened", "anything happen", "what's going on", "whats going on", "what did i miss", "ニュース", "情報", "材料", "噂", "何かあった", "なんかあった", "何があった" });
    private static string[] NightWords => NightWordsLex[Lang];
    private static readonly string[][] NightWordsLex = Lex("NightWords", new[] { "밤에", "밤새", "밤사이", "밤 사이", "간밤", "어제", "새벽에", "자는 동안", "overnight", "last night", "yesterday", "while i slept", "昨夜", "夜の間", "昨日", "寝てる間", "夜中" });
    private static string[] WhaleAsk => WhaleAskLex[Lang];
    private static readonly string[][] WhaleAskLex = Lex("WhaleAsk", new[] { "세력", "작전", "큰손", "고래", "기관", "주포", "누가 올", "누가 던", "누가 사", "누가 파", "매집", "whale", "whales", "big money", "big player", "manipulation", "manipulating", "who's buying", "who is buying", "who's selling", "who is selling", "仕手", "大口", "クジラ", "機関", "誰が買", "誰が売" });
    private static string[] MarketAsk => MarketAskLex[Lang];
    private static readonly string[][] MarketAskLex = Lex("MarketAsk", new[] { "장 어때", "오늘 장", "장 분위기", "분위기", "시장", "지수", "장 왜", "장이 왜", "오늘 어때", "장 좋", "장 안 좋", "장이 좋", "장이 안", "market", "how's today", "hows today", "how is today", "相場", "地合い", "今日どう", "市場" });
    private static string[] HotAsk => HotAskLex[Lang];
    private static readonly string[][] HotAskLex = Lex("HotAsk", new[] { "뭐 보고", "뭐 봄", "뭐 보는", "뭘 보", "뭐보", "뭐가 오", "뭐 오르", "뭐가 가", "뭐 감", "핫한", "핫함", "뭐가 핫", "뭐가 뜨", "뜨는", "급등주", "대장", "주도주", "뭐가 세", "뭐 빠", "뭐가 빠", "뭐 떨어", "뭐가 떨어", "what are you watching", "what are you looking at", "what's moving", "whats moving", "what's hot", "whats hot", "movers", "what's pumping", "what's running", "何見てる", "なに見てる", "何が上が", "何が動", "注目", "何が熱" });
    private static string[] HoldAsk => HoldAskLex[Lang];
    private static readonly string[][] HoldAskLex = Lex("HoldAsk", new[] { "뭐 들고", "뭘 들고", "뭐 갖고", "뭐 가지고", "뭐 샀", "뭘 샀", "뭐 삼", "뭘 삼", "뭐 탔", "뭐 담았", "보유 종목", "보유종목", "포트", "뭐 물렸", "what are you holding", "what do you hold", "what did you buy", "your bags", "portfolio", "positions", "何持って", "なに持って", "何買った", "ポートフォリオ", "ポジ" });
    private static string[] ProfitAsk => ProfitAskLex[Lang];
    private static readonly string[][] ProfitAskLex = Lex("ProfitAsk", new[] { "수익 어때", "수익률 어때", "다들 수익", "다들 벌", "벌었냐", "벌었음?", "벌었어?", "벌고 있", "계좌 어때", "계좌 상태", "얼마 벌", "얼마 잃", "다들 손실", "다들 물렸", "잃었냐", "수익 났냐", "수익 났음?", "수익률 몇", "수익 몇", "몇 퍼 벌", "몇 퍼 먹", "얼마 먹", "물렸음?", "물렸냐", "물렸어?", "물렸나?", "how's your pnl", "hows your pnl", "anyone green", "anyone up", "anyone red", "you guys up", "making money", "how much did you make", "how are you doing today", "みんな儲", "儲かってる", "勝ってる?", "負けてる?", "収支" });
    private static string[] WhyWords => WhyWordsLex[Lang];
    private static readonly string[][] WhyWordsLex = Lex("WhyWords", new[] { "왜", "이유", "근거", "why", "how come", "reason", "なぜ", "なんで", "どうして", "理由", "根拠" });
    private static string[] BackWords => BackWordsLex[Lang];
    private static readonly string[][] BackWordsLex = Lex("BackWords", new[] { "왜", "이유", "근거", "진짜", "ㄹㅇ", "정말", "레알", "그래서", "어째서", "why", "really", "for real", "seriously", "how so", "and then", "so what", "なぜ", "なんで", "マジ", "本当", "ほんと", "それで", "根拠" });
    private static string[] HowAsk => HowAskLex[Lang];
    private static readonly string[][] HowAskLex = Lex("HowAsk", new[] { "어떻게 해", "어떻게 하", "어떻게 함", "어떻게 넣", "어떻게 사", "어떻게 팔", "어케 해", "어케 함", "어케 넣", "뭐 하면 됨", "뭐 하면 돼", "뭐부터", "방법", "하는 법", "하는법", "뭔 뜻", "무슨 뜻", "뜻이 뭐", "알려줘", "알려 줘", "알려주", "가르쳐", "how do i", "how to", "what does", "explain", "teach me", "どうやって", "やり方", "って何", "教えて" });
    private static string[] NickAsk => NickAskLex[Lang];
    private static readonly string[][] NickAskLex = Lex("NickAsk", new[] { "줄여", "줄임말", "약자", "약칭", "별명", "뭐라고 불", "뭐라 불", "뭐라고 부", "뭐라 부", "short for", "nickname", "abbreviat", "what do you call", "略", "あだ名", "なんて呼", "何て呼" });
    // ── 내 말에서 뽑는 낱말 ──
    private static string[] Particles => ParticlesLex[Lang];
    private static readonly string[][] ParticlesLex = Lex("Particles", new[] { "이랑", "에서", "에게", "한테", "으로", "이나", "부터", "까지", "처럼", "보다", "랑", "로", "은", "는", "이", "가", "을", "를", "에", "의", "도", "만", "과", "와", "야", "요" });
    private static string[] VerbEnds => VerbEndsLex[Lang];
    private static readonly string[][] VerbEndsLex = Lex("VerbEnds", new[] { "한다", "했다", "했음", "하다", "이다", "된다", "싶다", "같다", "있다", "없다", "있음", "없음", "같음", "싶음", "하네", "하지", "할까", "인가", "는데", "어요", "아요", "해요", "니다", "거임", "건데", "인데", "하고", "해서", "하면", "함", "임", "됨", "듯", "네", "냐", "까", "죠", "다" });
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal) { "나", "내", "너", "오늘", "어제", "내일", "지금", "진짜", "그냥", "왜", "뭐", "이거", "그거", "저거", "여기", "거기", "다들", "님들", "근데", "그리고", "아니", "너무", "완전", "약간", "요즘", "갑자기",
        "이제", "아직", "벌써", "혹시", "원래", "보통", "뭔가", "누가", "어디", "언제", "어떻게", "사람", "생각", "얘기", "정도", "이건", "그건", "우리", "제가", "저는", "이런", "그런", "저런", "어떤", "무슨", "아무", "하나", "이번", "그게", "이게",
        "what", "that", "this", "with", "have", "just", "like", "really", "about", "there", "they", "them", "your", "from", "today", "some", "does", "when", "where", "which", "think", "know", "guys", "everyone", "anyone" };
    /// <summary>
    /// 글에서 얘깃거리가 될 낱말 하나를 뽑는다(봇이 그 낱말을 받아서 답한다: "치킨? 갑자기? ㅋㅋ"). 조사와 흔한 말, 서술어는 건너뛰고 남은 것 가운데 가장 긴 낱말.
    /// 띄어 쓰지 않는 언어(일본어)나 뽑을 낱말이 없는 글이면 null.
    /// </summary>
    public static string Keyword(string text)
    {
        string best = null; int bestScore = 0;
        foreach (string raw in (text ?? "").Split(' ', ',', '.', '?', '!', '~', '…', '·'))
        {
            string word = raw.Trim(); if (word.Length < 2 || word.Length > 10) { continue; }
            bool hangul = true, latin = true, aside = false;
            foreach (char c in word) { if (c < '\uAC00' || c > '\uD7A3') { hangul = false; } if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')) { latin = false; } }
            if (hangul)
            {
                // "주말에", "회사에서"처럼 때와 곳을 가리키는 말은 얘깃거리가 되기 어렵다. 뒤로 미룬다.
                foreach (string particle in Particles)
                {
                    if (word.Length - particle.Length < 2 || !word.EndsWith(particle, StringComparison.Ordinal)) { continue; }
                    aside = particle == "에" || particle == "에서" || particle == "부터" || particle == "까지" || particle == "으로" || particle == "로";
                    word = word.Substring(0, word.Length - particle.Length); break;
                }
                bool verb = false; foreach (string end in VerbEnds) { if (word.EndsWith(end, StringComparison.Ordinal)) { verb = true; break; } }
                if (verb || word.Length < 2 || StopWords.Contains(word)) { continue; }
            }
            else if (latin) { if (word.Length < 4 || StopWords.Contains(word.ToLowerInvariant())) { continue; } }
            else { continue; }
            // 길수록, 그리고 뒤에 나올수록(우리말은 하려는 말이 뒤에 온다) 얘깃거리로 친다.
            int score = word.Length * 2 - (aside ? 3 : 0);
            if (best == null || score >= bestScore) { best = word; bestScore = score; }
        }
        return best;
    }
    // ── 맞장구와 짧은 대꾸 ──
    private static string[] AgreeWords => AgreeWordsLex[Lang];
    private static readonly string[][] AgreeWordsLex = Lex("AgreeWords", new[] { "ㅇㅇ", "ㅇㅈ", "ㄹㅇ", "인정", "맞아", "맞네", "맞지", "맞음", "그치", "그니까", "그러게", "그렇지", "ㅇㅋ", "오키", "오케이", "굿", "좋아", "좋지", "ㄱㄱ", "당연", "그렇구나", "그렇군", "그렇네", "그럴 수도", "그럴수도", "그런가", "글쿤", "알겠", "이해함", "이해했", "아하", "오호", "ㅇㅎ", "나도 그", "나도 마찬", "나도임", "나도 ㅋ", "나도ㅋ", "me too", "same", "yeah", "yep", "yes", "true", "agreed", "right", "ok", "okay", "sure", "fr", "exactly", "うん", "そう", "それな", "たしかに", "確かに", "おけ", "了解", "はい" });
    private static string[] DisagreeWords => DisagreeWordsLex[Lang];
    private static readonly string[][] DisagreeWordsLex = Lex("DisagreeWords", new[] { "ㄴㄴ", "아닌데", "아닌 듯", "아닌듯", "아닌 것 같", "생각 안", "아니야", "아님", "아니", "노노", "별로", "싫어", "싫음", "안 해", "안해", "글쎄다", "nope", "nah", "no", "no way", "disagree", "not really", "いや", "違う", "ちがう", "ないわ", "いいえ" });
    private static string[] DunnoWords => DunnoWordsLex[Lang];
    private static readonly string[][] DunnoWordsLex = Lex("DunnoWords", new[] { "몰라", "모름", "모르겠", "글쎄", "몰겠", "모르지", "몰루", "ㅁㄹ", "idk", "dunno", "no idea", "not sure", "who knows", "わからん", "分からん", "知らん", "さあ", "わかんない" });
    private static string[] SurpriseWords => SurpriseWordsLex[Lang];
    private static readonly string[][] SurpriseWordsLex = Lex("SurpriseWords", new[] { "헐", "대박", "미쳤", "우와", "와우", "오오", "실화", "ㄷㄷ", "ㅁㅊ", "세상에", "와씨", "레전드", "ㄹㅈㄷ", "지렸", "소름", "와 ", "wow", "omg", "holy", "wtf", "insane", "damn", "マジか", "やば", "うお", "えぐ", "すげ" });
    // ── 묻는 낱말 ──
    private static string[] QuestionWords => QuestionWordsLex[Lang];
    private static readonly string[][] QuestionWordsLex = Lex("QuestionWords", new[] { "뭐", "뭘", "왜", "어디", "언제", "누가", "누구", "어떻게", "어케", "어떰", "어때", "어떤가", "몇", "얼마", "무슨", "뭔", "있냐", "없냐", "있나", "없나", "인가", "런가", "던가", "는가", "할까", "될까", "을까", "what", "why", "who", "how", "when", "where", "which", "anyone", "anybody", "any", "is it", "are you", "do you", "can i", "should i", "何", "なに", "なぜ", "なんで", "どう", "誰", "いつ", "どこ", "かな", "ですか", "ますか" });
    // ── 시답잖은 얘기의 갈래 ──
    private static readonly (string key, string[] words)[] Topics =
    {
        ("lag", new[] { "렉", "랙 걸", "버벅", "끊기", "끊김", "튕기", "튕김", "프레임", "fps", "lag", "stutter", "ラグ", "重い", "カクつ" }),
        ("game", new[] { "무슨 게임", "이 게임", "이게임", "게임임", "게임이야", "게임인가", "뭐하는 게임", "뭐 하는 게임", "what game", "this game", "何のゲーム", "このゲーム" }),
        ("place", new[] { "특별시", "광역시", "사는 곳", "우리 동네", "울 동네", "강남", "where i live", "my town", "住んで", "地元" }),
        ("who", new[] { "봇이", "봇임", "봇이냐", "봇이지", "봇 아니", "사람이냐", "사람임?", "사람 맞", "ai냐", "ai임", "몇 살", "몇살", "어디 살", "어디 사", "어디 삼", "직업", "뭐 하는 사람", "뭐 하는 분", "무슨 일 하", "무슨일 하", "하는 일이", "누구세요", "누구임", "누구냐", "are you a bot", "are you bots", "are you real", "are you human", "how old", "where are you from", "what do you do", "ボット", "人間?", "何歳", "どこ住み", "何者" }),
        ("doing", new[] { "뭐해", "뭐함", "뭐 해", "뭐 함", "뭐하냐", "뭐 하냐", "뭐 하는 중", "뭐하는 중", "뭐하고", "what are you doing", "what are you up to", "wyd", "what's up", "whats up", "sup", "何してる", "なにしてる", "何してん" }),
        ("time", new[] { "몇 시", "몇시", "언제 열", "언제 닫", "장 언제", "언제 끝", "마감 언제", "개장 언제", "what time", "when does", "when is the", "market open", "market close", "何時", "いつ開", "いつ終", "引けは" }),
        ("food", new[] { "밥", "점심", "저녁", "아침 먹", "야식", "치킨", "라면", "배고", "뭐 먹", "뭐먹", "먹을까", "커피", "피자", "국밥", "삼겹", "떡볶이", "짜장", "짬뽕", "탕수육", "햄버거", "간식", "음식", "배불", "배부", "치맥", "먹고 싶", "파전", "혼밥", "집밥", "맛집", "메뉴", "마라탕", "초밥", "족발", "lunch", "dinner", "breakfast", "hungry", "eat", "eating", "food", "coffee", "pizza", "burger", "snack", "ご飯", "昼飯", "夕飯", "腹減", "ラーメン", "コーヒー", "飯食", "何食" }),
        ("drink", new[] { "술", "혼술", "낮술", "술자리", "술집", "금주", "술 끊", "필름 끊", "숙취", "해장", "맥주", "소주", "한잔", "한 잔", "막걸리", "와인", "위스키", "beer", "drink", "drinks", "drunk", "whiskey", "酒", "ビール", "飲み", "ハイボール" }),
        ("sleep", new[] { "졸려", "졸리", "졸림", "피곤", "잠 와", "잠온", "잠와", "자고 싶", "못 잤", "못잤", "잠이 안", "잠 안 와", "잠 안와", "시간 잤", "자고 일어", "불면", "늦잠", "밤샘", "밤샜", "눈 감", "sleepy", "tired", "exhausted", "no sleep", "all nighter", "眠い", "眠たい", "疲れ", "徹夜", "寝不足" }),
        ("weather", new[] { "날씨", "비 오", "비온", "비 온", "비가 오", "비가 온", "눈 오", "눈온", "눈 온", "눈이 오", "눈이 온", "덥다", "더워", "춥다", "추워", "미세먼지", "장마", "weather", "raining", "rainy", "snowing", "cold out", "hot out", "freezing", "天気", "雨", "暑い", "寒い", "雪" }),
        ("bored", new[] { "심심", "노잼", "조용하", "조용~", "조용 ", "한산", "지루", "할 거 없", "할거 없", "할 게 없", "할게 없", "뭐 하지", "잠잠", "bored", "boring", "quiet", "dead chat", "so slow", "暇", "退屈", "静か", "過疎" }),
        ("work", new[] { "회사", "출근", "퇴근", "퇴사", "월급", "사장", "부장", "팀장", "야근", "직장", "알바", "백수", "회의", "상사", "work", "job", "boss", "salary", "paycheck", "office", "meeting", "overtime", "会社", "出勤", "給料", "上司", "残業", "仕事", "無職", "会議" }),
        ("rich", new[] { "부자", "은퇴", "람보", "건물주", "집 사", "차 사", "경제적 자유", "졸업", "파이어", "일확천금", "억 생기", "돈 벌면", "돈 많으면", "부자 되면", "1등 되면", "당첨되면", "당첨 되면", "rich", "retire", "lambo", "millionaire", "financial freedom", "金持ち", "引退", "億り人", "一攫千金" }),
        ("love", new[] { "여친", "남친", "결혼", "연애", "와이프", "마누라", "남편", "소개팅", "솔로", "썸", "girlfriend", "boyfriend", "wife", "husband", "married", "single", "dating", "彼女", "彼氏", "結婚", "嫁", "独身", "恋愛" }),
        ("fun", new[] { "게임", "영화", "넷플", "유튜브", "드라마", "축구", "야구", "노래", "웹툰", "만화", "game", "gaming", "movie", "netflix", "youtube", "football", "soccer", "baseball", "music", "ゲーム", "映画", "アニメ", "野球", "サッカー", "漫画" }),
        ("newbie", new[] { "뉴비", "초보", "주린이", "입문", "시작했", "처음 해", "처음이", "처음인데", "처음임", "처음 옴", "처음 왔", "오늘 처음", "가입", "newbie", "noobie", "new here", "beginner", "first time", "just started", "初心者", "初めて", "始めたばかり", "新参" }),
        ("praise", new[] { "고수", "잘한다", "잘하네", "대단", "멋지", "멋있", "존경", "천재", "똑똑", "형님", "genius", "pro", "respect", "nice one", "legend", "smart", "well played", "すごい", "天才", "さすが", "神", "尊敬" }),
        ("luck", new[] { "로또", "운이", "운빨", "기도", "제발", "물 떠놓고", "행운", "lottery", "luck", "lucky", "pray", "praying", "please god", "fingers crossed", "宝くじ", "運", "祈", "頼む", "神頼み" }),
        ("health", new[] { "아프", "아파", "감기", "독감", "기관지", "병원", "수술", "내시경", "건강검진", "두통", "허리", "몸살", "sick", "headache", "hospital", "my back", "a cold", "風邪", "病院", "頭痛", "腰が", "体調" }),
        ("home", new[] { "엄마", "아빠", "부모님", "집에", "집 가", "집 간", "애기", "애가", "강아지", "고양이", "댕댕", "냥이", "mom", "dad", "my kid", "kids", "dog", "cat", "at home", "親", "子供", "犬", "猫", "実家" }),
        ("angry", new[] { "짜증", "빡치", "빡침", "기분 잡", "잡쳤", "열받", "화나", "화남", "멘붕", "현타", "어이없", "annoyed", "annoying", "pissed", "tilted", "mad", "furious", "ムカつく", "イライラ", "萎え", "腹立つ" }),
        ("happy", new[] { "기분 좋", "기분좋", "행복", "신난", "신나", "개꿀", "꿀잼", "나이스", "좋았어", "ㄱㅇㄷ", "개이득", "좋은 날", "feeling good", "happy", "lets go", "let's go", "great day", "嬉しい", "最高", "気分いい", "楽しい" }),
    };

    // ── 봇이 되물은 말에 내가 한 답을 읽는 낱말 ──
    private static string[] ShortStyle => ShortStyleLex[Lang];
    private static readonly string[][] ShortStyleLex = Lex("ShortStyle", new[] { "단타", "스캘", "단기", "초단", "scalp", "scalper", "day trade", "day trader", "day trading", "short term", "短期", "デイトレ", "スキャ" });
    private static string[] LongStyle => LongStyleLex[Lang];
    private static readonly string[][] LongStyleLex = Lex("LongStyle", new[] { "장투", "장기", "존버", "묵히", "가치투자", "long term", "long-term", "hold forever", "investor", "長期", "ガチホ", "塩漬け" });
    // ── 인증과 함께 올린 글의 말투 ──
    public static string[] Cocky => CockyLex[Lang];
    private static readonly string[][] CockyLex = Lex("Cocky", new[] { "쉽네", "쉽다", "껌", "실력", "이게 나", "봤냐", "봤지", "나만 믿", "역시 나", "천재", "개꿀", "따라와", "ez", "easy", "too easy", "skill", "genius", "told you", "follow me", "余裕", "簡単", "天才", "俺を信じろ", "ほらね", "ついてこい" });
    public static string[] Humble => HumbleLex[Lang];
    private static readonly string[][] HumbleLex = Lex("Humble", new[] { "운이", "운 좋", "겨우", "다행", "간신히", "어쩌다", "lucky", "barely", "phew", "somehow", "運が", "なんとか", "たまたま" });
    public static string[] Hurting => HurtingLex[Lang];
    private static readonly string[][] HurtingLex = Lex("Hurting", new[] { "망했", "죽겠", "힘들", "어떡", "어쩌", "멘붕", "눈물", "살려", "하...", "미치겠", "ruined", "wrecked", "help", "i'm done", "what do i do", "hurts", "終わった", "助けて", "つらい", "どうしよう", "きつい" });
    public static string[] WantsAdvice => WantsAdviceLex[Lang];
    private static readonly string[][] WantsAdviceLex = Lex("WantsAdvice", new[] { "어떡", "어쩌", "팔까", "팔아야", "버틸까", "버텨야", "손절", "익절", "물타", "들고 갈까", "들고 가도", "should i", "what do i do", "sell or hold", "cut it", "hold or", "どうしよう", "売るべき", "損切り", "利確", "ナンピン", "持つべき" });
    public static string[] NotYet => NotYetLex[Lang];
    private static readonly string[][] NotYetLex = Lex("NotYet", new[] { "안 먹", "안먹", "아직", "굶", "못 먹", "못먹", "not yet", "haven't", "nothing yet", "skipped", "まだ", "食べてない", "食ってない" });
    public static string[] Hungry => HungryLex[Lang];
    private static readonly string[][] HungryLex = Lex("Hungry", new[] { "배고", "hungry", "starving", "腹減", "お腹すい" });
    public static string[] Ate => AteLex[Lang];
    private static readonly string[][] AteLex = Lex("Ate", new[] { "먹었", "먹음", "먹고", "ate", "had lunch", "had dinner", "eaten", "食べた", "食った" });
    public static string[] NewHere => NewHereLex[Lang];
    private static readonly string[][] NewHereLex = Lex("NewHere", new[] { "처음", "뉴비", "초보", "오늘", "얼마 안", "막 시작", "주린이", "어제", "이번 주", "just started", "new", "today", "first", "beginner", "this week", "初めて", "初心者", "今日", "始めたばかり", "昨日" });
    public static string[] LongAgo => LongAgoLex[Lang];
    private static readonly string[][] LongAgoLex = Lex("LongAgo", new[] { "년", "오래", "꽤 됐", "한참", "year", "years", "long time", "a while", "ages", "年", "長い", "だいぶ" });
    public static string[] Nothing => NothingLex[Lang];
    private static readonly string[][] NothingLex = Lex("Nothing", new[] { "없", "현금", "안 샀", "안샀", "nothing", "none", "cash", "didn't buy", "ない", "現金", "ノーポジ", "買ってない" });
    /// <summary>글에 그 낱말들 가운데 하나가 들어 있는가.</summary>
    public static bool Says(string text, string[] words) => HasWord((text ?? "").ToLowerInvariant(), words);

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
    private static bool IsHangul(char c) => c >= '\uAC00' && c <= '\uD7A3';
    /// <summary>
    /// 낱말들 가운데 글에 들어 있는 가장 긴 것의 길이(없으면 0).
    /// 한 글자 낱말(술, 렉, 밥, 똥)은 낱말의 첫머리에 올 때만 친다. 아니면 "기술", "컬렉션", "수술"에 걸린다.
    /// </summary>
    private static int Longest(string lower, string[] words)
    {
        int best = 0;
        foreach (string word in words)
        {
            if (word.Length <= best) { continue; }
            int at = lower.IndexOf(word, StringComparison.Ordinal);
            while (at >= 0)
            {
                int end = at + word.Length;
                bool left = !IsLatin(word[0]) || at == 0 || !IsLatin(lower[at - 1]);
                bool right = !IsLatin(word[word.Length - 1]) || end >= lower.Length || !IsLatin(lower[end]);
                if (word.Length == 1 && IsHangul(word[0]) && at > 0 && IsHangul(lower[at - 1])) { left = false; }
                if (left && right) { best = word.Length; break; }
                at = end < lower.Length ? lower.IndexOf(word, at + 1, StringComparison.Ordinal) : -1;
            }
        }
        return best;
    }
    /// <summary>
    /// 글이 어느 갈래의 얘기인가. 여러 갈래에 걸리면 더 길게 맞은(더 좁은) 쪽을 고른다: "야근"은 회사 얘기이기도 하지만 야근 얘기다.
    /// 길이가 같으면 자료 파일(Chat/chat_topics.ko / .en / .ja)의 갈래가 이긴다(그쪽이 더 잘게 나뉘어 있다).
    /// "뭐 함"(doing)은 다른 얘깃거리가 없을 때만 친다("게임 뭐 함?"은 게임 얘기다).
    /// 기분을 말하는 갈래(무섭다, 짜증난다, 심심하다 …)도 다른 얘깃거리가 같이 있으면 그쪽에 진다("전세 사기 무섭다"는 집 얘기다).
    /// </summary>
    private static readonly string[] MoodTopics = { "angry", "happy", "bored", "sleep" };
    private static string TopicOf(string lower)
    {
        string best = null; int bestScore = 0;
        foreach (var topic in ChatTopics.All)
        {
            if (topic.term) { continue; }
            int length = Longest(lower, topic.words), score = topic.weak ? 3 : length * 2 + 3;
            if (length > 0 && score > bestScore) { best = topic.key; bestScore = score; }
        }
        foreach (var topic in LangTopics)
        {
            int length = Longest(lower, topic.words), score = topic.key == "doing" ? 1 : Array.IndexOf(MoodTopics, topic.key) >= 0 ? 2 : length * 2 + 2;
            if (length > 0 && score > bestScore) { best = topic.key; bestScore = score; }
        }
        return best;
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
            // 줄임말(산전, 그푸, Novo, ノボ …)로 써도 알아듣는다.
            foreach (string nickname in StockNicknames.All(stock.symbol))
            {
                string name = Squeeze(nickname);
                if (name.Length >= 2 && Said(words, text, name) && name.Length + 50 > bestLength) { bestLength = name.Length + 50; best = stock; }
            }
        }
        return best;
    }

    /// <summary>글에 나온 또 다른 종목(first가 아닌 것). 온전한 이름이나 줄임말로 쓴 것만 친다.</summary>
    public static Stock FindOtherStock(string words, Stock first)
    {
        var manager = StockManager.Instance; string text = Squeeze(words);
        if (manager == null || first == null || text.Length == 0) { return null; }
        foreach (var stock in manager.stocks)
        {
            if (stock == null || stock == first || !stock.IsListed || stock.IsDelisted) { continue; }
            foreach (string raw in new[] { GameLocale.Text(stock.stockName), stock.stockName })
            { string name = Squeeze(raw); if (name.Length >= 2 && text.Contains(name)) { return stock; } }
            foreach (string nickname in StockNicknames.All(stock.symbol))
            { string name = Squeeze(nickname); if (name.Length >= 2 && Said(words, text, name)) { return stock; } }
        }
        return null;
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
        if (Lang == 1) { lower = (" " + lower + " ").Replace(" you guys ", " you ").Replace(" y'all ", " you ").Replace(" yall ", " you ").Replace(" u ", " you ").Replace(" ur ", " your ").Replace(" r ", " are ").Trim(); }
        foreach (string command in new[] { "!분석", "！분석", "!analyze", "!analyse", "!analysis", "!分析", "！分析" })
        {
            if (!lower.StartsWith(command, StringComparison.Ordinal)) { continue; }
            talk.Kind = PlayerTalkKind.Analyze; talk.Argument = line.Substring(command.Length).Trim(); talk.Stock = FindStock(talk.Argument, true);
            return talk;
        }
        talk.CallsAll = Has(lower, All); talk.Laughs = LaughPattern.IsMatch(line); talk.Cries = CryPattern.IsMatch(line);
        // 묻는 말꼬리는 웃음·울음·마침표를 떼고 본다("살까 ㅋㅋ", "언제 오르냐..").
        string tail = lower.TrimEnd('ㅋ', 'ㅎ', 'ㅠ', 'ㅜ', '.', '~', '!', ' ', ';', '^', '…', 'w', 'ｗ', '笑', '草', '。', '、', '！');
        talk.Asks = line.Contains("?") || line.Contains("？");
        if (Lang == 0)
        {
            foreach (string end in AskEnds) { if (tail.EndsWith(end, StringComparison.Ordinal)) { talk.Asks = true; break; } }
            // "익절은 언제 함", "얼마까지 감"처럼 물음표 없이 끝나는 음슴체 물음.
            if (!talk.Asks && HasWord(lower, WhenWords)) { foreach (string end in PlainEnds) { if (tail.EndsWith(end, StringComparison.Ordinal)) { talk.Asks = true; break; } } }
        }
        else if (Lang == 1)
        {
            // 영어는 물음표를 잘 안 찍는다. 묻는 말은 첫머리가 알려 준다. "what a day"·"how nice" 같은 감탄은 뺀다.
            string start = lower.TrimStart(); foreach (string lead in new[] { "so ", "ok ", "ok so ", "wait ", "yo ", "guys ", "bro ", "hey " }) { if (start.StartsWith(lead, StringComparison.Ordinal)) { start = start.Substring(lead.Length); } }
            if (!talk.Asks && StartsWord(start, EnAskLeads) && !start.StartsWith("what a ", StringComparison.Ordinal) && !start.StartsWith("how nice", StringComparison.Ordinal) && !start.StartsWith("do not", StringComparison.Ordinal)
                && !start.StartsWith("is what", StringComparison.Ordinal) && !start.StartsWith("will do", StringComparison.Ordinal) && !start.StartsWith("can't", StringComparison.Ordinal) && !start.StartsWith("cant", StringComparison.Ordinal)) { talk.Asks = true; }
        }
        else
        {
            foreach (string end in JaAskEnds) { if (tail.EndsWith(end, StringComparison.Ordinal)) { talk.Asks = true; break; } }
        }
        // 종목은 먼저 온전한 이름으로 찾고, 없으면 앞머리로 찾는다(세 글자 이상 겹칠 때만: "미래에는" 같은 말에 걸리지 않게).
        talk.Stock = FindStock(line, false);
        if (talk.Stock == null) { var loose = FindStock(line, true); if (loose != null && Overlap(line, loose) >= (Lang == 1 ? 5 : 3)) { talk.Stock = loose; } }
        var match = PercentPattern.Match(line);
        if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float percent)) { talk.Percent = percent; }

        if (!float.IsNaN(talk.Percent) && HasWord(lower, NotReturnWords)) { talk.Percent = float.NaN; }
        bool loss = HasWord(lower, LossWords) || !float.IsNaN(talk.Percent) && talk.Percent < 0f, gain = HasWord(lower, BragWords) || !float.IsNaN(talk.Percent) && talk.Percent > 0f && !talk.Asks;
        if (Lang == 1) { if (EnGainPattern.IsMatch(lower)) { gain = true; } if (EnLossPattern.IsMatch(lower)) { loss = true; } }
        string topic = TopicOf(lower);
        // "라면 먹었음"은 수익 자랑이 아니라 밥 얘기다: 종목도 수익률도 없이 먹는 얘기만 있으면 자랑으로 치지 않는다.
        // "나이 먹었네", "자격증 땄다", "친구 손절했다"도 돈 얘기가 아니다.
        bool plain = talk.Stock == null && float.IsNaN(talk.Percent);
        if (gain && plain && (topic == "food" || TopicSaid(lower, "food") || Has(lower, NotGainWords))) { gain = false; }
        if (loss && plain && (Has(lower, NotLossWords) || Has(lower, BlameWords))) { loss = false; }
        talk.Rudeness = RudenessOf(line, out talk.RudeFamily, out talk.RudeDeath);
        // 자기를 낮추는 말은 막말이 아니다("나 호구인듯"). 남을 가리키는 말이 섞여 있으면 막말로 둔다.
        if (talk.Rudeness == 1 && Has(lower + " ", SelfWords) && !Has(lower, AimedWords)) { talk.Rudeness = 0; }
        // 소식·세력·시장을 묻는 말인지는 금융 얘기가 아닌 낱말을 지우고 본다.
        string finance = lower; foreach (string friend in FalseFriends) { finance = finance.Replace(friend, " "); }
        // 주식 말고 다른 얘깃거리가 있는 글이다("점심 메뉴 추천 좀", "넷플릭스 뭐 봄"). 이런 글의 "추천"·"뭐 봄"·"알려줘"는 주식 얘기가 아니다.
        bool chat = topic != null && talk.Stock == null && Array.IndexOf(MoneyTopics, topic) < 0 && !HasWord(lower, StockWords);
        talk.Wonders = talk.Asks || HasWord(lower, QuestionWords);
        talk.Night = HasWord(lower, NightWords);
        talk.TellsBuy = Has(lower, BuyTellWords); talk.TellsSell = Has(lower, SellTellWords);
        talk.Comforts = Has(lower, ComfortWords); talk.Congrats = Has(lower, CongratsWords); talk.Praises = Has(lower, PraiseWords);
        talk.Style = HasWord(lower, ShortStyle) ? "short" : HasWord(lower, LongStyle) ? "long" : null;
        bool brief = line.Length <= BriefLength;
        talk.AsksBack = brief && talk.Stock == null && (HasWord(lower, WhyWords) || talk.Asks && HasWord(lower, BackWords) || HasWord(lower, DoubtWords)) && OnlyAsksBack(lower);
        talk.Bought = talk.Stock != null && !talk.Asks && HasWord(lower, BoughtWords); talk.Sold = talk.Stock != null && !talk.Asks && HasWord(lower, SoldWords);
        // 영어는 주어를 떼고 "bought X"·"sold X"라고 쓴다(문장 앞일 때만 본다. "should have bought"는 사지 않았다는 말이다).
        if (Lang == 1 && talk.Stock != null && !talk.Asks) { if (lower.StartsWith("bought ", StringComparison.Ordinal)) { talk.Bought = true; } if (lower.StartsWith("sold ", StringComparison.Ordinal)) { talk.Sold = true; } }
        string term = TermOf(lower);
        talk.Other = talk.Stock == null ? null : FindOtherStock(line, talk.Stock);
        if (talk.Rudeness > 0) { talk.Kind = PlayerTalkKind.Insult; }
        else if (term != null) { talk.Kind = PlayerTalkKind.AskTerm; talk.Topic = term; }
        else if (Has(lower, WhaleWords)) { talk.Kind = PlayerTalkKind.Whale; }
        else if (HasWord(lower, ProfitAsk)) { talk.Kind = PlayerTalkKind.AskProfit; }
        // "님들 덕분에 벌었음"은 자랑보다 고맙다는 말이다.
        else if (plain && !loss && lower.Contains("덕분") && !talk.Asks) { talk.Kind = PlayerTalkKind.Thanks; }
        // "손절 기준이 뭐임", "익절이 뭐야"처럼 뜻이나 방법을 묻는 말은 한탄·자랑이 아니다.
        else if (loss && !talk.Asks && !HasWord(lower, DefineWords)) { talk.Kind = PlayerTalkKind.Lament; }
        else if (gain && !talk.Asks && !HasWord(lower, DefineWords)) { talk.Kind = PlayerTalkKind.Brag; }
        else if (talk.Other != null && (talk.Wonders || HasWord(lower, CompareWords))) { talk.Kind = PlayerTalkKind.AskCompare; }
        else if (talk.Stock != null && HasWord(lower, CompanyAsk)) { talk.Kind = PlayerTalkKind.AskCompany; }
        else if (Has(finance, PickAsk) && talk.Stock == null && !chat) { talk.Kind = PlayerTalkKind.AskPick; }
        // "노보 평단 얼마임 다들"도 들고 있는 사람에게 묻는 말이다.
        else if (talk.Stock != null && (HasWord(lower, HolderAsk) || talk.Wonders && HasWord(lower, AverageWords) && !HasWord(lower, MyAverageWords))) { talk.Kind = PlayerTalkKind.AskHolders; }
        else if (talk.Stock != null && HasWord(lower, NickAsk)) { talk.Kind = PlayerTalkKind.AskNick; }
        else if (talk.Stock != null && Has(lower, SellAsk)) { talk.Kind = PlayerTalkKind.AskSell; }
        else if (talk.Stock != null && Has(lower, BuyAsk)) { talk.Kind = PlayerTalkKind.AskBuy; }
        else if (HasWord(finance, NewsAsk) && !Has(lower, Thank)) { talk.Kind = PlayerTalkKind.AskNews; }
        else if (HasWord(finance, WhaleAsk)) { talk.Kind = PlayerTalkKind.AskWhale; }
        else if (talk.Stock != null && HasWord(lower, WhyWords)) { talk.Kind = PlayerTalkKind.AskWhy; }
        else if (talk.Stock != null && HasWord(lower, PriceAsk) && !lower.Contains("얼마까지")) { talk.Kind = PlayerTalkKind.AskPrice; }
        else if (talk.Stock != null && (Has(lower, OpinionAsk) || talk.Asks)) { talk.Kind = PlayerTalkKind.AskOpinion; }
        else if (talk.Stock != null && Has(lower, DownWords)) { talk.Kind = PlayerTalkKind.ClaimDown; }
        else if (talk.Stock != null && Has(lower, UpWords)) { talk.Kind = PlayerTalkKind.ClaimUp; }
        else if (talk.Stock == null && HasWord(finance, OutlookAsk)) { talk.Kind = PlayerTalkKind.AskOutlook; }
        else if (talk.Stock == null && !chat && (talk.Asks || HasWord(lower, ShouldWords)) && (Has(lower, BuyAsk) || Has(lower, SellAsk))) { talk.Kind = PlayerTalkKind.AskWhich; }
        else if (!chat && HasWord(finance, HoldAsk)) { talk.Kind = PlayerTalkKind.AskHoldings; }
        else if (!chat && HasWord(finance, HotAsk)) { talk.Kind = PlayerTalkKind.AskHot; }
        else if (HasWord(finance, MarketAsk)) { talk.Kind = PlayerTalkKind.AskMarket; }
        else if (HasWord(lower, Thank)) { talk.Kind = PlayerTalkKind.Thanks; }
        else if (HasWord(lower, Sorry)) { talk.Kind = PlayerTalkKind.Apology; }
        else if (HasWord(lower, Farewell)) { talk.Kind = PlayerTalkKind.Bye; }
        else if ((topic == null || Array.IndexOf(ErrandTopics, topic) >= 0) && talk.Stock == null && !talk.Asks && HasWord(lower, ReturnWords)) { talk.Kind = PlayerTalkKind.Greeting; talk.Returned = true; }
        else if (HasWord(lower + " ", Greet) && !lower.Contains("ㅎㅇㅌ")) { talk.Kind = PlayerTalkKind.Greeting; }
        // 종목 이름만 꺼낸 말("산삼전자 보는 중")은 묻는 말이 아니라 그 종목 얘기다.
        else if (talk.Stock != null) { talk.Kind = PlayerTalkKind.Mention; }
        else if (!chat && HasWord(lower, HowAsk)) { talk.Kind = PlayerTalkKind.AskHow; }
        else if (topic != null) { talk.Topic = topic; talk.Kind = PlayerTalkKind.Topic; }
        else if (brief && Nod(lower, DunnoWords)) { talk.Kind = PlayerTalkKind.Dunno; }
        else if (brief && !talk.Wonders && Nod(lower, DisagreeWords)) { talk.Kind = PlayerTalkKind.Disagree; }
        else if (brief && (!talk.Wonders || lower.StartsWith("그런가", StringComparison.Ordinal)) && (Nod(lower, AgreeWords) || tail == "나도" || tail == "自分も" || tail == "俺も" || tail == "私も" || tail == "わいも")) { talk.Kind = PlayerTalkKind.Agree; }
        else if (brief && Nod(lower + " ", SurpriseWords)) { talk.Kind = PlayerTalkKind.Surprise; }
        else if (talk.Cries || line.Length <= (Lang == 1 ? 14 : 8) && Sighs(lower)) { talk.Kind = PlayerTalkKind.Sad; }
        else if (talk.Laughs) { talk.Kind = PlayerTalkKind.Laugh; }
        else if (talk.Wonders) { talk.Kind = PlayerTalkKind.Question; }
        // 부르기만 한 말("님들", "저기요")만 부름이다. 뒤에 딴 말이 붙어 있으면("다들 웃기네") 그 말을 알아듣지 못한 것이다.
        else if (talk.CallsAll && OnlyCalls(lower)) { talk.Kind = PlayerTalkKind.CallAll; }
        else { talk.Kind = PlayerTalkKind.Statement; }
        // "なんで?"의 "で?"처럼 짧은 낱말이 다른 말 속에 들어 있을 수 있으므로, 까닭을 묻는 말(왜·なんで·why)을 풀이를 청하는 말보다 먼저 본다.
        if (talk.AsksBack) { talk.BackKind = HasWord(lower, DisbeliefWords) ? "doubt" : HasWord(lower, WhyWords) ? "why" : HasWord(lower, ExplainWords) ? "explain" : "why"; }
        talk.Acks = talk.Kind == PlayerTalkKind.Agree && HasWord(lower, AckWords) && !HasWord(lower, YesWords);
        if (talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Disagree || talk.Kind == PlayerTalkKind.Dunno)
        {
            string rest = lower;
            foreach (var words in new[] { AgreeWords, DisagreeWords, DunnoWords, YesWords, AckWords }) { foreach (string word in words) { rest = rest.Replace(word, " "); } }
            talk.BareNod = Leftover(rest) <= 1;
        }
        if (!talk.Asks && talk.Kind != PlayerTalkKind.Insult)
        {
            if (Lang == 1) { talk.WillBuy = HasWord(lower, WillBuyWords); talk.WillSell = HasWord(lower, WillSellWords); }
            else
            {
                foreach (string end in WillBuyWords) { if (tail.EndsWith(end, StringComparison.Ordinal)) { talk.WillBuy = true; break; } }
                foreach (string end in WillSellWords) { if (tail.EndsWith(end, StringComparison.Ordinal)) { talk.WillSell = true; break; } }
            }
            if (talk.WillSell) { talk.WillBuy = false; }
            // "じゃあ買うわ"의 "うわ"를 놀라는 말로 읽지 않는다.
            if ((talk.WillBuy || talk.WillSell) && talk.Kind == PlayerTalkKind.Surprise) { talk.Kind = PlayerTalkKind.Statement; }
        }
        string asked = lower.Trim(' ', '?', '？', '!', '.', '~');
        talk.AsksSame = brief && talk.Stock == null && Array.IndexOf(AsksSameWords, asked) >= 0;
        // 받아 줄 수 없는 말과 뜻이 없는 글. 이런 말에는 대답·토론·말 걸기 어느 것도 붙지 않는다.
        if (talk.Kind != PlayerTalkKind.Insult && IsLewd(line)) { talk.Kind = PlayerTalkKind.Lewd; talk.Stock = null; talk.Topic = null; }
        else if ((talk.Kind == PlayerTalkKind.Other || talk.Kind == PlayerTalkKind.Statement || talk.Kind == PlayerTalkKind.Question) && IsNoise(line)) { talk.Kind = PlayerTalkKind.Noise; }
        return talk;
    }
    private static string[] AverageWords => AverageLex[Lang];
    private static readonly string[][] AverageLex = Lex(null, new[] { "평단", "평균단가", "avg price", "average price", "average cost", "cost basis", "avg", "平均単価", "取得単価", "平均取得" });
    private static string[] MyAverageWords => MyAverageLex[Lang];
    private static readonly string[][] MyAverageLex = Lex(null, new[] { "내 평단", "my avg", "my average", "my cost", "自分の平均", "俺の平均" });
    private static bool OnlyCalls(string lower)
    {
        string rest = lower; foreach (string word in All) { rest = rest.Replace(word, " "); }
        return Leftover(rest) <= 1;
    }
    /// <summary>한숨뿐인 짧은 말인가("에휴", "하", "노답").</summary>
    private static bool Sighs(string lower)
    {
        string bare = lower.Trim(' ', '.', '~', '!', '…', ';');
        foreach (string word in SighWords) { if (bare == word || bare.Length > 2 && word.Length >= 2 && bare.StartsWith(word, StringComparison.Ordinal)) { return true; } }
        return false;
    }
    /// <summary>그 갈래(Topics 표)의 낱말이 글에 들어 있는가.</summary>
    private static bool TopicSaid(string lower, string key)
    {
        foreach (var topic in LangTopics) { if (topic.key == key) { return Longest(lower, topic.words) > 0; } }
        return false;
    }
    /// <summary>낱말의 뜻을 묻는 글이면 그 낱말 풀이의 열쇠("term_limit"). 아니면 null.</summary>
    private static string TermOf(string lower)
    {
        if (!HasWord(lower, DefineWords)) { return null; }
        string best = null; int bestLength = 0;
        foreach (var topic in ChatTopics.All)
        {
            if (!topic.term) { continue; }
            foreach (string word in topic.words)
            {
                if (word.Length <= bestLength) { continue; }
                int at = lower.IndexOf(word, StringComparison.Ordinal);
                if (at < 0 || IsLatin(word[0]) && at > 0 && IsLatin(lower[at - 1])) { continue; }
                // 그 낱말 뒤에 조사만 두고 곧바로 묻는 말꼬리가 오는가("지정가가 뭐임", "매집이란"). 영어는 묻는 말이 앞에 온다("what is a limit order").
                string after = lower.Substring(at + word.Length).TrimStart();
                foreach (string particle in TermParticles) { if (after.StartsWith(particle, StringComparison.Ordinal)) { after = after.Substring(particle.Length).TrimStart(); break; } }
                bool asked = false;
                foreach (string define in DefineWords) { if (after.StartsWith(define, StringComparison.Ordinal)) { asked = true; break; } }
                if (!asked)
                {
                    string before = lower.Substring(0, at).TrimEnd();
                    foreach (string lead in TermLeads) { if (before.EndsWith(lead, StringComparison.Ordinal)) { asked = true; break; } }
                }
                if (asked) { best = topic.key; bestLength = word.Length; }
            }
        }
        return best;
    }
    private static string[] TermParticles => TermParticlesLex[Lang];
    private static readonly string[][] TermParticlesLex = Lex("TermParticles", new[] { "이라는 게", "라는 게", "이라는게", "라는게", "이란", "란", "이", "가", "은", "는", "를", "을" });
    private static string[] TermLeads => TermLeadsLex[Lang];
    private static readonly string[][] TermLeadsLex = Lex("TermLeads", new[] { "what is", "what is a", "what is an", "what's", "what's a", "whats", "whats a", "what does", "what are", "meaning of" });
    // 사겠다·팔겠다는 말꼬리(한국어·일본어는 문장 끝, 영어는 낱말).
    private static string[] WillBuyWords => WillBuyLex[Lang];
    private static readonly string[][] WillBuyLex = Lex(null, new[] { "살게", "살게요", "삼", "산다", "사야지", "사야겠다", "살래", "탈게", "탄다", "타야지", "담을게", "담는다", "들어간다", "들어갈게", "매수한다", "지른다", "질러야지", "사 본다", "사본다",
        "i'll buy", "ill buy", "i will buy", "buying it", "im buying", "i'm buying", "gonna buy", "going in", "i'll take some", "買うわ", "買う", "買おう", "買ってみる", "入るわ", "乗るわ", "インする", "拾うわ" });
    private static string[] WillSellWords => WillSellLex[Lang];
    private static readonly string[][] WillSellLex = Lex(null, new[] { "팔게", "팔게요", "판다", "팔래", "팔아야지", "팔아야겠다", "던진다", "던질게", "정리한다", "정리할게", "손절한다", "손절할게", "익절한다", "익절할게", "뺀다",
        "i'll sell", "ill sell", "i will sell", "selling it", "im selling", "i'm selling", "gonna sell", "getting out", "i'm out of this", "売るわ", "売る", "売ろう", "降りるわ", "切るわ", "損切りする", "利確する", "逃げるわ" });
    private static string[] AsksSameWords => AsksSameLex[Lang];
    private static readonly string[][] AsksSameLex = Lex(null, new[] { "님들은", "님은", "너는", "넌", "너넨", "너네는", "다들은", "형들은", "그쪽은", "님들은 어때", "다들 어때", "you", "u", "and you", "what about you", "how about you", "wbu", "hbu", "you guys", "what about you guys",
        "そっちは", "みんなは", "君は", "お前らは", "おまえらは", "そっちはどう", "みんなはどう" });
    private static string[] AskEnds => AskEndsLex[Lang];
    private static readonly string[][] AskEndsLex = Lex("AskEnds", new[] { "냐", "까", "나요", "ㅁ?", "인가", "는가", "런가", "던가", "없나", "있나", "되나", "려나", "까요", "는지", "을지" });
    private static string[] WhenWords => WhenWordsLex[Lang];
    private static readonly string[][] WhenWordsLex = Lex("WhenWords", new[] { "언제", "얼마", "몇", "어떻게", "어케", "어디서", "누가" });
    private static string[] PlainEnds => PlainEndsLex[Lang];
    private static readonly string[][] PlainEndsLex = Lex("PlainEnds", new[] { "함", "임", "됨", "줌", "감", "옴", "봄", "음", "해", "야", "지", "돼", "나" });
    private static string[] BackFill => BackFillLex[Lang];
    private static readonly string[][] BackFillLex = Lex("BackFill", new[] { "그렇게 생각", "그런데", "그럼", "그런", "그래", "근데", "아니", "그게", "그건", "뭔데", "무슨 소리", "뭔 소리", "무슨 말", "뭔 말", "어디임", "어디야", "있음", "있어", "뭐임", "뭐야", "해줘", "해 줘", "한 번만 더", "한번만 더", "치지 마", "치지마", "치네", "안 됨", "안됨", "좀", "임", "야", "요", "that", "so", "but", "is it", "you think" });
    /// <summary>
    /// 되묻는 말뿐인가("왜?", "진짜?", "근데 왜 그렇게 생각함?"). 다른 얘깃거리가 들어 있으면("호가창 왜 이래", "왜 아무도 대답 안 함") 바로 앞 말을 되묻는 것이 아니다.
    /// </summary>
    private static bool OnlyAsksBack(string lower)
    {
        string rest = lower;
        foreach (string word in BackWords) { rest = rest.Replace(word, " "); }
        foreach (string word in DoubtWords) { rest = rest.Replace(word, " "); }
        foreach (string word in BackFill) { rest = rest.Replace(word, " "); }
        return Leftover(rest) <= 1;
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
            var topical = ChatTopics.Lines(key);
            if (topical != null) { return topical; }
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
    private Stock talkOther;
    private sealed class TalkReply { public Stock other; public double due; public int room; public string key; public Stock stock; public long replyTo; public int onlyBot, notBot; public float percent; public double wait; public string said; public decimal entry; }
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
    /// <summary>채팅 화면에 글 쓰는 칸과 새벽 관찰실 잠금 화면을 붙인다(화면이 켜진 뒤 한 번).</summary>
    private void EnsureChatBar()
    {
        if (Time.unscaledTime < chatBarCheck) { return; }
        chatBarCheck = Time.unscaledTime + 2f;
        // 씬 전체를 뒤지면(FindObjectsByType) 2초마다 프레임이 100ms 넘게 멈춘다. 켜져 있는 채팅 화면이 스스로 올려 둔 목록만 본다.
        for (int i = TabletGroupChatUI.Live.Count - 1; i >= 0; i--)
        {
            var chat = TabletGroupChatUI.Live[i];
            if (chat == null) { TabletGroupChatUI.Live.RemoveAt(i); continue; }
            if (chat.scroll != null && chat.GetComponent<PlayerChatBar>() == null) { chat.gameObject.AddComponent<PlayerChatBar>(); }
        }
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
    private long PostBotLine(TradeBot bot, int room, string text, string key, Stock stock, long replyTo, string topicEvent = "PLAYER_TALK")
    {
        text = DetachMention(text);
        var message = new SocialMessage { id = ++hypeMessageId, roomId = room, senderId = bot.Social.Psychology.SocialId, at = SocialTime.GameMinute, realAt = SocialTime.Seconds,
            act = SocialAct.REACT, replyToId = replyTo, topicEvent = topicEvent, templateKey = key, text = text,
            facts = new SocialFacts { symbol = stock?.symbol, stockName = stock?.stockName, at = SocialTime.GameMinute }, debug = new SocialMessageDebug { trigger = "PLAYER_TALK" } };
        bool was = breathDelivering; breathDelivering = true;
        try { DeliverSocialMessage(message); } finally { breathDelivering = was; }
        return message.id;
    }

    // ── 플레이어가 쓴 말 ──
    /// <summary>플레이어가 채팅에 한 줄을 썼다. 쓸 수 없는 방이면 false.</summary>
    public bool SubmitPlayerChat(int room, string text) => SubmitPlayerChat(room, text, null);
    /// <summary>지금 들고 있는 종목의 수익률 인증 카드. 종목을 주면 그 종목, 아니면 수익(또는 손실)이 가장 큰 종목. 들고 있는 게 없으면 null.</summary>
    public ChatProfitCard PlayerHoldingProof(Stock wanted = null)
    {
        var account = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        if (account == null) { return null; }
        Stock subject = wanted != null && account.GetHolding(wanted) >= 1m && account.GetAvgPrice(wanted) > 0m ? wanted : null;
        if (subject == null)
        {
            decimal most = -1m;
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock == null || account.GetHolding(stock) < 1m || account.GetAvgPrice(stock) <= 0m || stock.currentPrice <= 0m) { continue; }
                decimal swing = Math.Abs(stock.currentPrice / account.GetAvgPrice(stock) - 1m);
                if (swing > most) { most = swing; subject = stock; }
            }
        }
        if (subject == null || subject.currentPrice <= 0m) { return null; }
        decimal average = account.GetAvgPrice(subject), quantity = account.GetHolding(subject);
        return new ChatProfitCard { kind = ChatProfitCardKind.Holding, symbol = subject.symbol, stockName = subject.stockName, amount = (subject.currentPrice - average) * quantity,
            percent = (subject.currentPrice / average - 1m) * 100m, averageCost = average, price = subject.currentPrice, quantity = quantity, capturedAt = SocialTime.GameMinute };
    }
    /// <summary>가장 최근에 판 것의 실현 손익 인증 카드. 이번 실행에서 판 적이 없으면 null.</summary>
    public ChatProfitCard PlayerRealizedProof()
    {
        var career = PlayerProgression.Instance;
        if (career == null || string.IsNullOrEmpty(career.LastSale.symbol) || career.LastSale.basis <= 0m || career.LastSale.volume <= 0m) { return null; }
        var sale = career.LastSale; var stock = StockBySymbol(sale.symbol);
        return new ChatProfitCard { kind = ChatProfitCardKind.RealizedGain, symbol = sale.symbol, stockName = stock != null ? stock.stockName : sale.symbol, amount = (sale.price - sale.basis) * sale.volume,
            percent = (sale.price / sale.basis - 1m) * 100m, averageCost = sale.basis, price = sale.price, quantity = sale.volume, capturedAt = SocialTime.GameMinute };
    }
    /// <summary>인증 카드를 붙여 올린다. 글은 카드의 종목과 수익률로 만든다. 봇들은 실제 계좌에서 나온 인증으로 받는다.</summary>
    public bool SubmitPlayerProof(int room, ChatProfitCard proof) => proof != null && SubmitPlayerChat(room, "", proof);
    private decimal talkEntry;
    /// <summary>
    /// 인증 카드와 함께 올린 글에 대한 반응. 카드에서 사실(수익인지 손실인지, 얼마인지)을, 글에서 말투를 읽어 둘을 합쳐 받는다:
    ///  - 수익 + 으스대는 말: 부러워하면서도 얄미워한다. 원한이 많은 봇은 배가 더 아프다.
    ///  - 수익 + 겸손한 말: 순하게 축하한다. 믿음이 더 오른다.
    ///  - 손실 + 속상한 말: 위로한다.   손실 + 웃음: 같이 웃어 준다(자조에는 블랙유머로).
    ///  - 어떡하냐고 물으면: 위로보다 조언이 먼저다. 조언은 그 봇이 그 종목을 실제로 어떻게 보는지에 따라 갈린다(버텨라 / 잘라라).
    /// 어느 경우든 공감 대신 조언이나 훈수부터 하는 봇이 섞인다.
    /// </summary>
    private void ReactToProof(int room, PlayerTalk talk, string text, ChatProfitCard proof, long id, double now)
    {
        bool gain = proof.amount >= 0m; float rate = (float)proof.percent; Stock stock = talk.Stock;
        bool asks = talk.Asks || PlayerTalk.Says(text, PlayerTalk.WantsAdvice);
        bool cocky = gain && (talk.Laughs || PlayerTalk.Says(text, PlayerTalk.Cocky)), humble = gain && !cocky && PlayerTalk.Says(text, PlayerTalk.Humble);
        bool hurting = !gain && (talk.Cries || PlayerTalk.Says(text, PlayerTalk.Hurting)), joking = !gain && !hurting && talk.Laughs;
        string tone = asks ? "ask" : cocky ? "cocky" : humble ? "humble" : hurting ? "sad" : joking ? "joke" : "plain";
        TraceTalk("    (인증 " + (gain ? "수익 " : "손실 ") + rate.ToString("+0.0;-0.0") + "% · 말투 " + tone + ")");
        float size = Mathf.Abs(rate);
        Hearers(room, (bot, mind) =>
        {
            if (gain)
            {
                bool sore = stock != null && bot.GetOwnedVolume(stock) > 0m && bot.GetAvgPrice(stock) > stock.currentPrice || mind.Traits.Grudge >= 70;
                // 으스대면 믿음은 덜 오르고 배 아픈 사람은 더 아프다. 겸손하면 그 반대다.
                float trust = Mathf.Clamp(size, 2f, 25f) * (sore ? .3f : 1f) * (cocky ? .6f : humble ? 1.2f : 1f);
                float grudge = (sore ? Mathf.Clamp(size * .5f, 2f, 16f) : 0f) * (cocky ? 1.6f : humble ? .5f : 1f) + (cocky && mind.Traits.Grudge >= 50 ? 2f : 0f);
                mind.ReactToPlayerTalk(trust, grudge, "플레이어의 수익 인증 " + rate.ToString("+0.0") + "%" + (cocky ? " (으스댐)" : ""));
            }
            else { mind.ReactToPlayerTalk(hurting || asks ? 3f : 2f, 0f, "플레이어의 손실 인증"); }
        });
        talkEntry = proof.averageCost;
        try
        {
            string side = gain ? "gain" : "loss";
            Plan(room, "proof." + side + "." + tone, stock, id, now + 1.2, 0, HypeRandom.Range(1, 3), .9f, rate, sure: true);
            // 조언: 물었으면 꼭 나오고, 묻지 않아도 열에 여섯은 누가 한마디 한다. 훈수는 그보다 드물다.
            if (stock != null) { Plan(room, "advice." + side, stock, id, now + 2.6, 0, 1, asks ? 1f : .6f, rate, sure: asks); }
            Plan(room, "nag." + side, stock, id, now + 4.0, 0, 1, cocky || !gain && !hurting ? .45f : .2f, rate);
            // 으스댄 수익에는 예전처럼 질투도 붙는다.
            if (gain && cocky && size >= 5f) { Plan(room, "brag.proof.envy", stock, id, now + 3.2, 0, 1, .7f, rate); }
        }
        finally { talkEntry = 0m; }
        if (gain) { Sway(room, stock, 1.2f); }
    }
    /// <summary>글과 인증 카드를 함께 올린다. 글이 비어 있으면 카드의 종목과 수익률로 한 줄을 만들어 준다.</summary>
    public bool SubmitPlayerChat(int room, string text, ChatProfitCard proof)
    {
        if (proof != null && string.IsNullOrWhiteSpace(text))
        {
            string name = GameLocale.Text(proof.stockName ?? proof.symbol ?? ""), rate = proof.percent.ToString("+0.0;-0.0;0.0") + "%";
            text = proof.kind != ChatProfitCardKind.Holding ? TalkWord(name + " " + rate + " 실현 인증", name + " " + rate + " realized", name + " " + rate + " 確定")
                : TalkWord(name + " " + rate + " 보유 인증", name + " " + rate + " holding", name + " " + rate + " 保有中");
        }
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
        if (proof != null)
        {
            // 인증을 붙인 글: 카드가 곧 사실이다. 글에서는 말투만 읽는다(으스대는지, 속상한지, 조언을 구하는지).
            card = proof; actual = proof.percent; held = true;
            talk.Stock = StockBySymbol(proof.symbol);
        }
        long id = PostPlayer(room, text, card);
        PlayerLines++;
        TraceTalk("나: " + text + "  → " + talk.Kind + (talk.Stock != null ? " " + talk.Stock.symbol : "") + (float.IsNaN(talk.Percent) ? "" : " " + talk.Percent.ToString("0.#") + "%") + (card != null ? " 인증 " + actual.ToString("+0.0;-0.0") + "%" : "")
            + (talk.CallsAll ? " 부름" : "") + (talk.Laughs ? " 웃음" : "") + (spam ? " 도배" : ""));
        if (spam) { Plan(room, "spam", null, id, now + 1.2, 0, 1, .7f); return true; }
        if (proof != null) { NotePlayerLine(room, talk, id, now); ReactToProof(room, talk, text, proof, id, now); return true; }

        // 짧은 말은 봇이 되받아 읊기도 한다("치킨? ㅋㅋ").
        // 되받을 때는 끝의 문장부호를 뗀다("양념으로!" → "양념으로? 맛잘알이네"). 영어는 같은 말도 글자 수가 많다.
        int briefSaid = PlayerTalk.Lang == 1 ? 26 : 12;
        talkSaid = text.Length <= briefSaid && text.IndexOf('{') < 0 && text.IndexOf('@') < 0 ? text.Trim().TrimEnd('.', '!', '?', '~', '！', '？', '。', '、', ',', ' ') : null;
        if (talkSaid != null && talkSaid.Length == 0) { talkSaid = null; }
        // 방금 나와 말을 섞은 봇이 있으면 그 봇이 먼저 받는다. 봇이 던진 물음에 내가 답했으면 물은 봇이 받는다.
        var partner = TalkPartnerIn(room, now); int near = partner == null ? 0 : partner.bot;
        NotePlayerLine(room, talk, id, now);
        int asker = 0;
        // 라운지에서 토론이 벌어지는 중이면 내 말이 그 판을 바꾼다. 토론이 받은 말에는 평소의 답글을 달지 않는다.
        // 종목 얘기·막말·웃음은 봇의 물음에 대한 답이든 아니든 판을 바꾸므로 먼저 들려준다.
        bool swayed = DebateHears(room, talk, now, early: true);
        // 종목을 대며 사라(팔라)고 권하거나 오른다(내린다)고 외치는 말은 누구의 물음에 대한 대답이 아니라 방에 던지는 말이다.
        bool calls = talk.Stock != null && !talk.Asks && (talk.TellsBuy || talk.TellsSell || talk.Kind == PlayerTalkKind.ClaimUp || talk.Kind == PlayerTalkKind.ClaimDown);
        if (!calls && HearPlayerAnswer(room, talk, text, id, now, ref asker)) { return true; }
        // 방금(20초 안에) 나와 말을 섞은 봇이 있으면 "ㅇㅇ"·"ㄴㄴ"는 그 봇에게 하는 말이다. 옆에서 벌어진 토론의 편을 드는 말로 읽지 않는다.
        bool toPartner = partner != null && now - partner.at <= 20 && (talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Disagree);
        if (swayed || !toPartner && DebateHears(room, talk, now)) { return true; }
        // 말 상대가 다음 말을 늘 받으면 한 봇하고만 떠들게 된다. 맞장구·웃음처럼 바로 앞 말에 붙는 말은 그 봇이 받고, 새 얘기는 셋에 한 번만 받는다.
        bool clings = talk.AsksBack || talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Disagree || talk.Kind == PlayerTalkKind.Dunno || talk.Kind == PlayerTalkKind.Thanks ||
            talk.Kind == PlayerTalkKind.Apology || talk.Kind == PlayerTalkKind.Laugh || talk.Kind == PlayerTalkKind.Sad || talk.Kind == PlayerTalkKind.Surprise;
        if (asker != 0) { near = asker; } else if (!clings && HypeRandom.Next() >= .34) { near = 0; }
        if (partner != null && talk.AsksBack && talk.Kind != PlayerTalkKind.Insult && talk.Kind != PlayerTalkKind.Analyze)
        {
            // "왜?" "진짜?": 방금 말한 봇이 자기 말을 풀어 준다.
            // 같은 봇에게 서너 번 넘게 캐물으면 그만 물으라고 하고 대화를 접는다.
            // 무엇을 되묻는지에 따라 답이 다르다: 못 믿겠다는 말에는 진짜라고, 풀어 달라는 말에는 다시 말해 주고,
            // 그 봇이 방금 내게 물은 것이었으면 왜 물었는지를, 잡담이었으면 가볍게 받는다. 까닭을 대는 답(내 감임, 차트가 그럼)은 종목·시세 얘기에만 쓴다.
            string follow = partner.depth >= 3 + partner.bot % 2 ? "follow.tired" : partner.asked ? "follow.asked" : talk.BackKind == "doubt" ? "follow.doubt" : talk.BackKind == "explain" ? "follow.explain"
                : partner.opinion ? "follow" : "follow.chat";
            Plan(room, follow, follow == "follow" ? partner.stock : null, id, now + 1.1, near, 1, 1f, sure: true);
            return true;
        }

        var said = playerNote;
        // "님들은?"·"너는?": 방금 내가 한 얘기(단타라는 말, 밥 얘기 …)를 방에 되묻는 말이다. 그 얘기를 묻는 글로 받는다.
        if (talk.AsksSame && said.lastTopic != null && now - said.lastTopicAt <= 90)
        {
            Plan(room, "topic." + said.lastTopic + ".q", null, id, now + 1.2, near, HypeRandom.Range(1, 3), .8f, sure: true);
            TraceTalk("    (방금 하던 " + said.lastTopic + " 얘기를 되물음)");
            return true;
        }
        // "그럼 살게"·"ㅇㅋ 그거 삼": 종목을 대지 않았으면 방금 나와 얘기하던 종목이다.
        if ((talk.WillBuy || talk.WillSell) && talk.Stock == null && (talk.Kind == PlayerTalkKind.Statement || talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Other))
        {
            Stock meant = partner != null && partner.stock != null ? partner.stock : said.stock != null && now - said.stockAt <= 120 ? said.stock : null;
            if (meant != null && meant.AcceptsOrders)
            {
                Plan(room, talk.WillSell ? "willSell" : "willBuy", meant, id, now + 1.2, near, HypeRandom.Range(1, 3), .7f, sure: true);
                Sway(room, meant, .4f);
                TraceTalk("    (" + meant.symbol + "을 " + (talk.WillSell ? "팔겠다" : "사겠다") + "는 말로 읽음)");
                return true;
            }
        }
        // 방금 꺼낸 잡담에 한마디 덧붙인 짧은 말("치킨 먹고 싶다" → "양념으로" → "콜라도 같이"): 그 얘기로 한 명이 받아 준다(두 번까지).
        // 덧붙인 말을 되받아 준다("양념으로? 맛잘알이네"). 갈래마다 따로 쓴 대사(topicAdd.food …)가 없으면 두루 쓰는 대사(topicAdd)로 받는다.
        if (talk.Kind == PlayerTalkKind.Statement && talkSaid != null && said.lastTopic != null && now - said.lastTopicAt <= 25 && said.topicFollows < 2 && !talk.AsksBack)
        {
            said.topicFollows++; said.lastTopicAt = now;
            Plan(room, "topicAdd." + said.lastTopic, null, id, now + 1.3, near, 1, .6f, sure: true);
            TraceTalk("    (방금 하던 " + said.lastTopic + " 얘기에 덧붙인 말)");
            return true;
        }
        // "왜?"·"진짜?"인데 방금 나와 말을 섞은 봇이 없다. 무엇을 되묻는지 알 수 없으니 아는 척 답하지 않는다. 가끔 한 명이 "뭐가?" 하고 만다.
        if (partner == null && talk.AsksBack && (talk.Kind == PlayerTalkKind.Question || talk.Kind == PlayerTalkKind.Statement))
        {
            Plan(room, "backNone", null, id, now + 1.3, 0, 1, .3f);
            TraceTalk("    (되묻는 말인데 말 상대가 없음)");
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
                Hearers(room, (bot, mind) => { mind.ReactToPlayerTalk(-2f * level, (HypeRandom.Next() < .5 ? 6f + mind.Traits.Grudge / 100f * 10f : 2f) * level, "플레이어의 막말"); mind.WarmToPlayer(-5f * level, "플레이어의 막말"); });
                string scold = level >= 3 ? (talk.RudeFamily ? "insult.family" : talk.RudeDeath ? "insult.death" : "insult.severe") : level == 2 ? "insult.swear" : "insult";
                Plan(room, scold, talk.Stock, id, now + 1.0, 0, level >= 3 ? HypeRandom.Range(3, 6) : level == 2 ? HypeRandom.Range(2, 5) : HypeRandom.Range(1, 4), 1f);
                // 계속 그러면 혀를 차거나 아예 무시하자는 말이 섞인다.
                if (again) { Plan(room, "insult.repeat", null, id, now + 2.2, 0, 2, .8f); }
                TraceTalk("    (막말 세기 " + level + (again ? ", 되풀이 " + rudeAt.Count + "번" : "") + ")");
            }
                break;
            case PlayerTalkKind.Lewd:
                // 음란한 말: 싸늘하게 한두 마디 하거나 아무도 받지 않는다. 방 전체의 호감이 깎인다.
                Hearers(room, (bot, mind) => mind.WarmToPlayer(-3f, "플레이어의 음란한 말"));
                Plan(room, "lewd", null, id, now + 1.4, 0, HypeRandom.Range(1, 3), .65f);
                TraceTalk("    (음란한 말: 받아 주지 않음)");
                break;
            case PlayerTalkKind.Noise:
                // 뜻이 없는 글: 대개 아무도 반응하지 않는다.
                Plan(room, "noise", null, id, now + 1.4, 0, 1, .12f);
                TraceTalk("    (뜻이 없는 글: 대개 반응하지 않음)");
                break;
            case PlayerTalkKind.Brag:
                if (card != null)
                {
                    float gain = (float)actual;
                    // 인증된 수익: 클수록 믿음이 오른다. 그 종목에 물려 있거나 원한이 많은 봇은 배가 아프다.
                    Hearers(room, (bot, mind) =>
                    {
                        bool sore = talk.Stock != null && bot.GetOwnedVolume(talk.Stock) > 0m && bot.GetAvgPrice(talk.Stock) > talk.Stock.currentPrice || mind.Traits.Grudge >= 70;
                        mind.ReactToPlayerTalk(Mathf.Clamp(gain * 1f, 2f, 25f) * (sore ? .3f : 1f), sore ? Mathf.Clamp(gain * .5f, 2f, 16f) : 0f, "플레이어의 수익 인증 " + gain.ToString("+0.0") + "%");
                    });
                    Plan(room, "brag.proof.praise", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 3), 1f, gain);
                    GainRenown(Mathf.Clamp(gain / 4f, 1f, 6f), "수익 인증 " + gain.ToString("+0") + "%", room, id, now);
                    Plan(room, "brag.proof.envy", talk.Stock, id, now + 2.6, 0, gain >= 10f ? HypeRandom.Range(1, 3) : HypeRandom.Range(0, 2), .8f, gain);
                    Sway(room, talk.Stock, 1.2f);
                }
                else if (held)
                {
                    // 들고는 있는데 손실 중이다. 자랑이 거짓이다.
                    Hearers(room, (bot, mind) => { mind.ReactToPlayerTalk(-5f, 1f, "플레이어의 거짓 자랑"); mind.WarmToPlayer(-2f, "플레이어의 거짓 자랑"); });
                    GainRenown(-3f, "거짓 자랑", room, id, now);
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
            case PlayerTalkKind.AskWhich:
                // 종목 없이 "지금 사도 됨?": 무엇을 말하는지 되묻는다.
                Plan(room, "which", null, id, now + 1.3, near, HypeRandom.Range(1, 3), .7f, sure: true);
                break;
            case PlayerTalkKind.AskOutlook:
                // 종목 없이 "더 오를까", "바닥인가": 봇마다 지금 기분(욕심·공포)대로 본다.
                Plan(room, "outlook", null, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true);
                break;
            case PlayerTalkKind.AskHolders:
                // "그푸 들고 있는 사람?": 실제로 들고 있는 봇은 평단과 수익을, 없는 봇은 없다고 답한다.
                Plan(room, "holders", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true);
                Sway(room, talk.Stock, .3f);
                break;
            case PlayerTalkKind.AskPrice:
                Plan(room, "price", talk.Stock, id, now + 1.1, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.AskCompany:
                // "산삼전자 뭐하는 회사임": 종목 정보에 나오는 업종으로 답한다.
                Plan(room, "company", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .5f, sure: true);
                break;
            case PlayerTalkKind.AskCompare:
                // "산전이랑 그푸 중에 뭐 삼?": 봇마다 자기가 더 싸다고 믿는 쪽을 고른다.
                talkOther = talk.Other;
                try { Plan(room, "compare", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 4), .85f, sure: true); } finally { talkOther = null; }
                Sway(room, talk.Stock, .3f); Sway(room, talk.Other, .3f);
                break;
            case PlayerTalkKind.AskTerm:
                // 낱말의 뜻을 묻는 말: 아는 봇이 풀어 준다.
                Plan(room, "topic." + talk.Topic, null, id, now + 1.4, near, HypeRandom.Range(1, 3), .5f, sure: true);
                break;
            case PlayerTalkKind.AskNick:
                // 종목을 뭐라고 줄여 부르는지 묻는 말: 실제로 쓰는 줄임말로 답한다.
                Plan(room, "askNick", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 3), .8f, sure: true);
                break;
            case PlayerTalkKind.ClaimUp: case PlayerTalkKind.ClaimDown:
                bool up = talk.Kind == PlayerTalkKind.ClaimUp;
                Plan(room, up ? "claimUp" : "claimDown", talk.Stock, id, now + 1.2, near, HypeRandom.Range(1, 4), .9f, sure: true);
                NotePlayerCall(room, talk.Stock, up, id, now);
                if (up) { Sway(room, talk.Stock, 1f); }
                // 이름이 알려져 있거나 나를 좋아하는 봇은 내 말을 따라 실제로 사기도(팔기도) 한다.
                FollowPlayerCall(room, talk.Stock, up, id, now, talk.TellsBuy || talk.TellsSell ? 1f : .7f);
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
                // "산전 샀다", "그푸 다 던짐"처럼 샀다(팔았다)고 알리는 말에는 그 얘기로 받는다.
                Plan(room, talk.Sold ? "sold" : talk.Bought ? "bought" : talk.WillSell ? "willSell" : talk.WillBuy ? "willBuy" : "mention", talk.Stock, id, now + 1.3, near, HypeRandom.Range(1, 3), .8f, sure: true);
                Sway(room, talk.Stock, .5f);
                // "두덕리 사라", "이거 추천함"처럼 오른다는 말 없이 권하기만 해도 따르는 봇이 있다. 결과도 지켜본다.
                if (talk.Stock != null && (talk.TellsBuy || talk.TellsSell) && !talk.Asks)
                {
                    NotePlayerCall(room, talk.Stock, talk.TellsBuy, id, now);
                    FollowPlayerCall(room, talk.Stock, talk.TellsBuy, id, now, 1f);
                }
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
                // 돌아왔다는 말에는 처음 보는 사람 맞듯 하지 않고 "어서 와"로 받는다.
                Plan(room, talk.Returned ? "welcome" : "greet", null, id, now + 1.0, near, HypeRandom.Range(1, 3), talk.Returned ? .5f : .75f, sure: true);
                break;
            case PlayerTalkKind.Bye:
                Plan(room, "bye", null, id, now + 1.0, near, HypeRandom.Range(1, 3), .6f, sure: true);
                // 간다고 한 사람에게 "아직 있음?" 하고 말을 걸지 않는다(다시 말하면 풀린다).
                pingStreak = 2;
                break;
            case PlayerTalkKind.Sad:
                // 우는 말(ㅠㅠ)과 한숨(에휴, 젠장)은 받는 말이 다르다. 한숨에 "울지 마라"는 어색하다.
                Plan(room, talk.Cries ? "sad" : "sigh", null, id, now + 1.3, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.Laugh:
                Plan(room, "laugh", null, id, now + 1.0, near, HypeRandom.Range(1, 3), .5f, sure: HypeRandom.Next() < .8);
                break;
            case PlayerTalkKind.CallAll:
                Plan(room, "all", null, id, now + 1.2, near, HypeRandom.Next() < .3 ? 2 : 1, .6f, sure: true);
                break;
            case PlayerTalkKind.Agree:
                // 딴 말이 붙은 맞장구("ㅇㅇ 아직 들고 있음")는 뭔가를 알려 주는 말이다. "ㅇㅈ"이 아니라 알아들었다는 말로 받는다.
                Plan(room, talk.BareNod ? "agree" : "agree.more", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Disagree:
                Plan(room, talk.BareNod ? "disagree" : "disagree.more", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Dunno:
                Plan(room, "dunno", null, id, now + 1.1, near, 1, 1f, sure: HypeRandom.Next() < .85);
                break;
            case PlayerTalkKind.Surprise:
                Plan(room, "surprise", null, id, now + 1.1, near, HypeRandom.Range(1, 3), .6f, sure: true);
                break;
            case PlayerTalkKind.Topic:
                // 그 얘기를 묻는 글이면("고양이 키우는 사람?") 묻는 글에 다는 답을 쓴다.
                // 묻는 글의 답(.q)이 따로 없는 갈래는 그 갈래의 보통 대사로 받는다(열쇠의 꼬리를 떼어 찾는다).
                // 토론이 그 얘기로 넘어가기로 했으면(토론 쪽에서도 한마디 한다) 직접 받는 답은 한 명만 단다.
                bool derailed = debate != null && debate.pending == "playerDerail" && !talk.Wonders;
                Plan(room, "topic." + talk.Topic + (talk.Wonders ? ".q" : ""), null, id, now + 1.2, near, derailed ? 1 : HypeRandom.Range(1, 3), .7f, sure: true);
                // 잠깐 자리를 비운다는 사람에게는 한동안 말을 걸지 않는다.
                if (talk.Topic == "afk") { pingNext = now + 240; }
                break;
            case PlayerTalkKind.Question:
            {
                // 무슨 물음인지 알아듣지 못했다. 아는 척 답하면 엉뚱한 소리가 되므로 대개는 아무도 받지 않는다.
                // 가끔 한 명이 "나도 모름" 같은 두루뭉술한 말만 한다. 내 말에서 낱말을 뽑아 되받는 답(echoQ)은 뜻을 모르고 하는 말이라 쓰지 않는다.
                Plan(room, "question", null, id, now + 1.6, near, 1, UnsureReplyChance);
                TraceTalk("    (뜻을 알아듣지 못한 물음: 대개 답하지 않음)");
                break;
            }
            default:
                // 예전에는 방금 하던 얘기(밥, 잠 …)가 있으면 말 상대가 그 얘기의 대사로 받았다. 하지만 내 말이 그 얘기인지 알 수 없으니
                // 엉뚱한 답이 됐다("택시 기사가 주식 얘기함" → "난 라면 먹음"). 알아듣지 못한 말은 그대로 흘려듣는다.
                // 무슨 뜻인지 알아듣지 못한 말이다. 억지로 답하면 더 어색하다. 대개는 흘려듣고, 가끔 한 명만 가볍게 받는다.
                Plan(room, "statement", null, id, now + 1.6, near, 1, UnsureReplyChance * .6f);
                TraceTalk("    (뜻을 알아듣지 못한 말: 대개 답하지 않음)");
                break;
        }
        // 웃음·울음이 섞인 말에는 기분에 따라 한마디 더 붙기도 한다.
        if (talk.Kind != PlayerTalkKind.Laugh && talk.Kind != PlayerTalkKind.Insult && talk.Laughs) { Plan(room, "laugh", null, id, now + 2.4, 0, 1, .25f); }
        if (talk.Kind != PlayerTalkKind.Sad && talk.Kind != PlayerTalkKind.Lament && talk.Cries) { Plan(room, "sad", null, id, now + 2.4, 0, 1, .25f); }
        WinFondness(room, talk, near, id, now);
        if (talk.Kind != PlayerTalkKind.Insult && talk.Kind != PlayerTalkKind.Lewd && talk.Kind != PlayerTalkKind.Noise) { RenownFromChatting(room, id, now); }
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
    /// <summary>
    /// 나를 부르는 멘션은 문장과 이어 붙이지 않는다. 멘션은 맨 앞에 따로 두고, 문장 안에서는 나를 "님"(you, 君)이라고 부른다.
    ///   "@당신은 어느 쪽임?" → "@당신 님은 어느 쪽임?",  "거봐 @당신도 내 편임" → "@당신 거봐 님도 내 편임"
    /// 멘션이 이미 맨 앞에 따로 서 있으면("@당신 근거는 있음?") 그대로 둔다.
    /// </summary>
    public static string DetachMention(string text)
    {
        if (string.IsNullOrEmpty(text)) { return text; }
        foreach (string you in YouMentions)
        {
            int at = text.IndexOf(you, StringComparison.Ordinal);
            if (at < 0) { continue; }
            int end = at + you.Length;
            bool called = at == 0 && (end == text.Length || text[end] == ' ');
            if (called && text.IndexOf(you, end, StringComparison.Ordinal) < 0) { return text; }
            string pronoun = you == "@당신" ? "님" : you == "@You" ? "you" : "君";
            string body = called ? text.Substring(end + 1) : text;
            return you + " " + body.Replace(you, pronoun).Trim();
        }
        return text;
    }
    private readonly Queue<double> rudeAt = new();
    private int greetedDay = -1;
    /// <summary>확인용: 내 채팅으로 호감이 오른(내린) 횟수.</summary>
    public int FondnessGains { get; private set; }
    public int FondnessLosses { get; private set; }
    /// <summary>
    /// 내 말로 호감을 얻는다(잃는다). 얘기하던 봇(near)이 있으면 그 봇의 마음이 가장 크게 움직이고, 방에 있던 다른 봇들도 조금 따라 움직인다.
    ///   위로("힘내")·축하("축하", "부럽")·칭찬("대단", "역시")·감사·사과·맞장구·같이 웃기·인사(하루 한 번)는 호감을 올린다.
    ///   반대하기는 호감을 조금 깎는다. 막말은 방 전체의 호감을 크게 깎는다(막말 쪽에서 따로 처리한다).
    /// 호감이 일정 선을 넘으면 그 봇이 한마디 한다("님이랑은 말이 좀 통하네").
    /// </summary>
    private void WinFondness(int room, PlayerTalk talk, int near, long id, double now)
    {
        if (talk.Kind == PlayerTalkKind.Insult || talk.Kind == PlayerTalkKind.Analyze || talk.Kind == PlayerTalkKind.Lewd || talk.Kind == PlayerTalkKind.Noise) { return; }
        float toNear = 0f, toRoom = 0f; string cause = null, thank = null;
        if (talk.Comforts) { toNear = 5f; toRoom = .8f; cause = "플레이어의 위로"; thank = "liked.comfort"; }
        else if (talk.Congrats) { toNear = 4f; toRoom = .5f; cause = "플레이어의 축하"; thank = "liked.congrats"; }
        else if (talk.Praises && near != 0) { toNear = 4f; cause = "플레이어의 칭찬"; thank = "liked.praise"; }
        else if (talk.Kind == PlayerTalkKind.Thanks) { toNear = 4f; toRoom = .3f; cause = "플레이어의 감사"; }
        else if (talk.Kind == PlayerTalkKind.Apology) { toNear = 3f; toRoom = 1.5f; cause = "플레이어의 사과"; }
        else if (talk.Kind == PlayerTalkKind.Agree) { toNear = 2.5f; cause = "플레이어의 맞장구"; }
        else if (talk.Kind == PlayerTalkKind.Laugh) { toNear = 2f; cause = "플레이어가 같이 웃음"; }
        else if (talk.Kind == PlayerTalkKind.Disagree) { toNear = -1.2f; cause = "플레이어의 반대"; }
        else if (talk.Kind == PlayerTalkKind.Greeting && day != null && greetedDay != day.Day) { greetedDay = day.Day; toRoom = .8f; toNear = 1.5f; cause = "플레이어의 인사"; }
        if (cause == null) { return; }
        TradeBot target = null;
        if (near != 0 && SocialBots.TryGetValue(near, out var talking) && talking != null && talking.Social != null && Present(talking, room)) { target = talking; }
        // 위로는 지금 힘든 봇에게 더 크게 닿는다.
        if (toRoom != 0f)
        {
            bool comfort = talk.Comforts;
            Hearers(room, (bot, mind) => { if (bot != target) { mind.WarmToPlayer(toRoom * (comfort && (bot.Social.Stress >= 50f || mind.Fear >= 60f) ? 3f : 1f), cause); } });
        }
        if (target == null) { TraceTalk("    (호감: " + cause + " · 방 전체 " + toRoom.ToString("+0.0;-0.0") + ")"); if (toRoom > 0f) { FondnessGains++; } return; }
        var heart = target.Social.Psychology; float before = heart.Fondness;
        if (talk.Comforts && (target.Social.Stress >= 50f || heart.Fear >= 60f)) { toNear *= 1.6f; }
        heart.WarmToPlayer(toNear, cause);
        if (heart.Fondness > before) { FondnessGains++; } else if (heart.Fondness < before) { FondnessLosses++; }
        TraceTalk("    (호감: " + cause + " · " + target.Social.Nickname + " " + before.ToString("0.0") + " → " + heart.Fondness.ToString("0.0") + ")");
        // 뜻을 알아듣지 못해 아무도 받지 않았을 말이라도, 위로·축하·칭찬을 받은 봇은 고맙다고 한다.
        bool answered = false;
        foreach (var reply in talkReplies) { if (reply.replyTo == id) { answered = true; break; } }
        if (thank != null && !answered) { Plan(room, thank, null, id, now + 1.3, target.Social.Id, 1, 1f, sure: true); }
        // 선을 넘으면 한마디 한다(오를 때 30, 내릴 때 -30).
        if (before < 30f && heart.Fondness >= 30f) { Plan(room, "fond.up", null, id, now + 3.4, target.Social.Id, 1, 1f, sure: true); }
        else if (before > -30f && heart.Fondness <= -30f) { Plan(room, "fond.down", null, id, now + 2.4, target.Social.Id, 1, 1f, sure: true); }
    }
    /// <summary>내 말의 뜻을 알아듣지 못했을 때 누군가 두루뭉술하게라도 받을 확률. 낮게 둔다: 모르면서 하는 답보다 답이 없는 쪽이 자연스럽다.</summary>
    private const float UnsureReplyChance = .25f;
    /// <summary>답글을 예약한다. count줄까지, 줄마다 chance의 확률로.</summary>
    private void Plan(int room, string key, Stock stock, long replyTo, double first, int onlyBot, int count, float chance, float percent = float.NaN, bool sure = false, int notBot = 0)
    {
        // onlyBot은 첫 줄을 맡을 봇이다(없으면 아무나). 둘째 줄부터는 다른 봇이 한다. sure면 첫 줄은 꼭 나온다.
        for (int i = 0; i < count; i++)
        {
            if (!(sure && i == 0) && HypeRandom.Next() >= chance) { continue; }
            talkReplies.Add(new TalkReply { due = first + i * (1.1 + HypeRandom.Next() * 1.6), room = room, key = key, stock = stock, replyTo = replyTo, onlyBot = i == 0 ? onlyBot : 0,
                notBot = i == 0 ? notBot : onlyBot != 0 ? onlyBot : notBot, percent = percent, said = talkSaid, entry = talkEntry, other = talkOther });
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
                // 답하는 도중에 다음 답글이 예약될 수 있으므로(되묻기) 먼저 목록에서 뺀다.
                talkReplies.RemoveAt(i);
                int spoke = now - reply.due < 20 ? SpeakReply(reply, now) : -1;
                if (spoke == 0) { reply.wait = now + 1.5; talkReplies.Add(reply); break; }
                if (spoke > 0) { talkNextReply = now + .9 + HypeRandom.Next() * .8; }
                break;
            }
        }
        ResolvePlayerCalls(now);
        TickPlayerPing(now);
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
        string stance = mind.Quadrant == InvestorQuadrant.Hostile || mind.Betrayal >= 40f || mind.Fondness <= -35f ? "hostile" : mind.Trust >= 40f || mind.Fondness >= 30f ? "friendly" : null;
        string mood = mind.Fear >= 55f ? "scared" : mind.Greed >= 60f ? "happy" : "dry";
        string key = reply.key, news = null; Stock stock = reply.stock; Stock pick = null; decimal mine = reply.entry;
        // 먼저 말을 걸거나 되묻는 것은 나와 사이가 나쁘지 않은 반말 봇만 한다.
        if ((key == "ping" || key == "askBack") && (stance == "hostile" || mind.Traits.Politeness >= Config.Int("styleFormalMin"))) { return -1; }
        bool scold = key.StartsWith("insult", StringComparison.Ordinal);
        if (!scold && mind.Traits.Politeness >= Config.Int("styleFormalMin"))
        {
            // 존댓말을 쓰는 봇은 자기 말투로 짧게 받는다.
            key = stock != null ? "formal.stock" : AsksKey(key) ? "formal.ask" : "formal"; stance = null;
        }
        else if (stock != null && (key == "ask" || key == "askBuy" || key == "askSell" || key == "claimUp" || key == "claimDown" || key == "mention" || key == "bought" || key == "sold" || key == "willBuy" || key == "willSell" || key == "answered.stock" || key == "advice.gain" || key == "advice.loss"))
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
        else if (key == "compare" && stock != null && reply.other != null)
        {
            // 두 종목 가운데 이 봇이 지금 값보다 더 높게 믿는 쪽. 둘 다 비싸다고 보면 둘 다 싫다고 한다.
            decimal first = stock.currentPrice > 0m ? bot.BelievedPrice(stock) / stock.currentPrice : 0m, second = reply.other.currentPrice > 0m ? bot.BelievedPrice(reply.other) / reply.other.currentPrice : 0m;
            pick = first >= second ? stock : reply.other;
            if (Math.Max(first, second) < .97m) { key = "compare.neither"; pick = null; }
            else if (Math.Abs(first - second) < .02m) { key = "compare.close"; }
        }
        else if (key == "askNick") { if (stock == null || string.IsNullOrEmpty(ChatHypeAssets.ShortName?.Invoke(stock.symbol))) { key = "askNick.none"; } }
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
        slots["news"] = news ?? ""; slots["said"] = reply.said ?? ""; slots["since"] = talkSince ?? "";
        // 회사가 무슨 일을 하는지 묻는 말에는 업종 이름을 넣는다(대사 고를 때 news 자리에 담아 왔다).
        if (key.StartsWith("company", StringComparison.Ordinal)) { slots["sector"] = news ?? ""; }
        // 줄임말을 묻는 말에는 실제로 쓰는 줄임말을 넣는다. 이름 자체는 줄이지 않고 온전히 부른다.
        if (key.StartsWith("askNick", StringComparison.Ordinal) && stock != null) { slots["short"] = ChatHypeAssets.ShortName?.Invoke(stock.symbol) ?? ""; slots.Remove("stockShort"); }
        talkRecent.Enqueue(text); while (talkRecent.Count > 14) { talkRecent.Dequeue(); }
        string line = ChatHypeAssets.Fill(text, slots);
        // 내 글에 다는 답에는 나를 가리키는 멘션을 붙인다(@당신 / @You / @あなた). AI 분석을 보고 하는 말은 내게 하는 말이 아니다.
        // 먼저 거는 말·되묻는 말·알아보는 말은 내 글에 다는 답이 아니다(내 글을 인용하지 않는다). 그래도 나를 부르기는 한다.
        bool calling = reply.replyTo > 0 || key.StartsWith("ping", StringComparison.Ordinal) || key.StartsWith("askBack", StringComparison.Ordinal);
        if (calling && !key.StartsWith("aiReact", StringComparison.Ordinal)) { line = YouMention + " " + line; }
        // 바로 올리지 않는다. 읽고 치는 시간만큼 "입력 중"이 뜬 뒤에 올라간다.
        QueueLine(bot, reply.room, line, "talk." + key, subject, reply.replyTo, "PLAYER_TALK", now);
        hypeSpokeAt[bot.Social.Id] = now; PlayerReplies++;
        if (calling && !key.StartsWith("aiReact", StringComparison.Ordinal)) { AfterTalkReply(reply, bot, key, subject, now); }
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
            SettleFollowers(call.messageId, right, call.room, now);
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
                ? TalkWord("종목 이름을 함께 적어 주세요. 예: !분석 노보", "Add a stock name. Example: !analyze Novo", "銘柄名を付けてください。例: !分析 ノボ")
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
