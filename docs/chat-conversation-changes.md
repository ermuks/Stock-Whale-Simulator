# 채팅 말투·주제 흐름·종목 링크·입력 중 표시

2026-11-15. 이번 작업 직전 상태를 기준으로 한 변경 보고서입니다.

## 적용 내용

- 답장이 끊겨도 괜찮다는 요청을 반영해 응답 확률·강제 대화·분노·중재 수치는 조정하지 않았습니다.
- 방마다 최근 대화에서 많이 언급된 세 주제를 우선합니다. 네 번째 주제도 확률이 남아 있으며, 평범한 새 주제는 한 번 망설였다가 발화할 수 있습니다. 급등·급락을 직접 목격한 사건, 실제 리딩 명령·성과, 진행 중 답변은 이 망설임을 우회합니다. 영구적인 종목 고정이나 세 주제 하드 제한은 없습니다.
- 기존 `MarketConversationFocus`의 실제 체결량·최근 가격 변동·체류 감점 데이터를 재사용해 새로 뜨거워진 종목에 더 높은 대화 가중치를 줍니다. 매매 판단·가격·계좌·봇 감정을 인위적으로 바꾸지 않습니다.
- 종목명과 티커에는 청록색 글자 및 옅은 배경을 붙입니다. 클릭은 기존 `StockManager.SelectStock`에 연결되어 종목 목록과 중앙 차트가 함께 바뀝니다. 원문에 없는 종목을 임의로 삽입하지 않습니다. 닉네임의 기존 강조·프로필 기능도 유지합니다.
- 실제 발화 준비를 마친 봇을 대기열에 넣고 게시 전에 `닉네임님이 채팅 입력 중...` 또는 `4명이 입력 중...`를 표시합니다. 일반적인 생각/발화 의도 전체를 입력 인원으로 세지 않습니다. 방 이동·음소거·만료·상폐가 생기면 대기 발화를 취소하며, 게시 시점에 보유·가격 사실을 다시 확인합니다.
- 담백함·차분한 관찰·수다·능청·조심스러움·큰 반응의 여섯 말투를 봇의 기존 저장 ID/성격에 안정적으로 배정했습니다. 240개 문구와 FOMO 커뮤니티 문구 24개를 추가했습니다. 기존 진지하지 않음 70%, 진지함 2%의 커뮤니티 말투 확률은 그대로입니다. 별도 문장 난수로 말투를 고르므로 표현만 바꾸는 실험에서 거래/감정 판단 난수를 소비하지 않습니다.
- 실제 로그에서 발견한 `미래바이오으로`, `그린푸드을` 같은 종목명 조사 오류를 보정했습니다. 주가·주문 수량·수익 사실은 변경하지 않습니다.

## 파일과 함수

- `Assets/Scripts/NightShift/ChatSocialTopicFocus.cs` (신규): `Observe`, `Weight`, `IntroducingAnother`, `ChooseRoomTopic`, `HesitateOnNewTopic`, `ObserveConversationTopic`. 방별 감쇠 주제 점수와 기존 시장 관심 데이터의 결합입니다. 작업 캐시는 방당 32개 주제이며 이는 세 주제 발화 제한과 다른 메모리 한도입니다.
- `Assets/Scripts/NightShift/ChatSocialTyping.cs` (신규): `BeginTyping`, `FlushTyping`, `ReadTyping`, `SocialTypingStatus.Text`. 기존 사회 갱신 주기를 사용하며 실제 게시 제한도 유지합니다.
- `Assets/Scripts/NightShift/ChatSocialVoices.cs` (신규): `VoiceKey`, `VoiceLabel`, `TryVoiceText`, `VoiceStockParticles`. 문구 선택·한국어 조사 보정입니다.
- `Assets/Scripts/NightShift/ChatSocialPipeline.cs`: `SocialAdvance`, `GenerateSocialMessage`. 주제 망설임/입력 대기/실제 게시 후 주제 기록을 연결합니다. 목격·자기 경험·답변의 사실 조건은 유지합니다.
- `Assets/Scripts/NightShift/ChatSocialUnity.cs`: `PrepareSocialRuntime`. 기존 `MarketDayController.ConversationFocus`를 대화 흐름에 주입합니다.
- `Assets/Scripts/NightShift/ChatSocialSpeech.cs`: `RenderSocialText`, `CommunityGroup`. 기존 사실 검증 이후 문장을 변형하며 실제 FOMO 상태에 커뮤니티 문구를 연결합니다.
- `Assets/Scripts/NightShift/ChatSocialAssets.cs`: `Install`, `Voice`. 카탈로그 검증과 키 인덱스입니다. 잘못된 재로드는 기존 설정을 보존합니다.
- `Assets/Scripts/NightShift/ChatSocialConfig.cs`, `ChatSocialData.cs`, `ChatSocialDiagnostics.cs`: 조정값 검증, 일회 망설임 상태, 디버그 말투 표시를 추가합니다.
- `Assets/Resources/Chat/chat_templates.json`, `chat_social_config.json`: 새 문구/설정만 추가했습니다. 기존 설정값·기존 템플릿·문맥 문구·커뮤니티 문구가 그대로인지 파일 비교로 확인했습니다.
- `Assets/Scripts/NightShift/ChatSocialMentions.cs`: 생성자, `Format`, `MatchEnd`, `TryStockSymbol`. 등록 명단의 가장 긴 이름을 매칭하고 종목 링크를 안전하게 생성합니다. 사용자가 쓴 TMP 태그는 그대로 실행하지 않습니다.
- `Assets/Scripts/MarketDay/ChatStockLinkUI.cs` (신규): `Bind`, `OnPointerDown`, `OnPointerClick`. 기존 World Space 이벤트 카메라를 사용합니다. 본문이 press/click을 함께 받아 부모 행 버튼이 클릭을 가로채지 않게 합니다. 드래그 대상은 기존 ScrollRect입니다.
- `Assets/Scripts/MarketDay/TabletGroupChatUI.cs`: `Start`, `EnsureMentionRoster`, `SelectMentionedStock`, `BuildTypingIndicator`, `RefreshTypingIndicator`, `Update`, `OnEnable`, `OnDisable`. 기존 가상 행 풀에 연결합니다. 입력 상태는 고정 푸터에서 0.15초 주기로 갱신하고, 상태 때문에 전체 목록 높이를 다시 만들지 않습니다. 기존 TMP SDF 폰트를 재사용합니다. 종목 명단은 상장·상폐 때만 무효화합니다.
- `Tests/ChatSocial/TestsConversationFlow.cs` (신규), `Tests.cs`: 문구·주제·입력 대기·링크의 기능 검사입니다.
- `Assets/Editor/ConversationFlowSmoke.cs` (신규), `AutonomousChatObservation.cs`, `Tests/ChatSocial/ObserveSceneChat.ps1`: 기존 실제 시장 관측기에 선택적인 짧은 UI 확인 모드를 추가했습니다. 봇 감정·발화 조건·주문을 주입하지 않습니다. 원래 장시간 관측 기본 동작은 유지합니다.

이번 작업에서 씬·프리팹·3D 기기 위치·거래 엔진·저장 스키마를 수정하지 않았습니다. 신규 C# 파일 전문과 여러 곳을 수정한 기존 파일의 함수별 원본/수정본은 [코드 비교](chat-conversation-code.md)에 있습니다.

## 추가 설정과 전후 값

기존 모든 설정값은 변경하지 않았습니다. 아래는 이번에 추가된 키입니다.

- `voiceStyleChance`: 기존 키 없음 → 1.0. 커뮤니티 문구 선택 후 남은 적격 문장을 고유 말투로 표현합니다.
- `topicPreferredCount`: 없음 → 3. 선호 주제 수이며 금지 상한이 아닙니다.
- `topicMemorySeconds`: 없음 → 120. 점수의 지수 감쇠 시간입니다.
- `topicContinueWeight`: 없음 → 4, `topicNewWeight`: 없음 → 0.3. 새 주제도 0이 되지 않습니다.
- `topicHotMultiplier`: 없음 → 2. 기존 시장 관심 확률에 추가로 적용하는 대화용 가중치입니다.
- `topicHesitationChance`: 없음 → 0.8. 사교성·급관심 정도로 조절되는 기본 확률입니다.
- `topicHesitationMinSec/MaxSec`: 없음 → 2/7초. 이미 기다린 같은 의도에는 반복 적용하지 않습니다.
- `typingMinSec/MaxSec`: 없음 → 0.9/2.6초. 기존 방별 발화 제한에 걸리면 실제 전송 대기는 더 길어질 수 있습니다.
- `typingPerRoom`: 없음 → 4. 방마다 동시에 실제 입력을 준비하는 인원 한도입니다.

## 검증

1. `Tests/ChatSocial/Run.ps1 -Stage 8`: **293 통과, 0 실패**. 기존 T/R/S 회귀 및 새 34항목을 포함합니다. 최종 출력: `Validation/ChatVoiceVariety/regression-final.txt`.
2. `Tests/ChatSocial/Compile.ps1`: 실제 Unity 참조를 이용한 런타임/에디터 C# 컴파일 성공. 기존 deprecated API 경고는 남아 있으며 새 컴파일 오류는 없습니다. 출력: `Validation/ChatVoiceVariety/compile-final.txt`.
3. 균등 활동 8종목, 최근 주제 3종목의 통제된 선택 검사: 10,000회 중 기존 세 주제 **89.93%**, 그 외 **10.07%**. 새 종목에 실제 관측용 거래량 급증과 +50% 가격 변화를 기록한 별도 검사에서는 그 종목 선택 **93.71%**. 이는 가중치 기능 검사이며 실제 시장 거래/채팅 비율로 주장하지 않습니다.
4. 실제 `TradeScene`, 기존 시드 12345·1,191봇·정상 속도·플레이어 주문 없음으로 순차 실행했습니다. 첫 180초 관측(run-03)은 실제 채팅 68건/체결 26,606건/실행 오류 0이었지만, 검증기가 카메라를 꺼 둬 클릭 검사 미완료로 **실패 결과를 보존**했습니다.
5. 카메라 정보를 유지한 60초 재검증(run-04): 실제 채팅 **22건**, 체결 **7,161건**, 실행 오류/시장 무결성 오류 **0**. 실제 풀 행의 TMP 링크 좌표 → 기존 Unity press/click 이벤트 → `그린푸드` 선택 이벤트 **1회** → 중앙 `ChartManager.textName` 갱신을 확인했습니다. 드래그 시 종목 선택 없음, ScrollRect로 드래그 전달, 1명/다수 입력 중 문구 및 실제 UI 일치도 통과했습니다. 선택은 검사 후 원래 종목으로 복원했습니다.
6. 한 번에 하나씩, 한 코어·BelowNormal·20FPS·메모리 상한으로 실행했습니다. run-04 프로세스 최대 메모리는 **1,845MiB**, 전체 실행은 시작/종료 포함 **93.7초**, 게임 내 관측은 **60.05초**였습니다. `timeScale=1`, 고정 프레임 시간 가속 없음. 씬 및 채팅 JSON의 실행 전후 해시가 일치합니다.
7. run-04 전체 60초 동안 공용방은 총 5종목, NPC방도 총 5종목을 언급했습니다. 상위 세 종목 비율은 각각 10/12, 8/10입니다. 시간이 지나거나 새 사건이 생기면 네 번째 주제가 등장할 수 있다는 요청을 유지한 결과이며, 항상 세 개 이하임을 주장하지 않습니다. 주제 집계는 메시지의 실제 관련 종목 메타데이터 기준입니다.

실제 원자료: `Validation/AutonomousChat/run-03`, `run-04`. 요약: `Validation/ChatVoiceVariety/evidence.json`. 이전 단계의 결과 파일은 덮어두지 않고 작업 직전 복사본을 복원했으며 이번 출력은 `Validation/ChatVoiceVariety/regression-results`에 따로 보존했습니다.

## 실제 생성 원문

아래는 별도 감정/발화 조건을 지정하지 않은 run-04의 서로 다른 메시지입니다. 이어지는 대화를 만들기 위해 붙인 예시가 아닙니다.

> [09:13 / 공용방] 느린호가735: 노보손해보험 때문에 기다리겠다는 다짐이 계속 흔들리는 중.
>
> [09:14 / 공용방] 새벽개미733: 덕상가주에이아이 +27.4%! 이 숫자 보니까 실감 나네.

전체 텍스트는 `Validation/ChatVoiceVariety/run-04-actual-chat.txt`에 있습니다. 원본 로그의 조사 오류를 사후에 고쳐 증거처럼 제시하지 않았습니다. 조사의 후속 수정은 별도 자동 검사 및 최종 컴파일로 확인했습니다.

## 남은 확인 범위

- 이번 실제 씬 검사는 headless 상태의 글자 기하/이벤트/연결 검사입니다. 화면을 렌더링한 스크린샷 및 실제 마우스로 장시간 읽는 가독성 검사는 수행하지 않았습니다. 신규 입력 표시와 강조의 실제 모니터 시인성은 플레이 화면 확인이 남아 있습니다.
- 기존 R 기준 회귀 리플레이와 새 대화 흐름의 기능 검사를 구별합니다. 새 입력 지연을 켠 실전 3일 시장의 R 지표/심리 분포 장기 재측정은 이번 짧은 검사로 대체하지 않습니다. 합격 범위를 넓히거나 답변을 강제로 늘리지 않았습니다.
- 구체적인 시장 사건에 대한 자기 경험, 기존 리딩방 신호와 진행 중 답변이 집중 주제를 잠시 넘어설 수 있습니다. 이는 새 요청의 소프트 선호이며 세 주제 제한 실패로 감추지 않습니다.
