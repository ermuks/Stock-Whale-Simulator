# DonDon 태블릿 UI 리디자인 에셋 패키지

## 구성
- `References/`: 직전 확정한 네 화면의 PNG 시안. 레이아웃·시각적 기준으로만 사용하며 Unity UI 배경 텍스처로 직접 넣지 않습니다.
- `Atlases/DonDon_Solid_Icons_256.png`: 8열 × 6행, 셀 256×256, 전체 2048×1536 RGBA 아이콘 아틀라스.
- `Atlases/DonDon_Solid_Icons_128.png`: 동일 순서, 1024×768 RGBA.
- `Atlases/DonDon_Solid_Icons_64.png`: 동일 순서, 512×384 RGBA.
- `Atlases/DonDon_IconPreview_DARK_BACKGROUND_NOT_ATLAS.png`: 파일 확인용 미리보기, 배경이 있어 Unity UI 아이콘 아틀라스로 사용하면 안 됩니다.
- `Icons_256/`: 용도별로 분리한 개별 256×256 PNG 아이콘 48개.
- `Docs/IconManifest.json`, `IconIndex.csv`: 각 스프라이트의 식별자와 8×6 셀 위치.
- `Docs/DesignTokens.json`: UI 색상, 컴포넌트 기본 규격, 상태 스타일.
- `Codex_Astra6_Medium_Prompt.md`: Codex에 바로 붙여 넣을 구현 지시문.
- `Docs/GameTimeAndLogic.md`: 게임 시간과 계산 규칙 체크리스트.

## 중요한 이미지 보증
1. 아틀라스와 `Icons_256` PNG는 **배경 픽셀 Alpha = 0**인 RGBA입니다.
2. 아이콘은 **흰색 단색 + 안티앨리어싱 알파**입니다. 그림자, 그라데이션, 체크무늬가 없습니다.
3. 아이콘 색상(청록/빨강/파랑/회색)은 Unity `Image.color`로 제어합니다. 상태별로 PNG를 복제할 필요가 없습니다.
4. 아틀라스는 8열×6행 균일 셀, 여백/간격 0입니다. 셀 중앙에 그림을 배치하여 UI 배율을 바꿔도 일관된 시각 크기를 유지합니다.
5. 미리보기 PNG의 어두운 배경은 **검토용**이며 투명 스프라이트가 아닙니다.

## Unity Import
1. `DonDon_Solid_Icons_256.png` 선택 → Texture Type = Sprite (2D and UI), Sprite Mode = Multiple, Alpha Is Transparency = ON, Mip Maps = OFF.
2. Sprite Editor → Slice → Grid by Cell Size → 256×256, Offset 0, Padding 0, Pivot Center → Apply.
3. 이름과 순서는 `Docs/IconIndex.csv` 참조. Unity의 Y 좌표는 툴 설정에 따라 하단 원점일 수 있으므로, 표의 `rect_y_from_top_256`은 이미지 상단 원점입니다.
4. UI에서는 이미지 자체를 늘리는 대신 `RectTransform` 20~32 기준으로 시작하고 **태블릿 실제 월드스페이스 화면에서 가독성**을 확인합니다.
5. UI Panel/Border/Progress는 Unity 이미지 컴포넌트의 단색/기본 그래픽으로 구현하고 해당 아이콘들만 PNG로 연결합니다.
6. 픽셀별 투명 영역을 읽는 아이콘 클릭 영역에 의존하지 말고, 상위 버튼 전체가 터치되게 합니다.

## 권장 디자인
- 다크 미니멀 / 평면 UI / 흰색 솔리드 아이콘 / 청록 포인트.
- 정보가 중요할수록 크고 밝게. 작은 설명을 네모 박스로 감싸지 않습니다.
- 매수/매도와 손익의 **의미색**은 런타임 데이터에 적용합니다.
- 숫자는 원 단위 정수, 세 자리 쉼표, 열 우측 정렬 및 Tabular Numerals. 값 변경 시 열 폭 흔들림 방지.
- 한 화면에 모든 기능을 밀어 넣지 않습니다. 상세 데이터는 화면 전환·확장으로 노출합니다.

## 특이사항
시안 이미지의 예시 숫자 및 날짜는 **미적 참조용**입니다. 구현 값은 반드시 실제 게임 상태와 단일 게임 시계에서 바인딩하세요. 특히 주문 화면의 예제에는 자산을 초과하는 주문 입력이 있으므로 그대로 사용하면 안 됩니다.
