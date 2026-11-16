# 뉴스·기업 심리·태블릿 UI 적용 기록

2026-11-15. 기존 시장과 두 계좌 태블릿에 적용했다. 기존 씬·프리팹·3D 위치·입력 카메라를 변경하지 않았다. 작업 전 미커밋 변경을 유지했다.

## 플레이 동선과 결과

- 태블릿 → **사건·경영** → 시장 뉴스. 왼쪽 목록, 오른쪽 본문을 각각 스크롤한다. 기업/제목 검색, 전체/보유/선택/미확인 필터, 분야 선택창을 제공한다.
- 기사에는 제목·회사·시간·출처·확인 상태, 배경과 이해관계, 사회적 반응의 맥락, 다음 확인 지점, 관련 기업, 실제 공개 채팅을 표시한다. 같은 사건을 읽었다고 모든 투자자의 생각을 공개하지 않는다.
- 기존 시장 알림은 **알림 기록**, 기업 경영은 **기업 경영**, 리딩방 성장 조건은 **성장·권한**으로 유지했다. 관련 기사는 기업 검색으로 연결하며 이 종목 거래는 기존 주문 화면을 연다.
- 2번 계좌의 기존 해금 조건(1번 계좌 총자산이 1억 원을 초과)을 그대로 사용한다. 해금 후 **기사 만들기**에서 대상·분야·호재/악재 방향·초안을 선택하고 명시적으로 발행한다. 미리보기에는 시장 효과가 없다.
- 가짜 기사는 기존 뉴스/관심도/거래소 기준 호가/실제 주문 경로에 영향을 준다. 주가·기업 실질 가치에 목표 값을 직접 대입하지 않는다. 시장의 반대 주문과 유한한 자금 때문에 상승·하락이나 체결을 보장하지 않는다.
- 발행 후 **현실시간 600초** 재사용 제한. 일시정지 중에도 감소한다. 기존 PlayerProgression 스냅샷에 남은 시간과 UTC 기록을 추가해 해당 저장 경로에서 경과 시간을 반영한다. 봇의 기존 가짜 뉴스 발행과 플레이어 쿨타임은 독립적이다.
- 후속 확인 보도는 기본 65% 확률, 시장 시간 120~164초 후 예약한다. 원문과 같은 storyId를 유지하고 처음 주장과 반대 방향의 공개 근거를 전달한다.

## 기업을 대하는 심리

플레이어에 대한 신뢰/배신과 별도로, 봇마다 회사별 애착·확신·회의·최근 해석·직접 수익 경험을 유지한다. 이는 실증 예측 모델이 아니라 게임을 위한 유한한 휴리스틱이다.

1. 익숙하거나 좋아하는 회사의 약한 악재를 일부 지지자는 잡음으로 받아들이며 방어할 수 있다. 같은 기사를 중립적인 투자자는 부정적으로 해석할 수 있다.
2. 근거가 강한 악재가 반복되면 지지자의 애착도 감소한다. 정정 보도는 회의를 높인다. 호감 때문에 영구히 모든 악재를 무시하는 상태로 고정하지 않는다.
3. 실제 매도 손익은 해당 회사에 대한 경험으로 반영한다. 한 주문의 분할 체결은 60초 창 안에서 비용 가중으로 합산해 보상을 중복 증폭하지 않는다.
4. 기사를 실제 읽은 봇과 발언을 실제 목격한 봇만 추가 영향을 받는다. 소문은 직접 경험보다 약하며 같은 기사 반복 노출을 제한한다.
5. 가족 같은 팬덤, 협력사 기대, 경쟁사 반사이익 등의 가상 세계 연결을 데이터로 정의했다. 회사별 관계는 실제 외부 기업 관계를 추측한 것이 아니다. 연관 효과는 보유/관심 회사에 최대 두 연결까지만 전달한다.

친숙한 기업에 대한 투자 편향은 [Huberman의 연구 소개](https://business.columbia.edu/faculty/research/familiarity-breeds-investment)를, 뉴스·거래량·극단적인 수익률이 투자자의 주의를 끄는 구분은 [Barber/Odean의 연구 초록](https://academic.oup.com/rfs/article-abstract/21/2/785/1607197)을 참고했다. 연구가 이 게임의 수치·팬덤 방어 확률·손익 반응 공식을 검증한 것은 아니다.

## 기사 다양성과 UI 참고

`Assets/Resources/News/news_writing.json`에 경영·신뢰, 실적·자금, 계약·공급망, 기술·제품, 사회·노동, 환경, 정책, 브랜드, 기업 동향의 9개 분야를 정의했다. 분야별 배경/이해관계/관찰점 조합과 긍정/부정 초안, 회사별 사회적 접점·관련 기업을 분리했다. 본문은 현재의 실제 게임 사건 제목을 기반으로 구성하며, 확인되지 않은 매출·계약 금액을 임의의 확정 사실로 추가하지 않는다. 외부 LLM이나 실시간 인터넷 기사를 호출하지 않는다.

[Google News의 공식 개편 소개](https://blog.google/intl/en-ca/products/explore-get-answers/read-all-about-it-a-new-look-for-google-news/)에서 주제 탐색, 출처와 맥락을 구분하는 구조를 참고했다. 게임에는 어두운 배경, 목록/본문 분리, 제목→회사/출처→본문 위계, 얇은 스크롤바로 적용했다.

## 설명·버튼·입력창 구분

- 설명: 일반 굵기의 TMP 텍스트. 주요 숫자의 크기는 유지하고 본문은 Pretendard Regular SDF로 표시한다.
- 주요 실행: imagegen으로 만든 청록색 버튼 PNG. 주문 제출·기사 발행 등 주요 동작에 사용한다.
- 보조 동작: imagegen으로 만든 차콜색 버튼 PNG. 선택·취소·필터·이동 영역에 사용한다.
- 입력: imagegen으로 만든 어두운 안쪽 면과 밝은 테두리의 PNG. 기존 가격/수량/분할/이체/기사 검색 필드를 유지한다. 포커스 테두리와 커서, 호버·눌림·비활성 색 전환을 구분한다.
- 두 태블릿의 기존 Canvas를 재사용한다. 상단 탐색 탭과 종목/기사 선택 행에는 일반 행동 버튼 스킨을 일괄 덮지 않는다. 신규 대기 주문 행의 취소 버튼도 동일 스킨을 받는다.
- 실화면 확인 중 최초 주문 가격에 decimal scale이 그대로 문자열화되어 `5890.000...`처럼 표시되고 정수 입력 검사에 실패하던 문제를 발견했다. `DeviceTerminalUI.Update`의 첫 가격 채움을 정수 형식으로 고쳤다.

생성 원본은 `Assets/Resources/UI/TabletControls/`의 `ActionPrimary-v1.png`, `ActionSecondary-v1.png`, `InputField-v1.png`다. 모두 2172×724 RGBA이고 원본 픽셀을 재가공하지 않았다. 초기 primary의 중앙 투명 결함은 채택하지 않고 imagegen 편집본으로 교체했다. PNG들을 합치거나 기존 Sprite를 덮어쓰지 않았다. [전체 프롬프트와 생성 경로](tablet-control-imagegen-prompts.md)에 내장 도구 사용 내역을 남겼다.

최종 Import는 Sprite/Single, PPU100, Full Rect, Center, alpha transparency, MipMap off, Clamp, Bilinear, None compression, 기본 플랫폼 Max1024다. 표시용 Sprite는 외부 투명 여백을 제외한 사각형을 **한 번 캐시**하며 Border는 원본 기준 좌/하/우/상80px을 실제 Import 축척에 맞춰 사용한다. 버튼용 원본 좌표 rect `(0,104,2172,524)`, 입력용 `(0,84,2172,556)`, Image Sliced/Fill Center/흰색, PPU multiplier4다. 매 프레임 Sprite 로드·컴포넌트 검색·전체 레이아웃 재생성은 없다.

Pretendard 1.3.9 Regular/SemiBold는 [공식 배포처](https://github.com/orioncactus/pretendard/releases/tag/v1.3.9)에서 가져왔다. 원본 OTF와 라이선스는 `Assets/Fonts/News/`, 동적 SDFAA 2048 atlas와 fallback은 `Assets/Resources/UI/News/`에 있다. 기존 TMP 에셋을 덮어쓰지 않았다.

## 주요 파일·함수

- `NewsManager.Publish/Awake`: 기존 발행 이벤트와 기사 본문·독자 시스템 연결.
- `FakeNewsManager.PreviewPlayer/TryPublishPlayer/Update/RestorePlayerCooldown`: 초안, 해금/쿨타임, 후속 확인.
- `NewsArticle.cs`: 분류·본문 조합·필터·현실시간 시계. `CompanyOpinionBook.cs`: 회사별 해석·실제 성과·기억·스냅샷.
- `CompanyNewsAudience.Update`: 0.25초마다 최대24명 확인, 기사당 최초 발언 최대2건. 최대16개 최근 기사, 표시 기사40개.
- `ChatSocialCompanyNews.cs` 및 기존 Pipeline/Emotions/Speech: 자신의 관측 재검증, 실제 목격, 기존 큐를 통한 발언. 조건부 문구216개 추가.
- `MarketDayController.Sold`, `TradeBot.BuyLimit/SellLimit`: 실제 성과를 기억하고 다음 일반 주문 크기에 0.65~1.35 범위의 뉴스 반응 적용. 체결·예약금/보유 한도는 기존 엔진.
- `NewsDeskUI.Install/RefreshList/RenderArticle/Publish`: 기존 목록 풀, 본문/필터/초안/발행. `DeviceMarketJournal.Add`: 기사와 실제 거래 관측 연결.
- `TabletAccountUI.Start`, `DeviceTerminalUI.Update`, `TabletControlStyle`, `PendingOrderListUI.CreateRow`, `CompactGrowthUI.Configure`: 두 태블릿의 실제 입력/버튼에 생성 이미지와 폰트 적용.
- `NewsFontSetup`, `TabletControlSpriteSetup`: 신규 에셋만 생성·Import하는 에디터 메뉴.
- `PlayerProgression.Capture/Restore`, `InvestorPsychology/ChatSocialData`: 기존 저장 계약에 선택 필드 추가. 기존 저장의 누락 필드를 허용한다.
- `Tests/ChatSocial/TestsNewsSociety.cs`, `Host/Prepare/Run/Tests`: 실제 모델 36개 검사 및 기존 회귀에 연결.

[신규 C# 전문과 함수 단위 원본·수정본](news-society-code.md)을 별도로 제공한다.

## 검증과 근거

- Unity 참조 런타임·에디터 컴파일 성공. 실제 Unity Scene 로드와 PlayMode 실행 성공.
- **기존 회귀+신규 자동검사356 통과, 실패0.** 기존 R1~R8 기준 시나리오와 합격 범위, 기존 사회 설정값을 변경하지 않았다. 고정 시드 재현성과 새 기업 기억·수익/손실·기사 중복·쿨타임 경계·본문 미리보기 일치 등을 확인했다.
- 200개 독립 모델에 1시간 분량의 혼합 기사 노출을 주는 결정적 검사에서 지수 범위와 다양한 의견이 유지됐다. 이는 1시간 실제 시장 실행을 대신한 결과가 아니다.
- 최종 **실제 게임 검사39 통과**: 자연 기사, 검색 빈 화면, 보유/선택/미확인/분야 필터, 기존 알림, 스크롤, 실제 카메라 Raycast, 정수 입력, 동작 버튼 활성화, 해금 전 차단, 초안 무효과, 단일 발행, 중복 제출 차단, 일시정지 중 쿨타임, 기존 스냅샷 복원.
- 임시 촬영기 제거 후에도 런타임/에디터 C# 컴파일을 다시 통과했다(`Validation/NewsSociety/compile-after-cleanup.txt`). 기존 FindObjectsOfType 폐기 예정 API·필드 숨김·참조 비교 경고는 남아 있다. 작업 파일의 Git 공백 검사는 Windows CRLF를 줄 끝으로 처리한 설정에서 통과했다.
- 최종 실행: 1191봇, 시장39.12초, 기사9건, 읽기1614건(고유 봇 수가 아님). 발행한 가짜 기사를 읽고 해당 기업의 주문 크기 편향이 생긴 봇60명. 이 수치는 실제 체결60건이나 뉴스만의 순수 가격 효과를 뜻하지 않는다.
- 1600×900/1920×1080 실제 기존 게임 카메라 캡처를 육안 확인했다. 표시한 뉴스·주문·초안의 TMP 잘림 없음. 본문은 의도적으로 독립 스크롤된다. 3D 위치와 씬 SHA256 불변.
- 한 코어, BelowNormal,20fps,한 실행씩 진행. 최종 프로세스77.2초,최대2114MiB,정상종료. 배속 기능 없음. 임시 촬영 스크립트는 Assets에서 제거하고 Validation에 보존했다.
- 중간 실패도 보존했다: 첫 글꼴 생성 시 빈 fallback 목록 초기화 오류, 최초 두꺼운 폰트/행 메타 잘림, 드롭다운 글리프 누락, 정수 가격 채움 오류를 수정하고 재확인했다. 유휴 VBCSCompiler가 실행 중인 검증으로 잘못 잡히던 촬영 가드는 실제 CPU가 쉬는 컴파일 서버만 제외하도록 보완했다.

최종 실화면/검사: `Validation/Screenshots/2026-11-15-news-controls-verified/`.
회귀 출력·설정 보존 증명·촬영 원본: `Validation/NewsSociety/`.

## 남은 범위

- 여러 게임 날짜에 걸친 실제 거래량·팬덤 분포·정정 이후 효과의 장기 균형은 아직 확인하지 않았다. 짧은 실행과 모델 검사를 장기 시장 검증으로 보고하지 않는다.
- 현재 본문은 데이터 기반 조합이다. 작성 중인 세계관의 더 구체적인 노동/소비/공급망 사건과 후속 보도 관계를 추가하면 콘텐츠 차이가 더 커질 수 있다.
- 기존 PlayerProgression은 동일 worldId를 검사하는 저장 조각이다. 완전한 월드 저장/불러오기 기능을 새로 만들지 않았다. 미발행 후속 확인 예약과 전체 뉴스 아카이브를 영구 저장하는 확장도 이번 범위가 아니다.
- 카메라 Raycast·버튼 이벤트·입력/스크롤 값 동작을 검사했다. 직접 마우스로 장시간 사용하는 사용자 테스트와 모든 후반 기업 경영 화면의 시각 검증은 별도다.
