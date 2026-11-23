# Steam 도전과제 목록 (37개)

Steamworks → 앱 관리 → 통계 및 도전과제 → 도전과제에서 아래 **API 이름**을 글자 그대로 등록한다.
게임 코드(`Assets/Scripts/Steam/SteamAchievements.cs`)가 이 이름으로 달성을 올린다. 이름이 다르면 올라가지 않는다.
이름·설명은 Steamworks에서 언어별로 넣는다(아래 표를 그대로 써도 된다). 아이콘(달성/미달성, 각 256×256 JPG)은 따로 만들어야 한다.

## 거래

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `FIRST_BUY` | **거래 시작!** / 종목을 처음으로 매수하세요 | **Open for Business** / Buy a stock for the first time | **取引開始！** / 初めて銘柄を買う |
| `FIRST_SELL` | **팔아야 내 돈** / 종목을 처음으로 매도하세요 | **Cashing Out** / Sell a stock for the first time | **売ってこそ自分の金** / 初めて銘柄を売る |
| `FIRST_PROFIT` | **첫 수익** / 산 값보다 비싸게 파세요 | **In the Green** / Sell for more than you paid | **初めての利益** / 買値より高く売る |
| `SPLIT_ORDER` | **나눠서 조용히** / 분할 주문을 넣으세요 | **Slice It Up** / Place a split order | **分けて静かに** / 分割注文を出す |
| `NET_WORTH_1B` | **큰손** / 순자산 10억 돈을 달성하세요 | **Big Money** / Reach a net worth of 1,000,000,000 DON | **大口** / 純資産10億ドンを達成する |
| `DELISTED` | **휴지 조각** / 보유한 종목이 상장폐지되는 것을 겪으세요 | **Worthless Paper** / Hold a stock when it gets delisted | **紙くず** / 保有銘柄の上場廃止を経験する |

## 채팅과 투자자

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `CHAT` | **한마디 거들기** / 채팅방에 글을 올리세요 | **Speak Up** / Post a message in the chat | **ひとこと参加** / チャットに書き込む |
| `PROFILE` | **뒷조사** / 투자자의 프로필을 열어 보세요 | **Background Check** / Open an investor's profile | **身元調査** / 投資家のプロフィールを開く |
| `PROFIT_PROOF` | **인증합니다** / 채팅방에 손익 인증을 올리세요 | **Receipts** / Share a profit or loss card in the chat | **証拠はこちら** / チャットに損益カードを貼る |

## 방과 하루

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `DOOR_NOTE` | **문틈의 쪽지** / 문 아래로 들어온 쪽지를 주워 읽으세요 | **A Note Under the Door** / Pick up a note slipped under your door | **ドアの下のメモ** / ドアの下から届いたメモを拾って読む |
| `DRAWER` | **서랍 속** / 책상 서랍을 열어 보세요 | **Top Drawer** / Open the desk drawer | **引き出しの中** / 机の引き出しを開ける |
| `SLEEP` | **오늘은 여기까지** / 잠을 자고 다음 날을 맞으세요 | **Call It a Day** / Sleep and wake up to the next day | **今日はここまで** / 眠って次の日を迎える |
| `WEEK` | **첫 주 생존** / 첫 주의 거래일 5일을 넘기세요 | **Survived the Week** / Get through the first five trading days | **最初の一週間** / 最初の5営業日を乗り切る |
| `UNLOCK_INFO` | **정보는 돈이다** / 해금 탭에서 정보를 하나 여세요 | **Information Costs Money** / Unlock an item in the Unlocks tab | **情報は金なり** / 解放タブで項目をひとつ開ける |

## 리딩방

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `DAWN_ROOM` | **비밀방 입장!** / 새벽 관찰실에 참여하세요 | **Behind Closed Doors** / Join the Dawn Watch Room | **秘密の部屋へ！** / 夜明けの観察室に参加する |
| `OPEN_ROOM` | **리딩방 개설!** / 리딩방을 개설하세요 | **Room of Your Own** / Open your own signal room | **ルーム開設！** / 自分のルームを開設する |
| `FIRST_MEMBER` | **첫 손님** / 리딩방에 개미를 초대하세요 | **First Guest** / Invite an investor into your room | **最初のお客さん** / ルームに個人投資家を招待する |
| `FIRST_CALL` | **따라오세요** / 리딩방 회원들에게 매매 지시를 내리세요 | **Follow Me** / Issue a call to your room's members | **ついて来て** / 会員に売買指示を出す |
| `PROMOTE` | **방 홍보** / 라운지에 내 리딩방을 홍보하세요 | **Shameless Plug** / Promote your room in the lounge | **ルーム宣伝** / ラウンジで自分のルームを宣伝する |
| `OPENING_RUSH` | **장 시작 몰아주기** / 개장과 동시에 회원들을 한 종목에 몰아주세요 | **Opening Rush** / Send your members into one stock at the open | **寄り付き集中** / 寄り付きと同時に会員を一銘柄へ向かわせる |

## 작전

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `TACTIC_IGNITE` | **불붙이기** / 불붙이기로 매도 호가를 쓸어 올리세요 | **Ignite** / Sweep the asks with Ignite | **点火** / 点火で売り板を買い上げる |
| `TACTIC_WASH` | **거래량 부풀리기** / 차명 계좌와 주고받아 거래량을 만드세요 | **Paint the Tape** / Trade with a nominee account to fake volume | **出来高の水増し** / 名義口座との売買で出来高を作る |
| `TACTIC_CLOSE` | **종가 관리** / 종가 관리를 예약하세요 | **Mark the Close** / Arm a Mark the close | **終値の吊り上げ** / 終値の吊り上げを予約する |
| `TACTIC_WALL` | **허수 매수벽** / 허수 매수벽을 세우세요 | **Fake Bid Wall** / Put up a fake bid wall | **見せ板の買い壁** / 見せ板の買い壁を置く |
| `TACTIC_SLAM` | **내려찍기** / 매수 호가에 물량을 한 번에 던지세요 | **Slam** / Dump your shares into the bids at once | **叩き落とし** / 買い板に一気に売りをぶつける |

## 두 번째 계좌, 기사, 밤일

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `SECOND_ACCOUNT` | **두 번째 계좌** / 두 번째 계좌를 여세요 | **Second Account** / Unlock your second account | **二つ目の口座** / 二つ目の口座を開く |
| `SPREAD` | **호가 장사** / 호가 차익 자동 매매를 시작하세요 | **Working the Spread** / Start automated spread trading | **板の商い** / スプレッド自動売買を始める |
| `ACCUMULATE` | **조용한 매집** / 자동 매집을 시작하세요 | **Quiet Accumulation** / Start an automated accumulation | **静かな買い集め** / 自動の買い集めを始める |
| `FAKE_NEWS` | **기사 한 줄** / 내 손으로 기사를 내보내세요 | **Extra! Extra!** / Publish a story of your own | **記事を一本** / 自分の手で記事を出す |
| `NIGHT_WORK` | **밤일** / 장이 끝난 밤에 내일을 준비하세요(기사 미리 사기·기사 심기·리딩방 예고) | **Night Shift** / Prepare for tomorrow at night (buy a story, plant a story or tease your room) | **夜の仕事** / 引け後の夜に明日の準備をする(記事の先読み・仕込み・予告) |

## 기업

| API 이름 | 한국어 | English | 日本語 |
|---|---|---|---|
| `SMALL_COMPANY` | **첫 인수** / 비상장 소기업의 지분을 사들이세요 | **First Acquisition** / Buy into a small private company | **初めての買収** / 非上場の小企業の株を取得する |
| `FOUND_COMPANY` | **창업** / 내 기업을 설립하세요 | **Founder** / Found your own company | **起業** / 自分の会社を設立する |
| `POLICY` | **경영 방침** / 경영하는 회사의 방침을 정하세요 | **Company Policy** / Set the policy of a company you run | **経営方針** / 経営する会社の方針を決める |
| `GROWTH` | **성장 투자** / 회사의 성장 사업에 투자를 시작하세요 | **Growth Project** / Start a growth project at your company | **成長投資** / 会社の成長事業に着手する |
| `IPO` | **상장!** / 내 기업을 상장시키세요 | **Going Public** / Take your company public | **上場！** / 自分の会社を上場させる |
| `TAKEOVER` | **삼키다** / 상장 기업의 경영권을 차지하세요 | **Swallowed Whole** / Take control of a listed company | **飲み込む** / 上場企業の経営権を握る |
| `ENDING` | **꿈에서 깨다** / 엔딩을 보세요 | **Waking Up** / See the ending | **夢から覚める** / エンディングを見る |

## 판정 방식

- 게임 상태를 1초마다 읽어 올리는 것: `CHAT`, `DRAWER`, `WEEK`, `UNLOCK_INFO`, `DAWN_ROOM`, `OPEN_ROOM`, `SECOND_ACCOUNT`, `SPREAD`, `ACCUMULATE`, `FOUND_COMPANY`, `IPO`, `TAKEOVER`, `NET_WORTH_1B`, `ENDING`. 저장을 불러와도 이미 이룬 것은 올라간다.
- 그 일이 일어나는 순간에 올리는 것: 나머지 전부.
- Steam에 연결되지 않은 채로 달성한 것은 PC에 적어 두었다가 다음에 연결되면 올린다.
- Unity 편집기에서는 Steam에 연결하지 않는다. 편집기에서 시험하려면 환경 변수 `SWS_STEAM_IN_EDITOR=1`을 주고 프로젝트 루트에 앱 ID를 적은 `steam_appid.txt`를 둔다.
- 빌드한 실행 파일을 Steam 밖에서 직접 띄워 시험하려면 실행 파일 옆에 앱 ID만 적은 `steam_appid.txt`를 둔다(Steam에는 올리지 않는다).
