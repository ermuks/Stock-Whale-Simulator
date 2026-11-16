# 한국어·미국 영어·일본어 현지화

2026-11-15. Unity 6000.5.5f1, 공식 Localization 1.5.14.

## 적용 내용

- ko/en/ja 각 3,548개 String Table 항목. 실제 사용 중인 채팅 문구 2,110개, 기존 기업 이벤트 제목 240개, 뉴스 작성 자료 267개를 포함한다. 항목 수에는 UI와 포스트잇 문구도 포함된다.
- UI, 주문, 종목 목록, 차트, 채팅, 뉴스, 전화, 정산 및 포스트잇을 기존 Canvas에 연결했다. 이 작업의 씬 변경은 `Text :: Operation Monitor Heading`의 글자 크기 44→36 두 직렬화 필드뿐이다. 3D 위치·회전·Canvas Event Camera는 수정하지 않았다.
- 한국어 채팅은 원문을 유지한다. 미국 영어와 일본어는 직역 대신 커뮤니티의 어휘와 문장 리듬을 사용한다. formal/casual/rough 및 기존 진지함·장난기·분노 조건을 유지하며, 번역 자체가 발화 확률이나 봇 심리를 바꾸지 않는다.
- 닉네임과 플레이어가 입력한 회사명은 식별자로 유지한다. 고유명사를 강제로 개명하지 않는다. 게임에 정의된 종목명은 번역하되 원래 symbol로 선택한다.
- 대화 속 손익은 당시 수치를 이용해 근사 표현한다. 예를 들어 5,284,003원은 영어 `~₩5m`, 일본어 `約500万ウォン`으로 표현할 수 있다. 원화를 달러·엔으로 바꾸지 않는다. 주문 가격·수량과 정확한 회계 표시는 근사화하지 않는다.

## 데이터와 편집

- `Assets/Localization/Tables/DonDon`은 실제 Unity String Table Collection이다. `DonDon_ko`, `DonDon_en`, `DonDon_ja`를 Unity Localization Tables 창에서 편집할 수 있다.
- `Assets/Localization/Import/DonDon.csv`는 검토한 번역 원본이다. `DonDon → Localization → Import reviewed translations`를 실행할 때만 테이블로 가져온다. 런타임이 CSV를 자동으로 덮어쓰거나 읽지 않는다. 테이블을 직접 수정했다면 재가져오기 전에 CSV에도 반영해야 한다.
- `SourceIndex.json`은 기존 한국어 표시 문자열과 안정된 테이블 키의 연결이다. 새 원문은 CSV와 인덱스에 함께 등록한다. 기존 키를 번역문에 따라 바꾸지 않는다.
- 기존 포맷 문자열과 채팅 슬롯을 호환하기 위해 Smart String 자동 해석은 끄고, 표시 계층에서 슬롯을 치환한다. 슬롯 이름과 개수는 세 언어에서 동일하게 검증한다.
- 게임 판단에 사용하는 한국어 사건명·키워드·뉴스 데이터는 원본으로 유지한다. 번역은 화면에 표시할 때 적용한다. 뉴스 필터와 검색은 원본 식별자를 유지하면서 번역 제목·회사명도 검색한다.

## 주요 코드와 책임

- `GameLocale.Boot/Load/SetLanguage/Text/Template`: 공식 Localization 초기화, 세 언어 선택, 비동기 테이블 로딩, 기존 원문과 포맷 호환. 설정은 `PlayerPrefs["DonDon.Language"]`에 저장한다. 번역 결과 캐시는 2,048개로 제한한다.
- `GameLocalizedText.Attach/Set/Matches/Refresh`: TMP와 기존 Text에 원문을 보관하고 값 또는 언어가 바뀔 때만 갱신한다. 입력 필드의 실제 입력값은 번역 대상에서 제외한다. 씬 탐색은 로딩 시에만 한다.
- `GameLocaleFonts.Load`: 공통 일본어 SDF 폴백 연결. 기존 Pretendard·숫자 전용 폰트·손글씨는 유지한다.
- `InvestorSociety.RenderSocialText`: 기존 RNG로 선택한 문장·대상·접미사·표시 슬롯과 원시 수치를 메시지에 기록한다. 선택을 다시 하지 않는다.
- `LocalizedChat.Text`: 저장된 발화 스냅샷으로 현재 언어를 표시한다. 현재 계좌를 다시 조회하거나 주문을 내지 않는다.
- `TabletGroupChatUI.DisplayMessage/RefreshMentionRoster`: 번역된 본문을 기준으로 높이를 측정하고, 종목명·티커·닉네임 강조와 링크를 연결한다. 언어 변경 시 표시 캐시만 갱신한다.
- `ChatSocialMentions.Format/BoundaryAfter`: 일본어 조사와 띄어쓰기 없는 문장을 처리한다. 원래 symbol을 담은 링크는 유지한다.
- `NewsDeskUI.RefreshList/MatchesQuery/SetArticle/SelectRow`: 번역문 높이와 검색, 선택·스크롤 유지. 새 기사 선택은 제목부터 읽도록 스크롤 위치를 배치 후 적용한다.
- `CompanyNewsAudience.Spoken/Responses`: 기사에 연결된 공개 발언도 원래 SocialMessage를 보관해 언어 변경 시 같은 발언을 다시 표시한다.
- `InvestorJourneyNotes.EnsureCopy`: 공통 언어와 String Table을 연결한다. 떨어진 완료 포스트잇도 해당 언어의 완료 표시로 갱신한다.
- `DailyIncomeController.NameCompany/Restore`: 입력·복원한 회사명은 번역에서 보호한다. 수입·세금·정산 계산은 그대로다.
- 나머지 UI 함수는 직접 `.text` 대입을 `GameLocalizedText.Set`, 원문 값 비교를 `Matches`, 높이 측정을 번역된 문자열 기준으로 연결했다. 전체 함수별 원본·수정본과 신규 파일 전문은 `localization-code.md`에 있다.
- `Tests/ChatSocial/Prepare.py`의 테스트 호스트에 기존 `journeyMilestones` 저장 필드가 빠져 있어 실제 클래스와 맞췄다. 회귀 테스트 조건은 완화하지 않았다.

## 사용

현재 게임 설정 화면 자체는 이번 작업에 포함하지 않았다. 플레이 중 Unity 메뉴 `DonDon → Localization → Preview Korean / English / Japanese`로 확인할 수 있다. 추후 설정의 언어 선택은 아래 API에 연결하면 된다.

```csharp
GameLocale.SetLanguage("en"); // ko, en, ja만 허용
```

언어는 재실행 시 복원된다. 플레이어 월드 저장과 별도인 환경 설정이며, 월드 저장·나가기 메뉴가 추가된 것은 아니다. 에디터가 Play Mode가 아닐 때의 Preview 메뉴는 Localization 미리보기 Locale만 선택한다.

## 폰트

일반 UI의 일본어는 Noto Sans CJK JP Regular를 동적 TMP SDF(64pt, padding 8, SDFAA, 2048², multi-atlas)로 포함했다. 좁은 기존 행에서 일본어가 통째로 생략되던 문제는 폴백의 줄 높이·상하 기준을 Pretendard의 비율로 맞춰 해결했다. 글리프 자체를 눌러 찌그러뜨리지 않는다. 번역에 사용하는 1,095개 문자를 미리 생성하고 `Clear Dynamic Data On Build`를 꺼서 빌드 이후에도 유지되는 것을 확인했다.

원본 OTF와 OFL을 `Assets/Fonts/Localization`에 포함했고, 배포용 라이선스도 `StreamingAssets/ThirdPartyLicenses`에 복사한다. 손글씨는 기존 Nanum Pen Script/Yomogi SDF 연결을 유지한다.

- 폰트 원본: https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese
- 패키지 근거: https://docs.unity.com/en-us/engine/6000.5/manual/packages-list/packages-all/pack-safe/com-unity-localization
- 공식 시작 안내: https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/QuickStartGuideWithVariants.html

## 검증과 한계

검증은 다른 Unity가 없는 상태에서 CPU 1개, BelowNormal, 게임 화면 20fps, 메모리 4GB/시간 제한으로 하나씩 실행한다. 게임 배속은 추가하지 않았다.

- `Tests/Localization/CheckCatalog.py`: 세 언어의 누락·중복 키·슬롯·한국어 잔존, 채팅 2,110개와 기업 뉴스 240개의 포함 여부, 원화 단위 검사.
- `DonDonLocalizationValidation.Run`: 실제 TradeScene Play Mode에서 47개 검사 통과. 세 언어, 1600×900/1920×1080의 종목·차트·채팅·태블릿 주문/자산/뉴스/정산·전화 촬영. 숫자 보존, 입력값, 기사 선택과 스크롤, Canvas 로컬 좌표와 Event Camera, 일본어 종목 링크, 주문 레이블 잘림 검사. 최종 실행 63.8초, Unity 최대 메모리 2,335MiB.
- `Tests/ChatSocial/Run.ps1 -Stage 8 -Scenario voice`: 기존 말투 및 보안·수치·사회 상태 회귀 검사 44개 통과.
- `DonDonLocalizationValidation.ValidateHeading`: 캡처에서 발견한 작은 작업 모니터 제목의 잘림을 36px로 수정한 뒤 세 언어 모두 별도 검사 통과.
- `DonDonLocalizationValidation.BuildContent`: Addressables 플레이어 콘텐츠 빌드와 일본어 글리프 1,095개 유지 검사 통과. 최종 실행 37.0초, Unity 최대 메모리 1,998MiB. 런타임·에디터 스크립트 컴파일도 통과했다.
- 실제 시장이 생성한 발언은 캡처 실행 폴더의 `actual-chat-en.txt`, `actual-chat-ja.txt`에 기록한다. 별도 감정을 강제로 설정한 시연 로그가 아니다. `snapshot-test.txt`는 수치 검증용 고정 입력이며 실제 시장 발언과 구분한다.
- 이번 검증은 현지화와 UI의 회귀를 확인하는 짧은 실행이다. 장시간 경제 밸런스, 모든 후반 경영 상태, 전체 게임 EXE 배포 검증을 완료했다는 의미는 아니다.
- 언어 전환용 설정 화면과 실제 원어민 플레이테스트는 남아 있다. 자동 생성된 봇 닉네임과 사용자 입력 이름은 번역하지 않는 현재 정책을 적용한다.

최종 실행 로그와 캡처는 `Validation/Localization/Verified`, 배포용 번역 데이터 검증은 `Validation/Localization/ContentFinal`, 제목 잘림 검사는 `Validation/Localization/Heading`에 둔다. 초기 실패 로그도 삭제하지 않아 수정 원인을 추적할 수 있다.
