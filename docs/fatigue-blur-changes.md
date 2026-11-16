# 피로에 따른 미세 블러 — 2026-11-15

피로 상태를 실제 플레이어 카메라의 URP RenderGraph 효과에 연결했다. 기존 눈꺼풀·가장자리 효과와 함께 작동하며, 시장 속도·피로 증가량·수면 규칙은 변경하지 않았다.

## 동작

- 피로 25% 이하: 가우시안 패스 없음. 25→100%에서 부드럽게 증가, 최대 반경은 **최종 화면 기준 가로·세로 각각 2픽셀**. 사용자 후속 요청으로 1→2픽셀 조정.
- 피로 50% 초과: 카메라 회전에 비례하는 모션 블러. 셔터 길이 0.025초, 양 끝 사이 폭 최대 4픽셀. 시선 정지 다음 프레임에는 0.
- ESC·설정·정산 등 메뉴가 열린 동안 피로 블러 제외. 확대 전환, 큰 회전·이동, 해상도/FOV 변경, 긴 프레임 지연에서 회전 이력 초기화.
- 정지 시 9회 샘플의 GPU 패스 하나, 이동 시 5회 샘플 패스 하나 추가. ReadPixels·CPU 이미지 복사·프레임별 객체 검색은 실제 게임 효과에 없음. 영상 검증 도구만 화면을 읽어 저장한다.
- 월드 스페이스 모니터·태블릿을 포함한 게임 카메라 화면에 적용. 화면 위에 따로 그리는 HUD와 설정 UI는 그대로 선명하게 유지.

## 수정 위치

- `Assets/Scripts/Settings/FatigueBlurFeature.cs`: `AddRenderPasses`, `GaussianRadius`, `MotionPixels`, `MotionHistory.Sample`, `OpticsPass.RecordRenderGraph`. 신규 렌더러 기능과 카메라 회전 이력.
- `Assets/Resources/UI/SystemMenu/FatigueBlur.shader`: Gaussian / Camera Motion 두 패스.
- `Assets/Scripts/Settings/PlayerRestController.cs`: `Awake`에서 실제 카메라를 한 번 캐시하고 `ViewCamera`로 제공.
- `Assets/Settings/PC_Renderer.asset`, `Mobile_Renderer.asset`: 기존 렌더러에 Fatigue Optics 추가. 현재 최대값 2 / 4. Unity가 저장하며 URP 에셋 스키마를 2→3으로 갱신했으며 기존 렌더링/SSAO 설정 값은 유지.
- `Assets/Editor/FatigueBlurValidation.cs`: 설치, 수식·카메라 이력·실제 이미지 비교 검증.
- `Assets/Editor/FatigueVideoCapture.cs`: `Run`은 설치, `Prepare`는 검증, `RenderFrame`은 실제 피로와 느린 카메라 회전을 포함한 영상 촬영.
- 씬·프리팹·3D 배치는 수정하지 않았다. TradeScene SHA256은 `622351BBCC1A603CE6C96A757148EB11E82C0262BDEAA4CE5ABF2A0D88217C40`로 작업 전후 동일하다.

[신규 소스 전문 및 기존 함수 원본/수정본](fatigue-blur-source.md)

## 2픽셀 조정 및 검증

- 사용자 요청으로 기본값·Inspector 상한·계산 상한·PC/Mobile 저장 설정을 1→2픽셀로 변경. 피로도 증가 곡선과 모션 블러는 그대로 유지.
- 기존 3×3 샘플 간격만 늘리면 얇은 글자와 선이 겹치는 현상이 실제 캡처에서 발생했다. `[1,4,6,4,1]` 가우시안 필터를 bilinear 샘플 쌍으로 계산하도록 수정하여 5×5 범위를 기존과 동일한 9회 샘플로 처리한다.
- `GaussianRadius`, `OpticsPass.RecordRenderGraph` 및 셰이더, 렌더러 에셋, 검증 기대값, 영상 자막을 갱신했다. 함수별 1픽셀 원본/2픽셀 수정본은 소스 문서 끝에 추가했다.
- 낮음/높음 품질 각각 720p·1080p 실제 렌더링 통과. 피로 100%에서 반경 2픽셀, 정상 상태 연산 생략, ESC 제외, 모션 정지·이동 처리 통과. 얇은 선의 중복 모양이 해소된 화면을 확인했다.
- 최종 실행: `Validation/Settings/FatigueBlur2pxSmooth`, 69.6초, 최대 2439MiB, Unity 한 개·affinity 1·20fps. 결과 PASS, 720프레임 촬영, 언어 설정 복원. 최초 단순 확대 결과는 `FatigueBlur2px`에 비교용으로 보존.
- 2픽셀 영상: `output/videos/DonDon-Fatigue-Blur-2px.mp4`. 아래의 1픽셀 검증과 v2 영상은 이전 기록이다.

## 초기 1픽셀 검증 결과

- C# 런타임/에디터 컴파일 통과. Unity 플레이 렌더링의 셰이더·RenderGraph 오류 없음.
- 낮음/높음 그래픽 품질 각각 1280×720, 1920×1080 실제 카메라 확인. 화면 차분과 선명도 비교로 블러가 출력에 반영됨을 확인.
- 정상 피로에서는 GPU 패스를 건너뛰고, 높은 피로에서 반경 1픽셀 유지. 정지·이동·전환·긴 프레임·FOV 변화·ESC 분기 통과.
- 영상에서 정지 구간의 모션 값은 0, 회전 구간에서는 약 0.6~2.1픽셀. 4픽셀 상한 별도 검증.
- 36초 H.264, 720프레임, 20fps 영상 인코딩 후 전체 디코드 확인. 영상에서는 비교를 위해 시장을 정지하고 피로도를 제어했다. 게임 배속 기능을 추가하지 않았다.
- Unity 프로세스 하나, CPU affinity 1, BelowNormal, 20fps, 4GB 중단 한도로 순차 실행. 성공 실행 71.7초, 최대 2458MiB. 모든 검증 프로세스 종료, 언어 설정 복원.
- 첫 Unity 실행은 신규 검증 스크립트의 잘못된 길이 GUID로 컴파일 단계에서 중단. GUID 수정 후 다시 실행하여 통과했다. 로직 검증 생략이나 조건 완화 없음.

증거: `Validation/Settings/FatigueBlur02/blur-checks.txt`, `optics.csv`, `resources.json`, `quality*-clear.png`, `quality*-blur.png`. 요약: `Validation/FatigueBlur/summary.json`. 영상: `output/videos/DonDon-Fatigue-Blur-v2.mp4`.

## 한계

고정 좌석 게임에 맞춰 카메라 **회전**으로 모션 블러를 계산한다. 개별 물체의 속도 벡터나 물체별 잔상은 생성하지 않는다. GPU별 성능 벤치마크나 XR 검증은 수행하지 않았다.

URP 연동은 설치된 17.5.0 패키지의 BlitWithMaterial 예제와 [Unity RenderGraph blit 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-blit.html)를 참고해 카메라 색 텍스처를 교체하여 불필요한 재복사를 줄였다.
