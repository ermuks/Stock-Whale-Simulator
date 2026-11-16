# 시세 색상 선택 — 이미지 버튼

텍스트로 나열하던 색상 선택을 PNG 캔들 이미지 버튼 두 개로 교체했다. 버튼 안에는 TMP_Text가 없으며 3캔들이 하나로 그려진 Sprite 한 개만 표시한다. 선택 테두리와 카드 배경은 일반 Unity UI Image다. PNG의 각 캔들을 여러 UI 사각형으로 조립하지 않았다.

## 에셋과 생성 경로

- `Assets/Resources/UI/SystemMenu/CandleThemeRedBlue.png`: 빨강 / 파랑 / 빨강. 원본 1586×992.
- `Assets/Resources/UI/SystemMenu/CandleThemeGreenRed.png`: 초록 / 빨강 / 초록. 원본 1585×992.
- built-in `image_gen` 사용. 두 번째 이미지는 첫 번째 이미지를 참조해 색상만 바꾸도록 요청했다. CLI/API fallback 미사용.
- 생성 원본은 `.codex/generated_images/01a12fe2-0d10-7a83-8454-e2f7ae0de499/`의 `exec-008f5a49-830b-4a65-b134-2984209c531b.png`, `exec-1e0952e6-84f3-4467-a1ae-3a6e276f5a59.png`다. 프로젝트에는 복사본을 넣었고 원본을 삭제하지 않았다.
- PNG는 실제 알파 배경이며 그대로 보존했다. Unity에서는 Sprite Single / Full Rect / Center / PPU100 / max512 / no compression / no mipmap / Clamp / Bilinear / Alpha Is Transparency로 가져온다. 런타임 Image는 Simple / Preserve Aspect / Raycast Target off다.

## UI 변경

`SystemMenuUI.BuildSettings`의 색 이름 선택과 ▲상승/▼하락 문자 견본을 제거했다. `MarketColorChoices`가 344×168 이미지 버튼 두 개를 만들고 선택된 버튼에 밝은 2px 테두리를 표시한다. 설정 저장과 실제 시세 색상 변경은 기존 GameSettings.Commit 경로 그대로다. UI 카테고리의 짧은 ‘시세 색상’ 헤더만 남겼다.

## 최종 프롬프트

### 첫 번째 이미지

```text
Create a production-ready transparent PNG sprite for a minimal dark trading-game settings button. ONLY a compact group of EXACTLY THREE solid financial candlesticks, centered, front-facing flat 2D. Color order from LEFT TO RIGHT: RED (#EF5964), BLUE (#4C96E8), RED (#EF5964). Each candlestick has one solid rectangular body and a thin vertical wick extending above and below its center. Equal body widths, equal horizontal spacing. First candle in the lower-left with medium body; second candle slightly higher with short body; third candle highest with medium-tall body, a simple subtle rising three-candle silhouette. Crisp straight square corners, uniform opaque colors, no texture. Canvas landscape 512x320, three-candle group occupies about 70% width and 75% height with balanced transparent padding. True alpha-transparent empty background. NO text, NO labels, NO letters, NO numbers, NO arrows, NO axes, NO grid, NO panel, NO button, NO border, NO shadow, NO glow, NO gradient, NO perspective, NO extra candles. This is one image asset, not a mockup.
```

### 두 번째 이미지 편집

```text
Edit this transparent three-candlestick UI sprite. Change ONLY the colors: left and right candlesticks become GREEN (#37C995), middle candlestick becomes RED (#EF5964). Result order must be GREEN, RED, GREEN. Preserve the exact same three silhouettes, rectangular bodies, thin centered wicks, positions, relative heights, spacing, framing, canvas dimensions and transparent background as the input. Clean flat solid colors with crisp edges. Do not add any text, numbers, labels, button, border, background, shadow, glow, texture, grid, axes, or additional candles.
```

검증 로그/실제 화면은 `Validation/Settings/MarketColorCards01/`에 저장한다. `MarketColorsValidation.RunVisual`은 기존 테마 검증에 ‘버튼 내부 텍스트 0개’, ‘정확한 PNG 연결’, ‘원본 비율 보존’, ‘선택 테두리’ 검사를 추가하며 종료 시 사용자 설정을 복원한다.

실행 결과: 160 checks PASS, 41.6초, peak 2096 MiB. 단일 Unity/CPU affinity 1로 진행했다. 실제 한국어 설정 캡처에서 두 카드의 3캔들 색상 순서와 텍스트 제거를 육안 확인했다. 한·영·일 및 1600×900/1920×1080 캡처/레이아웃 검사도 통과했다.
