# Chart history and focused input

## Changes

- `CandleHistoryManager`: keeps the existing 100 completed candles for bot/statistical consumers. `ChartHistory` holds up to 8,192 base bars separately. `OnMatched` records actual trades into game-minute OHLCV via `ChartHistorySeries.RecordTrade`; `MinuteHistory` retains up to 8,192 traded minutes per stock. Delisting removes both archives.
- `ChartHistorySeries` (new, complete source in `Assets/Scripts/ChartUI/ChartHistorySeries.cs`): combines actual minute bars into 1m/5m/15m/1h/1D. Preserves first open, maximum high, minimum low, last close and summed volume; keeps days separate. Initial synthetic bars have no game timestamps and appear only in the original/base view. No-trade minutes are omitted rather than fabricated.
- `ChartManager.Navigation` (new, complete source in `Assets/Scripts/ChartUI/ChartManager.Navigation.cs`): builds actual Image/Button/TMP controls on the existing chart overlay. Latest arrow appears only while browsing history. Period switching returns to latest; ordinary refresh retains scroll state. All controls reject input outside central monitor focus, during transitions, text entry, device view or pause.
- `ChartManager`: retains existing mesh, collider drag path, cursor overlay and selected-stock routing. Dragging back to the rightmost data resumes follow. Past viewport anchors to a source timestamp/bucket rather than a shifting array index. On archive expiration, clamps to the oldest retained bar. Clears stale candle selections when dragging.
- No scene, device model, camera pose, trading rules or simulation speed changed by this task.

## Existing function comparisons (ChartManager: more than three edit regions)

- `Start`: previously created guide overlay only; now also builds navigation once.
- `Update`: previously returned on pause leaving capture untouched; now releases capture, updates control availability, and keeps existing throttled refresh.
- `HandleChartDrag`: previously retained numeric index only; now captures the actual historical timestamp and clears old selection after movement.
- `ChartInputBlocked`: previously allowed input when no desk controller existed; now requires chart focus and blocks pause explicitly.
- `LateUpdate`: previously marked the last displayed bar as forming unconditionally; aggregated bars must also belong to the current game-time bucket during trading.
- `RefreshChartFromHistory`: previously read only the last 100 source bars; now builds the chosen chart series and restores a historical anchor before selecting visible bars.

## Persistence and limits

SessionStore includes CandleHistoryManager as a reflection-serialized root, including its private dictionaries. No second save format is introduced. Old checkpoints have no new minute records: exact minute history begins with subsequent trades, without reconstructing invented trades from old OHLC bars. Save/load round-trip of the new dictionaries is not yet runtime-tested. Archives are bounded, not unlimited history. Once a viewed bar expires, the view clamps to the oldest remaining record.

## Verification

- Full runtime/editor C# compilation: passed (`Validation/chart-navigation-compile.log`).
- `ChartNavigationValidation.Run`: 10 initial checks passed: OHLCV aggregation, organic volume, day separation, archive cap, nonfocused rejection, focused state, visible capacity, history anchoring on append/eviction, latest return, period change.
- Initial sequential Unity run: 45.3 seconds, one CPU core, peak working set 2,221 MB; no concurrent editor launched.
- Actual scene-camera captures at 1600×900 and 1920×1080 inspected; timeframe toolbar readable above plot. Candles in these captures are controlled test data, not a market balance run.
- Follow-up run adds EventSystem world-space raycast and pointer-click verification (results in `Validation/Settings/ChartNavigation02`). OS mouse dragging is not automated by this probe; viewport state transitions and existing drag-path integration are checked separately.
