# DonDon 지원 언어

2026-11-15 사용자 확정 지시.

- 영어: en
- 한국어: ko
- 일본어: ja

이 세 언어만 지원한다. 다른 언어의 번역·언어 선택지·OS 언어 자동 확장을 추가하지 않는다.
폰트에 다른 유니코드 글리프가 포함되는 것과 게임이 그 언어를 지원하는 것은 별개다.

전역 문구는 Unity Localization의 DonDon String Table Collection으로 관리한다.
GameLocale.SetLanguage는 ko/en/ja만 받으며, 정의하지 않은 값은 거부한다.
기본 언어는 한국어이며 사용자가 선택한 언어를 환경 설정에 저장한다.
포스트잇도 공통 언어를 따르고, 기존 StickyNotes JSON은 초기 로딩용 폴백으로 유지한다.

한글·영문 손글씨는 Nanum Pen Script, 일본어 손글씨는 Yomogi를 사용한다.
일본어는 기본 TMP SDF의 명시적 폴백으로 연결했다. 두 폰트의 원본 파일을 함께 포함하여 동적 SDF에서도 글리프를 추가할 수 있다.
일반 일본어 UI는 Noto Sans CJK JP 동적 SDF 폴백을 사용한다.
현재 현지화 적용 범위·검증 결과·남은 항목은 localization-changes.md를 참고한다.
