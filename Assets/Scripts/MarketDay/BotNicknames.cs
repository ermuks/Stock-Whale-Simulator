using System.Collections.Generic;

/// <summary>
/// 봇 닉네임을 여러 파츠의 조합으로 만든다. 같은 시드·번호·언어는 언제나 같은 이름이 된다.
/// 한 세계 안에서는 겹치지 않는다. 공백·기호·종목 이름은 쓰지 않는다(채팅의 @지목과 종목 링크가 글자로 동작한다).
/// 언어마다 그 나라 투자 게시판에서 흔히 보이는 짜임(수식어+동물+숫자, 한 문장 닉네임, 자조 섞인 별명)을 따로 가진다.
/// </summary>
public static class BotNicknames
{
    private sealed class Parts
    {
        public string[] Heads, Cores, Tails, Titles, Phrases;
        public string Joiner;
        public int MaxLength;
    }

    private static readonly string[] Numbers =
    {
        "7", "77", "99", "2", "22", "486", "1004", "119", "123", "0", "88", "365", "24", "5", "3", "007", "17", "84", "128", "42"
    };

    private static readonly Parts Korean = new Parts
    {
        MaxLength = 9,
        Joiner = "와",
        // 수식어: 상태, 성격, 생활, 매매 습관
        Heads = new[]
        {
            "존버하는", "물린", "익절한", "손절한", "풀매수", "야수의", "새벽의", "퇴근한", "출근하는", "야근중인",
            "라면먹는", "커피수혈", "월급쟁이", "백수", "자취하는", "눈팅하는", "조용한", "성급한", "느긋한", "겁많은",
            "용감한", "배고픈", "졸린", "떡상기원", "익절기원", "반등기다린", "상한가꿈꾼", "본전찾는", "눈물젖은", "뚝심의",
            "팔랑귀", "뇌동매매", "단타치는", "장투하는", "적금깬", "소심한", "대범한", "엉뚱한", "성실한", "까칠한",
            "다정한", "시크한", "수줍은", "당당한", "갈팡질팡", "우왕좌왕", "침착한", "흥분한", "방구석", "주말없는",
            "월요병", "점심굶은", "통장텅빈", "계좌열어본", "차트보는", "호가읽는", "뉴스믿는", "소문듣는", "평단낮춘", "불타는",
            "식은", "깨어있는", "잠못드는", "한숨쉬는", "웃고있는", "울고있는", "버티는", "도망친", "돌아온", "처음온",
            "달콤한", "붉은", "젊은", "늙은", "씩씩한", "억울한", "행복한", "심심한", "수상한", "평범한"
        },
        // 본체: 동물, 사람, 물건, 시장 용어
        Cores = new[]
        {
            "개미", "불개미", "왕개미", "일개미", "다람쥐", "수달", "너구리", "고양이", "강아지", "거북이",
            "토끼", "곰", "여우", "부엉이", "참새", "펭귄", "고슴도치", "햄스터", "두더지", "올빼미",
            "까치", "오리", "코알라", "판다", "미어캣", "나무늘보", "카피바라", "돌고래", "문어", "새우",
            "원숭이", "앵무조개", "해파리", "물개", "두루미", "청설모", "비둘기", "개구리", "고래", "멸치",
            "김대리", "박과장", "이부장", "최사원", "정차장", "자취생", "대학원생", "취준생", "알바생", "직장인",
            "사장님", "막내", "신입", "복학생", "삼수생", "동네형", "옆집누나", "삼촌", "이모", "할배",
            "단타왕", "장투러", "차트쟁이", "익절러", "손절러", "존버러", "물타기장인", "불타기장인", "호구", "흑우",
            "초보", "고수지망생", "관망러", "눈팅러", "스캘퍼", "뇌동러", "평단지킴이", "시드지킴이", "예수금", "통장",
            "지갑", "월급", "용돈", "저금통", "계좌", "호가창", "양봉", "음봉", "캔들", "차트",
            "라면", "컵라면", "삼각김밥", "믹스커피", "아메리카노", "소주한잔", "치킨", "떡볶이", "붕어빵", "군고구마",
            "우산", "먼지", "모니터", "키보드", "마우스", "의자", "슬리퍼", "담요", "선풍기", "달력",
            "새벽", "종가", "개장", "주말", "월요일", "퇴근길", "막차", "첫차", "야식", "알람"
        },
        // 꼬리: "개미의눈물" 같은 이름을 만든다
        Tails = new[]
        {
            "의눈물", "의꿈", "의하루", "의역습", "의일기", "의한숨", "의복수", "의계좌", "의평단", "의주말",
            "의야식", "의직감", "의변명", "의다짐", "의후회", "의희망"
        },
        Titles = new[] { "님", "씨", "형", "쓰", "짱", "맘", "파파", "옹", "선생", "박사" },
        // 한 문장 닉네임: 게시판에서 자주 보이는 하소연·다짐·농담
        Phrases = new[]
        {
            "또뭘로떨굴건데", "팔로하지말라고", "내돈내놔라", "살려주세요", "오늘도물렸다", "내일은오른다", "그만내려라", "본전만다오",
            "익절은언제나옳다", "사면내리고", "팔면오르고", "나만안올라", "제발가즈아", "이번엔진짜", "반등은온다", "평단이높아요",
            "물타다대주주", "잠이안온다", "오늘도관망", "매수는기술", "매도는예술", "욕심이문제", "어질어질하네", "살려달라고했다",
            "누가좀말려줘", "손가락이문제", "월급으로물타기", "지금이라도살까", "어제살걸", "그때팔걸", "붉은비상이다", "파란불싫어요",
            "빨간불좋아요", "상한가가고싶다", "개미는뚠뚠", "치킨값벌자", "커피값만벌자", "퇴사하고싶다", "적금이나들걸", "차트는거짓말"
        }
    };

    private static readonly Parts English = new Parts
    {
        MaxLength = 18,
        Joiner = "And",
        Heads = new[]
        {
            "Sleepy", "Broke", "Lucky", "Salty", "Humble", "Angry", "Quiet", "Greedy", "Patient", "Nervous",
            "Hungry", "Tired", "Bored", "Reckless", "Cautious", "Stubborn", "Sneaky", "Jolly", "Grumpy", "Lazy",
            "Diamond", "Paper", "Leveraged", "Bagholding", "Dipbuying", "Overnight", "Caffeinated", "Unemployed", "Midnight", "Weekend",
            "Frozen", "Flying", "Sweaty", "Shaky", "Brave", "Clueless", "Smug", "Lonely", "Loud", "Tiny",
            "Mighty", "Rusty", "Dusty", "Crispy", "Wobbly", "Sunny", "Gloomy", "Frugal", "Bold", "Late"
        },
        Cores = new[]
        {
            "Otter", "Raccoon", "Hamster", "Penguin", "Badger", "Moose", "Gecko", "Walrus", "Pigeon", "Possum",
            "Bull", "Bear", "Whale", "Shrimp", "Squirrel", "Sloth", "Panda", "Goose", "Turtle", "Corgi",
            "Trader", "Investor", "Bagholder", "Scalper", "Hodler", "DipBuyer", "Intern", "Accountant", "Plumber", "Barista",
            "Dad", "Uncle", "Grandpa", "Neighbor", "Roommate", "Student", "Retiree", "NightOwl", "DayJob", "SideHustle",
            "Candle", "Wick", "Ticker", "Ledger", "Wallet", "Paycheck", "Portfolio", "Margin", "Dividend", "Breakout",
            "Ramen", "Coffee", "Bagel", "Taco", "Nugget", "Pancake", "Pretzel", "Waffle", "Noodle", "Donut",
            "Keyboard", "Monitor", "Couch", "Basement", "Garage", "Spreadsheet", "Alarm", "Monday", "Friday", "Overtime"
        },
        Tails = new[]
        {
            "Hands", "Gains", "Dreams", "Regrets", "Tears", "Vibes", "Energy", "Szn", "Gang", "Mode",
            "Capital", "Holdings", "Research", "Fund", "Life", "Club"
        },
        Titles = new[] { "Guy", "Dude", "Bro", "Sr", "Jr", "PhD", "Esq", "Mom", "Dad", "King" },
        Phrases = new[]
        {
            "BuyHighSellLow", "NotFinancialAdvice", "ThisIsFine", "WhyDidIBuy", "ShouldHaveSold", "OneMoreDip", "PleaseGoUp", "StopTheDrop",
            "TrustMeBro", "AllInAgain", "DownBadDaily", "GreenByClose", "RentMoneyGone", "WifeDoesntKnow", "StillHolding", "NeverSelling",
            "JustBreakEven", "BoughtTheTop", "SoldTheBottom", "ChartsAreLies", "TooLateAgain", "MaybeTomorrow", "SendHelpPls", "ExitLiquidity",
            "CantStopChecking", "LunchMoneyOnly", "QuitMyJobSoon", "ShouldveBoughtMore", "MyBadTrade", "RedDaysOnly", "TouchGrassLater", "SleepIsForBulls"
        }
    };

    private static readonly Parts Japanese = new Parts
    {
        MaxLength = 10,
        Joiner = "と",
        Heads = new[]
        {
            "眠い", "貧乏な", "強気の", "弱気の", "静かな", "焦る", "のんびり", "臆病な", "勇敢な", "腹ぺこ",
            "含み損の", "含み益の", "塩漬け", "ガチホ", "爆益祈願", "退場寸前", "様子見の", "高値掴み", "底値狙い", "損切り",
            "残業中の", "無職の", "週末の", "深夜の", "早起き", "月曜の", "社畜", "脱サラ", "兼業", "専業",
            "泣いてる", "笑ってる", "迷える", "耐える", "逃げた", "戻った", "初心者", "万年", "自称", "元気な",
            "素直な", "頑固な", "気弱な", "陽気な", "孤独な", "普通の", "怪しい", "幸せな", "暇な", "必死の"
        },
        Cores = new[]
        {
            "イナゴ", "アリ", "タヌキ", "キツネ", "ネコ", "イヌ", "カメ", "ウサギ", "クマ", "フクロウ",
            "スズメ", "ペンギン", "ハムスター", "モグラ", "カワウソ", "パンダ", "コアラ", "ナマケモノ", "カピバラ", "イルカ",
            "タコ", "エビ", "クジラ", "ハト", "カエル", "サル", "リス", "アヒル", "クラゲ", "メダカ",
            "会社員", "係長", "課長", "部長", "新人", "学生", "主婦", "隠居", "おじさん", "兄さん",
            "投資家", "ホルダー", "デイトレ", "スキャ", "養分", "狼", "仙人", "職人", "名人", "見習い",
            "ナンピン", "指値", "成行", "陽線", "陰線", "板読み", "配当", "優待", "権利落ち", "寄り付き",
            "ラーメン", "おにぎり", "缶コーヒー", "カップ麺", "焼き鳥", "たこ焼き", "たい焼き", "焼き芋", "牛丼", "味噌汁",
            "こたつ", "座布団", "扇風機", "目覚まし", "終電", "始発", "夜食", "月曜日", "給料日", "ボーナス"
        },
        Tails = new[]
        {
            "の涙", "の夢", "の日記", "の逆襲", "のため息", "の口座", "の週末", "の夜食", "の言い訳", "の決意",
            "の後悔", "の希望", "の直感", "の誓い", "の祈り", "の独り言"
        },
        Titles = new[] { "さん", "くん", "太郎", "次郎", "マン", "ママ", "パパ", "先生", "博士", "丸" },
        Phrases = new[]
        {
            "また下げるんか", "金返せ", "助けてください", "今日も含み損", "明日は上がる", "もう下げるな", "同値でいいから", "利確は正義",
            "買えば下がる", "売れば上がる", "自分だけ上がらん", "今度こそ本気", "眠れない夜", "今日も様子見", "欲が敵", "クラクラする",
            "誰か止めて", "指が勝手に", "給料でナンピン", "今から買おうか", "昨日買えば", "あの時売れば", "マイ転しました", "プラ転祈願",
            "ストップ高希望", "焼き鳥代稼ぐ", "コーヒー代でいい", "会社辞めたい", "定期にしとけば", "チャートは嘘つき", "ヨコヨコでいい", "握力だけはある"
        }
    };

    private static readonly HashSet<string> used = new();

    private static uint Mix(int seed, int id, string salt)
    { return CompanyBusiness.Hash("NICK:" + salt + ":" + seed + ":" + id); }

    private static Parts current = Korean;

    // 봇은 번역 표가 다 읽히기 전에 만들어진다. 그때는 GameLocale.Code가 아직 기본값이므로 저장된 언어 설정을 직접 읽는다.
    private static Parts Current()
    {
        string code = GameLocale.Ready ? GameLocale.Code : UnityEngine.PlayerPrefs.GetString("DonDon.Language", "ko");
        return code == "en" ? English : code == "ja" ? Japanese : Korean;
    }

    public static string Make(int seed, int id)
    {
        // 새 세계의 첫 봇에서 초기화한다. 번호는 세계마다 1부터 다시 시작한다.
        if (id <= 1)
        {
            used.Clear();
            current = Current();
        }

        Parts parts = current;
        string head = parts.Heads[Mix(seed, id, "H") % (uint)parts.Heads.Length];
        string core = parts.Cores[Mix(seed, id, "C") % (uint)parts.Cores.Length];
        string number = Numbers[Mix(seed, id, "N") % (uint)Numbers.Length];
        uint pattern = Mix(seed, id, "P") % 100;
        string name;
        if (pattern < 34)
        {
            name = head + core;
        }
        else if (pattern < 52)
        {
            // 증권 앱이 처음 붙여 주는 이름과 같은 짜임: 수식어+본체+숫자
            name = head + core + number;
        }
        else if (pattern < 64)
        {
            name = core + parts.Tails[Mix(seed, id, "T") % (uint)parts.Tails.Length];
        }
        else if (pattern < 74)
        {
            name = core + number;
        }
        else if (pattern < 81)
        {
            name = core + parts.Titles[Mix(seed, id, "S") % (uint)parts.Titles.Length];
        }
        else if (pattern < 95)
        {
            name = parts.Phrases[Mix(seed, id, "F") % (uint)parts.Phrases.Length];
            if (Mix(seed, id, "FN") % 4 == 0)
            {
                name += number;
            }
        }
        else
        {
            string other = parts.Cores[Mix(seed, id, "C2") % (uint)parts.Cores.Length];
            name = other == core ? head + core : core + parts.Joiner + other;
        }

        // 채팅 줄에 들어가도록 너무 긴 이름은 줄인다.
        if (name.Length > parts.MaxLength)
        {
            name = (head + core).Length <= parts.MaxLength ? head + core : core + number;
        }

        // 겹치면 다른 수식어를 차례로 붙여 보고, 그래도 겹치면 번호를 붙인다.
        for (int attempt = 0; attempt < 8 && used.Contains(name); attempt++)
        {
            string next = parts.Heads[(Mix(seed, id, "H") + (uint)attempt + 1) % (uint)parts.Heads.Length] + core;
            name = next.Length <= parts.MaxLength ? next : core + number;
        }

        if (used.Contains(name) || name.Length == 0)
        {
            name = core + id;
        }

        used.Add(name);
        return name;
    }
}
