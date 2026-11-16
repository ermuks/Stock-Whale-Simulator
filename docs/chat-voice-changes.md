# 커뮤니티 말투·구어체 손익·중재·참여 알림 적용

2026-11-15 사용자 후속 요청. 기존 0~8단계 구현과 이전 미니 캔들/다중 주문/카메라 변경 위에 적용했다.

## 적용 내용

- **말투 성향:** 기존 격식 수치와 독립적인 `진지함`/`진지하지 않음`. 새 봇의 진지함 비율 45%. 같은 시드와 ID에서 고정되며 저장·복원한다. 기존 v1/v2 저장에 필드가 없으면 기존 성격을 보존하고 이 성향만 보충한다.
- **커뮤니티 표현:** 27개 화행·문맥 그룹, 170개 변형, 중복을 제외한 136개 문장. 수익 자랑/손실/질문/관찰/맞장구/반박/중재/급등락/퇴장 뒤 반응을 기존 실제 사실 조건에 연결했다. 적용 가능한 문장에 한해 진지하지 않음 70%, 진지함 2%. 최근 봇·방 문구와 겹치면 기존 문장으로 돌아가므로 전체 메시지에서의 비율은 더 낮다. 진지한 봇의 기존 급등락 문구·말끝 장식도 절제한다.
- **금액·수익률:** 대화용 슬롯만 바꾼다. 5,284,003원 → `500만 정도`/`500장`/`500 정도`; +50.0% → `50퍼`/`50%`. 1장은 일관되게 만 원이라 5장은 5만 원일 때만 나온다. 음수 부호, 손실 맥락, 미세한 비영 손익을 보존한다. 주가·주문 수량·지정가와 사실/회계 데이터는 그대로다. 기존 문장에 끼우는 금액은 조사 연결이 어색하지 않도록 `500만원쯤` 형태도 사용한다.
- **닉네임 언급:** 실제 등록된 닉네임과 `방장`을 찾아 TMP `<mark>` 배경 `#D4E1E832`(약 20% 알파)으로 표시한다. 긴 이름 우선, 숫자 ID 부분 일치 방지, `님`·조사와 복수 언급 지원. TMP 태그는 문자로 처리한다. 실제 메시지 원문과 로그는 수정하지 않으며 표시 문자열을 캐시하고 측정에도 같은 문자열을 쓴다.
- **중재:** 차분하고 사교적인 목격자는 친구를 편드는 반박보다 중재를 먼저 고려한다. 실제 언쟁의 참여 제한·목격 조건을 그대로 따른다. 성공 시 기존 양방향 분노 감소(-12)·관계 상승을 사용한다. 종료된 언쟁에 제3자를 다시 예약하여 봇이 갇힐 수 있던 경로도 막았다.
- **거절·퇴장:** 중재받은 봇의 스트레스가 80 이상이면 10~50%로 대화 중단을 예약한다. 6~10초 동안 항변과 답변이 오갈 수 있고, 이를 목격한 허세·분노가 높은 상대가 다시 도발할 수 있다. 스트레스성 퇴장은 기존 방 이탈 훅을 거쳐 리딩 명령 취소/예약 해제를 유지한다. 공용방에서는 대화만 30게임분 쉬고 거래는 계속한다. 해당 방 운영자는 자신의 방을 스트레스 때문에 버리지 않는다.
- **퇴장 후 발언:** 실제 상대가 나간 뒤, 그 자리에 있는 상대만 35% 확률로 반응을 시도한다. `OPPONENT_LEFT` 사건과 실제 닉네임을 사용하며 상대를 대상으로 하는 새 호출을 만들지 않는다. 말하지 않는 경우 기존 사회 판단 난수를 바꾸지 않는 별도 사건 난수다.
- **입장·퇴장·강퇴 UI:** `ChatRoomNotice` 유형을 사용한다. 기존 풀 행에 중앙 정렬한 작은 9-slice 배경과 텍스트를 표시하고 아바타·닉네임 헤더·시간·스트레스 게이지·프로필 클릭을 숨긴다. 일반 채팅 행으로 재사용하면 원래 구성으로 돌아간다. 시장 뉴스는 참여 알림으로 오인하지 않는다. NPC 리딩방 플레이어 입장/퇴장도 연결했다. 매우 긴 알림은 한 줄 말줄임 처리한다.

## 변경 파일·핵심 함수

- `ChatSocialData.cs`: `SocialTraits` 생성/마이그레이션, `RestoreSocial`, 문구 디버그·최근 기록.
- 신규 `ChatSocialSpeech.cs`: `RenderSocialText`, `CommunityGroup`, `SpeechChoiceKey` — 사실과 화행을 유지한 문맥 문구/숫자 표시.
- 신규 `ChatSocialNumbers.cs`: `Amount`, `Return` — 한국어 손익 축약.
- 신규 `ChatSocialMentions.cs`: `Format`, `MatchEnd`, `Literal` — 닉네임 검색·표시 태그 이스케이프.
- 신규 `ChatSocialMediation.cs`: `ConsiderMediationExit`, `TickMediationReaction`, `ReactToRejectedMediation`, `OfferDepartureReaction`.
- `ChatSocialPipeline.cs`: `GenerateSocialMessage`, `Present`, `SpeechRequirementsMatch` — 출력/최근 문구·일시적 공용방 이탈 연동.
- `ChatSocialThreads.cs`: `AfterSocialPublish`, `InviteThirdParty` — 중재 이후 항변·목격 반응과 종료된 언쟁 예약 방지.
- `ChatSocialEmotions.cs`: `Mediate`, `BeforeSocialBot`, `DepartSocial` — 실제 분노 감소, 스트레스 퇴장 예약/실행, 퇴장 후 반응.
- `ChatSocialModeration.cs`, `ChatSocialUnity.cs`, `ChatSocialDecisions.cs`: 알림 유형 및 기존 퇴장 콜백 연결. 강퇴 시 일반 퇴장 알림을 중복 발행하지 않는다.
- `InvestorSociety.cs`: `Join`, `Leave`, `PostRoomNotice`; `NpcLeadingRoom.cs`: `JoinPlayer`, `LeavePlayer`.
- `BotChatData.cs`: `ChatRoomNotice`, `BotChatMessage.IsRoomNotice`; `MarketDayController.cs`: `PublishRoomNotice`.
- `TabletGroupChatUI.cs`: `EnsureMentionRoster`, `DisplayMessage`, `BuildNoticeVisual`, `SetNoticeMode`, `LayoutNotice`, `Update`, `RebuildMessageLayout`. 기존 Canvas·풀·스크롤·닉네임 클릭 연결을 재사용.
- `InvestorProfileUI.RefreshDetails`, `ChatSocialDiagnostics.Bot/Message`: 성향·선택 문구·숫자 표현의 근거 확인.
- `ChatSocialAssets.cs`, `ChatSocialConfig.cs`, `ChatSocialLanguage.cs`, 두 Chat JSON: 데이터 로드·검증·인덱스·설정.
- `TestsSpeech.cs`, `TestsMediation.cs`, 기존 `Tests.cs`/`TestsPolish.cs`/`Host.cs`: 신규 검사와 회귀 실행 연결.

씬·프리팹·모니터/태블릿 3D 변환·PNG Import/Border 설정은 이번 후속 작업에서 변경하지 않았다. 작업 전부터 변경되어 있던 TradeScene 및 주문/차트 패치는 그대로 보존했다.

## 검증

- **전체 259개 통과, 실패 0. Runtime/Editor C# 컴파일 오류 0.** 기존 Unity API 폐기 예정 경고는 남아 있다.
- 1만 회 문장 선택: 진지하지 않음 6,986회(69.86%), 진지함 212회(2.12%). 성향 1,000봇 중 진지함 446명. 저장 호환, 기존 난수 불변, 사실 불변, 1,200명 닉네임 목록도 검사했다.
- 중재 성공·실패, 양쪽 분노, 짧은 반응 시간, 실제 이탈·재가입 제한, 공용방 이탈 중 거래 유지, 후속 도발, 중복 알림 방지, 종료된 언쟁 예약 방지 검사.
- 스트레스성 리딩방 퇴장도 R5 자진 퇴장 수에 포함한다. 한 언쟁에서 양쪽 당사자가 연쇄 퇴장하지 않도록 기존 사건별 제한을 공유한다.
- R1~R8 고정 5시드의 기존 목표를 모두 통과. 응답률 56.054/55.769/56.000/56.897/59.218%, 자진 퇴장 3/3/4/4/3. 목표 범위를 바꾸지 않았다.
- 첫 기본 실행에서 시드73129 응답률 51.471%, 퇴장6으로 실패했다. 중재 성공 기본값 .3→.4 실험과 기존 퇴장 유예30→40초 실험은 통과하지 않아 둘 다 원복했다. 최종적으로 **새 퇴장 후 반응 .60→.35**, 침묵 시 기존 판단 난수를 소비하지 않는 사건 난수로 조정했다. 실패 출력도 `Validation/ChatVoice`에 보존했다.
- 실행은 한 코어·BelowNormal·순차 진행. 배속 기능을 추가하지 않았다. Unity Editor나 게임 카메라를 새로 실행하지 않았으므로 실제 화면의 색·잘림·마우스 조작은 이번 검증에서 확인하지 않았다. 기존 TMP 패키지의 mark/알파 지원은 소스에서 확인했다.
- 이전 단계 검증 자료를 이번 출력으로 덮어쓰지 않도록 새 산출물을 별도 보관하고 작업 시작 시점의 자료를 복원했다.

검증 원본: `Validation/ChatVoice/final-tests.txt`, `final-compile.txt`, `settings-diff.json`, `rates-reaction-035.txt`, `mediation-tests.txt`, `conversation-raw.jsonl`.

## 실제 생성 원문

아래는 감정·발화 종류와 중재 성공/실패 조건을 고정한 테스트에서 기존 게임 채팅 파이프라인이 생성한 문장이다. 문구를 수작업으로 수정하지 않았다. 일반 시장 장기 자율 로그와 구분한다. 퇴장 알림 줄은 실제 퇴장 콜백에서 기록했다.

### 중재 성공

```text
잔고지킴001: @우산너구리002 진짜. 우기기만 하면 대화가 되냐?
우산너구리002: @잔고지킴001 말만 들으면 이미 결말 보고 온 사람임
새벽개미003: @잔고지킴001 두 분 다 감정이 올라온 것 같습니다. 잠시 쉬시죠.
[검증 상태] 양쪽 분노 하락 · 언쟁 종료
```

### 중재 거절·퇴장

```text
잔고지킴001: @우산너구리002 진짜. 우기기만 하면 대화가 되냐?
우산너구리002: @잔고지킴001 말만 들으면 이미 결말 보고 온 사람임
새벽개미003: @잔고지킴001 두 분 다 감정이 올라온 것 같습니다. 잠시 쉬시죠.
잔고지킴001: @새벽개미003 지금 웃으면서 넘길 기분은 아님
우산너구리002: @잔고지킴001 그 표정 여기까지 보이는 것 같네 ㅋㅋ
── 잔고지킴001 님이 퇴장했습니다. ──
우산너구리002: 어 잔고지킴001 나갔네 ㅋㅋ 잘 가라
```

신규 C# 전문과 변경 덩어리가 많은 파일의 함수별 원본/수정본은 [코드 비교](chat-voice-code.md)에 수록했다.
