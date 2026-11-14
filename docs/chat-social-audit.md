# 채팅 사회화 0단계 조사

조사일: 2026-11-15. 기준 프로젝트: `D:/GitHub/DonDon`, Unity 6000.5.5f1. 조사 중 게임 코드·씬·프리팹·리소스는 수정하지 않았다.

명세는 첨부한 `C:/Users/saerm/Downloads/chat-social-spec.md` 963줄 전체를 읽고 `docs/chat-social-spec.md`로 바이트 그대로 복사했다. SHA-256: `49d626615cc0cca733e477d86c7ff7b4ab2755e2eaa2d201d1a29e46e56aa624`.

프로젝트와 상위 경로에서 적용할 AGENTS.md는 발견하지 못했다. 처음에는 Git 저장소도 없었다. 단계별 커밋을 위해 로컬 저장소를 초기화한다. 외부 저장소 생성이나 푸시는 수행하지 않는다. CPU 충돌 이력에 따라 검증은 한 프로세스씩, 한 코어/BelowNormal로 제한한다.

## Q1. 문장 생성·JSON·가중치

- `Assets/Scripts/MarketDay/MarketDayController.cs`: `Awake`가 `Resources/Chat/BotChatTemplates.ko`를 로드한다. `Enqueue`, `ProcessChat`, `Eligible`가 사건·후보·템플릿을 고른다. 사건 큐, 최근 보유자/매도자 인덱스, 후보 처리 예산을 이미 갖고 있다.
- `Assets/Scripts/MarketDay/BotChatData.cs`: `BotChatTemplateCatalog.Document/Entry`, 생성자, `ForEvent`가 schemaVersion 1 문서를 검증하고 `ChatEvent`별 템플릿 리스트를 만든다. 필드는 id/event/personality/tone/holdingCondition/text/requirements/minStress/maxStress/weight이다. 슬롯 검사는 `{stockName}`만 허용한다. 가중치는 유효한 템플릿의 `weight` 합에서 추첨한다.
- `Assets/Resources/Chat/BotChatTemplates.ko.json`: 192개 문장, 16개 사건, 격식은 192개 모두 `Casual`. SmallTalk 24개, Agreement 16개, PriceRise/PriceFall 각각 16개, 나머지 12종 각각 10개다. 한국어 파일에 U+FFFD 대체 문자는 없다.
- `Assets/Scripts/NightShift/BotMind.cs`: `Compose`가 문장 뒤 어미·설명을 붙인다. 고정된 별도 잡담 목록도 있다.
- `Assets/Scripts/NightShift/InvestorConversation.cs`: `Greeting`, `Testimony`, `MissedForecast`, `Waiting`, `MarketRemark`가 별도 C# 문자열을 구성한다. 시장 값·실현 결과를 읽는 좋은 경로지만 JSON 문장 선택과 분리되어 있다.
- `Assets/Scripts/NightShift/InvestorSociety.cs`: `TickBot`, `Observe`, `Speak`도 발화를 생성한다. 현재 공용과 리딩방에 서로 다른 생성 경로가 존재한다.

판단: 방·큐·실제 사실 조회를 재사용하고 메시지 생성만 하나의 화행 파이프라인으로 연결해야 한다. JSON 파일을 병렬로 새 채팅 엔진과 함께 돌리면 중복 발화·쿨다운 우회가 생긴다. 기존 템플릿 카탈로그의 역할을 확장한다.

## Q2. 호출 대상과 응답

- `MarketDayController.ProcessChat`: `Agreement`일 때 `replySource.Nickname + "님, "`을 붙이고 `lastRepliedSequence`를 기록한다. 대상 ID는 그 메시지 생성자에 전달하지 않는다.
- `InvestorSociety.TickBot`: 최근 목격자의 닉네임을 문자열에 붙인다. `Observe`는 반대 수익 경험이 있으면 닉네임 접두사와 `ReplyTo`가 있는 문장을 즉시 발행할 수 있다.
- `InvestorPsychology.cs`의 `InvestorMessageContext`에는 `EventId/ReplyTo/TargetId/Room/Hops`가 이미 있다. 그러나 일반 대화는 TargetId 0, 플레이어 관련 증언은 -1인 경로가 많아 화면 호출과 데이터가 일치하지 않는다.
- `BotChatMessage`는 readonly struct이며 각 방의 `Sequence`는 방 안에서 증가한다. 전역 메시지 ID로 그대로 사용하면 방끼리 충돌한다.

판단: 기존 Context를 확장하고, 화면 `@닉네임`을 대상 ID로부터 최종 렌더링해야 한다. 응답 의도·예약 지연·스레드는 아직 없다. 스레드 첫 응답 때 원 메시지에도 ID를 붙이려면 기존 링 버퍼의 메시지 교체/버전 갱신 경로도 필요하다.

## Q3. 탐욕·공포·신뢰·배신감·FOMO의 저장과 모든 소비 지점

소유 원본은 `InvestorPsychology`의 float 속성 `Greed/Fear/Trust/Betrayal/Fomo`다. `BotSocialState`가 Psychology를 하나 소유한다. `BotSocialState.Greed/Trust`는 100으로 나눈 어댑터이며 별도 원장이 아니다.

`Assets/Scripts` 전체의 속성 참조를 조사한 소비 경로는 다음과 같다. 클래스명에 Fomo가 들어가는 봇은 심리 Fomo 수치와 구분했다.

- `InvestorPsychology` 생성자: 탐욕 50~80, 나머지 기본 0. `Record`: 실제 리딩 결과로 신뢰·배신감·공포·탐욕 변경. `ObserveForecast`: 거부한 지시의 목표 도달로 신뢰/FOMO 변경. `Decay`: 시간 감쇠. `RecallBetrayal`: 재회유 시 배신 기억 재활성화. `Witness`: 소문으로 신뢰 또는 배신감과 공포/FOMO 변경. `ObserveRisk/ObserveMissedGain`: 평가손실·놓친 상승 반영. `Acceptance/JoinChance/LeaveChance`: 다섯 지수를 확률에 사용. `Clamp`: 탐욕·공포·FOMO만 0~100, 신뢰·배신감에는 상한이 없다.
- `BotChatData.cs / BotSocialState.Greed, Trust`: 0~1 스케일 어댑터. `BotChatMessage` 생성자: 메시지 작성 당시 신뢰와 스트레스 스냅샷.
- `BotMind.BuyBias`: 탐욕·공포·FOMO가 실제 매수 편향에 들어간다. `PersuasionChance`는 JoinChance를 사용한다. `Compose`는 탐욕·스트레스에 따라 어미를 바꾼다.
- `InvestorSociety.TickBot`: 신뢰 기반 지루함/잔류, JoinChance/LeaveChance, 직접 경험 발화. `ReadRecent/Observe/Context/RecordOutcome/Invite`: 목격 효과, 신뢰 스냅샷, 직접 결과, 회유 반응.
- `LeadingRoomOrders.Decide`: Acceptance. `Tick`: 공포 기반 손절과 ObserveRisk.
- `TabletGroupChatUI.Update`: 기존 프로필 텍스트에 다섯 수치 표시. `InvestorProfileUI.Amount/RefreshSummary/RefreshDetails`: 신뢰·배신감·탐욕·공포·FOMO·스트레스의 여섯 게이지와 상세값.
- `TerminalGlyph.SetValue/UpdatePulse/Breathe/OnPopulateMesh`: 전달받은 값이 100을 초과하면 과열 맥동. UI를 그리는 코드 자체는 심리 원장을 직접 읽지 않는다.
- `IncumbentExchangeAI`의 플레이어 관계 스냅샷 수집: 신뢰와 배신감을 비교하여 지지/반대 집계를 만든다. 4분면 도입 시 이 집계도 함께 바꿔야 한다.
- `TradeBot.PickStock`: `Social.Greed`로 회유 종목 선호 확률을 계산한다.
- `WorldDramaDirector.TryFundDecision`: 탐욕으로 기업 소유 봇의 목표 매수/매도 편향을 계산한다.

`FomoBot`, `MonitorBot`, `WhaleFollowerBot`의 FOMO 이름/모드는 별도의 투자 전략이다. 숫자 `InvestorPsychology.Fomo`를 단순히 지우고 전략 클래스까지 삭제해서는 안 된다. 수치의 위 소비 경로를 태그 여부/대상 종목/만료 시각으로 옮기고 기존 전략과 결합해야 한다.

**확인 필요한 의도 충돌:** 이전 사용자 요청으로 신뢰·배신감은 100% 초과를 허용하고 맥동하도록 의도적으로 구현되어 있다. 명세 3장의 기본 0~100 정수 규칙에는 이 예외가 적혀 있지 않다. 명세 0장의 두 번째 중단 조건으로 질문을 남겼다. 답변 전에는 이 값을 자르거나 연출을 제거하지 않는다.

## Q4. 리딩 수락

- `InvestorPsychology.Acceptance`는 `Clamp01(.08 + Greed*.003 + Trust*.005 + Fomo*.002 - Betrayal*.006 - Fear*.003)`이다. 신뢰와 배신감이 하나의 선형 합으로 상쇄된다.
- `LeadingRoomOrders.Consider/Decide`: 같은 roll로 수락/보류/거절을 나눈다. 가용 현금과 최소 가격을 검사하고 `commandCashFraction`·`maxOrderShares`로 목표 수량을 정한다.
- `Tick`: 모든 수락 봇이 동일한 지시 Sell 가격을 기다리며 공포 70 초과 시 확률적으로 손절한다. 애증의 조기 익절·축소 금액·좁은 손절 규칙은 없다.
- `Record/ObserveForecast`: 앞선 요청대로 실제 명령 수익자는 수익률×2, 거부 후 성공을 본 봇은 목표 수익률×1 신뢰를 얻는 코드가 있다.

판단: 명세 9.4가 명시적으로 요구한 교체 대상이다. 4분면/히스테리시스와 금액·익절·손절을 기존 Plan/명령/주문 체결에 적용한다. 실제 체결 원장이나 유한 자금 검사는 유지한다.

## Q5. 봇 사이 관계

`InvestorPsychology.Perceptions/RememberSpeaker`는 최근 32개 발언의 작성자·사건·감정·신뢰성을 저장하지만 방향별 호감/분노 값은 없다. `BetrayalMemory`는 플레이어 관련 기억 강도이며 다른 봇과의 관계가 아니다. `InvestorSociety.suggestions`도 추천 방 번호이지 친구 관계가 아니다.

판단: 기존 Psychology 아래 상대 ID를 키로 한 방향성 희소 관계 테이블을 추가한다. 초기 친구 생성은 고정 시드와 정렬된 봇 ID로 수행한다. 기존 Perceptions는 목격 이력으로 유지/확장한다.

## Q6. 고정 성향

- `BotSocialState` 생성자: `Personality`, `Tone`, `Risk/Horizon/Chase/Sensitivity/Recovery`, `Impulsiveness/Articulation/Confidence`와 역할을 생성한다. 현재 Tone은 Casual로 고정이다.
- `InvestorPsychology` 생성자: `BaselineGreed/Credulity/Activity/ActivityOffset`를 고정 시드로 생성한다.
- `TradeBot.Risk.cs`: 투자 위험 반응 성향과 위험 등급에 대한 지연된 가중치가 별도로 있다.

판단: 기존 투자 성향을 지우지 않는다. temper/grudge/suggestibility/sociability/bravado/politeness를 기존 사회 상태에 추가한다. Credulity/Activity/Articulation과 역할이 가까운 부분은 대응을 명시하며 독립적인 두 발화 엔진을 만들지 않는다. 1단계에서는 새 필드 추가만으로 기존 채팅 확률이 바뀌면 안 된다.

## Q7. 리딩방 기능과 UI

- `InvestorSociety`: 공용 채팅은 MarketDayController가 소유하고, Rooms는 플레이어 방 1~3과 NPC 방 4다. `IsRoomOpen/Join/Leave/Invite/Issue/CancelRoom`이 구현되어 있다. bot의 Psychology.Room은 한 개의 사설 방만 허용하고 공용방도 읽는다.
- `LeadingRoomUI.Start/Invite/Issue/Cancel`: 방 선택·회유·명령 발행·취소 버튼을 연결한다. `NpcLeadingRoom.JoinPlayer/LeavePlayer`와 NPC 방 관측·주문 검토 기능이 있다.
- `TabletGroupChatUI.SelectRoom/SelectBot`, `InvestorProfileUI.Bind`: 방/봇 선택과 프로필 UI. 기존 메시지 행 풀링·드래그·최신 따라가기를 유지할 연결점이다.
- `PlayerProgression.OpenLeadingRooms`: 실제 체결 거래대금/실현손익 조건으로 자기 방 운영 권한을 연다.

경고·채팅 금지·추방, 방별 영구 추방 목록, 재입장 쿨다운, discipline/chill, 스레드 종료, 친구 동조 퇴장은 없다. 현재 Leave는 인원 집합에서 빼고 명령을 취소하지만 인사나 단계 이력을 만들지 않는다.

판단: 플레이어 방의 기존 봇 선택 동작에 작은 조치 메뉴를 연결한다. NPC 방에서는 플레이어가 방장 조치를 사용할 수 없으며, 게임 시작의 NPC 방 흐름은 보존한다.

## Q8. 목격 판정

`InvestorSociety.TickBot/ReadRecent/ReadBeforeSpeech`가 현재 활동 중인 봇에게 공용방과 가입한 리딩방의 최근 N개 메시지를 읽힌다. 메시지마다 `witnessProbability * (.35 + Activity*.65)`로 추첨한다. 현재 씬의 witnessProbability는 .35다. 읽은 마지막 Sequence는 방마다 별도로 관리하고 가입 전에 올라온 사설 메시지는 목격하지 않는다.

`Observe/Witness`는 동일 EventId, 최대 전파 홉 수, 신뢰성, 개인 Credulity, 기존 반대 경험으로 소문 효과를 제한한다. 그러나 부정 소문은 현재 배신감을 올린다. 스레드별 ±10, 간접 신뢰 하루 ±15 예산과 발신자별 관계 효과는 없다.

판단: 기존 실제 목격 확률/활동 판정을 재사용하고 6장의 수신자/목격자 효과를 그 결과에만 적용한다. 신규 전역 감정 방송을 만들지 않는다. 부정 목격으로 배신감 증가하는 기존 경로는 명세 6.3대로 제거하고 간접 신뢰 변화로 바꾼다.

## Q9. 게임 시간·배속·장 마감

- `MarketDayController.GameMinute/Duration/Update/AdvancePhase`: PreOpen/Trading/Closing/PostClose의 단일 날짜·장 시계다.
- 실제 씬 객체 1303051287: 장 시작 대기 0초, 거래 900초, 장후 20초. 거래 구간은 게임 09:00~15:30(390분), 장후는 15:30~18:00(150분)에 대응한다. 장후가 끝나면 Day가 증가하며 다음 거래일로 넘어간다. 밤 시간은 별도 플레이 구간이 없다.
- `TradingElapsed`는 거래 중에만 게임 초 단위로 누적된다. `Time.time`은 스케일이 적용된 초이며 거래 외 구간에도 흐른다.
- `MouseCameraController.TogglePause` 및 보기/일시정지 처리: Time.timeScale은 0 또는 1. 배속 기능은 없다. MarketDayController는 0일 때 갱신을 중지한다.
- `InvestorSociety.Update`는 현재 CanTrade일 때만 처리한다. 공용 `ProcessChat`는 장후에도 발화할 수 있다. 기존 감정 감쇠/지연은 대부분 Time.time의 초 단위다.

판단: 새 배속을 추가하지 않는다. 명세의 1배속 실제 초는 스케일된 초, 게임 분/시간은 기존 Day/GameMinute를 입력하는 시계 어댑터로 구분할 수 있다. 밤 점프와 장후를 거래 중과 같은 고정 환산율로 바꾸면 안 된다. 테스트의 24시간/3일은 이 실제 장 시계 규칙을 사용하고 일시정지 동안 지연·감쇠가 멈추는지 확인한다. 현재 조사로는 시간 체계를 새로 정의해야 하는 충돌은 없다.

## Q10. 난수·재현성

- `MarketDayController.Awake`: System.Random(seed)와 UnityEngine.Random.InitState(seed), 실제 씬 seed=12345.
- `InvestorSociety.Awake`: 별도 System.Random(settings.seed), 실제 씬 seed=73129.
- `BotSocialState/InvestorPsychology`: seed + id*7919의 독립 생성기. `CandleHistoryManager.StableSeed/GenerateHistory`: 종목별 가격/거래량 생성기.
- 거래 봇은 UnityEngine.Random과 Time.time 기반 Update/코루틴 스케줄도 사용한다. 시드만 같다고 서로 다른 프레임 스케줄에서 전체 시장 재현성이 보장되는 구조는 아니다.

판단: 사회 시스템의 결정은 주입 가능한 난수·시계와 정렬된 ID/이벤트 순서로 고정해야 한다. 테스트 실행기는 실제 사회·주문 코드를 사용하고 엔진 경계를 어댑터로 대체한다. 단순 확률 공식만 흉내 낸 테스트를 전체 시장 검증으로 보고하면 안 된다. 기존 테스트용 고정 시드와 자원 제한 실행기를 재사용한다.

## Q11. 급관심과 매매 종목 선택

- `MarketConversationFocus.Register/Record/Refresh/Choose/EventWeight`: 실제 체결을 1초 버킷에 저장하며 최근 60초와 이전 60초를 비교한다. 전체 시장 거래량/거래대금 점유율에 가속도와 절대 등락률을 곱한다. 현재 탐색 비중 .18. 이는 명세의 해당 종목 5분/3시간 평소 대비 점수와 다르다.
- `StockInterestManager.AddInterest/SelectionWeight/PickStockByWeightForBot/Decay`: 직전 수정에서 로그 거래대금, 최대 기본 관심도 8배, 25% 위험 보존 탐색, 5초별 .90 감쇠가 추가됐다. `StockNewsAttentionManager`는 뉴스 최대 3배와 .92 감쇠다.
- `TradeBot.PickStock`: 전문 종목 → 회유 종목 → 관심 가중치. `GetPreference/OnSellFilled`: 수익을 낸 종목 선호를 누적한다.
- `Liquidity/FireflyBot.PickStock`: 별도의 후보 점수에서 SelectionWeight를 사용한다. `WhaleBot.ResolveTargetStock`는 소유 회사/고정 목표 또는 역관심 선택이다. FomoBot의 추격·사건 반응도 존재한다.
- 현재 종목별 봇 피로, 수익 후 순환매 유효 기간, 명세식 과열 제동 및 FOMO 예외는 없다.

판단: 7단계의 수정 전 기준은 이미 적용된 직전 패치 상태여야 한다. 과거 수정을 되돌린 숫자를 이번 기준선으로 대체하지 않는다. A를 적용한 뒤 실제 체결을 사용하는 3일 실험을 수행하고 목표 미달 시 B/C/D를 차례로 적용한다. 기존 매도와 유동성 공급·회사 소유 역할을 신규 진입 피로로 막지 않는다. 앞선 12,000회 선택 검사 결과를 11.1의 실제 시장 지표로 재사용하지 않는다.

## Q12. 저장 형식·호환성

`Assets`의 런타임 C#에서 persistentDataPath/SaveGame/SaveFile/WriteAllText/WriteAllBytes/BinaryFormatter/SerializeObject/PlayerPrefs/Capture/Restore를 조사했다.

- `PlayerProgression.Snapshot/Capture/Restore`: version 1, worldId, decimal 문자열 turnover/realizedProfit, roomsOpened의 진행도 조각이다. Restore는 같은 WorldId인지 검사한다. `Awake`가 새 WorldId를 만들며 주석도 월드 세이브가 아니라고 명시한다.
- `GameKeyBindings`: Pause/Interact의 PlayerPrefs와 Save. 경제·봇 상태가 아니다.
- JSON의 schemaVersion은 템플릿/초기 설정 파일 버전이며 플레이 세이브 버전이 아니다.
- 전체 계좌/주문/봇/방/난수 상태의 영속 월드 저장 경로 및 기존 FOMO 저장 파일은 발견하지 못했다. `.unity/.prefab/.asset`의 Unity 직렬화 설정과 플레이 세이브를 혼동하면 안 된다.

판단: 기존 진행도 v1 및 키 설정 호환을 유지하면서 사회 상태의 버전 있는 저장 어댑터를 추가할 수 있다. 구버전/필드 누락 fixture로 migration을 검증하되 실제 없는 ‘기존 전체 세이브 로드’를 통과했다고 쓰지 않는다. 이후 실제 월드 저장 연동에서 호환 파괴가 필요해지면 명세 0장의 첫 조건으로 중단한다. 현재 확인된 코드에서는 비호환 변경이 필수라고 판단할 근거가 없다.

## Q13. 실제 매매 기록과 사실 조회

- `Trader.GetOwnedVolume/GetOwnedAverage/GetOwnedCost`: 매도 예약 수량·원가를 포함하는 실제 잔존 포지션. GetHolding만 사용하면 대기 매도 물량을 ‘보유 없음’으로 오판한다.
- `Stock.OnMatchedTrade/Matched`, `OrderBook`의 체결 경로: 시각, buyer/seller, 실제 체결 price/volume, resting order의 원가를 조회할 훅이다.
- `MarketDayController.Sold`와 `BotSocialState.PositionMemory`: 최근 매도 시간·수량·가격·실현손익이 있으나 최근 매수 체결의 공통 이력과 장기 거래 원장은 없다.
- `LeadingRoomOrders.Fill/Attribute`: 명령에 귀속된 실제 체결을 추적하고 `InvestorExperience`에 원가·수익·명령 ID를 남긴다. `ObserveCommands/FlushObservedResults`는 미체결 관찰 성공을 별도 기록하여 실제 투자 수익과 구분한다.
- `NpcLeadingRoom.Trade`는 NPC 운영자 최근 매수를 추적한다. `DeviceMarketJournal.Observe`는 플레이어 UI 거래 기록이며 모든 봇의 원장으로 사용할 수 없다.

판단: OnMatchedTrade로 봇별 최근 체결 사실 기록을 확장한다. 해당 시점의 소유 수량/잔존 원가/실현 기록을 템플릿 슬롯에 연결한다. 신규 ‘샀다/팔았다’ 문구는 실제 체결 기록이 있을 때만 허용하고 관찰 성공을 본인의 수익으로 쓰지 않는다.

## 개념 대응표

- 성격: 기존 `BotSocialState`와 `InvestorPsychology`를 확장. 투자 성향 보존, 사회 성격 6종 추가.
- 감정: 기존 `InvestorPsychology` 유지. 직접/간접 변화 경로·로그를 통합. 신뢰·배신 상한 예외는 D01 사용자 결정 적용.
- 관계: Psychology에 방향성 희소 상대 관계를 추가. Perceptions는 목격 이력으로 구분.
- 기억: 실제 Experiences/Observations 원장은 보존. 8개 고현저성 발화 기억과 usedCount를 같은 사회 상태 안에 추가; 실제 원장을 8개로 잘라서 대체하지 않음.
- 메시지: 기존 `BotChatMessage/InvestorMessageContext` 확장. 현재 스트레스 스냅샷과 UI Sequence 보존.
- 스레드·발화 의도: 아직 없음. 기존 InvestorSociety가 관리하는 방/봇 상태에 추가. 새 사회 시뮬레이터와 기존 것을 동시에 돌리지 않음.
- 방 상태: 기존 `InvestorSociety.Room` 확장. 공용 메시지 저장소는 MarketDayController와 연결. 기존 NPC 방 4 유지.
- 설정: 기존 `BotChatTemplateCatalog` 및 Resources/Chat 역할을 확장. 초기값과 가중치/문구의 세 책임을 구분하고 개발 재로드 구현.
- 사실: 기존 Trader/OrderBook 체결 및 PositionMemory/리딩 경험 확장.
- 표시: 기존 TabletGroupChatUI/InvestorProfileUI/LeadingRoomUI, 기존 Canvas와 행 풀링 재사용.

## 충돌 목록과 처리 제안

상세 이력은 `docs/chat-social-decisions.md`에 둔다. 0단계에서는 변경 제안과 상태만 기록하며 아직 바꾸지 않은 코드를 변경 완료로 표시하지 않는다.

- D01: 100% 초과 신뢰·배신과 과열 맥동 ↔ 0~100 정수. **사용자 결정: 초과 값과 기존 맥동 보존, 6~9장 확률/배율만 min(값,100), 4분면은 원본 값.**
- D02: 수치 FOMO·게이지 ↔ 지속 시간 태그. 명세 10.1대로 6단계에 전 소비 지점 이관.
- D03: 선형 수락/동일 목표 매도 ↔ 4분면. 명세 9.2/9.4대로 6단계 교체.
- D04: 부정 목격으로 배신감 상승 ↔ 직접 경험만 배신. 명세 6.3대로 4단계 변경.
- D05: 문자열 호출/즉시 독립 응답 ↔ 대상 ID·지연·스레드. 2~3단계 기존 메시지/방 확장.
- D06: 여러 생성 경로·템플릿/문장 시간 쿨다운 ↔ 단일 네 단계 파이프라인·최근 키 반복 제한. 2~3단계 통합.
- D07: 기존 NPC 입문방 ↔ 공용+플레이어 방 중심 명세. 기존 NPC 방은 보존하고 플레이어 조치 권한을 자기 방으로 제한.
- D08: 과거 소문·원장 FIFO ↔ 8개 현저성 발화 기억. 실제 금융 원장 보존, 별도 제한 기억만 추가.
- D09: 절대 점유율 중심 관심 ↔ 평소 대비 A/B/C/D. 7단계 이전 기준선부터 새로 측정.
- D10: 진행도 조각만 저장 ↔ 사회 세이브 마이그레이션. 기존 v1과 키 설정을 유지하고 사회 스냅샷 호환 fixture를 추가.
- D11: float/일부 하드코딩·게임 초/현실 초 혼용 ↔ 설정 값과 두 시간 단위. 기존 장 시계를 입력으로 사용하며 배속·임의 24시간 시계를 만들지 않음.
- D12: 신뢰·배신 단순 비교를 하는 기존 거래소 AI ↔ 4분면. 외부 소비자의 지지/반대 집계도 6단계에 함께 수정.

0단계 완료 조건 Q1~Q13·개념 대응·충돌 목록은 모두 갖췄다. D01 답변을 수신했으며 후속 단계 진행이 가능하다. 조사 자체에는 T/R/S가 정의되어 있지 않으며 해당 항목을 실행/통과했다고 간주하지 않았다.
