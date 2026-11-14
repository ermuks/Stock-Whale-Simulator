# 8단계 신규 C# 전문

아래 링크는 발췌가 아닌 실제 적용한 파일 전체다.

- [ChatSocialLanguage.cs](../../../Assets/Scripts/NightShift/ChatSocialLanguage.cs): 말끝·문맥별 표현, 화행별 템플릿 부족 집계.
- [ChatSocialMaintenance.cs](../../../Assets/Scripts/NightShift/ChatSocialMaintenance.cs): 종료 스레드·노출·예약 작업 캐시 정리.
- [ChatSocialLog.cs](../../../Assets/Scripts/NightShift/ChatSocialLog.cs): 오류 격리된 디스크 감사 로그.
- [ChatSocialDiagnostics.cs](../../../Assets/Scripts/NightShift/ChatSocialDiagnostics.cs): 발화 원인·봇 상태 보고서.
- [ChatSocialDebugUnity.cs](../../../Assets/Scripts/NightShift/ChatSocialDebugUnity.cs): 개발 전용 F9 검사기·강제 명령·고정 시드 재시작.
- [ChatSocialPromises.cs](../../../Assets/Scripts/NightShift/ChatSocialPromises.cs): 명시적 매매 예고와 다음 판단의 일회 확률 적용.

기존 파일의 큰 수정은 [함수 단위 비교](function-comparisons.md)를 확인한다. 전체 변경 파일 목록과 측정값은 [누적 진행 기록](../../chat-social-progress.md)에 있다.
