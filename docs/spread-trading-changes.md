# 두 번째 태블릿 · 스프레드 매매

2026-11-15. 기존 두 번째 계좌/태블릿 Canvas에 적용. 새 전체 화면 Canvas나 3D 모델을 만들지 않았다.

## 동작

- 1번 계좌 자산이 1억 원을 초과하면 두 번째 계좌가 해금되는 기존 규칙 유지. 해금은 현금/주식을 지급하지 않는다.
- 해금 후 두 번째 태블릿의 기본 진입 화면은 스프레드. 대시보드의 기존 자동 운용 버튼도 `스프레드`로 변경.
- `± 1~20틱` 슬라이더. 기본 ±2틱. 각 방향으로 한국 주식 호가 단위를 순서대로 이동하므로 1,999↔2,000, 4,995↔5,000 등의 경계에서도 유효한 가격이다. 최소 매수가 1원.
- `매매 시작`: 선택한 종목을 대상으로 매수 지정가 1건과 매도 지정가 1건을 유지. 실행 중 다른 종목을 선택해도 자동 주문 대상은 바뀌지 않는다. 취소 후 새 종목으로 시작한다.
- 실제 주문 가능한 현금/주식을 전부 사용한다. 기존 2·5·10% 예산 비율이나 최소 운용 자금 조건을 적용하지 않는다. 한 주 미만 잔돈은 남는다.
- 기존 수동 주문의 예약금·예약 주식 및 DeviceOrderSession의 아직 제출되지 않은 분할 예약을 제외한다.
- 현금만 있으면 매수, 주식만 있으면 매도부터 가능. 양쪽 모두 없으면 실행 상태로 기다리며 자산이 생기면 가능한 쪽을 주문한다.
- 가격/자산/슬라이더 변화에 따라 기본 0.5초 주기로 필요한 주문만 교체한다. 동일 가격·잔량이면 취소하지 않아 큐 우선순위를 유지한다. UI는 0.25초 갱신.
- 스프레드 주문은 반대편 최우선 호가와 교차하면 그 방향만 대기한다. 지정 범위를 벗어나 시장가처럼 쓸어 담거나 자기 수동 주문을 취소하지 않는다.
- `매매 취소`: 이 기능의 양쪽 미체결을 즉시 회수하고 자동 재주문을 중지한다. 이미 체결된 자산과 별도 수동/분할 주문은 유지한다. 일시정지 중 취소도 가능.
- 장 종료·거래중지·상장폐지 상태에서는 양쪽 주문을 회수한다.
- 실현손익은 실제 매도 체결가와 해당 매도 주문의 취득원가로 계산. 수익을 만들어 지급하지 않으며 손실도 표시한다.

## 변경 파일 / 핵심 함수

- `Assets/Scripts/NightShift/TradingOperationController.Spread.cs` 신규: `SpreadPrices`, `StartSpread`, `RefreshSpread`, `MaintainQuote`, `CancelSpread`. 기존 Trader/OrderBook 호출과 주문 소유권 추적을 사용.
- `Assets/Scripts/NightShift/TradingOperationController.cs`: partial 선언, 기존 enum 번호를 보존한 Spread 추가, `Update` 분기, 기존 `SubmitSlice`와의 분리, `OnFill`의 실제 실현손익 집계. 이전 모드는 다른 호출자와의 호환을 위해 남기며 두 번째 태블릿에서는 노출하지 않는다.
- `Assets/Scripts/NightShift/SpreadTradingUI.cs` 신규: `Install`로 기존 deskPanel을 재사용, `Build`로 고정 그리드 구성, `BuildSlider`로 호가 범위 조절, `Update`로 표시 갱신. 주문 실행은 시작/취소 입력과 컨트롤러만 담당한다.
- `Assets/Scripts/NightShift/DeskOperationUI.cs`: 새 뷰가 설치된 패널은 옛 UI 갱신을 중지.
- `Assets/Scripts/NightShift/TabletAccountUI.cs`: `Start`, `Unlock`, `ShowPage`에서 두 번째 기본 화면과 기존 대시보드/수동 주문/뉴스 연결 유지.
- Unity에서 신규 C# `.meta` 생성. 기존 `.meta`는 교체하지 않음.

신규 C# 전문과 함수별 원본/수정본: [spread-trading-code.md](spread-trading-code.md).

## 데이터 / 스타일

- 종목·현재가: `StockManager.CurrentStock`, 실행 중에는 `TradingOperationController.Target`.
- 가용 현금·주식: `DeviceOrderSession.AvailableCash/AvailableShares`, 실제 계좌는 `PlayerAccounts.automated`.
- 주문·체결·취득원가: 기존 `Trader.BuyLimit/SellLimit`, `OrderBook`, `Stock.OnMatchedTrade`, `Order.costBasis`.
- 버튼은 이전 작업에서 imagegen으로 만든 Primary/Secondary PNG를 그대로 재사용. 신규 합성 이미지나 아이콘 생성 없음.
- 글꼴은 기존 Pretendard Reading SDF와 고정 숫자 폭 Trading Numerals SDF. 숫자 우측 정렬, 짧은 레이블, 같은 높이의 버튼, 얇은 구분선.
- 자동 주문 화면의 종목명/가용자산/양방향 대기/슬라이더/실제 체결/손익을 고정 열로 표시. 주기적 전체 레이아웃 재생성 없음.

## 검증

최종 결과: `Validation/Screenshots/2026-11-15-spread-verified/capture.json`, `checks.txt`, `resources.json`.

- 런타임/에디터 C# 컴파일 통과. 테스트용 에디터 스크립트를 제거한 후에도 컴파일 통과. 기존 deprecated API 경고는 남아 있다.
- 실제 Trader/OrderBook을 사용하는 격리된 런타임 계좌로 주문 검증. 실제 게임 씬에서는 두 번째 계좌 해금·버튼/슬라이더의 카메라 Raycast·수동 거래/뉴스 화면 연결 검증. 총 58개 PASS.
- 호가 경계/최소 1원/유효하지 않은 가격, 현금만/주식만/무자산, 전체 가용 자산 사용, 부분 체결, 재주문, 실제 손실, 현금·주식 총량 보존, 수동·분할 예약 보존, 자기 반대 주문과 교차 방지, 취소/중지/상폐 회수 확인.
- 변동 없는 상태에서 100회 갱신해 동일 주문 인스턴스와 OrderBook.Version 유지 확인.
- 1600×900·1920×1080 실제 게임 카메라 PNG 검토. 텍스트 오버플로 없음. 슬라이더 클릭과 시작/취소 버튼 실제 포인터 이벤트 확인.
- 마지막 검증: Unity 하나, CPU affinity 1, BelowNormal, 20fps, timeScale 1. 약 41.8초, 프로세스 최대 working set 1,982MiB. 배속 기능 추가 없음.
- `TradeScene.unity`, `PlayerAccounts.cs`, `DeviceOrderSession.cs`, `Trader.cs`, `OrderBook.cs`는 작업 직전 바이트와 동일. SHA256 기록: `Validation/SpreadTrading/preservation-proof.json`.
- 검증에서 지급한 자산과 격리 계좌는 플레이 세션에만 존재. 씬/사용자 저장 데이터로 저장하지 않음.

적용 화면:

- `Validation/Screenshots/2026-11-15-spread-verified/spread-running-1920x1080.png`
- `Validation/Screenshots/2026-11-15-spread-verified/spread-running-1600x900.png`
- `Validation/Screenshots/2026-11-15-spread-verified/spread-empty-1600x900.png`
- `Validation/Screenshots/2026-11-15-spread-verified/spread-stopped.png`

재현: `Validation/SpreadTrading/prepare_capture.py`로 임시 에디터 드라이버를 생성하고 `Validation/RunSpreadCapture.ps1 -Run <새 폴더명>`을 단독 실행한다. 종료 후 임시 `Assets/Editor/SpreadCaptureJob.cs`와 해당 `.meta`를 제거한다. 실행한 드라이버는 `Validation/SpreadTrading/capture-driver.cs.txt`로 보관.

## 남은 범위와 다음 제안

- 이 작업은 단일 종목 스프레드 주문과 기존 태블릿 연결까지다. 여러 종목 동시 자동 운용, 저장 후 자동 재시작, 장시간 손익 밸런스는 이번 검증에 포함하지 않았다.
- 급격한 추세에서 보유 주식의 가치가 하락할 수 있다. 실현손익은 팔린 물량만 계산하므로 미실현손익은 기존 대시보드에서 확인한다.
- 추가 기능의 유료 해금 가격/구매 UI는 임의로 정하지 않았다. 기존 가짜 뉴스 기능의 현실시간 600초 쿨타임은 변경하지 않았다.
- 후속 후보: 큰손의 거래 흔적 분석, 봇 정보원 계약, 실제 자료에 근거한 반박 제보, 리딩방 간 공동 매매 제안. 각 도구의 영구 해금 비용은 2번 계좌의 주문 가능 현금에서 지불하는 방식이 적합하다. 효과는 기존 뉴스·기억·신뢰·실제 거래에 연결하고 확정 수익/강제 가격 이동으로 처리하지 않는다.
