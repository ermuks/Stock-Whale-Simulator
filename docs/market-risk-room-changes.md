# 위험 알림·단일 리딩방·중앙 가격 표시

2026-11-15. 기존 작업 파일을 보존한 상태에서 추가 수정했다. 씬·프리팹·3D 위치·카메라 배치는 이번 작업에서 바꾸지 않았다.

## 플레이어에게 달라지는 점

- 플레이어가 개설하고 운영하는 방은 **내 리딩방 하나**다. 공용 라운지와 봇 운영 입문방은 유지한다. 2·3번 방 탭을 숨기는 것에 더해 가입·초대·명령 API에서도 해당 방을 차단한다.
- 새 방은 비어 있는 상태로 시작한다. 공용 라운지에서 참여자의 닉네임/메시지를 선택하고 `첫 회원 초대`를 누르면 유효한 첫 초대는 확정 가입한다. 방을 먼저 선택하더라도 앞서 선택한 초대 후보를 기억한다.
- 활동 중인 일반 봇, 공용방 소속, 정원·거래 시간·제재 조건을 통과한 상대가 대상이다. NPC 방에 이미 소속되었거나 강퇴/재가입 제한 중인 상대를 강제로 빼오지는 않는다. **첫 가입 보장과 투자 지시 수락은 별개**다.
- 첫 회원이 생겨도 방이 전체 봇에게 자동 공개되지 않는다. 직접 초대받았거나, 실제 수익을 낸 회원의 공개 칭찬을 직접 목격해야 자발적 가입 후보가 된다. 실제 경험 사건 ID·방·명령·수익을 확인하며, 사적인 방 대화는 밖으로 전파하지 않는다. 이후 가입 여부는 기존 개별 판단을 따른다.
- 위험 알림을 실제 확인한 관심/보유 종목의 봇은 성향에 따라 추가 매수를 줄이거나 잠시 보류하고 채팅으로 반응한다. 탐욕이 강하고 위험 선호가 높은 봇은 무시하기도 한다. 기존 대기 주문 전체를 강제로 취소하지 않는다.
- 중앙 모니터 왼쪽 위에 종목명 → 작은 티커 → 현재가를 표시한다. 가격 TMP 58, 높이 48%로 글자 공간을 확보하고, 전일대비 금액 영역도 늘려 `▲ 454` 같은 기존 표기를 유지한다. 영구적으로 헤더에 겹치던 시장 이벤트 배너는 6초 후 사라진다.

## 수정 파일·핵심 함수

- `Assets/Scripts/NightShift/InvestorSociety.cs`: `IsRoomOpen`, `ChooseRoom`, `Join`, `Invite`, `Issue`, `TickBot`, `CancelRoom`. 사용 가능한 플레이어 방 수는 1이며, 옛 2·3번 식별자는 저장 호환용으로만 남겼다. 이전 방의 실제 멤버십은 공용방으로 정리하고 기존 주문 취소 경로를 사용한다.
- **신규** `Assets/Scripts/NightShift/ChatSocialRecruitment.cs`: `CanDiscoverPlayerRoom`, `ObserveRoomReferral`, `InvitedRoomMask`, `RestoreInvitedRooms`. 개인 초대 이력·실제 목격한 방의 인지와 최초 초대 사용 여부를 관리한다.
- `Assets/Scripts/NightShift/LeadingRoomUI.cs`: `ConfigureRoomTabs`, `ShowPlayerRoom`, `Invite`, `Update`. 기존 탭 세 개를 공용/NPC/내 리딩방으로 배치하고 초대 후보, 첫 초대 문구와 실패 피드백을 유지한다.
- `Assets/Scripts/MarketDay/TabletGroupChatUI.cs`: `SelectRoom`. 옛 2·3번 방 접근을 공용방으로 돌린다. 기존 채팅 풀·스크롤·클릭 구조는 유지한다.
- `Assets/Scripts/NightShift/PlayerProgression.cs`: `OpenLeadingRooms`, `Capture`, `Restore`. 첫 초대 사용 상태를 선택적 스냅샷 필드로 보존한다. `DeviceTerminalUI.OpenRooms`의 안내도 단일 방과 직접 초대로 수정했다.
- `Assets/Scripts/NightShift/ChatSocialData.cs`: `SocialFacts`, `SocialRoomState`, `SocialSnapshot`, `CaptureSocial`, `RestoreSocial`. 위험 사건 버전/관찰자와 개인 초대·방 인지 정보를 추가하고 구형 필드 누락을 허용한다.
- **신규** `Assets/Scripts/NightShift/ChatSocialMarketRisk.cs`: `NotifyMarketRisk`, `TickMarketRisk`, `ObserveMarketRisk`, `MarketRiskBuyScale`, `RefreshMarketRiskFacts`. 실제 위험 등급 변경 → 제한적인 관찰 → 개인 기억/판단 → 게시 직전 사실 재검증을 연결한다.
- `Assets/Scripts/MarketDay/MarketDayController.cs`: `RiskChanged`에서 실제 위험 사건을 전달한다. `ChatSocialDecisions.TickSocialAutonomy`의 기존 24봇 처리 예산 안에서 관찰한다.
- `Assets/Scripts/TradeBot/TradeBot.cs`의 `BuyLimit`, `TradeBot.Risk.cs`의 `CanBuyStock`: 실제 인지한 위험에 따른 다음 매수 규모/보류만 적용한다. 체결·예약금·현금 원장과 거래소 봇의 주문 계산은 유지한다.
- `ChatSocialPipeline.GenerateSocialMessage`/`Slot`, `ChatSocialSpeech.RenderSocialText`, `ChatSocialTopicFocus.HesitateOnNewTopic`: 실제 위험 사실과 말투를 연결한다. 오래된 등급은 게시하지 않으며, 다른 봇의 관찰을 자신의 경험으로 가져오지 않는다. `ChatSocialEmotions.ApplySocialEffectsCore`에서 실제 목격자에게만 방 후기를 전달한다.
- `Assets/Scripts/ChartManager.cs`: `ConfigureHeaderLayout`, `PlaceHeaderText`. 기존 헤더의 위치·높이·TMP 여백을 정리했다. `MarketEventFeed.Awake/Notify/Update`는 일시 배너 표시 시간을 관리한다.
- `Assets/Resources/Chat/chat_social_config.json`, `chat_templates.json`, `ChatSocialConfig.Validate`: 위험 반응 설정 11개와 사실 조건이 있는 문구 216개를 추가했다. **기존 설정 306개, 기존 문구 및 R 합격 범위는 변경하지 않았다.**
- **신규 검사** `Tests/ChatSocial/TestsMarketRiskRecruitment.cs`, 기존 `Host.cs`, `Prepare.py`, `Tests.cs`: 실제 가입/초대 함수 추출 및 27개 회귀 검사를 추가했다. `Compile.ps1`은 Unity 응답 파일에 남은 삭제된 임시 소스 경로를 걸러 현재 파일로 컴파일한다.

신규 C# 전문과 여러 곳을 수정한 기존 파일의 함수별 원본/수정본은 [코드 기록](market-risk-room-code.md)에 있다. 원본은 HEAD가 아니라 이번 작업 시작 시 백업한 작업 파일이다.

## 설정과 처리 한도

- 알림 관찰 창 60초, 목격 기본 확률 0.7에 개별 활동성 반영.
- 발화 기본 확률 0.45에 사교성 반영. 알림당 최대 2명 제안, 방별 제안 간격 10초. 기존 입력 중·쿨다운·방당 게시 제한을 추가로 통과해야 실제 발행된다.
- 새로운 최고 위험 단계당 공포 최대 6에 개인 경계 성향 반영. 동일 버전 중복 및 등급 왕복에 따른 공포 누적을 방지한다.
- 위험에 따른 매수 축소 최대 0.9. 고위험 이상에서 경계도 0.7 이상이면 20초 보류, 개인 관찰 효과는 120초 후 해제한다. 회복 알림을 놓쳐 영구적으로 매수가 막히는 상황을 방지한다.
- 실제 수익 후기의 방 인지는 120게임분 유효하다. 직접 받은 초대 이력은 별도로 유지한다.
- 최근 알림·봇의 종목별 위험 기억은 각각 최대 16종목이다. 전 봇을 매 프레임 순회하지 않는다.

전후 값 증거: `Validation/MarketRiskRoomPatch/settings-proof.json`.

## 검증

1. **자동 검사 320 통과 / 0 실패.** 기존 T·R 및 5시드 리플레이, 위험 미관찰/중복/완화/보류 만료, 첫 초대 보장, 초대 전 무단 가입 방지, 실제 후기/가짜 후기/사적 후기 구분, 하나의 플레이어 방만 허용, 구형 2번 방 상태 복원, 저장 스냅샷 검사를 포함한다. 새 기능 27건을 더한 결과다.
2. **런타임·에디터 C# 컴파일 성공.** 마지막 임시 촬영기 제거와 단일 방 안내 문구 정리 후에도 재컴파일 성공. 기존 Unity API 폐기 예정 경고는 남아 있다. 첫 컴파일의 삭제된 촬영기 경로 오류는 보존하고 컴파일 러너를 수정했다.
3. 실제 저장된 TradeScene, **1,191봇, 배속 없이** 121.16초 실행했다. 위험 알림 4건, 개인 관찰 기록 1,100건 중 다음 매수 축소 898건, 일시 보류 38건, 추가 축소 없는 반응 164건이었다. 이는 관찰 건수이며 고유 봇 수나 실제 주문 건수와 다르다.
4. 위 실행에서 강제 발화·감정 설정 없이 다음 문장이 실제 게시됐다.
   - 09:41 / 봇 277: `대만디스플레이 경고 알림 왔네. 얼마나 더 가나 보긴 볼래.`
   - 09:45 / 봇 312: `노보손해보험 고위험이면 더 신중해야겠네요. 추가 주문은 작게 하겠습니다.`
5. 단일 방 적용 후 최종 실제 시장을 40.64초 더 실행했다. 기존 게임 카메라에서 1920×1080·1600×900 가격과 전일대비 금액의 잘림/넘침이 없음을 렌더링 이미지와 TMP 결과로 확인했다. 2·3번 탭 비활성, `내 리딩방` 탭 하나, NPC 탭 유지, 시장 배너 종료도 확인했다.
6. 모든 무거운 검증은 순차 실행, CPU 한 코어·BelowNormal·게임 최대20FPS. 실제 실행 최대 메모리 2,347MiB, 최종 촬영 2,020MiB. 두 실행 모두 정상 종료, 씬 해시 불변. 배속 기능을 추가하지 않았다.

증거:

- `Validation/MarketRiskRoomPatch/regression-final.txt`, `regression-results/`, `compile-cleanup.txt`
- `Validation/Screenshots/2026-11-15-risk-header/`: 실제 위험 사건·발화 JSONL, 초기 차트 화면
- `Validation/Screenshots/2026-11-15-final-single-room/`: 최종 가격·탭 화면, TMP 기록, 자원/종료 로그
- 이전 `docs/chat-social-results`는 작업 직전 백업으로 복원했다. 이번 결과는 별도 Validation 폴더에 보존했다.

## 제한·다음 확인

- 위험 반응을 추가한 시장의 장기간 거래량·가격·감정 분포는 이번 짧은 실행만으로 통과 판정하지 않았다. 기존 R 리플레이와 실제 장시간 시장은 다른 검사다.
- 첫 회원 초대·입소문 경로는 실제 production 함수를 사용하는 자동 검사로 확인했다. 최종 카메라 촬영에서 직접 마우스로 개설 → 초대 → 장기 수익 → 자연 모집까지 완주한 것은 아니다.
- 프로젝트의 저장은 기존 진행/사회 스냅샷 범위다. 없는 전체 월드 저장을 새로 구현하거나 검증했다고 주장하지 않는다.
- 다음 검증은 다른 Unity 작업 없이 한 번에 하나씩, 초대 후 실제 수익 후기를 통한 자연 모집과 장기간 위험 반응의 시장 영향을 관찰하는 것이다.

![단일 내 리딩방 탭과 기존 NPC 방](../Validation/Screenshots/2026-11-15-final-single-room/02-chat.png)

![1600×900 중앙 가격 표시](../Validation/Screenshots/2026-11-15-final-single-room/01-chart-1600x900.png)
