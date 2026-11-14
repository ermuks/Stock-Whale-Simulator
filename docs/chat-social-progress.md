# 채팅 사회화 단계별 진행

## 0단계 — 기존 코드 조사 (완료)

- 변경: 첨부 명세를 `docs/chat-social-spec.md`로 그대로 보관. `chat-social-audit.md`에 Q1~Q13, 기존 개념 대응, 12개 충돌을 기록. `chat-social-decisions.md`에 처리 방향과 사용자 확정 예외 D01을 기록. `.gitignore`에 Unity 생성 캐시와 기존 검증 산출물을 제외.
- 검증: 명세 963줄 전체 읽음. 원본과 복사본 SHA-256 일치 (`49d626615cc0cca733e477d86c7ff7b4ab2755e2eaa2d201d1a29e46e56aa624`). 기존 게임 코드·씬·프리팹·리소스 변경 없음. 적용할 AGENTS.md 없음. Q1~Q13 및 매핑·충돌 목록 확인.
- T/R/S: 조사 단계에는 실행 완료 조건이 없으므로 측정값 없음. 실행하지 않은 테스트를 통과로 기록하지 않음.
- 명세 예외: 사용자 확정 D01. 신뢰·배신은 100 초과와 맥동 유지, 6~9장의 확률/배율 입력만 최대 100, 4분면은 실제 값. 그 외 지수 범위 유지.
- 설정 키 추가/기본값 변경: 없음.
- 2시간 로그: 조사 중 게임을 실행하거나 변경하지 않았으므로 새 로그를 생성하지 않음. 실행기 추가 이후 단계마다 실제 생성 로그를 남김.
- 다음: 1단계 데이터·설정·마이그레이션·로그·실행기. CPU 검증은 한 코어, 낮은 우선순위, 순차 실행.

## 1단계 — 모델·호환 스냅샷·설정·검증 실행기 (완료)

- 파일: `ChatSocialData.cs`에 사회 성격 6종, 방향 관계, 8개 기억, 메시지/스레드/의도/방 상태, 분리된 RNG·시계·감정/사건 로그 및 사회 스냅샷 v2를 추가했다. `InvestorPsychology.cs`는 독립 RNG로 모델을 초기화하고 기존 감정 변경을 원인과 함께 기록한다. 기존 수익·손실 계산과 채팅 선택은 유지했다. `BotChatData.cs`는 선택적 사회 메타데이터를 추가했고, `InvestorSociety.cs`는 기존 방에 상태를 추가하고 로그에 장 시각을 전달한다.
- 파일: `ChatSocialConfig.cs`, `ChatSocialAssets.cs`와 Resources/Chat의 `chat_social_config.json`, `chat_act_weights.json`, `chat_templates.json`을 추가했다. 기존 192개 문구는 새 데이터에 보관했지만 이 단계에서 실행 경로는 전환하지 않았다. `Assets/Editor/ChatSocialTools.cs`는 세 파일을 검증한 뒤 한 번에 개발 재로드한다.
- 파일: `Tests/ChatSocial/{Prepare.py,Host.cs,Tests.cs,Run.ps1,Compile.ps1}`. 기존 `ProcessChat`, `Eligible`, 사건 큐 및 `PlayerProgression.Capture/Restore` 본문을 빌드 때 원문 그대로 추출하고 시각·엔진 경계만 대체하는 실행기다. 새 사회 코드와 카탈로그는 실제 파일을 직접 컴파일한다. 거래 엔진 장기 검증을 대체하는 실행기가 아니다.
- 검사: 19건 통과, 실패 0. 기존 진행도 v1 fixture와 잘못된 worldId 거절, 사회 v1→v2 복원, FOMO 구필드 폐기, 미래 버전 거절, 100 초과 보존/수식 입력 제한, 기억 퇴출, 방향 관계 초기화, 일시정지 시각, 원인 로그를 확인했다. 기존 프로젝트에는 전체 월드 세이브가 없으므로 전체 월드 로드를 통과했다고 주장하지 않는다.
- **T1:** 동일 시드 각 2회, 20봇·게임 분 1,440분 주입. 73129=357개, 73130=364개, 73131=371개, 73132=363개, 73133=363개 메시지가 각각 완전히 일치했다. 출력 해시는 `docs/chat-social-results/stage-1/`에 보관했다. 기존 시장 시계 진행 자체/실거래를 재현한 테스트는 아니다. 원본 MarketDayController 및 PlayerProgression 파일 변경 없음도 Git diff로 확인했다.
- 전체 Runtime/Editor C# 컴파일: 오류 0. 기존 obsolete/reference-comparison 경고 존재. Unity 플레이 화면은 실행하지 않았다. 모든 검사는 순차·한 코어·BelowNormal.
- R 측정: 이 단계에는 R 완료 조건 없음. 범위를 바꾸거나 통과로 간주하지 않았다.
- 13.4: 세 시드의 처음 2게임시간 원문 로그를 저장하고 읽었다. 아직 구형 채팅이므로 퇴장 이유는 없고, 서로 무관한 커피·날씨·생활 발언이 대부분이다. 동일 잡담이 재등장하고 같은 봇은 짧은 어미를 유지하지만 연결 대화가 부족하다. 이를 2~4단계에서 고친다.
- 명세 차이: D01 상한 예외 유지. 금융 원장은 삭제하지 않았고 사회 기억을 별도로 추가했다. 기존 저장이 없는 봇에 대한 새 스냅샷 어댑터를 제공하며 월드 저장 메뉴를 임의로 만들지 않았다.
- 설정: `chat_social_config.values`에 성격 분포·친구·기억·쿨다운·스레드·감정·제재·4분면·FOMO·관심 분배·T/R 범위 출발값을 추가. `chat_act_weights`에 동기/응답/효과 표를 추가했다. 기존 런타임 기본값을 튜닝하지 않았다. R 목표는 원문 그대로다.
- 신규 C# 전문은 해당 파일이 원문이다. 주요 함수 전후: `InvestorPsychology` 생성자는 기존 초기값 생성 후 별도 `InitializeSocial`만 호출한다. `Record/ObserveForecast/Decay/RecallBetrayal/Witness/ObserveRisk/ObserveMissedGain`은 기존 계산 후 `AuditEmotion`을 호출한다. `InvestorSociety.Update`는 기존 시계로 로그 시각만 설정한다. 메시지 생성자는 기존 필드를 유지하며 nullable Social 인자만 추가한다.
