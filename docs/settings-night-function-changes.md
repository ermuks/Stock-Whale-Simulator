# 기존 스크립트 변경 비교
비교 기준: 이 작업 시작 시 Validation/Settings/before/Scripts에 보관한 원본. 기존 사용자 변경은 이 비교에 포함하지 않는다.

## Assets/Scripts/ChartManager.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -243,24 +243,25 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         HandleChartDrag();
         float interval = isDraggingChart ? refreshIntervalWhileDragging : refreshInterval;
         if (interval <= 0f && isDraggingChart)
         {
             RefreshChartFromHistory();
         }
-        else if (Time.time - _lastRefreshTime >= interval)
+        else if (SessionClock.Time - _lastRefreshTime >= interval)
         {
             RefreshChartFromHistory();
         }
 
-        if (Time.time - _lastStatsRefreshTime >= StatsRefreshInterval)
+        if (SessionClock.Time - _lastStatsRefreshTime >= StatsRefreshInterval)
         {
             RefreshSymbolAndName();
             RefreshTradeBasedValues();
             RefreshStatistics();
             RefreshTradeHistory();
-            _lastStatsRefreshTime = Time.time;
+            _lastStatsRefreshTime = SessionClock.Time;
         }
     }
 
@@ -330,6 +331,7 @@
 
     void LateUpdate()
     {
+        if (PauseMenuController.BlocksInput) { _guides?.HideCursor(); return; }
         if (_guides == null)
         { return; }
         if (ChartInputBlocked() || isDraggingChart || !TryGetChartPoint(out Vector3 point) || point.x >= candleRenderer.PlotWidth || point.y < 0f)
@@ -666,7 +668,7 @@
         candleRenderer.candles = _currentVisibleCandles;
         candleRenderer.GenerateMesh();
         if (_guides != null) { _guides.Refresh(currentStock); }
-        _lastRefreshTime = Time.time;
+        _lastRefreshTime = SessionClock.Time;
         // 기존 클릭 선택의 OHLC 정보 유지. 커서 정보는 LateUpdate에서 따로 처리한다.
         if (candleSelectData != null && candleSelectData.HasSelection && candleSelectData.SelectedGlobalIndex.HasValue)
         {
```

## Assets/Scripts/DeskControlHints.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -78,7 +78,7 @@
     {
         if (views == null || hints == null)
         { return; }
-        bool shown = views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
+        bool shown = !PauseMenuController.BlocksInput && views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
         bool showLabels = shown && showShortcutHints;
         if (showLabels != wasVisible)
         { hints.gameObject.SetActive(showLabels); wasVisible = showLabels; }
```

## Assets/Scripts/DeskViewController.cs
변경 덩어리: 2

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -80,6 +80,7 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { Hovered = DeskViewTarget.None; return; }
         if (!Ready)
         { Hovered = DeskViewTarget.None; return; }
         if (MouseCameraController.TextEntryActive)
@@ -216,6 +217,7 @@
 
     void LateUpdate()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         if (Target >= DeskViewTarget.TabletOne)
         {
             var tablet = tablets[(int)Target - 3];
```

## Assets/Scripts/DisplayAimCursor.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -42,6 +42,7 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { EndPointer(false); if (displayCursor != null) { displayCursor.gameObject.SetActive(false); } return; }
         if (displayCanvas == null || displayCursor == null || _mainCam == null || _collider == null)
         { EndPointer(false); return; }
         if (DeskViewController.Instance != null || TabletViewController.IsTabletHeld || PhoneCallController.IsRaised)
```

## Assets/Scripts/MouseCameraController.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -77,6 +77,7 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
         {
             Cursor.lockState = CursorLockMode.None;
@@ -99,8 +100,8 @@
         ApplyMouseWheelFov();
         float mouseX = Input.GetAxis("Mouse X");
         float mouseY = Input.GetAxis("Mouse Y");
-        _currentAngleY += mouseX * sensitivity;
-        _currentAngleX -= mouseY * sensitivity;
+        _currentAngleY += mouseX * sensitivity * GameSettings.Current.sensitivity;
+        _currentAngleX -= mouseY * sensitivity * GameSettings.Current.sensitivity;
         _currentAngleX = Mathf.Clamp(_currentAngleX, minAngleX, maxAngleX);
         _currentAngleY = Mathf.Clamp(_currentAngleY, minAngleY, maxAngleY);
         transform.eulerAngles = new Vector3(_currentAngleX, _currentAngleY, 0f);
@@ -117,7 +118,7 @@
         }
         if (Mathf.Approximately(wheel, 0f))
         { return; }
-        _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - wheel * wheelFovSensitivity, MinFov, MaxFov);
+        _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - wheel * GameSettings.Current.wheel, MinFov, MaxFov);
     }
 
     public void CompleteInitialSetup()
```

## Assets/Scripts/Order.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -34,7 +34,7 @@
         this.volume = volume;
         this.isBuy = isBuy;
         this.timestamp = timestamp;
-        createdAt = UnityEngine.Time.time;
+        createdAt = SessionClock.Time;
         this.owner = owner;
         purpose = owner is ExchangeBot exchange ? exchange.SubmittedPurpose : owner is WhaleBot whale && whale.IsDisposalSubmission ? OrderPurpose.OwnerPreDisposal : OrderPurpose.Ordinary;
         purposeSlot = owner is ExchangeBot bot ? bot.SubmittedSlot : -1;
```

## Assets/Scripts/PsychologicalLevelManager.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -112,7 +112,7 @@
             currentResistance = GetNextResistance(stock.currentPrice),
             previousPrice = stock.currentPrice,
             isWallActive = false,
-            lastCheckTime = Time.time
+            lastCheckTime = SessionClock.Time
         };
 
         state.tradeHandler = (price, vol) => CheckPriceMovement(stock, price);
@@ -125,8 +125,8 @@
     {
         if (!_states.TryGetValue(stock, out var state)) return;
 
-        if (Time.time - state.lastCheckTime < 0.5f) return;
-        state.lastCheckTime = Time.time;
+        if (SessionClock.Time - state.lastCheckTime < 0.5f) return;
+        state.lastCheckTime = SessionClock.Time;
 
         decimal resistance = state.currentResistance;
         if (resistance <= 0m) return;
@@ -142,7 +142,7 @@
                 if (UnityEngine.Random.value < wallFormChance)
                 {
                     state.isWallActive = true;
-                    state.wallFormedTime = Time.time;
+                    state.wallFormedTime = SessionClock.Time;
                     OnWallForming?.Invoke(stock, resistance);
                 }
             }
```

## Assets/Scripts/Stock.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -38,7 +38,7 @@
 
     public string RiskLabel => RiskPending ? "판정 대기" : RiskGrade == StockRiskGrade.Safe ? "안전" : RiskGrade == StockRiskGrade.Warning ? "경고" : RiskGrade == StockRiskGrade.Danger ? "위험" : RiskGrade == StockRiskGrade.HighRisk ? "고위험" : "거래중지";
     public UnityEngine.Color RiskColor => StockManager.Instance == null ? UnityEngine.Color.gray : RiskGrade == StockRiskGrade.Safe ? StockManager.Instance.economy.safeColor : RiskGrade == StockRiskGrade.Warning ? StockManager.Instance.economy.warningColor : RiskGrade == StockRiskGrade.Danger ? StockManager.Instance.economy.dangerColor : RiskGrade == StockRiskGrade.HighRisk ? StockManager.Instance.economy.highColor : StockManager.Instance.economy.haltedColor;
-    public string RiskDisplay => "<color=#" + UnityEngine.ColorUtility.ToHtmlStringRGB(RiskPending ? UnityEngine.Color.gray : RiskColor) + ">" + RiskLabel + (IsTradingHalted ? " " + UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(0f, HaltUntil - UnityEngine.Time.time)) + "초" : "") + "</color>";
+    public string RiskDisplay => "<color=#" + UnityEngine.ColorUtility.ToHtmlStringRGB(RiskPending ? UnityEngine.Color.gray : RiskColor) + ">" + RiskLabel + (IsTradingHalted ? " " + UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(0f, HaltUntil - SessionClock.Time)) + "초" : "") + "</color>";
 
     public string symbol;
     public string stockName;
```

## Assets/Scripts/StockIntegrityChecker.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -40,9 +40,9 @@
 
     void Update()
     {
-        if (Time.time - _lastCheckTime >= checkInterval)
+        if (SessionClock.Time - _lastCheckTime >= checkInterval)
         {
-            _lastCheckTime = Time.time;
+            _lastCheckTime = SessionClock.Time;
             CheckAllStocks();
         }
     }
```

## Assets/Scripts/StockInterestManager.cs
변경 덩어리: 2

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -80,7 +80,7 @@
             }
         }
 
-        _nextDecayTime = Time.time + decayInterval;
+        _nextDecayTime = SessionClock.Time + decayInterval;
     }
 
     void OnDestroy()
@@ -116,9 +116,9 @@
 
     void Update()
     {
-        if (Time.time >= _nextDecayTime)
-        {
-            _nextDecayTime = Time.time + decayInterval;
+        if (SessionClock.Time >= _nextDecayTime)
+        {
+            _nextDecayTime = SessionClock.Time + decayInterval;
             Decay();
         }
     }
```

## Assets/Scripts/StockManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -157,6 +157,7 @@
 
     private void HandleStockNavigation()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         if (!SetupConfirmed || stocks == null || stocks.Count == 0)
         { return; }
         int direction = Input.GetKeyDown(KeyCode.W) ? -1 : Input.GetKeyDown(KeyCode.S) ? 1 : 0;
```

## Assets/Scripts/StockNewsAttentionManager.cs
변경 덩어리: 2

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -35,7 +35,7 @@
                 RegisterStock(stock);
             }
         }
-        _nextDecayTime = Time.time + decayInterval;
+        _nextDecayTime = SessionClock.Time + decayInterval;
         StockManager.OnStockDelisted += OnStockDelisted;
     }
 
@@ -54,9 +54,9 @@
 
     void Update()
     {
-        if (Time.time >= _nextDecayTime)
+        if (SessionClock.Time >= _nextDecayTime)
         {
-            _nextDecayTime = Time.time + decayInterval;
+            _nextDecayTime = SessionClock.Time + decayInterval;
             Decay();
         }
     }
```

## Assets/Scripts/StockStatistics.cs
변경 덩어리: 7

### void OnTradeExecuted(Stock stock, decimal price, decimal volume)
원본
```csharp
    void OnTradeExecuted(Stock stock, decimal price, decimal volume)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return; }

        float now = Time.time;
        if (price > stats.week52High)
        {
            stats.week52High = price;
        }

        if (price < stats.week52Low)
        {
            stats.week52Low = price;
        }

        if (price > stats.dayHigh)
        {
            stats.dayHigh = price;
        }

        if (price < stats.dayLow)
        {
            stats.dayLow = price;
        }

        bool isBuyInitiated = IsBuyInitiated(stock, price);
        var record = new TradeRecord
        {
            price = price,
            volume = volume,
            time = now,
            isBuyInitiated = isBuyInitiated
        };
        stats.recentTrades.Add(record);
        if (stats.recentTrades.Count > maxTradeRecords)
        {
            stats.recentTrades.RemoveAt(0);
        }

        stats.strengthTrades.Add(record);
        _stats[stock] = stats;
        if (_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] += price * volume;
        }
        else
        {
            _sessionTurnover[stock] = price * volume;
        }

        TrackVolumeAndDetectSurge(stock, volume, now);
    }
```
수정본
```csharp
    void OnTradeExecuted(Stock stock, decimal price, decimal volume)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return; }

        float now = SessionClock.Time;
        if (price > stats.week52High)
        {
            stats.week52High = price;
        }

        if (price < stats.week52Low)
        {
            stats.week52Low = price;
        }

        if (price > stats.dayHigh)
        {
            stats.dayHigh = price;
        }

        if (price < stats.dayLow)
        {
            stats.dayLow = price;
        }

        bool isBuyInitiated = IsBuyInitiated(stock, price);
        var record = new TradeRecord
        {
            price = price,
            volume = volume,
            time = now,
            isBuyInitiated = isBuyInitiated
        };
        stats.recentTrades.Add(record);
        if (stats.recentTrades.Count > maxTradeRecords)
        {
            stats.recentTrades.RemoveAt(0);
        }

        stats.strengthTrades.Add(record);
        _stats[stock] = stats;
        if (_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] += price * volume;
        }
        else
        {
            _sessionTurnover[stock] = price * volume;
        }

        TrackVolumeAndDetectSurge(stock, volume, now);
    }
```

### public void RegisterStock(Stock stock)
원본
```csharp
    public void RegisterStock(Stock stock)
    {
        if (stock == null)
        { return; }

        if (!_stats.ContainsKey(stock))
        {
            var stats = new StockStats
            {
                stock = stock,
                week52High = stock.currentPrice,
                week52Low = stock.currentPrice,
                prevDayClose = stock.currentPrice,
                dayOpen = stock.currentPrice,
                dayHigh = stock.currentPrice,
                dayLow = stock.currentPrice,
                dayStartTime = Time.time,
                week52StartTime = Time.time,
                recentTrades = new List<TradeRecord>(),
                strengthTrades = new List<TradeRecord>()
            };
            _stats[stock] = stats;
            _lastSurgeTime[stock] = 0f;
            _prevWindowVolume[stock] = 0m;
            _currentWindowVolume[stock] = 0m;
            _windowStartTime[stock] = Time.time;
        }

        if (!_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] = 0m;
        }

        stock.OnTradeExecutedWithStock -= OnTradeExecuted;
        stock.OnTradeExecutedWithStock += OnTradeExecuted;
        stock.OnMatchedTrade -= OnMatchedPrice;
        stock.OnMatchedTrade += OnMatchedPrice;
    }
```
수정본
```csharp
    public void RegisterStock(Stock stock)
    {
        if (stock == null)
        { return; }

        if (!_stats.ContainsKey(stock))
        {
            var stats = new StockStats
            {
                stock = stock,
                week52High = stock.currentPrice,
                week52Low = stock.currentPrice,
                prevDayClose = stock.currentPrice,
                dayOpen = stock.currentPrice,
                dayHigh = stock.currentPrice,
                dayLow = stock.currentPrice,
                dayStartTime = SessionClock.Time,
                week52StartTime = SessionClock.Time,
                recentTrades = new List<TradeRecord>(),
                strengthTrades = new List<TradeRecord>()
            };
            _stats[stock] = stats;
            _lastSurgeTime[stock] = 0f;
            _prevWindowVolume[stock] = 0m;
            _currentWindowVolume[stock] = 0m;
            _windowStartTime[stock] = SessionClock.Time;
        }

        if (!_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] = 0m;
        }

        stock.OnTradeExecutedWithStock -= OnTradeExecuted;
        stock.OnTradeExecutedWithStock += OnTradeExecuted;
        stock.OnMatchedTrade -= OnMatchedPrice;
        stock.OnMatchedTrade += OnMatchedPrice;
    }
```

### void Update()
원본
```csharp
    void Update()
    {
        float now = Time.time;
        _updateKeys.Clear();
        _updateKeys.AddRange(_stats.Keys);
        bool doCleanup = now - _lastCleanupTime >= CLEANUP_INTERVAL;
        if (doCleanup)
        {
            _lastCleanupTime = now;
        }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float cutoffTime = now - candleDuration;
        foreach (var stock in _updateKeys)
        {
            if (!_stats.TryGetValue(stock, out var stats))
            { continue; }

            bool modified = false;
            if (now - stats.week52StartTime >= week52InSeconds)
            {
                stats.week52High = stats.stock.currentPrice;
                stats.week52Low = stats.stock.currentPrice;
                stats.week52StartTime = now;
                modified = true;
            }

            if (doCleanup && stats.strengthTrades.Count > 0)
            {
                int removed = stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
                if (removed > 0)
                {
                    modified = true;
                }
            }

            if (modified)
            {
                _stats[stock] = stats;
            }
        }
    }
```
수정본
```csharp
    void Update()
    {
        float now = SessionClock.Time;
        _updateKeys.Clear();
        _updateKeys.AddRange(_stats.Keys);
        bool doCleanup = now - _lastCleanupTime >= CLEANUP_INTERVAL;
        if (doCleanup)
        {
            _lastCleanupTime = now;
        }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float cutoffTime = now - candleDuration;
        foreach (var stock in _updateKeys)
        {
            if (!_stats.TryGetValue(stock, out var stats))
            { continue; }

            bool modified = false;
            if (now - stats.week52StartTime >= week52InSeconds)
            {
                stats.week52High = stats.stock.currentPrice;
                stats.week52Low = stats.stock.currentPrice;
                stats.week52StartTime = now;
                modified = true;
            }

            if (doCleanup && stats.strengthTrades.Count > 0)
            {
                int removed = stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
                if (removed > 0)
                {
                    modified = true;
                }
            }

            if (modified)
            {
                _stats[stock] = stats;
            }
        }
    }
```

### public float GetTradeStrength(Stock stock)
원본
```csharp
    public float GetTradeStrength(Stock stock)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return 100f; }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float now = Time.time;
        float cutoffTime = now - candleDuration;
        stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
        _stats[stock] = stats;
        decimal buyVolume = 0m;
        decimal sellVolume = 0m;
        foreach (var trade in stats.strengthTrades)
        {
            if (trade.isBuyInitiated)
            {
                buyVolume += trade.volume;
            }
            else
            {
                sellVolume += trade.volume;
            }
        }

        if (sellVolume <= 0m)
        { return buyVolume > 0m ? 999f : 100f; }

        return (float)(buyVolume / sellVolume * 100m);
    }
```
수정본
```csharp
    public float GetTradeStrength(Stock stock)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return 100f; }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float now = SessionClock.Time;
        float cutoffTime = now - candleDuration;
        stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
        _stats[stock] = stats;
        decimal buyVolume = 0m;
        decimal sellVolume = 0m;
        foreach (var trade in stats.strengthTrades)
        {
            if (trade.isBuyInitiated)
            {
                buyVolume += trade.volume;
            }
            else
            {
                sellVolume += trade.volume;
            }
        }

        if (sellVolume <= 0m)
        { return buyVolume > 0m ? 999f : 100f; }

        return (float)(buyVolume / sellVolume * 100m);
    }
```

### public void BeginTradingDay(IReadOnlyDictionary<Stock, decimal> previousCloses)
원본
```csharp
    public void BeginTradingDay(IReadOnlyDictionary<Stock, decimal> previousCloses)
    {
        _updateKeys.Clear(); _updateKeys.AddRange(_stats.Keys);
        foreach (var stock in _updateKeys)
        {
            var stats = _stats[stock];
            if (previousCloses != null && previousCloses.TryGetValue(stock, out decimal close) && close > 0m)
            { stats.prevDayClose = close; }
            stats.dayOpen = stats.dayHigh = stats.dayLow = stock.currentPrice;
            stats.dayStartTime = Time.time;
            _stats[stock] = stats;
        }
    }
```
수정본
```csharp
    public void BeginTradingDay(IReadOnlyDictionary<Stock, decimal> previousCloses)
    {
        _updateKeys.Clear(); _updateKeys.AddRange(_stats.Keys);
        foreach (var stock in _updateKeys)
        {
            var stats = _stats[stock];
            if (previousCloses != null && previousCloses.TryGetValue(stock, out decimal close) && close > 0m)
            { stats.prevDayClose = close; }
            stats.dayOpen = stats.dayHigh = stats.dayLow = stock.currentPrice;
            stats.dayStartTime = SessionClock.Time;
            _stats[stock] = stats;
        }
    }
```

### public StockStats(Stock s)
원본
```csharp
    public StockStats(Stock s)
    {
        stock = s;
        week52High = s?.currentPrice ?? 0m;
        week52Low = s?.currentPrice ?? 0m;
        prevDayClose = s?.currentPrice ?? 0m;
        dayOpen = s?.currentPrice ?? 0m;
        dayHigh = s?.currentPrice ?? 0m;
        dayLow = s?.currentPrice ?? 0m;
        dayStartTime = Time.time;
        week52StartTime = Time.time;
        recentTrades = new List<TradeRecord>();
        strengthTrades = new List<TradeRecord>();
    }
```
수정본
```csharp
    public StockStats(Stock s)
    {
        stock = s;
        week52High = s?.currentPrice ?? 0m;
        week52Low = s?.currentPrice ?? 0m;
        prevDayClose = s?.currentPrice ?? 0m;
        dayOpen = s?.currentPrice ?? 0m;
        dayHigh = s?.currentPrice ?? 0m;
        dayLow = s?.currentPrice ?? 0m;
        dayStartTime = SessionClock.Time;
        week52StartTime = SessionClock.Time;
        recentTrades = new List<TradeRecord>();
        strengthTrades = new List<TradeRecord>();
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -120,8 +120,8 @@
                 dayOpen = stock.currentPrice,
                 dayHigh = stock.currentPrice,
                 dayLow = stock.currentPrice,
-                dayStartTime = Time.time,
-                week52StartTime = Time.time,
+                dayStartTime = SessionClock.Time,
+                week52StartTime = SessionClock.Time,
                 recentTrades = new List<TradeRecord>(),
                 strengthTrades = new List<TradeRecord>()
             };
@@ -129,7 +129,7 @@
             _lastSurgeTime[stock] = 0f;
             _prevWindowVolume[stock] = 0m;
             _currentWindowVolume[stock] = 0m;
-            _windowStartTime[stock] = Time.time;
+            _windowStartTime[stock] = SessionClock.Time;
         }
 
         if (!_sessionTurnover.ContainsKey(stock))
@@ -153,7 +153,7 @@
             if (previousCloses != null && previousCloses.TryGetValue(stock, out decimal close) && close > 0m)
             { stats.prevDayClose = close; }
             stats.dayOpen = stats.dayHigh = stats.dayLow = stock.currentPrice;
-            stats.dayStartTime = Time.time;
+            stats.dayStartTime = SessionClock.Time;
             _stats[stock] = stats;
         }
     }
@@ -198,7 +198,7 @@
         if (stock == null || !_stats.TryGetValue(stock, out var stats))
         { return; }
 
-        float now = Time.time;
+        float now = SessionClock.Time;
         if (price > stats.week52High)
         {
             stats.week52High = price;
@@ -306,7 +306,7 @@
     private const float CLEANUP_INTERVAL = 5f;
     void Update()
     {
-        float now = Time.time;
+        float now = SessionClock.Time;
         _updateKeys.Clear();
         _updateKeys.AddRange(_stats.Keys);
         bool doCleanup = now - _lastCleanupTime >= CLEANUP_INTERVAL;
@@ -366,7 +366,7 @@
         { return 100f; }
 
         float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
-        float now = Time.time;
+        float now = SessionClock.Time;
         float cutoffTime = now - candleDuration;
         stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
         _stats[stock] = stats;
@@ -421,8 +421,8 @@
         dayOpen = s?.currentPrice ?? 0m;
         dayHigh = s?.currentPrice ?? 0m;
         dayLow = s?.currentPrice ?? 0m;
-        dayStartTime = Time.time;
-        week52StartTime = Time.time;
+        dayStartTime = SessionClock.Time;
+        week52StartTime = SessionClock.Time;
         recentTrades = new List<TradeRecord>();
         strengthTrades = new List<TradeRecord>();
     }
```

## Assets/Scripts/TabletViewController.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -96,6 +96,7 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         if (tabletObject == null || viewCamera == null)
         { return; }
         if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
```

## Assets/Scripts/TradeBotManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -200,7 +200,7 @@
         WhaleBot.OnWhalePumpStarted += OnWhalePumpStarted;
         WhaleBot.OnWhaleOperationEnded += OnWhaleOperationEnded;
         WhaleFollowerBot.OnReturnedToPool += OnFollowerReturnedToPool;
-        _lastPopulationCheck = Time.time;
+        _lastPopulationCheck = SessionClock.Time;
         StartCoroutine(PopulationFlowLoop());
         StartCoroutine(OrderCleanupLoop());
     }
```

## Assets/Scripts/Institutions/InstitutionBootstrapper.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -76,8 +76,8 @@
         { yield break; }
 
         // 창고 준비 대기 (없어도 세팅은 하되, 창고가 있으면 반드시 TransferTo로 이동)
-        float t0 = Time.time;
-        while (ExchangeWarehouse.Instance == null && Time.time - t0 < 3f)
+        float t0 = SessionClock.Time;
+        while (ExchangeWarehouse.Instance == null && SessionClock.Time - t0 < 3f)
         {
             yield return null;
         }
```

## Assets/Scripts/Interaction/InteractionManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -45,6 +45,7 @@
 
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { HideHint(); return; }
         if (_cam == null)
         {
             _cam = Camera.main;
```

## Assets/Scripts/Liquidity/LiquidityBotManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -59,8 +59,8 @@
             { yield break; }
         }
 
-        float t0 = Time.time;
-        while (ExchangeWarehouse.Instance == null && Time.time - t0 < 3f)
+        float t0 = SessionClock.Time;
+        while (ExchangeWarehouse.Instance == null && SessionClock.Time - t0 < 3f)
         {
             yield return null;
         }
```

## Assets/Scripts/MarketDay/BotChatData.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -237,8 +237,8 @@
         Context = context.EventId > 0 ? context : new InvestorMessageContext(0, 0, 0f, bot == null ? 1f : bot.Social.Confidence, bot == null ? 0f : bot.Social.Psychology.Trust, false, target: 0);
         Sequence = sequence;
         BotId = bot == null ? 0 : bot.Social.Id;
-        Day = day;
-        Minute = minute;
+        Day = day + minute / 1440;
+        Minute = minute % 1440;
         Nickname = bot == null ? "시장 안내" : bot.Social.Nickname;
         Profile = bot == null ? "공개 종목 상태 안내" : bot.Social.Profile;
         Text = text;
```

## Assets/Scripts/MarketDay/MarketDayController.cs
변경 덩어리: 21

### IEnumerator Start()
원본
```csharp
    IEnumerator Start()
    {
        yield return null;
        yield return null;
        while (Initializers > 0 || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            yield return null;
        }

        if (StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        {
            Debug.LogError("하루 초기화: StockManager/ExchangeWarehouse 필요");
            yield break;
        }

        ExchangeBot.Instance?.InitializeAssets();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!subscribed.Contains(stock))
            {
                stock.OnTradeExecutedWithStock += OnMarketTrade;
                stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
                subscribed.Add(stock);
            }

            eventPrices[stock] = stock.currentPrice;
            if (!holders.ContainsKey(stock))
            {
                holders.Add(stock, new List<TradeBot>());
            }

            if (!recentSellers.ContainsKey(stock))
            {
                recentSellers.Add(stock, new List<TradeBot>(64));
            }
        }

        InitialDistributionComplete = true;
        for (int i = 0; i < Trader.Registry.Count; i++)
        {
            var trader = Trader.Registry[i];
            trader.BeginDailyBaseline();
            if (trader is TradeBot bot)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    UpdatePositionIndex(bot, stock);
                }
            }

            if (i % Mathf.Max(1, botsPerFrame) == 0)
            {
                yield return null;
            }
        }

        Ready = true;
        nextPublish = Time.time + publishMin;
    }
```
수정본
```csharp
    IEnumerator Start()
    {
        yield return null;
        yield return null;
        while (Initializers > 0 || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            yield return null;
        }

        if (StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        {
            Debug.LogError("하루 초기화: StockManager/ExchangeWarehouse 필요");
            yield break;
        }

        ExchangeBot.Instance?.InitializeAssets();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!subscribed.Contains(stock))
            {
                stock.OnTradeExecutedWithStock += OnMarketTrade;
                stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
                subscribed.Add(stock);
            }

            eventPrices[stock] = stock.currentPrice;
            if (!holders.ContainsKey(stock))
            {
                holders.Add(stock, new List<TradeBot>());
            }

            if (!recentSellers.ContainsKey(stock))
            {
                recentSellers.Add(stock, new List<TradeBot>(64));
            }
        }

        InitialDistributionComplete = true;
        for (int i = 0; i < Trader.Registry.Count; i++)
        {
            var trader = Trader.Registry[i];
            trader.BeginDailyBaseline();
            if (trader is TradeBot bot)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    UpdatePositionIndex(bot, stock);
                }
            }

            if (i % Mathf.Max(1, botsPerFrame) == 0)
            {
                yield return null;
            }
        }

        Ready = true;
        nextPublish = SessionClock.Time + publishMin;
    }
```

### public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
원본
```csharp
    public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
    {
        if (!(trader is TradeBot bot) || bot.Social == null || volume <= 0m || basis <= 0m)
        { return; }

        var memory = Memory(bot, stock);
        if (memory == null)
        { return; }

        if (Time.time - memory.SellTime > recentTradeLifetime)
        {
            memory.SellVolume = 0m;
            memory.SellProfit = 0m;
        }

        decimal total = memory.SellVolume + volume;
        memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
        memory.SellVolume = total;
        memory.SellTime = Time.time;
        decimal profit = (price - basis) * volume;
        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), Time.time);
        memory.SellProfit += profit;
        memory.PendingProfit += profit;
        memory.PendingBasis += basis * volume;
        if (!recentSellers.TryGetValue(stock, out var list))
        {
            list = new List<TradeBot>(64);
            recentSellers.Add(stock, list);
        }

        if (!list.Contains(bot))
        {
            if (list.Count >= 64)
            {
                list.RemoveAt(0);
            }

            list.Add(bot);
        }

        Enqueue(profit > 0m ? ChatEvent.ProfitRealized : ChatEvent.LossRealized, stock, bot);
    }
```
수정본
```csharp
    public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
    {
        if (!(trader is TradeBot bot) || bot.Social == null || volume <= 0m || basis <= 0m)
        { return; }

        var memory = Memory(bot, stock);
        if (memory == null)
        { return; }

        if (SessionClock.Time - memory.SellTime > recentTradeLifetime)
        {
            memory.SellVolume = 0m;
            memory.SellProfit = 0m;
        }

        decimal total = memory.SellVolume + volume;
        memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
        memory.SellVolume = total;
        memory.SellTime = SessionClock.Time;
        decimal profit = (price - basis) * volume;
        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), SessionClock.Time);
        memory.SellProfit += profit;
        memory.PendingProfit += profit;
        memory.PendingBasis += basis * volume;
        if (!recentSellers.TryGetValue(stock, out var list))
        {
            list = new List<TradeBot>(64);
            recentSellers.Add(stock, list);
        }

        if (!list.Contains(bot))
        {
            if (list.Count >= 64)
            {
                list.RemoveAt(0);
            }

            list.Add(bot);
        }

        Enqueue(profit > 0m ? ChatEvent.ProfitRealized : ChatEvent.LossRealized, stock, bot);
    }
```

### void Update()
원본
```csharp
    void Update()
    {
        if (!Ready || IntegrityFailed || Time.timeScale <= 0f)
        { return; }

        float dt = Time.deltaTime;
        if (Phase == MarketPhase.Closing)
        {
            elapsed += dt;
            RemainingSeconds = Mathf.Max(0f, postCloseSeconds - elapsed);
        }
        else
        {
            float step = Mathf.Min(dt, Mathf.Max(0f, Duration - elapsed));
            elapsed += step;
            if (Phase == MarketPhase.Trading)
            {
                TradingElapsed += step * (23400.0 / Math.Max(0.1, tradingSeconds));
            }

            RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
            if (RemainingSeconds <= 0f && phaseWork == null)
            {
                phaseWork = StartCoroutine(AdvancePhase());
            }
        }

        using (StressMarker.Auto())
        {
            int count = Mathf.Min(Mathf.Max(1, botsPerFrame), Bots.Count);
            for (int i = 0; i < count && Bots.Count > 0; i++)
            {
                if (stressCursor >= Bots.Count)
                {
                    stressCursor = 0;
                }

                UpdateStress(Bots[stressCursor++]);
            }
        }

        using (ChatMarker.Auto())
        {
            ProcessChat();
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (!Ready || IntegrityFailed || ReportPending || Time.timeScale <= 0f)
        { return; }

        float dt = Time.deltaTime;
        if (Phase == MarketPhase.Closing)
        {
            RemainingSeconds = 0f;
        }
        else
        {
            float step = Mathf.Min(dt, Mathf.Max(0f, Duration - elapsed));
            elapsed += step;
            if (Phase == MarketPhase.Trading)
            {
                TradingElapsed += step * (28800.0 / Math.Max(0.1, tradingSeconds));
            }

            RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
            if (RemainingSeconds <= 0f && phaseWork == null)
            {
                phaseWork = StartCoroutine(AdvancePhase());
            }
        }

        using (StressMarker.Auto())
        {
            int count = Mathf.Min(Mathf.Max(1, botsPerFrame), Bots.Count);
            for (int i = 0; i < count && Bots.Count > 0; i++)
            {
                if (stressCursor >= Bots.Count)
                {
                    stressCursor = 0;
                }

                UpdateStress(Bots[stressCursor++]);
            }
        }

        using (ChatMarker.Auto())
        {
            ProcessChat();
        }
    }
```

### private void ObserveConversationTrade(Stock stock,decimal price,decimal volume,IOrderOwner buyer,IOrderOwner seller,Order resting)
원본
```csharp
    private void ObserveConversationTrade(Stock stock,decimal price,decimal volume,IOrderOwner buyer,IOrderOwner seller,Order resting)
    {
        if (!ReferenceEquals(buyer,seller)) { ConversationFocus.Record(stock,price,volume,Time.time); }
        InvestorSociety.Instance?.ObserveSocialFill(stock, price, volume, buyer, seller, resting);
    }
```
수정본
```csharp
    private void ObserveConversationTrade(Stock stock,decimal price,decimal volume,IOrderOwner buyer,IOrderOwner seller,Order resting)
    {
        if (!ReferenceEquals(buyer,seller)) { ConversationFocus.Record(stock,price,volume,SessionClock.Time); }
        InvestorSociety.Instance?.ObserveSocialFill(stock, price, volume, buyer, seller, resting);
    }
```

### public void Enqueue(ChatEvent kind, Stock stock, TradeBot bot, decimal referencePrice = 0m)
원본
```csharp
    public void Enqueue(ChatEvent kind, Stock stock, TradeBot bot, decimal referencePrice = 0m)
    {
        if (bot != null && bot.Social == null)
        { return; }

        var key = (kind, stock, bot);
        if (eventIndices.ContainsKey(key))
        { return; }

        if (events.Count >= EventLimit || eventOrder.Count >= EventLimit * 2)
        {
            RemoveOldestEvent();
        }

        long ticket = ++eventTicket;
        eventIndices.Add(key, events.Count);
        eventOrder.Enqueue((kind, stock, bot, ticket));
        events.Add(new MarketEvent { Kind = kind, Stock = stock, Bot = bot, Time = Time.time, ReferencePrice = referencePrice, Ticket = ticket });
    }
```
수정본
```csharp
    public void Enqueue(ChatEvent kind, Stock stock, TradeBot bot, decimal referencePrice = 0m)
    {
        if (bot != null && bot.Social == null)
        { return; }

        var key = (kind, stock, bot);
        if (eventIndices.ContainsKey(key))
        { return; }

        if (events.Count >= EventLimit || eventOrder.Count >= EventLimit * 2)
        {
            RemoveOldestEvent();
        }

        long ticket = ++eventTicket;
        eventIndices.Add(key, events.Count);
        eventOrder.Enqueue((kind, stock, bot, ticket));
        events.Add(new MarketEvent { Kind = kind, Stock = stock, Bot = bot, Time = SessionClock.Time, ReferencePrice = referencePrice, Ticket = ticket });
    }
```

### private void ProcessChat()
원본
```csharp
    private void ProcessChat()
    {
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        {
            int budget = Math.Min(candidatesPerFrame, events.Count);
            for (int i = 0; i < budget; i++)
            {
                int index = PickConversationEvent(Time.time); var pending = events[index];
                bool current = Time.time - pending.Time <= eventLifetime;
                current &= pending.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
                current &= pending.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
                current &= pending.Kind != ChatEvent.PriceRise || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice >= pending.ReferencePrice * (1m + (decimal)surgeRatio);
                current &= pending.Kind != ChatEvent.PriceFall || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice <= pending.ReferencePrice * (1m - (decimal)surgeRatio);
                if (current) { InvestorSociety.Instance.OfferMarketEvent(pending.Kind, pending.Stock, pending.Bot); }
                RemoveEvent(index);
            }
            return;
        }
        float now = Time.time;
        while (sentenceExpiry.Count > 0 && now - sentenceExpiry.Peek().time > sentenceCooldown)
        {
            var expired = sentenceExpiry.Dequeue();
            if (recentSentences.TryGetValue(expired.text, out float time) && time == expired.time)
            {
                recentSentences.Remove(expired.text);
            }
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (now - events[i].Time > eventLifetime)
            {
                RemoveEvent(i);
            }
        }

        if (Bots.Count > 0 && now >= nextConversation && (Phase == MarketPhase.Trading || Phase == MarketPhase.PostClose))
        {
            bool activeMarket = Phase == MarketPhase.Trading && ConversationFocus.HasActivity(now);
            nextConversation = now + (activeMarket ? 8f : 15f) + (float)random.NextDouble() * (activeMarket ? 10f : 20f);
            Stock topic = activeMarket && random.NextDouble() < marketConversationChance ? ConversationFocus.Choose(random.NextDouble(),now) : null;
            bool reply = messageCount > 0 && now - lastBotMessageTime < 20f;
            var latest = messageCount > 0 ? GetMessage(messageCount - 1) : default;
            reply &= latest.BotId > 0 && latest.Event != ChatEvent.Agreement && latest.Sequence > lastRepliedSequence;
            reply &= random.NextDouble() < 0.5;
            if (reply)
            {
                replySource = latest;
            }
            Enqueue(topic != null ? ChatEvent.SmallTalk : reply ? ChatEvent.Agreement : ChatEvent.SmallTalk, topic, null);
        }
        if (now < nextPublish || events.Count == 0 || Bots.Count == 0)
        { return; }

        for (int step = 0; step < Mathf.Max(1, candidatesPerFrame) && events.Count > 0; step++)
        {
            int index = PickConversationEvent(now);
            var ev = events[index];
            bool currentEvent = ev.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
            currentEvent &= ev.Kind != ChatEvent.Agreement || replySource.Sequence > lastRepliedSequence && now - ev.Time < 20f;
            currentEvent &= ev.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
            currentEvent &= ev.Kind != ChatEvent.PriceRise || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice >= ev.ReferencePrice * (1m + (decimal)surgeRatio);
            currentEvent &= ev.Kind != ChatEvent.PriceFall || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice <= ev.ReferencePrice * (1m - (decimal)surgeRatio);
            if (!currentEvent)
            {
                RemoveEvent(index);
                continue;
            }

            TradeBot candidate = ev.Bot;
            List<TradeBot> pool = Bots;
            if (ev.Stock != null && candidate == null && ev.Kind != ChatEvent.SmallTalk)
            {
                var map = ev.Kind == ChatEvent.PriceRiseAfterSell ? recentSellers : holders;
                if (!map.TryGetValue(ev.Stock, out pool) || pool.Count == 0)
                {
                    RemoveEvent(index);
                    continue;
                }
            }

            if (candidate == null && pool.Count > 0)
            {
                candidate = pool[(random.Next(pool.Count) + ev.Cursor) % pool.Count];
            }

            ev.Cursor++;
            ev.Candidates++;
            events[index] = ev;
            if (ev.Candidates >= 64 || ev.Bot != null)
            {
                RemoveEvent(index);
            }

            if (candidate == null || !candidate.isActiveAndEnabled || InvestorSociety.Instance != null && !candidate.Social.Psychology.ActiveAt(now) || now - candidate.Social.LastSpeech < botCooldown || now - candidate.Social.LastEvents[(int)ev.Kind] < sameEventCooldown || random.NextDouble() > speechChance)
            { continue; }

            if (ev.Kind == ChatEvent.Agreement && candidate.Social.Id == replySource.BotId)
            { continue; }
            var templates = Catalog.ForEvent(ev.Kind);
            float sum = 0f;
            for (int t = 0; t < templates.Count; t++)
            {
                if (Eligible(candidate, ev.Stock, templates[t]))
                {
                    sum += templates[t].weight;
                }
            }

            if (sum <= 0f)
            { continue; }

            float roll = (float)random.NextDouble() * sum;
            BotChatTemplateCatalog.Entry chosen = null;
            for (int t = 0; t < templates.Count; t++)
            {
                if (!Eligible(candidate, ev.Stock, templates[t]))
                { continue; }

                roll -= templates[t].weight;
                if (roll <= 0f)
                {
                    chosen = templates[t];
                    break;
                }
            }

            if (chosen == null)
            { continue; }

            string text = chosen.text.Replace("{stockName}", ev.Stock == null ? "" : ev.Stock.stockName);
            InvestorSociety.Instance?.ReadBeforeSpeech(candidate);
            if (now - candidate.Social.LastSpeech < botCooldown)
            { continue; }
            text = ev.Kind == ChatEvent.SmallTalk && ev.Stock != null ? InvestorConversation.MarketRemark(candidate,ev.Stock,random.Next()) : BotMind.Compose(candidate, ev.Kind, ev.Stock, text);
            if (ev.Kind == ChatEvent.Agreement)
            {
                text = replySource.Nickname + "님, " + text;
            }
            if (recentSentences.ContainsKey(text))
            { continue; }

            var state = candidate.Social;
            state.LastSpeech = now;
            state.LastEvents[(int)ev.Kind] = now;
            if (state.RecentSpeech.Count >= 16)
            {
                state.RecentSpeech.Dequeue();
            }

            state.RecentSpeech.Enqueue((chosen.id, now));
            if (sentenceExpiry.Count >= messages.Length)
            {
                var oldest = sentenceExpiry.Dequeue();
                recentSentences.Remove(oldest.text);
            }

            sentenceExpiry.Enqueue((text, now));
            recentSentences[text] = now;
            if (ev.Kind == ChatEvent.Agreement)
            {
                lastRepliedSequence = replySource.Sequence;
            }
            AddMessage(new BotChatMessage(++sequence, candidate, Day, GameMinute, text, chosen.id, ev.Kind));
            nextPublish = now + (float)(publishMin + random.NextDouble() * Math.Max(0, publishMax - publishMin));
            return;
        }
    }
```
수정본
```csharp
    private void ProcessChat()
    {
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        {
            int budget = Math.Min(candidatesPerFrame, events.Count);
            for (int i = 0; i < budget; i++)
            {
                int index = PickConversationEvent(SessionClock.Time); var pending = events[index];
                bool current = SessionClock.Time - pending.Time <= eventLifetime;
                current &= pending.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
                current &= pending.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
                current &= pending.Kind != ChatEvent.PriceRise || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice >= pending.ReferencePrice * (1m + (decimal)surgeRatio);
                current &= pending.Kind != ChatEvent.PriceFall || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice <= pending.ReferencePrice * (1m - (decimal)surgeRatio);
                if (current) { InvestorSociety.Instance.OfferMarketEvent(pending.Kind, pending.Stock, pending.Bot); }
                RemoveEvent(index);
            }
            return;
        }
        float now = SessionClock.Time;
        while (sentenceExpiry.Count > 0 && now - sentenceExpiry.Peek().time > sentenceCooldown)
        {
            var expired = sentenceExpiry.Dequeue();
            if (recentSentences.TryGetValue(expired.text, out float time) && time == expired.time)
            {
                recentSentences.Remove(expired.text);
            }
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (now - events[i].Time > eventLifetime)
            {
                RemoveEvent(i);
            }
        }

        if (Bots.Count > 0 && now >= nextConversation && (Phase == MarketPhase.Trading || Phase == MarketPhase.PostClose))
        {
            bool activeMarket = Phase == MarketPhase.Trading && ConversationFocus.HasActivity(now);
            nextConversation = now + (activeMarket ? 8f : 15f) + (float)random.NextDouble() * (activeMarket ? 10f : 20f);
            Stock topic = activeMarket && random.NextDouble() < marketConversationChance ? ConversationFocus.Choose(random.NextDouble(),now) : null;
            bool reply = messageCount > 0 && now - lastBotMessageTime < 20f;
            var latest = messageCount > 0 ? GetMessage(messageCount - 1) : default;
            reply &= latest.BotId > 0 && latest.Event != ChatEvent.Agreement && latest.Sequence > lastRepliedSequence;
            reply &= random.NextDouble() < 0.5;
            if (reply)
            {
                replySource = latest;
            }
            Enqueue(topic != null ? ChatEvent.SmallTalk : reply ? ChatEvent.Agreement : ChatEvent.SmallTalk, topic, null);
        }
        if (now < nextPublish || events.Count == 0 || Bots.Count == 0)
        { return; }

        for (int step = 0; step < Mathf.Max(1, candidatesPerFrame) && events.Count > 0; step++)
        {
            int index = PickConversationEvent(now);
            var ev = events[index];
            bool currentEvent = ev.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
            currentEvent &= ev.Kind != ChatEvent.Agreement || replySource.Sequence > lastRepliedSequence && now - ev.Time < 20f;
            currentEvent &= ev.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
            currentEvent &= ev.Kind != ChatEvent.PriceRise || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice >= ev.ReferencePrice * (1m + (decimal)surgeRatio);
            currentEvent &= ev.Kind != ChatEvent.PriceFall || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice <= ev.ReferencePrice * (1m - (decimal)surgeRatio);
            if (!currentEvent)
            {
                RemoveEvent(index);
                continue;
            }

            TradeBot candidate = ev.Bot;
            List<TradeBot> pool = Bots;
            if (ev.Stock != null && candidate == null && ev.Kind != ChatEvent.SmallTalk)
            {
                var map = ev.Kind == ChatEvent.PriceRiseAfterSell ? recentSellers : holders;
                if (!map.TryGetValue(ev.Stock, out pool) || pool.Count == 0)
                {
                    RemoveEvent(index);
                    continue;
                }
            }

            if (candidate == null && pool.Count > 0)
            {
                candidate = pool[(random.Next(pool.Count) + ev.Cursor) % pool.Count];
            }

            ev.Cursor++;
            ev.Candidates++;
            events[index] = ev;
            if (ev.Candidates >= 64 || ev.Bot != null)
            {
                RemoveEvent(index);
            }

            if (candidate == null || !candidate.isActiveAndEnabled || InvestorSociety.Instance != null && !candidate.Social.Psychology.ActiveAt(now) || now - candidate.Social.LastSpeech < botCooldown || now - candidate.Social.LastEvents[(int)ev.Kind] < sameEventCooldown || random.NextDouble() > speechChance)
            { continue; }

            if (ev.Kind == ChatEvent.Agreement && candidate.Social.Id == replySource.BotId)
            { continue; }
            var templates = Catalog.ForEvent(ev.Kind);
            float sum = 0f;
            for (int t = 0; t < templates.Count; t++)
            {
                if (Eligible(candidate, ev.Stock, templates[t]))
                {
                    sum += templates[t].weight;
                }
            }

            if (sum <= 0f)
            { continue; }

            float roll = (float)random.NextDouble() * sum;
            BotChatTemplateCatalog.Entry chosen = null;
            for (int t = 0; t < templates.Count; t++)
            {
                if (!Eligible(candidate, ev.Stock, templates[t]))
                { continue; }

                roll -= templates[t].weight;
                if (roll <= 0f)
                {
                    chosen = templates[t];
                    break;
                }
            }

            if (chosen == null)
            { continue; }

            string text = chosen.text.Replace("{stockName}", ev.Stock == null ? "" : ev.Stock.stockName);
            InvestorSociety.Instance?.ReadBeforeSpeech(candidate);
            if (now - candidate.Social.LastSpeech < botCooldown)
            { continue; }
            text = ev.Kind == ChatEvent.SmallTalk && ev.Stock != null ? InvestorConversation.MarketRemark(candidate,ev.Stock,random.Next()) : BotMind.Compose(candidate, ev.Kind, ev.Stock, text);
            if (ev.Kind == ChatEvent.Agreement)
            {
                text = replySource.Nickname + "님, " + text;
            }
            if (recentSentences.ContainsKey(text))
            { continue; }

            var state = candidate.Social;
            state.LastSpeech = now;
            state.LastEvents[(int)ev.Kind] = now;
            if (state.RecentSpeech.Count >= 16)
            {
                state.RecentSpeech.Dequeue();
            }

            state.RecentSpeech.Enqueue((chosen.id, now));
            if (sentenceExpiry.Count >= messages.Length)
            {
                var oldest = sentenceExpiry.Dequeue();
                recentSentences.Remove(oldest.text);
            }

            sentenceExpiry.Enqueue((text, now));
            recentSentences[text] = now;
            if (ev.Kind == ChatEvent.Agreement)
            {
                lastRepliedSequence = replySource.Sequence;
            }
            AddMessage(new BotChatMessage(++sequence, candidate, Day, GameMinute, text, chosen.id, ev.Kind));
            nextPublish = now + (float)(publishMin + random.NextDouble() * Math.Max(0, publishMax - publishMin));
            return;
        }
    }
```

### public void PublishParticipant(TradeBot bot, string text, InvestorMessageContext context = default)
원본
```csharp
    public void PublishParticipant(TradeBot bot, string text, InvestorMessageContext context = default)
    {
        if (bot == null || bot.Social == null || string.IsNullOrWhiteSpace(text))
        { return; }
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        { InvestorSociety.Instance.OfferExistingSpeech(bot, 0, context, ChatEvent.Agreement); return; }
        AddMessage(new BotChatMessage(++sequence, bot, Day, GameMinute, text, "contact", ChatEvent.Agreement, context));
        bot.Social.LastSpeech = Time.time;
    }
```
수정본
```csharp
    public void PublishParticipant(TradeBot bot, string text, InvestorMessageContext context = default)
    {
        if (bot == null || bot.Social == null || string.IsNullOrWhiteSpace(text))
        { return; }
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        { InvestorSociety.Instance.OfferExistingSpeech(bot, 0, context, ChatEvent.Agreement); return; }
        AddMessage(new BotChatMessage(++sequence, bot, Day, GameMinute, text, "contact", ChatEvent.Agreement, context));
        bot.Social.LastSpeech = SessionClock.Time;
    }
```

### public void StartMorningAfterSleep()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public void StartMorningAfterSleep()
    {
        if (Phase != MarketPhase.PostClose || phaseWork != null) { return; }
        ReportPending = false; Day++; Phase = MarketPhase.PreOpen; elapsed = 0f; RemainingSeconds = Duration;
    }
```

### public bool Eligible(TradeBot bot, Stock stock, BotChatTemplateCatalog.Entry entry)
원본
```csharp
    public bool Eligible(TradeBot bot, Stock stock, BotChatTemplateCatalog.Entry entry)
    {
        var state = bot.Social;
        if (state == null || state.Stress < entry.minStress || state.Stress > entry.maxStress || entry.toneKind != state.Tone || (entry.personalityKind != BotPersonality.Any && entry.personalityKind != state.Personality))
        { return false; }

        bool held = stock != null && bot.GetOwnedVolume(stock) > 0m;
        if ((entry.holdingKind == HoldingCondition.Holding && !held) || (entry.holdingKind == HoldingCondition.NotHolding && held) || (entry.text.Contains("{stockName}") && stock == null))
        { return false; }

        float now = Time.time;
        foreach (var speech in state.RecentSpeech)
        {
            if (speech.id == entry.id && now - speech.time < sentenceCooldown)
            { return false; }
        }

        BotSocialState.PositionMemory memory = null;
        if (stock != null)
        {
            state.Positions.TryGetValue(stock, out memory);
        }

        decimal cost = held ? bot.GetOwnedAverage(stock) : 0m;
        decimal profit = held ? stock.currentPrice - cost : 0m;
        foreach (var requirement in entry.facts)
        {
            bool recent = memory != null && now - memory.SellTime <= recentTradeLifetime;
            bool valid = requirement switch
            {
                ChatRequirement.UnrealizedProfitPositive => held && cost > 0m && profit > 0m,
                ChatRequirement.UnrealizedProfitNegative => held && cost > 0m && profit < 0m,
                ChatRequirement.CrossedUnrealizedLossToNonNegative => held && profit >= 0m && memory != null && now - memory.CrossTime <= eventLifetime,
                ChatRequirement.RecentProfitableSell => recent && memory.SellProfit > 0m,
                ChatRequirement.RecentLosingSell => recent && memory.SellProfit < 0m,
                ChatRequirement.RecentSellThenPriceRise => recent && memory.SellPrice > 0m && stock.currentPrice >= memory.SellPrice * (1m + (decimal)afterSellRiseRatio),
                ChatRequirement.DailyReturnPositive => bot.DailySettledDay == Day && bot.DailyReturn > 0m,
                ChatRequirement.DailyReturnNegative => bot.DailySettledDay == Day && bot.DailyReturn < 0m,
                ChatRequirement.RecentStressDrop => stock == state.LastDropStock && now - state.LastDropTime <= eventLifetime && state.LastDropAmount >= stressDropThreshold,
                _ => false
            };
            if (!valid)
            { return false; }
        }

        return true;
    }
```
수정본
```csharp
    public bool Eligible(TradeBot bot, Stock stock, BotChatTemplateCatalog.Entry entry)
    {
        var state = bot.Social;
        if (state == null || state.Stress < entry.minStress || state.Stress > entry.maxStress || entry.toneKind != state.Tone || (entry.personalityKind != BotPersonality.Any && entry.personalityKind != state.Personality))
        { return false; }

        bool held = stock != null && bot.GetOwnedVolume(stock) > 0m;
        if ((entry.holdingKind == HoldingCondition.Holding && !held) || (entry.holdingKind == HoldingCondition.NotHolding && held) || (entry.text.Contains("{stockName}") && stock == null))
        { return false; }

        float now = SessionClock.Time;
        foreach (var speech in state.RecentSpeech)
        {
            if (speech.id == entry.id && now - speech.time < sentenceCooldown)
            { return false; }
        }

        BotSocialState.PositionMemory memory = null;
        if (stock != null)
        {
            state.Positions.TryGetValue(stock, out memory);
        }

        decimal cost = held ? bot.GetOwnedAverage(stock) : 0m;
        decimal profit = held ? stock.currentPrice - cost : 0m;
        foreach (var requirement in entry.facts)
        {
            bool recent = memory != null && now - memory.SellTime <= recentTradeLifetime;
            bool valid = requirement switch
            {
                ChatRequirement.UnrealizedProfitPositive => held && cost > 0m && profit > 0m,
                ChatRequirement.UnrealizedProfitNegative => held && cost > 0m && profit < 0m,
                ChatRequirement.CrossedUnrealizedLossToNonNegative => held && profit >= 0m && memory != null && now - memory.CrossTime <= eventLifetime,
                ChatRequirement.RecentProfitableSell => recent && memory.SellProfit > 0m,
                ChatRequirement.RecentLosingSell => recent && memory.SellProfit < 0m,
                ChatRequirement.RecentSellThenPriceRise => recent && memory.SellPrice > 0m && stock.currentPrice >= memory.SellPrice * (1m + (decimal)afterSellRiseRatio),
                ChatRequirement.DailyReturnPositive => bot.DailySettledDay == Day && bot.DailyReturn > 0m,
                ChatRequirement.DailyReturnNegative => bot.DailySettledDay == Day && bot.DailyReturn < 0m,
                ChatRequirement.RecentStressDrop => stock == state.LastDropStock && now - state.LastDropTime <= eventLifetime && state.LastDropAmount >= stressDropThreshold,
                _ => false
            };
            if (!valid)
            { return false; }
        }

        return true;
    }
```

### private IEnumerator AdvancePhase()
원본
```csharp
    private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0)
                    {
                        yield return null;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                ClosingPrices[stock] = stock.currentPrice;
            }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
    }
```
수정본
```csharp
    private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0)
                    {
                        yield return null;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                ClosingPrices[stock] = stock.currentPrice;
            }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            elapsed = 0f; ReportPending = true;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            PlayerRestController.Instance?.NightPassedAwake();
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
        if (ReportPending)
        {
            PlayerRestController.Instance?.ObserveClock();
            PauseMenuController.Instance?.ReceiveClosingReport();
        }
    }
```

### public void UpdateStress(TradeBot bot, bool force = false)
원본
```csharp
    public void UpdateStress(TradeBot bot, bool force = false)
    {
        if (bot == null || bot.Social == null)
        { return; }

        var state = bot.Social;
        double seconds = TradingElapsed - state.LastTradingSeconds;
        if (!force && Time.time - state.LastSampleTime < stressSampleSeconds)
        { return; }

        state.LastSampleTime = Time.time;
        state.LastTradingSeconds = TradingElapsed;
        float before = state.Stress;
        Stock reliefStock = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            decimal qty = bot.GetOwnedVolume(stock);
            BotSocialState.PositionMemory memory;
            if (qty > 0m)
            {
                memory = Memory(bot, stock);
            }
            else
            {
                state.Positions.TryGetValue(stock, out memory);
            }

            if (memory == null)
            { continue; }

            double heldSeconds = Math.Min(seconds, Math.Max(0.0, TradingElapsed - memory.AcquiredTradingSeconds));
            float positionStressBefore = state.Stress;
            decimal cost = bot.GetOwnedAverage(stock);
            decimal rate = qty > 0m && cost > 0m ? (stock.currentPrice - cost) / cost : 0m;
            if (qty > 0m && cost > 0m)
            {
                if (rate < 0m)
                {
                    memory.LossSeconds += heldSeconds;
                    state.Stress += (float)(heldSeconds / 3600.0) * (lossPerGameHour + (float)(-rate) * severityPerGameHour) * state.Sensitivity;
                }

                if (memory.Observed)
                {
                    if (memory.WasLoss && rate >= 0m)
                    {
                        state.Stress -= breakEvenDrop * state.Recovery;
                        memory.CrossTime = Time.time;
                        Enqueue(ChatEvent.BreakEvenRecovered, stock, bot);
                    }

                    if ((memory.LastPrice > 0m && stock.currentPrice >= memory.LastPrice * (1m + (decimal)surgeRatio)) || (memory.LastReturn < 0m && rate - memory.LastReturn >= (decimal)recoveryRatio))
                    {
                        state.Stress -= recoveryDrop * state.Recovery;
                    }
                }

                memory.WasLoss = rate < 0m;
                memory.Observed = true;
                memory.LastReturn = rate;
                memory.LastPrice = stock.currentPrice;
                if (!memory.WasLoss)
                {
                    memory.LossSeconds = 0;
                }

                if (rate != 0m)
                {
                    Enqueue(rate < 0m ? ChatEvent.HoldingLoss : ChatEvent.HoldingProfit, stock, bot);
                }
            }
            else
            {
                memory.Observed = false;
                memory.WasLoss = false;
                memory.LossSeconds = 0;
            }

            if (memory.PendingBasis > 0m && Time.time - memory.LastRealizedResponse >= realizedAggregationSeconds)
            {
                float reaction = Mathf.Clamp((float)(memory.PendingProfit / memory.PendingBasis) * realizedScale, -10f, 10f);
                state.Stress -= reaction * (reaction > 0f ? state.Recovery : state.Sensitivity);
                memory.PendingProfit = 0m;
                memory.PendingBasis = 0m;
                memory.LastRealizedResponse = Time.time;
            }

            if (state.Stress < positionStressBefore)
            {
                reliefStock = stock;
            }
        }

        state.Stress = Mathf.Clamp(state.Stress, 0f, 100f);
        if (before - state.Stress >= stressDropThreshold)
        {
            state.LastDropTime = Time.time;
            state.LastDropAmount = before - state.Stress;
            state.LastDropStock = reliefStock;
            Enqueue(ChatEvent.StressRelief, reliefStock, bot);
        }
    }
```
수정본
```csharp
    public void UpdateStress(TradeBot bot, bool force = false)
    {
        if (bot == null || bot.Social == null)
        { return; }

        var state = bot.Social;
        double seconds = TradingElapsed - state.LastTradingSeconds;
        if (!force && SessionClock.Time - state.LastSampleTime < stressSampleSeconds)
        { return; }

        state.LastSampleTime = SessionClock.Time;
        state.LastTradingSeconds = TradingElapsed;
        float before = state.Stress;
        Stock reliefStock = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            decimal qty = bot.GetOwnedVolume(stock);
            BotSocialState.PositionMemory memory;
            if (qty > 0m)
            {
                memory = Memory(bot, stock);
            }
            else
            {
                state.Positions.TryGetValue(stock, out memory);
            }

            if (memory == null)
            { continue; }

            double heldSeconds = Math.Min(seconds, Math.Max(0.0, TradingElapsed - memory.AcquiredTradingSeconds));
            float positionStressBefore = state.Stress;
            decimal cost = bot.GetOwnedAverage(stock);
            decimal rate = qty > 0m && cost > 0m ? (stock.currentPrice - cost) / cost : 0m;
            if (qty > 0m && cost > 0m)
            {
                if (rate < 0m)
                {
                    memory.LossSeconds += heldSeconds;
                    state.Stress += (float)(heldSeconds / 3600.0) * (lossPerGameHour + (float)(-rate) * severityPerGameHour) * state.Sensitivity;
                }

                if (memory.Observed)
                {
                    if (memory.WasLoss && rate >= 0m)
                    {
                        state.Stress -= breakEvenDrop * state.Recovery;
                        memory.CrossTime = SessionClock.Time;
                        Enqueue(ChatEvent.BreakEvenRecovered, stock, bot);
                    }

                    if ((memory.LastPrice > 0m && stock.currentPrice >= memory.LastPrice * (1m + (decimal)surgeRatio)) || (memory.LastReturn < 0m && rate - memory.LastReturn >= (decimal)recoveryRatio))
                    {
                        state.Stress -= recoveryDrop * state.Recovery;
                    }
                }

                memory.WasLoss = rate < 0m;
                memory.Observed = true;
                memory.LastReturn = rate;
                memory.LastPrice = stock.currentPrice;
                if (!memory.WasLoss)
                {
                    memory.LossSeconds = 0;
                }

                if (rate != 0m)
                {
                    Enqueue(rate < 0m ? ChatEvent.HoldingLoss : ChatEvent.HoldingProfit, stock, bot);
                }
            }
            else
            {
                memory.Observed = false;
                memory.WasLoss = false;
                memory.LossSeconds = 0;
            }

            if (memory.PendingBasis > 0m && SessionClock.Time - memory.LastRealizedResponse >= realizedAggregationSeconds)
            {
                float reaction = Mathf.Clamp((float)(memory.PendingProfit / memory.PendingBasis) * realizedScale, -10f, 10f);
                state.Stress -= reaction * (reaction > 0f ? state.Recovery : state.Sensitivity);
                memory.PendingProfit = 0m;
                memory.PendingBasis = 0m;
                memory.LastRealizedResponse = SessionClock.Time;
            }

            if (state.Stress < positionStressBefore)
            {
                reliefStock = stock;
            }
        }

        state.Stress = Mathf.Clamp(state.Stress, 0f, 100f);
        if (before - state.Stress >= stressDropThreshold)
        {
            state.LastDropTime = SessionClock.Time;
            state.LastDropAmount = before - state.Stress;
            state.LastDropStock = reliefStock;
            Enqueue(ChatEvent.StressRelief, reliefStock, bot);
        }
    }
```

### private void AddMessage(BotChatMessage message)
원본
```csharp
    private void AddMessage(BotChatMessage message)
    {
        if (message.BotId > 0)
        {
            lastBotMessageTime = Time.time;
        }
        if (messageCount == messages.Length)
        {
            messageStart = (messageStart + 1) % messages.Length;
            messageCount--;
        }

        messages[(messageStart + messageCount++) % messages.Length] = message;
    }
```
수정본
```csharp
    private void AddMessage(BotChatMessage message)
    {
        if (message.BotId > 0)
        {
            lastBotMessageTime = SessionClock.Time;
        }
        if (messageCount == messages.Length)
        {
            messageStart = (messageStart + 1) % messages.Length;
            messageCount--;
        }

        messages[(messageStart + messageCount++) % messages.Length] = message;
    }
```

### public void AcknowledgeReport()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public void AcknowledgeReport() { ReportPending = false; }
```

### void Awake()
원본
```csharp
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ChatSocialDebugUnity.ApplySeed(this);
        random = new System.Random(seed);
        ConversationFocus.ExplorationShare = topicExplorationShare; ConversationFocus.RefreshSeconds = topicRefreshSeconds;
        ConversationFocus.GameMinutes = () => (Day - 1) * 1440d + GameMinute;
        UnityEngine.Random.InitState(seed);
        messages = new BotChatMessage[Mathf.Clamp(messageLimit, 16, 1024)];
        RemainingSeconds = preOpenSeconds;
        var asset = Resources.Load<TextAsset>("Chat/BotChatTemplates.ko");
        Catalog = new BotChatTemplateCatalog(asset == null ? "" : asset.text, Debug.LogWarning);
        StockManager.OnStockDelisted += OnDelisted;
        StockManager.OnStockListed += OnListed;
    }
```
수정본
```csharp
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (GetComponent<PlayerRestController>() == null) { gameObject.AddComponent<PlayerRestController>(); }
        ChatSocialDebugUnity.ApplySeed(this);
        random = new System.Random(seed);
        ConversationFocus.ExplorationShare = topicExplorationShare; ConversationFocus.RefreshSeconds = topicRefreshSeconds;
        ConversationFocus.GameMinutes = () => (Day - 1) * 1440d + GameMinute;
        UnityEngine.Random.InitState(seed);
        messages = new BotChatMessage[Mathf.Clamp(messageLimit, 16, 1024)];
        RemainingSeconds = preOpenSeconds;
        var asset = Resources.Load<TextAsset>("Chat/BotChatTemplates.ko");
        Catalog = new BotChatTemplateCatalog(asset == null ? "" : asset.text, Debug.LogWarning);
        StockManager.OnStockDelisted += OnDelisted;
        StockManager.OnStockListed += OnListed;
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -64,6 +64,8 @@
     public bool Ready { get; private set; }
     public bool IntegrityFailed { get; private set; }
     public bool InitialDistributionComplete { get; private set; }
+    public bool ReportPending { get; private set; }
+    public bool CheckpointReady => Ready && Phase == MarketPhase.PostClose && ReportPending && elapsed == 0f && phaseWork == null;
 
     public readonly Dictionary<Stock, decimal> ClosingPrices = new();
     public readonly List<TradeBot> Bots = new(2000);
@@ -107,22 +109,35 @@
     public long MessageVersion => sequence;
     public int PendingEvents => events.Count;
 
-    public int GameMinute
+    // Overnight minutes deliberately extend past 1440, keeping social/event time monotonic.
+    public int GameMinute => (int)ExactGameMinute;
+    public int ClockMinute => GameMinute % 1440;
+    public int CalendarDay => Day + GameMinute / 1440;
+    public double AbsoluteGameMinute => (Day - 1) * 1440d + ExactGameMinute;
+    public double ExactGameMinute
     {
         get
         {
             float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, Duration));
             if (Phase == MarketPhase.PreOpen)
-            { return 8 * 60 + (int)(60f * progress); }
+            { return 8 * 60 + 60d * progress; }
 
             if (Phase == MarketPhase.Trading)
-            { return 9 * 60 + (int)(390f * progress); }
-
-            return 15 * 60 + 30 + (int)(150f * progress);
-        }
-    }
-
-    private float Duration => Phase == MarketPhase.PreOpen ? preOpenSeconds : Phase == MarketPhase.Trading ? tradingSeconds : postCloseSeconds;
+            { return 9 * 60 + 480d * progress; }
+
+            return Phase == MarketPhase.Closing ? 1020d : 1020d + 900d * progress;
+        }
+    }
+
+    private float Duration => Phase == MarketPhase.PreOpen ? (Day == 1 ? preOpenSeconds : tradingSeconds / 8f) :
+        Phase == MarketPhase.Trading ? tradingSeconds : tradingSeconds * 15f / 8f;
+
+    public void AcknowledgeReport() { ReportPending = false; }
+    public void StartMorningAfterSleep()
+    {
+        if (Phase != MarketPhase.PostClose || phaseWork != null) { return; }
+        ReportPending = false; Day++; Phase = MarketPhase.PreOpen; elapsed = 0f; RemainingSeconds = Duration;
+    }
 
     void Awake()
     {
@@ -133,6 +148,7 @@
         }
 
         Instance = this;
+        if (GetComponent<PlayerRestController>() == null) { gameObject.AddComponent<PlayerRestController>(); }
         ChatSocialDebugUnity.ApplySeed(this);
         random = new System.Random(seed);
         ConversationFocus.ExplorationShare = topicExplorationShare; ConversationFocus.RefreshSeconds = topicRefreshSeconds;
@@ -205,7 +221,7 @@
         }
 
         Ready = true;
-        nextPublish = Time.time + publishMin;
+        nextPublish = SessionClock.Time + publishMin;
     }
 
     public bool Register(TradeBot bot)
@@ -352,7 +368,7 @@
         if (memory == null)
         { return; }
 
-        if (Time.time - memory.SellTime > recentTradeLifetime)
+        if (SessionClock.Time - memory.SellTime > recentTradeLifetime)
         {
             memory.SellVolume = 0m;
             memory.SellProfit = 0m;
@@ -361,9 +377,9 @@
         decimal total = memory.SellVolume + volume;
         memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
         memory.SellVolume = total;
-        memory.SellTime = Time.time;
+        memory.SellTime = SessionClock.Time;
         decimal profit = (price - basis) * volume;
-        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), Time.time);
+        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), SessionClock.Time);
         memory.SellProfit += profit;
         memory.PendingProfit += profit;
         memory.PendingBasis += basis * volume;
@@ -396,14 +412,13 @@
 
     void Update()
     {
-        if (!Ready || IntegrityFailed || Time.timeScale <= 0f)
+        if (!Ready || IntegrityFailed || ReportPending || Time.timeScale <= 0f)
         { return; }
 
         float dt = Time.deltaTime;
         if (Phase == MarketPhase.Closing)
         {
-            elapsed += dt;
-            RemainingSeconds = Mathf.Max(0f, postCloseSeconds - elapsed);
+            RemainingSeconds = 0f;
         }
         else
         {
@@ -411,7 +426,7 @@
             elapsed += step;
             if (Phase == MarketPhase.Trading)
             {
-                TradingElapsed += step * (23400.0 / Math.Max(0.1, tradingSeconds));
+                TradingElapsed += step * (28800.0 / Math.Max(0.1, tradingSeconds));
             }
 
             RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
@@ -583,10 +598,12 @@
             closingTraders.Clear();
             if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
             Phase = MarketPhase.PostClose;
+            elapsed = 0f; ReportPending = true;
             Enqueue(ChatEvent.MarketClose, null, null);
         }
         else
         {
+            PlayerRestController.Instance?.NightPassedAwake();
             Day++;
             Phase = MarketPhase.PreOpen;
             elapsed = 0f;
@@ -596,6 +613,11 @@
         // StartCoroutine can complete synchronously; always yield before clearing its handle.
         yield return null;
         phaseWork = null;
+        if (ReportPending)
+        {
+            PlayerRestController.Instance?.ObserveClock();
+            PauseMenuController.Instance?.ReceiveClosingReport();
+        }
     }
 
     public void UpdateStress(TradeBot bot, bool force = false)
@@ -605,10 +627,10 @@
 
         var state = bot.Social;
         double seconds = TradingElapsed - state.LastTradingSeconds;
-        if (!force && Time.time - state.LastSampleTime < stressSampleSeconds)
-        { return; }
-
-        state.LastSampleTime = Time.time;
+        if (!force && SessionClock.Time - state.LastSampleTime < stressSampleSeconds)
+        { return; }
+
+        state.LastSampleTime = SessionClock.Time;
         state.LastTradingSeconds = TradingElapsed;
         float before = state.Stress;
         Stock reliefStock = null;
@@ -645,7 +667,7 @@
                     if (memory.WasLoss && rate >= 0m)
                     {
                         state.Stress -= breakEvenDrop * state.Recovery;
-                        memory.CrossTime = Time.time;
+                        memory.CrossTime = SessionClock.Time;
                         Enqueue(ChatEvent.BreakEvenRecovered, stock, bot);
                     }
 
@@ -676,13 +698,13 @@
                 memory.LossSeconds = 0;
             }
 
-            if (memory.PendingBasis > 0m && Time.time - memory.LastRealizedResponse >= realizedAggregationSeconds)
+            if (memory.PendingBasis > 0m && SessionClock.Time - memory.LastRealizedResponse >= realizedAggregationSeconds)
             {
                 float reaction = Mathf.Clamp((float)(memory.PendingProfit / memory.PendingBasis) * realizedScale, -10f, 10f);
                 state.Stress -= reaction * (reaction > 0f ? state.Recovery : state.Sensitivity);
                 memory.PendingProfit = 0m;
                 memory.PendingBasis = 0m;
-                memory.LastRealizedResponse = Time.time;
+                memory.LastRealizedResponse = SessionClock.Time;
             }
 
             if (state.Stress < positionStressBefore)
@@ -694,7 +716,7 @@
         state.Stress = Mathf.Clamp(state.Stress, 0f, 100f);
         if (before - state.Stress >= stressDropThreshold)
         {
-            state.LastDropTime = Time.time;
+            state.LastDropTime = SessionClock.Time;
             state.LastDropAmount = before - state.Stress;
             state.LastDropStock = reliefStock;
             Enqueue(ChatEvent.StressRelief, reliefStock, bot);
@@ -727,7 +749,7 @@
 
     private void ObserveConversationTrade(Stock stock,decimal price,decimal volume,IOrderOwner buyer,IOrderOwner seller,Order resting)
     {
-        if (!ReferenceEquals(buyer,seller)) { ConversationFocus.Record(stock,price,volume,Time.time); }
+        if (!ReferenceEquals(buyer,seller)) { ConversationFocus.Record(stock,price,volume,SessionClock.Time); }
         InvestorSociety.Instance?.ObserveSocialFill(stock, price, volume, buyer, seller, resting);
     }
     private int PickConversationEvent(float now)
@@ -792,7 +814,7 @@
         long ticket = ++eventTicket;
         eventIndices.Add(key, events.Count);
         eventOrder.Enqueue((kind, stock, bot, ticket));
-        events.Add(new MarketEvent { Kind = kind, Stock = stock, Bot = bot, Time = Time.time, ReferencePrice = referencePrice, Ticket = ticket });
+        events.Add(new MarketEvent { Kind = kind, Stock = stock, Bot = bot, Time = SessionClock.Time, ReferencePrice = referencePrice, Ticket = ticket });
     }
 
     private void RemoveOldestEvent()
@@ -815,8 +837,8 @@
             int budget = Math.Min(candidatesPerFrame, events.Count);
             for (int i = 0; i < budget; i++)
             {
-                int index = PickConversationEvent(Time.time); var pending = events[index];
-                bool current = Time.time - pending.Time <= eventLifetime;
+                int index = PickConversationEvent(SessionClock.Time); var pending = events[index];
+                bool current = SessionClock.Time - pending.Time <= eventLifetime;
                 current &= pending.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
                 current &= pending.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
                 current &= pending.Kind != ChatEvent.PriceRise || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice >= pending.ReferencePrice * (1m + (decimal)surgeRatio);
@@ -826,7 +848,7 @@
             }
             return;
         }
-        float now = Time.time;
+        float now = SessionClock.Time;
         while (sentenceExpiry.Count > 0 && now - sentenceExpiry.Peek().time > sentenceCooldown)
         {
             var expired = sentenceExpiry.Dequeue();
@@ -984,7 +1006,7 @@
         if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
         { InvestorSociety.Instance.OfferExistingSpeech(bot, 0, context, ChatEvent.Agreement); return; }
         AddMessage(new BotChatMessage(++sequence, bot, Day, GameMinute, text, "contact", ChatEvent.Agreement, context));
-        bot.Social.LastSpeech = Time.time;
+        bot.Social.LastSpeech = SessionClock.Time;
     }
     public void PublishSocialParticipant(TradeBot bot, SocialMessage message, InvestorMessageContext context)
     {
@@ -1013,7 +1035,7 @@
         if ((entry.holdingKind == HoldingCondition.Holding && !held) || (entry.holdingKind == HoldingCondition.NotHolding && held) || (entry.text.Contains("{stockName}") && stock == null))
         { return false; }
 
-        float now = Time.time;
+        float now = SessionClock.Time;
         foreach (var speech in state.RecentSpeech)
         {
             if (speech.id == entry.id && now - speech.time < sentenceCooldown)
@@ -1055,7 +1077,7 @@
     {
         if (message.BotId > 0)
         {
-            lastBotMessageTime = Time.time;
+            lastBotMessageTime = SessionClock.Time;
         }
         if (messageCount == messages.Length)
         {
```

## Assets/Scripts/MarketDay/MarketWallClock.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -32,7 +32,7 @@
     {
         if (ownedMesh == null)
         { return; }
-        int minute = MarketDayController.Instance == null ? 480 : MarketDayController.Instance.GameMinute;
+        int minute = MarketDayController.Instance == null ? 480 : MarketDayController.Instance.ClockMinute;
         minute = Mathf.Clamp(minute,0,1439);
         if (minute == DisplayMinute)
         { return; }
```

## Assets/Scripts/MarketDay/TabletGroupChatUI.cs
변경 덩어리: 4

### void Update()
원본
```csharp
    void Update()
    {
        if (localeRevision != GameLocale.Revision)
        { localeRevision = GameLocale.Revision; mentionRosterDirty = true; }
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            GameLocalizedText.Set(participants,lastParticipantCount.ToString("N0"));
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            GameLocalizedText.Set(hudStatus,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                GameLocalizedText.Set(status,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        GameLocalizedText.Set(row.Nickname,message.Nickname,true);
                        GameLocalizedText.Set(row.Time,$"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}");
                        GameLocalizedText.Set(row.Text,DisplayMessage(message),true);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(GameLocale.Text(row.Nickname.text), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(GameLocale.Text(row.Text.text), width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    GameLocalizedText.Set(profile,"참여자가 퇴장했습니다.");
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    GameLocalizedText.Set(profile,$"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}");
                }
            }
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (localeRevision != GameLocale.Revision)
        { localeRevision = GameLocale.Revision; mentionRosterDirty = true; }
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            GameLocalizedText.Set(participants,lastParticipantCount.ToString("N0"));
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            GameLocalizedText.Set(hudStatus,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                GameLocalizedText.Set(status,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        GameLocalizedText.Set(row.Nickname,message.Nickname,true);
                        GameLocalizedText.Set(row.Time,$"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}");
                        GameLocalizedText.Set(row.Text,DisplayMessage(message),true);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(GameLocale.Text(row.Nickname.text), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(GameLocale.Text(row.Text.text), width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (SessionClock.Time >= nextAngerRefresh)
            {
                nextAngerRefresh = SessionClock.Time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(SessionClock.Time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    GameLocalizedText.Set(profile,"참여자가 퇴장했습니다.");
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    GameLocalizedText.Set(profile,$"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}");
                }
            }
        }
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -371,7 +371,7 @@
             lastHudMinute = minute;
             lastHudDay = day.Day;
             lastHudPhase = day.Phase;
-            GameLocalizedText.Set(hudStatus,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
+            GameLocalizedText.Set(hudStatus,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
         }
 
         if (!monitorChat && !TabletViewController.IsTabletHeld)
@@ -396,7 +396,7 @@
                 lastMinute = minute;
                 lastDay = day.Day;
                 string phase = PhaseText(day.Phase);
-                GameLocalizedText.Set(status,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
+                GameLocalizedText.Set(status,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
             }
 
             if (!chatSelected)
@@ -474,9 +474,9 @@
             }
 
             UpdateUnread();
-            if (Time.time >= nextAngerRefresh)
-            {
-                nextAngerRefresh = Time.time + .25f;
+            if (SessionClock.Time >= nextAngerRefresh)
+            {
+                nextAngerRefresh = SessionClock.Time + .25f;
                 foreach (var row in rows)
                 {
                     if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
@@ -487,7 +487,7 @@
                     row.AngerDot.gameObject.SetActive(angerStage >= 2);
                     if (angerStage < 2) { continue; }
                     var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
-                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
+                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(SessionClock.Time * 5f) * .5f + .5f) : 1f;
                     row.AngerDot.color = color;
                 }
             }
```

## Assets/Scripts/News/CompanyNewsAudience.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -57,15 +57,15 @@
     }
     void Update()
     {
-        if (Time.time < next || MarketDayController.Instance == null || InvestorSociety.Instance == null) { return; }
-        next = Time.time + interval;
+        if (SessionClock.Time < next || MarketDayController.Instance == null || InvestorSociety.Instance == null) { return; }
+        next = SessionClock.Time + interval;
         var society = InvestorSociety.Instance;
         if (boundSociety != society)
         {
             if (boundSociety != null) { boundSociety.SocialMessagePublished -= Spoken; }
             boundSociety = society; boundSociety.SocialMessagePublished += Spoken;
         }
-        notices.RemoveAll(item => item.stock.IsDelisted || Time.time - item.article.publishedAt > readingWindow);
+        notices.RemoveAll(item => item.stock.IsDelisted || SessionClock.Time - item.article.publishedAt > readingWindow);
         var bots = MarketDayController.Instance.Bots;
         for (int i = 0; i < Math.Min(botsPerTick, bots.Count); i++)
         {
@@ -73,7 +73,7 @@
             var bot = bots[cursor++];
             if (bot?.Social == null || bot.IsExchangeParticipant || !society.Present(bot, 0)) { continue; }
             var mind = bot.Social.Psychology;
-            if (!mind.ActiveAt(Time.time) || nextRead.TryGetValue(bot.Social.Id, out float until) && Time.time < until) { continue; }
+            if (!mind.ActiveAt(SessionClock.Time) || nextRead.TryGetValue(bot.Social.Id, out float until) && SessionClock.Time < until) { continue; }
             // Oldest first: a later correction cannot be read before the original and then overwritten by it.
             foreach (var notice in notices)
             {
@@ -83,16 +83,16 @@
                 bool interested = held || mind.WatchedStock == notice.stock || opinion.affinity >= 60;
                 double roll = NewsWriting.Hash(bot.Social.Id + ":" + article.id + ":read") / (double)uint.MaxValue;
                 if (roll >= witnessChance * (.5 + mind.Activity) * (interested ? 1 : .2)) { continue; }
-                if (!mind.Companies.Observe(article.symbol, article.id, article.sentiment, article.credibility, article.correction, mind.Credulity, held, Time.time)) { continue; }
-                nextRead[bot.Social.Id] = Time.time + 8; Observations++;
+                if (!mind.Companies.Observe(article.symbol, article.id, article.sentiment, article.credibility, article.correction, mind.Credulity, held, SessionClock.Time)) { continue; }
+                nextRead[bot.Social.Id] = SessionClock.Time + 8; Observations++;
                 ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = bot.Social.Id,
-                    kind = "COMPANY_NEWS_READ", reason = article.symbol + " " + mind.Companies.Stance(article.symbol, article.sentiment, article.correction), value = mind.Companies.TradeScale(article.symbol, true, Time.time) });
+                    kind = "COMPANY_NEWS_READ", reason = article.symbol + " " + mind.Companies.Stance(article.symbol, article.sentiment, article.correction), value = mind.Companies.TradeScale(article.symbol, true, SessionClock.Time) });
                 int linked = 0;
                 foreach (var link in article.links)
                 {
                     var related = Find(link.symbol);
                     if (related == null || bot.GetOwnedVolume(related) <= 0 && mind.WatchedStock != related) { continue; }
-                    mind.Companies.Observe(link.symbol, article.id, article.sentiment * link.transmission, article.credibility * .6, false, mind.Credulity, bot.GetOwnedVolume(related) > 0, Time.time);
+                    mind.Companies.Observe(link.symbol, article.id, article.sentiment * link.transmission, article.credibility * .6, false, mind.Credulity, bot.GetOwnedVolume(related) > 0, SessionClock.Time);
                     if (++linked >= 2) { break; }
                 }
                 double speaking = NewsWriting.Hash(bot.Social.Id + ":" + article.id + ":speak") / (double)uint.MaxValue;
```

## Assets/Scripts/News/FakeNewsManager.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -33,14 +33,14 @@
     public void RecordManipulation(Stock stock)
     {
         if (stock == null) { return; }
-        manipulations[stock] = (Time.time, false);
+        manipulations[stock] = (SessionClock.Time, false);
     }
     public bool CanPublish(Stock stock) => stock != null && manipulations.TryGetValue(stock, out var record) &&
-        (!oneNewsPerManipulation || !record.published) && Time.time - record.time >= minSecondsAfterManipulation;
+        (!oneNewsPerManipulation || !record.published) && SessionClock.Time - record.time >= minSecondsAfterManipulation;
     public bool TryPublishFakeNews(Stock stock, bool isPump)
     {
         if (!CanPublish(stock) || NewsManager.Instance == null || !stock.AcceptsOrders) { return false; }
-        var article = PublishRumor(stock, "contracts", isPump, Mathf.FloorToInt(Time.time));
+        var article = PublishRumor(stock, "contracts", isPump, Mathf.FloorToInt(SessionClock.Time));
         if (article == null) { return false; }
         manipulations[stock] = (manipulations[stock].time, true); return true;
     }
@@ -50,7 +50,7 @@
         if (stock == null || manager == null) { return null; }
         string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
         return NewsWriting.Compose(manager.Writing, 0, stock.symbol, stock.stockName, title, positive ? .64 : -.64,
-            DeviceMarketJournal.Stamp(), Time.time, true, category, variant);
+            DeviceMarketJournal.Stamp(), SessionClock.Time, true, category, variant);
     }
     public bool TryPublishPlayer(Stock stock, string category, bool positive, int variant, out NewsArticle article, out string reason)
     {
@@ -76,18 +76,18 @@
         if (article != null && NewsWriting.Hash(article.id + ":followup:" + stock.symbol) % 10000 < followupChance * 10000)
         {
             if (followups.Count >= 16) { followups.RemoveAt(0); }
-            followups.Add((stock, article, Time.time + followupDelaySeconds + NewsWriting.Hash(article.id.ToString()) % 45));
+            followups.Add((stock, article, SessionClock.Time + followupDelaySeconds + NewsWriting.Hash(article.id.ToString()) % 45));
         }
         return article;
     }
     void Update()
     {
-        if (Time.time < nextCheck || !MarketDayController.CanTrade || NewsManager.Instance == null) { return; }
-        nextCheck = Time.time + 1;
+        if (SessionClock.Time < nextCheck || !MarketDayController.CanTrade || NewsManager.Instance == null) { return; }
+        nextCheck = SessionClock.Time + 1;
         for (int i = followups.Count - 1; i >= 0; i--)
         {
             var item = followups[i];
-            if (Time.time < item.due) { continue; }
+            if (SessionClock.Time < item.due) { continue; }
             followups.RemoveAt(i);
             if (item.stock == null || item.stock.IsDelisted) { continue; }
             NewsManager.Instance.Publish(item.stock, item.article.sentiment > 0 ? .94f : 1.06f,
```

## Assets/Scripts/News/NewsManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -158,7 +158,7 @@
         if (stock != null)
         {
             article = NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
-                DeviceMarketJournal.Stamp(), Time.time, fabricated, category, variant, storyId, correction);
+                DeviceMarketJournal.Stamp(), SessionClock.Time, fabricated, category, variant, storyId, correction);
             Articles.Insert(0, article);
             if (Articles.Count > 40) { Articles.RemoveAt(40); }
             ArticleVersion++;
```

## Assets/Scripts/NightShift/BotMind.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -10,7 +10,7 @@
         { return 0f; }
         float fear = Mathf.Clamp01(s.Stress / 100f * .35f + s.Psychology.Fear / 100f * .45f + (1f - s.Confidence) * .2f);
         float result = (s.Greed - .5f) * .18f + (s.Impulsiveness - .5f) * .1f - fear * .14f - s.Suspicion * .05f + (s.Psychology.HasFomoFor(stock) ? (float)ChatSocialConfig.Current.Get("fomoBuyBias") : 0);
-        if (s.InfluencedStock == stock && Time.time < s.InfluenceUntil)
+        if (s.InfluencedStock == stock && SessionClock.Time < s.InfluenceUntil)
         { result += .06f + s.Greed * .12f - s.Suspicion * .06f; }
         return Mathf.Clamp(result, -.25f, .25f);
     }
```

## Assets/Scripts/NightShift/ChatSocialMarketRisk.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -75,7 +75,7 @@
         { return 0; }
         foreach (var prior in riskBulletins)
         { if (prior.stock == stock && prior.version == stock.RiskVersion) { return prior.id; } }
-        double now = UnityEngine.Time.time;
+        double now = SessionClock.Time;
         riskBulletins.RemoveAll(row => row.stock == stock || row.expires < now || row.stock.IsDelisted);
         if (riskBulletins.Count >= 16) { riskBulletins.RemoveAt(0); }
         var bulletin = new MarketRiskBulletin { stock = stock, version = stock.RiskVersion, grade = (int)stock.RiskGrade,
```

## Assets/Scripts/NightShift/ChatSocialUnity.cs
변경 덩어리: 4

### public void ObserveSocialFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
원본
```csharp
    public void ObserveSocialFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (ReferenceEquals(buyer, seller) || volume <= 0 || resting == null)
        { return; }
        double at = day == null ? 0 : (day.Day - 1) * 1440d + day.GameMinute;
        if (buyer is TradeBot buying && buying.Social != null)
        {
            buying.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = true, price = price, quantity = volume, cost = price, at = at });
            Offer(buying, buying.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(buying, stock));
        }
        if (seller is TradeBot selling && selling.Social != null)
        {
            decimal basis = ReferenceEquals(resting.owner, seller) ? resting.costBasis : selling.SubmittedSellCost;
            selling.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = false, price = price, quantity = volume, cost = basis, at = at });
            bool leading = Orders?.Get(selling) is LeadingRoomOrders.Plan plan && plan.Command.Stock == stock && !plan.Reported && plan.Remaining > 0;
            if (!leading && basis > 0)
            {
                decimal profit = (price - basis) * volume;
                selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, Time.time,
                    assetBefore: Math.Max(0, selling.Equity() - profit)), false);
            }
            Offer(selling, selling.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(selling, stock));
        }
    }
```
수정본
```csharp
    public void ObserveSocialFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (ReferenceEquals(buyer, seller) || volume <= 0 || resting == null)
        { return; }
        double at = day == null ? 0 : (day.Day - 1) * 1440d + day.GameMinute;
        if (buyer is TradeBot buying && buying.Social != null)
        {
            buying.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = true, price = price, quantity = volume, cost = price, at = at });
            Offer(buying, buying.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(buying, stock));
        }
        if (seller is TradeBot selling && selling.Social != null)
        {
            decimal basis = ReferenceEquals(resting.owner, seller) ? resting.costBasis : selling.SubmittedSellCost;
            selling.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = false, price = price, quantity = volume, cost = basis, at = at });
            bool leading = Orders?.Get(selling) is LeadingRoomOrders.Plan plan && plan.Command.Stock == stock && !plan.Reported && plan.Remaining > 0;
            if (!leading && basis > 0)
            {
                decimal profit = (price - basis) * volume;
                selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, SessionClock.Time,
                    assetBefore: Math.Max(0, selling.Equity() - profit)), false);
            }
            Offer(selling, selling.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(selling, stock));
        }
    }
```

### private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
원본
```csharp
    private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
    {
        var npc = NpcLeadingRoom.Instance; var signal = npc?.Current;
        if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= Time.time)
        { return null; }
        return new SocialFacts { signal = true, eventId = signal.Id, symbol = signal.Stock.symbol, stockName = signal.Stock.stockName, at = SocialTime.GameMinute,
            bidStatus = signal.Buy > 0 ? Money(signal.Buy) + (signal.RestingBuy ? "원 매수 주문 대기" : "원 최근 매수 체결") : "매수 주문 없음",
            askStatus = signal.Sell > 0 ? Money(signal.Sell) + "원 매도 주문 대기" : "매도 주문 없음", signalResult = Money(npc.RealizedProfit) };
    }
```
수정본
```csharp
    private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
    {
        var npc = NpcLeadingRoom.Instance; var signal = npc?.Current;
        if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= SessionClock.Time)
        { return null; }
        return new SocialFacts { signal = true, eventId = signal.Id, symbol = signal.Stock.symbol, stockName = signal.Stock.stockName, at = SocialTime.GameMinute,
            bidStatus = signal.Buy > 0 ? Money(signal.Buy) + (signal.RestingBuy ? "원 매수 주문 대기" : "원 최근 매수 체결") : "매수 주문 없음",
            askStatus = signal.Sell > 0 ? Money(signal.Sell) + "원 매도 주문 대기" : "매도 주문 없음", signalResult = Money(npc.RealizedProfit) };
    }
```

### private void PrepareSocialRuntime()
원본
```csharp
    private void PrepareSocialRuntime()
    {
        BindSocialBots(day.Bots);
        if (!socialWired)
        {
            ConfigureLiveConversation(day.ConversationFocus);
            ChooseSocialTopic = bot => ChooseRoomTopic(bot, bot.Social.Psychology.Room, bot.Social.Psychology.SocialRng.Next());
            ChooseWatchedStock = ChoosePersonalWatch;
            RefreshOfferedFacts = RefreshNpcFacts;
            SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, Time.time).ReturnPercent;
            WitnessChance = bot => Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + bot.Social.Psychology.Activity * Config.Get("witnessActivityScale"));
            SocialMessagePublished += DeliverSocialMessage;
            SocialDeparted += OnSocialDeparture;
            CanModerateRoom = room => IsRoomOpen(room);
            SocialSystemNotice += DeliverSocialSystemNotice;
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "SocialLogs"); Directory.CreateDirectory(directory);
                socialLog = new SocialLogSink(new StreamWriter(Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl")),
                    message => Debug.LogWarning("Social log disabled; in-memory audit continues: " + message));
                ChatSocialAudit.Journal.Changed += WriteSocialChange; ChatSocialAudit.Journal.Happened += WriteSocialEvent;
            }
            catch (IOException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            socialWired = true;
        }
        SocialTime.Set(day.Day, day.GameMinute, Time.time);
        if (socialLog != null && Time.unscaledTime >= nextSocialLogFlush)
        { socialLog.Flush(); nextSocialLogFlush = Time.unscaledTime + (float)Config.Get("logFlushSeconds"); }
    }
```
수정본
```csharp
    private void PrepareSocialRuntime()
    {
        BindSocialBots(day.Bots);
        if (!socialWired)
        {
            ConfigureLiveConversation(day.ConversationFocus);
            ChooseSocialTopic = bot => ChooseRoomTopic(bot, bot.Social.Psychology.Room, bot.Social.Psychology.SocialRng.Next());
            ChooseWatchedStock = ChoosePersonalWatch;
            RefreshOfferedFacts = RefreshNpcFacts;
            SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, SessionClock.Time).ReturnPercent;
            WitnessChance = bot => Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + bot.Social.Psychology.Activity * Config.Get("witnessActivityScale"));
            SocialMessagePublished += DeliverSocialMessage;
            SocialDeparted += OnSocialDeparture;
            CanModerateRoom = room => IsRoomOpen(room);
            SocialSystemNotice += DeliverSocialSystemNotice;
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "SocialLogs"); Directory.CreateDirectory(directory);
                socialLog = new SocialLogSink(new StreamWriter(Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl")),
                    message => Debug.LogWarning("Social log disabled; in-memory audit continues: " + message));
                ChatSocialAudit.Journal.Changed += WriteSocialChange; ChatSocialAudit.Journal.Happened += WriteSocialEvent;
            }
            catch (IOException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            socialWired = true;
        }
        SocialTime.Set(day.Day, day.GameMinute, SessionClock.Time);
        if (socialLog != null && Time.unscaledTime >= nextSocialLogFlush)
        { socialLog.Flush(); nextSocialLogFlush = Time.unscaledTime + (float)Config.Get("logFlushSeconds"); }
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -16,7 +16,7 @@
             ChooseSocialTopic = bot => ChooseRoomTopic(bot, bot.Social.Psychology.Room, bot.Social.Psychology.SocialRng.Next());
             ChooseWatchedStock = ChoosePersonalWatch;
             RefreshOfferedFacts = RefreshNpcFacts;
-            SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, Time.time).ReturnPercent;
+            SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, SessionClock.Time).ReturnPercent;
             WitnessChance = bot => Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + bot.Social.Psychology.Activity * Config.Get("witnessActivityScale"));
             SocialMessagePublished += DeliverSocialMessage;
             SocialDeparted += OnSocialDeparture;
@@ -33,7 +33,7 @@
             catch (UnauthorizedAccessException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
             socialWired = true;
         }
-        SocialTime.Set(day.Day, day.GameMinute, Time.time);
+        SocialTime.Set(day.Day, day.GameMinute, SessionClock.Time);
         if (socialLog != null && Time.unscaledTime >= nextSocialLogFlush)
         { socialLog.Flush(); nextSocialLogFlush = Time.unscaledTime + (float)Config.Get("logFlushSeconds"); }
     }
@@ -107,7 +107,7 @@
             if (!leading && basis > 0)
             {
                 decimal profit = (price - basis) * volume;
-                selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, Time.time,
+                selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, SessionClock.Time,
                     assetBefore: Math.Max(0, selling.Equity() - profit)), false);
             }
             Offer(selling, selling.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(selling, stock));
@@ -136,7 +136,7 @@
     private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
     {
         var npc = NpcLeadingRoom.Instance; var signal = npc?.Current;
-        if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= Time.time)
+        if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= SessionClock.Time)
         { return null; }
         return new SocialFacts { signal = true, eventId = signal.Id, symbol = signal.Stock.symbol, stockName = signal.Stock.stockName, at = SocialTime.GameMinute,
             bidStatus = signal.Buy > 0 ? Money(signal.Buy) + (signal.RestingBuy ? "원 매수 주문 대기" : "원 최근 매수 체결") : "매수 주문 없음",
```

## Assets/Scripts/NightShift/DailyIncomeController.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -87,7 +87,7 @@
         { return; }
         if (!Enter(bot,1,out string reason))
         {
-            bot.Social.Psychology.NextMembership = Time.time + 60f;
+            bot.Social.Psychology.NextMembership = SessionClock.Time + 60f;
             InvestorSociety.Instance?.Leave(bot,"이용권 갱신 거절: " + reason);
         }
     }
```

## Assets/Scripts/NightShift/DeviceMarketJournal.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -36,7 +36,7 @@
     public static string Stamp()
     {
         var day = MarketDayController.Instance;
-        int minute = day == null ? 540 : day.GameMinute;
+        int minute = day == null ? 540 : day.ClockMinute;
         return (minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2");
     }
     void OnEnable()
@@ -70,7 +70,7 @@
     {
         if (resting == null || quantity <= 0m || !tapes.TryGetValue(stock, out var tape))
         { return; }
-        int second = Mathf.FloorToInt(Time.time), slot = second % 60;
+        int second = Mathf.FloorToInt(SessionClock.Time), slot = second % 60;
         if (tape.Seconds[slot] != second)
         { tape.Seconds[slot] = second; tape.Buy[slot] = tape.Sell[slot] = 0m; }
         if (resting.isBuy)
@@ -92,7 +92,7 @@
         volume = buy = 0m;
         if (stock == null || !tapes.TryGetValue(stock, out var tape))
         { return; }
-        int now = Mathf.FloorToInt(Time.time);
+        int now = Mathf.FloorToInt(SessionClock.Time);
         for (int i = 0; i < 60; i++)
         {
             if (now - tape.Seconds[i] >= 60)
```

## Assets/Scripts/NightShift/DeviceOrderSession.cs
변경 덩어리: 4

### void Update()
원본
```csharp
    void Update()
    {
        if (!Active || Time.timeScale <= 0f || Time.time < next)
        { return; }
        next = Time.time + .25f;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var request = active[i];
            if (request.Stock == null || !request.Stock.AcceptsOrders || !MarketDayController.CanTrade)
            { Cancel(request, "거래 종료·중지로 미체결 취소"); continue; }
            if (request.Unsubmitted > 0m && Time.time >= request.Next)
            { SendSlice(request); }
            RefreshPending(request);
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (!Active || Time.timeScale <= 0f || SessionClock.Time < next)
        { return; }
        next = SessionClock.Time + .25f;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var request = active[i];
            if (request.Stock == null || !request.Stock.AcceptsOrders || !MarketDayController.CanTrade)
            { Cancel(request, "거래 종료·중지로 미체결 취소"); continue; }
            if (request.Unsubmitted > 0m && SessionClock.Time >= request.Next)
            { SendSlice(request); }
            RefreshPending(request);
        }
    }
```

### private void SendSlice(Request request)
원본
```csharp
    private void SendSlice(Request request)
    {
        decimal quantity = decimal.Floor(request.Quantity / request.Parts) + (request.Part < request.Quantity % request.Parts ? 1m : 0m);
        if (request.Buy && quantity * request.Limit > PlayerAccounts.Available(request.Owner))
        { Cancel(request, "다음 회차의 주문 가능 현금 부족"); return; }
        if (!request.Buy && quantity > request.Owner.GetHolding(request.Stock))
        { Cancel(request, "다음 회차의 매도 가능 수량 부족"); return; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        before.Clear();
        foreach (var order in snapshot) { before.Add(order); }
        request.Unsubmitted -= quantity; request.Part++;
        submitting = request;
        try
        {
            if (request.Buy) { request.Owner.BuyLimit(request.Limit, quantity, 0, request.Stock); }
            else { request.Owner.SellLimit(request.Limit, quantity, 0, request.Stock); }
        }
        finally { submitting = null; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        foreach (var order in snapshot)
        {
            if (!before.Contains(order))
            { request.Pending.Add(order); tracked[order] = request; }
        }
        request.Next = Time.time + request.Interval;
        Feedback = "#" + request.Number.ToString("D4") + " " + request.Stock.stockName + " · " + request.Part + "/" + request.Parts + "회 접수";
    }
```
수정본
```csharp
    private void SendSlice(Request request)
    {
        decimal quantity = decimal.Floor(request.Quantity / request.Parts) + (request.Part < request.Quantity % request.Parts ? 1m : 0m);
        if (request.Buy && quantity * request.Limit > PlayerAccounts.Available(request.Owner))
        { Cancel(request, "다음 회차의 주문 가능 현금 부족"); return; }
        if (!request.Buy && quantity > request.Owner.GetHolding(request.Stock))
        { Cancel(request, "다음 회차의 매도 가능 수량 부족"); return; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        before.Clear();
        foreach (var order in snapshot) { before.Add(order); }
        request.Unsubmitted -= quantity; request.Part++;
        submitting = request;
        try
        {
            if (request.Buy) { request.Owner.BuyLimit(request.Limit, quantity, 0, request.Stock); }
            else { request.Owner.SellLimit(request.Limit, quantity, 0, request.Stock); }
        }
        finally { submitting = null; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        foreach (var order in snapshot)
        {
            if (!before.Contains(order))
            { request.Pending.Add(order); tracked[order] = request; }
        }
        request.Next = SessionClock.Time + request.Interval;
        Feedback = "#" + request.Number.ToString("D4") + " " + request.Stock.stockName + " · " + request.Part + "/" + request.Parts + "회 접수";
    }
```

### public bool Submit(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
원본
```csharp
    public bool Submit(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        string error = Validate(stock, buy, price, quantity, parts, interval);
        if (!string.IsNullOrEmpty(error))
        { Feedback = error; return false; }
        var request = new Request
        {
            Stock = stock, Owner = account, Number = ++Number, Buy = buy, Limit = price,
            Quantity = quantity, Unsubmitted = quantity, Parts = parts, Interval = interval,
            Budget = AvailableCash, Next = Time.time, Status = "접수 · 체결 대기"
        };
        Latest = request; active.Add(request);
        subscriptions.TryGetValue(stock, out int count);
        if (count == 0) { stock.OnMatchedTrade += OnFill; }
        subscriptions[stock] = count + 1;
        // Reserve synchronously so consecutive clicks cannot spend the same cash.
        SendSlice(request); RefreshPending(request);
        return true;
    }
```
수정본
```csharp
    public bool Submit(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        string error = Validate(stock, buy, price, quantity, parts, interval);
        if (!string.IsNullOrEmpty(error))
        { Feedback = error; return false; }
        var request = new Request
        {
            Stock = stock, Owner = account, Number = ++Number, Buy = buy, Limit = price,
            Quantity = quantity, Unsubmitted = quantity, Parts = parts, Interval = interval,
            Budget = AvailableCash, Next = SessionClock.Time, Status = "접수 · 체결 대기"
        };
        Latest = request; active.Add(request);
        subscriptions.TryGetValue(stock, out int count);
        if (count == 0) { stock.OnMatchedTrade += OnFill; }
        subscriptions[stock] = count + 1;
        // Reserve synchronously so consecutive clicks cannot spend the same cash.
        SendSlice(request); RefreshPending(request);
        return true;
    }
```

### public void ReconnectSession()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.OnMatchedTrade -= OnFill;
            if (subscriptions.ContainsKey(stock)) { stock.OnMatchedTrade += OnFill; }
        }
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -76,6 +76,14 @@
     private readonly Dictionary<Stock, int> subscriptions = new();
     private Request submitting;
     private float next;
+    public void ReconnectSession()
+    {
+        foreach (var stock in StockManager.Instance.stocks)
+        {
+            stock.OnMatchedTrade -= OnFill;
+            if (subscriptions.ContainsKey(stock)) { stock.OnMatchedTrade += OnFill; }
+        }
+    }
 
     void Start()
     {
@@ -132,7 +140,7 @@
         {
             Stock = stock, Owner = account, Number = ++Number, Buy = buy, Limit = price,
             Quantity = quantity, Unsubmitted = quantity, Parts = parts, Interval = interval,
-            Budget = AvailableCash, Next = Time.time, Status = "접수 · 체결 대기"
+            Budget = AvailableCash, Next = SessionClock.Time, Status = "접수 · 체결 대기"
         };
         Latest = request; active.Add(request);
         subscriptions.TryGetValue(stock, out int count);
@@ -144,15 +152,15 @@
     }
     void Update()
     {
-        if (!Active || Time.timeScale <= 0f || Time.time < next)
-        { return; }
-        next = Time.time + .25f;
+        if (!Active || Time.timeScale <= 0f || SessionClock.Time < next)
+        { return; }
+        next = SessionClock.Time + .25f;
         for (int i = active.Count - 1; i >= 0; i--)
         {
             var request = active[i];
             if (request.Stock == null || !request.Stock.AcceptsOrders || !MarketDayController.CanTrade)
             { Cancel(request, "거래 종료·중지로 미체결 취소"); continue; }
-            if (request.Unsubmitted > 0m && Time.time >= request.Next)
+            if (request.Unsubmitted > 0m && SessionClock.Time >= request.Next)
             { SendSlice(request); }
             RefreshPending(request);
         }
@@ -181,7 +189,7 @@
             if (!before.Contains(order))
             { request.Pending.Add(order); tracked[order] = request; }
         }
-        request.Next = Time.time + request.Interval;
+        request.Next = SessionClock.Time + request.Interval;
         Feedback = "#" + request.Number.ToString("D4") + " " + request.Stock.stockName + " · " + request.Part + "/" + request.Parts + "회 접수";
     }
     private void RefreshPending(Request request)
```

## Assets/Scripts/NightShift/IncumbentExchangeAI.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -64,9 +64,9 @@
     {
         if (!MarketDayController.CanTrade)
         { ClearResponse(); return; }
-        if (Time.time >= expires)
+        if (SessionClock.Time >= expires)
         { ClearResponse(); }
-        Observe(Time.time);
+        Observe(SessionClock.Time);
     }
     public bool Observe(float now)
     {
```

## Assets/Scripts/NightShift/InvestorConversation.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -58,7 +58,7 @@
         { return "아직 결정 못 했어요. 자금이랑 호가부터 좀 볼게요."; }
         if (active && plan.State == LeadingRoomOrders.Stage.AwaitBuy)
         { return plan.Command.Stock.stockName + " " + plan.Command.Buy.ToString("N0") + "원 매수 조건까지 기다릴게요."; }
-        bool busy = MarketDayController.Instance != null && MarketDayController.Instance.ConversationFocus.HasActivity(Time.time);
+        bool busy = MarketDayController.Instance != null && MarketDayController.Instance.ConversationFocus.HasActivity(SessionClock.Time);
         if (variant % (busy ? 10 : 4) != 0 && StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
         { return MarketRemark(bot, variant); }
         return BotMind.Compose(bot, ChatEvent.SmallTalk, null, "잠깐 쉬었다 볼까요.");
@@ -67,7 +67,7 @@
     {
         var stocks = StockManager.Instance.stocks;
         var focus = MarketDayController.Instance?.ConversationFocus;
-        var stock = focus?.Choose((variant & int.MaxValue)/(double)(int.MaxValue+1L),Time.time) ?? stocks[variant % stocks.Count];
+        var stock = focus?.Choose((variant & int.MaxValue)/(double)(int.MaxValue+1L),SessionClock.Time) ?? stocks[variant % stocks.Count];
         return MarketRemark(bot,stock,variant);
     }
     public static string MarketRemark(TradeBot bot,Stock stock,int variant)
@@ -80,10 +80,10 @@
             return variant % 2 == 0 ? name + " 제 평단은 " + basis.ToString("N0") + "원. 평가손익 " + rate.ToString("+0.0;-0.0;0") + "%네요."
                 : name + " " + shares.ToString("N0") + "주 들고 있어요. " + (rate < 0m ? "손실 중이라 더 신중하게 보려고요." : "평가이익이 있어도 아직 판 건 아니에요.");
         }
-        if (variant % 5 == 1 && bot.Social.Positions.TryGetValue(stock, out var memory) && memory.SellVolume > 0m && Time.time - memory.SellTime < 180f)
+        if (variant % 5 == 1 && bot.Social.Positions.TryGetValue(stock, out var memory) && memory.SellVolume > 0m && SessionClock.Time - memory.SellTime < 180f)
         { return name + " 최근 매도 결과는 " + memory.SellProfit.ToString("+0;-0;0") + "원. " + (memory.SellProfit < 0m ? "좀 쉬면서 생각해 볼게요." : "이번 결과를 다음에도 기대하진 않으려고요."); }
         var focus = MarketDayController.Instance?.ConversationFocus;
-        var tape = focus == null ? default : focus.Read(stock,Time.time);
+        var tape = focus == null ? default : focus.Read(stock,SessionClock.Time);
         if (tape.Volume > 0m)
         {
             string volume = tape.Volume.ToString("N0"), rate = tape.ReturnPercent.ToString("+0.0;-0.0;0.0");
```

## Assets/Scripts/NightShift/InvestorSociety.cs
변경 덩어리: 10

### public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
원본
```csharp
    public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
        { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = Time.time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{room.DisplayName}: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
```
수정본
```csharp
    public bool Issue(int roomId, Stock stock, decimal buy, decimal sell)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
        { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = SessionClock.Time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{room.DisplayName}: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
```

### public void Trace(TradeBot bot, string cause)
원본
```csharp
    public void Trace(TradeBot bot, string cause)
    {
        if (DebugTrace.Count >= 64) { DebugTrace.Dequeue(); }
        string entry = $"{Time.time:0.0}s #{bot.Social.Id} {cause}";
        DebugTrace.Enqueue(entry);
        if (settings.trace) { Debug.Log("[InvestorSociety] " + entry); }
    }
```
수정본
```csharp
    public void Trace(TradeBot bot, string cause)
    {
        if (DebugTrace.Count >= 64) { DebugTrace.Dequeue(); }
        string entry = $"{SessionClock.Time:0.0}s #{bot.Social.Id} {cause}";
        DebugTrace.Enqueue(entry);
        if (settings.trace) { Debug.Log("[InvestorSociety] " + entry); }
    }
```

### public void ReadBeforeSpeech(TradeBot bot)
원본
```csharp
    public void ReadBeforeSpeech(TradeBot bot)
    {
        day ??= MarketDayController.Instance;
        if (day == null || bot.Social == null || !bot.Social.Psychology.ActiveAt(Time.time))
        { return; }
        ReadRecent(bot, 0, Time.time);
        if (bot.Social.Psychology.Room > 0) { ReadRecent(bot, bot.Social.Psychology.Room, Time.time); }
    }
```
수정본
```csharp
    public void ReadBeforeSpeech(TradeBot bot)
    {
        day ??= MarketDayController.Instance;
        if (day == null || bot.Social == null || !bot.Social.Psychology.ActiveAt(SessionClock.Time))
        { return; }
        ReadRecent(bot, 0, SessionClock.Time);
        if (bot.Social.Psychology.Room > 0) { ReadRecent(bot, bot.Social.Psychology.Room, SessionClock.Time); }
    }
```

### public LeadingRoomOrders EnsureSessionOrders()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public LeadingRoomOrders EnsureSessionOrders()
    { return Orders ??= new LeadingRoomOrders(this); }
```

### public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
원본
```csharp
    public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
    {
        if (bot?.Social == null) { return false; }
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        if (room.PlayerOwned && !playerInvited && !CanDiscoverPlayerRoom(bot, roomId))
        { return false; }
        if (room.PlayerOwned && DailyIncomeController.Instance != null && !DailyIncomeController.Instance.Enter(bot,roomId,out string feeReason))
        { LastAction = bot.Social.Nickname + ": " + feeReason; Trace(bot,"이용료 거절: " + feeReason); return false; }
        if (room.PlayerOwned && playerInvited)
        { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```
수정본
```csharp
    public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
    {
        if (bot?.Social == null) { return false; }
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        if (room.PlayerOwned && !playerInvited && !CanDiscoverPlayerRoom(bot, roomId))
        { return false; }
        if (room.PlayerOwned && DailyIncomeController.Instance != null && !DailyIncomeController.Instance.Enter(bot,roomId,out string feeReason))
        { LastAction = bot.Social.Nickname + ": " + feeReason; Trace(bot,"이용료 거절: " + feeReason); return false; }
        if (room.PlayerOwned && playerInvited)
        { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
        mind.Room = roomId; mind.JoinedRoomAt = SessionClock.Time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (SessionClock.Time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

### public void Speak(TradeBot bot, int room, string text, InvestorMessageContext context, ChatEvent kind = ChatEvent.Agreement)
원본
```csharp
    public void Speak(TradeBot bot, int room, string text, InvestorMessageContext context, ChatEvent kind = ChatEvent.Agreement)
    {
        if (bot != null && SocialReady)
        { OfferExistingSpeech(bot, room, context, kind); return; }
        day ??= MarketDayController.Instance;
        if (day == null)
        { return; }
        if (room == 0) { day.PublishParticipant(bot, text, context); }
        else
        {
            var channel = GetRoom(room);
            if (channel == null)
            { return; }
            if (bot != null && !channel.RegisterText(text, Time.time))
            { bot.Social.Psychology.NextSpeech = Time.time + 8f; return; }
            channel.Add(new BotChatMessage(channel.Version + 1, bot, day.Day, day.GameMinute, text, "society", kind, context));
            if (bot != null) { channel.NextConversation = Time.time + Mathf.Max(1f, settings.roomMessageInterval); }
        }
        if (bot != null)
        { bot.Social.LastSpeech = Time.time; bot.Social.Psychology.NextSpeech = Time.time + settings.speechCooldown + (float)random.NextDouble() * 30f; }
    }
```
수정본
```csharp
    public void Speak(TradeBot bot, int room, string text, InvestorMessageContext context, ChatEvent kind = ChatEvent.Agreement)
    {
        if (bot != null && SocialReady)
        { OfferExistingSpeech(bot, room, context, kind); return; }
        day ??= MarketDayController.Instance;
        if (day == null)
        { return; }
        if (room == 0) { day.PublishParticipant(bot, text, context); }
        else
        {
            var channel = GetRoom(room);
            if (channel == null)
            { return; }
            if (bot != null && !channel.RegisterText(text, SessionClock.Time))
            { bot.Social.Psychology.NextSpeech = SessionClock.Time + 8f; return; }
            channel.Add(new BotChatMessage(channel.Version + 1, bot, day.Day, day.GameMinute, text, "society", kind, context));
            if (bot != null) { channel.NextConversation = SessionClock.Time + Mathf.Max(1f, settings.roomMessageInterval); }
        }
        if (bot != null)
        { bot.Social.LastSpeech = SessionClock.Time; bot.Social.Psychology.NextSpeech = SessionClock.Time + settings.speechCooldown + (float)random.NextDouble() * 30f; }
    }
```

### void Update()
원본
```csharp
    void Update()
    {
        if (Time.timeScale > 0f) { Orders?.FlushObservedResults(settings.botsPerTick); }
        if (!MarketDayController.CanTrade || Time.time < nextTick)
        { return; }
        day = MarketDayController.Instance;
        if (day != null) { ChatSocialAudit.GameMinute = (day.Day - 1) * 1440d + day.GameMinute; }
        if (day == null || !day.Ready || StockManager.Instance == null)
        { return; }
        if (Orders == null) { Orders = new LeadingRoomOrders(this); }
        PrepareSocialRuntime();
        nextTick = Time.time + Mathf.Max(.05f, settings.updateInterval);
        using (TickMarker.Auto())
        {
            int count = Math.Min(Math.Max(1, settings.botsPerTick), day.Bots.Count);
            MaxBotsProcessedPerTick = Math.Max(MaxBotsProcessedPerTick, count);
            for (int i = 0; i < count; i++)
            {
                if (cursor >= day.Bots.Count) { cursor = 0; }
                var bot = day.Bots[cursor++];
                if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
                { continue; }
                TickBot(bot, Time.time);
            }
            SocialAdvance(day.Day, day.GameMinute, Time.time, settings.botsPerTick);
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (Time.timeScale > 0f) { Orders?.FlushObservedResults(settings.botsPerTick); }
        if (!MarketDayController.CanTrade || SessionClock.Time < nextTick)
        { return; }
        day = MarketDayController.Instance;
        if (day != null) { ChatSocialAudit.GameMinute = (day.Day - 1) * 1440d + day.GameMinute; }
        if (day == null || !day.Ready || StockManager.Instance == null)
        { return; }
        if (Orders == null) { Orders = new LeadingRoomOrders(this); }
        PrepareSocialRuntime();
        nextTick = SessionClock.Time + Mathf.Max(.05f, settings.updateInterval);
        using (TickMarker.Auto())
        {
            int count = Math.Min(Math.Max(1, settings.botsPerTick), day.Bots.Count);
            MaxBotsProcessedPerTick = Math.Max(MaxBotsProcessedPerTick, count);
            for (int i = 0; i < count; i++)
            {
                if (cursor >= day.Bots.Count) { cursor = 0; }
                var bot = day.Bots[cursor++];
                if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
                { continue; }
                TickBot(bot, SessionClock.Time);
            }
            SocialAdvance(day.Day, day.GameMinute, SessionClock.Time, settings.botsPerTick);
        }
    }
```

### public bool Invite(int botId, int roomId)
원본
```csharp
    public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned || !IsRoomOpen(roomId))
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
        if (!firstMember && (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool willing = firstMember || random.NextDouble() < mind.JoinChance;
        LastAction = bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        bool accepted = willing && Join(bot, roomId, playerInvited: true);
        if (accepted) { LastAction = bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다."; }
        return accepted;
    }
```
수정본
```csharp
    public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned || !IsRoomOpen(roomId))
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
        if (!firstMember && (SessionClock.Time < nextContact || SessionClock.Time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = SessionClock.Time + settings.contactCooldown; mind.LastContact = SessionClock.Time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool willing = firstMember || random.NextDouble() < mind.JoinChance;
        LastAction = bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        bool accepted = willing && Join(bot, roomId, playerInvited: true);
        if (accepted) { LastAction = bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다."; }
        return accepted;
    }
```

### public void ReconnectSession()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public void ReconnectSession()
    {
        day = MarketDayController.Instance;
        EnsureSessionOrders().ReconnectSession();
        PrepareSocialRuntime();
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -66,7 +66,7 @@
     void Update()
     {
         if (Time.timeScale > 0f) { Orders?.FlushObservedResults(settings.botsPerTick); }
-        if (!MarketDayController.CanTrade || Time.time < nextTick)
+        if (!MarketDayController.CanTrade || SessionClock.Time < nextTick)
         { return; }
         day = MarketDayController.Instance;
         if (day != null) { ChatSocialAudit.GameMinute = (day.Day - 1) * 1440d + day.GameMinute; }
@@ -74,7 +74,7 @@
         { return; }
         if (Orders == null) { Orders = new LeadingRoomOrders(this); }
         PrepareSocialRuntime();
-        nextTick = Time.time + Mathf.Max(.05f, settings.updateInterval);
+        nextTick = SessionClock.Time + Mathf.Max(.05f, settings.updateInterval);
         using (TickMarker.Auto())
         {
             int count = Math.Min(Math.Max(1, settings.botsPerTick), day.Bots.Count);
@@ -85,12 +85,20 @@
                 var bot = day.Bots[cursor++];
                 if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
                 { continue; }
-                TickBot(bot, Time.time);
-            }
-            SocialAdvance(day.Day, day.GameMinute, Time.time, settings.botsPerTick);
+                TickBot(bot, SessionClock.Time);
+            }
+            SocialAdvance(day.Day, day.GameMinute, SessionClock.Time, settings.botsPerTick);
         }
     }
     public long NextEventId() => ChatSocialIds.Next();
+    public LeadingRoomOrders EnsureSessionOrders()
+    { return Orders ??= new LeadingRoomOrders(this); }
+    public void ReconnectSession()
+    {
+        day = MarketDayController.Instance;
+        EnsureSessionOrders().ReconnectSession();
+        PrepareSocialRuntime();
+    }
     public Room GetRoom(int id) => id >= 1 && id <= Rooms.Length ? Rooms[id - 1] : null;
     public bool IsRoomOpen(int id)
     {
@@ -214,10 +222,10 @@
     public void ReadBeforeSpeech(TradeBot bot)
     {
         day ??= MarketDayController.Instance;
-        if (day == null || bot.Social == null || !bot.Social.Psychology.ActiveAt(Time.time))
-        { return; }
-        ReadRecent(bot, 0, Time.time);
-        if (bot.Social.Psychology.Room > 0) { ReadRecent(bot, bot.Social.Psychology.Room, Time.time); }
+        if (day == null || bot.Social == null || !bot.Social.Psychology.ActiveAt(SessionClock.Time))
+        { return; }
+        ReadRecent(bot, 0, SessionClock.Time);
+        if (bot.Social.Psychology.Room > 0) { ReadRecent(bot, bot.Social.Psychology.Room, SessionClock.Time); }
     }
     public bool Observe(TradeBot bot, BotChatMessage message, int roomId, float now, double replyRoll)
     {
@@ -260,13 +268,13 @@
             var channel = GetRoom(room);
             if (channel == null)
             { return; }
-            if (bot != null && !channel.RegisterText(text, Time.time))
-            { bot.Social.Psychology.NextSpeech = Time.time + 8f; return; }
+            if (bot != null && !channel.RegisterText(text, SessionClock.Time))
+            { bot.Social.Psychology.NextSpeech = SessionClock.Time + 8f; return; }
             channel.Add(new BotChatMessage(channel.Version + 1, bot, day.Day, day.GameMinute, text, "society", kind, context));
-            if (bot != null) { channel.NextConversation = Time.time + Mathf.Max(1f, settings.roomMessageInterval); }
+            if (bot != null) { channel.NextConversation = SessionClock.Time + Mathf.Max(1f, settings.roomMessageInterval); }
         }
         if (bot != null)
-        { bot.Social.LastSpeech = Time.time; bot.Social.Psychology.NextSpeech = Time.time + settings.speechCooldown + (float)random.NextDouble() * 30f; }
+        { bot.Social.LastSpeech = SessionClock.Time; bot.Social.Psychology.NextSpeech = SessionClock.Time + settings.speechCooldown + (float)random.NextDouble() * 30f; }
     }
     public void RecordOutcome(TradeBot bot, InvestorExperience experience)
     {
@@ -290,11 +298,11 @@
         { LastAction = bot.Social.Nickname + ": " + feeReason; Trace(bot,"이용료 거절: " + feeReason); return false; }
         if (room.PlayerOwned && playerInvited)
         { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
-        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
+        mind.Room = roomId; mind.JoinedRoomAt = SessionClock.Time; room.Members.Add(bot.Social.Id); Joins++;
         mind.LastWitnessRoom = 0;
         mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
         PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
-        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
+        if (SessionClock.Time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
         { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
         Trace(bot, roomId + "번 방 참여"); return true;
     }
@@ -326,7 +334,7 @@
         { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
         var mind = bot.Social.Psychology;
         bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
-        if (!firstMember && (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown))
+        if (!firstMember && (SessionClock.Time < nextContact || SessionClock.Time - mind.LastContact < settings.repeatContactCooldown))
         { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
         if (mind.Room != 0)
         { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
@@ -335,7 +343,7 @@
         if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
             mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
         { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
-        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
+        nextContact = SessionClock.Time + settings.contactCooldown; mind.LastContact = SessionClock.Time;
         mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
         bool willing = firstMember || random.NextDouble() < mind.JoinChance;
         LastAction = bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
@@ -351,7 +359,7 @@
         if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || buy < 1m || sell <= buy || buy != PriceTickPolicy.RoundToTick(buy) || sell != PriceTickPolicy.RoundToTick(sell))
         { LastAction = "거래 가능한 종목과 올바른 원화 호가를 입력하세요. 매도가는 매수가보다 커야 합니다."; return false; }
         CancelRoom(roomId);
-        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = Time.time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
+        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, IssuedAt = SessionClock.Time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
         LastAction = $"{room.DisplayName}: {stock.stockName} 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
         Speak(null, roomId, LastAction, default); return true;
     }
@@ -367,7 +375,7 @@
     public void Trace(TradeBot bot, string cause)
     {
         if (DebugTrace.Count >= 64) { DebugTrace.Dequeue(); }
-        string entry = $"{Time.time:0.0}s #{bot.Social.Id} {cause}";
+        string entry = $"{SessionClock.Time:0.0}s #{bot.Social.Id} {cause}";
         DebugTrace.Enqueue(entry);
         if (settings.trace) { Debug.Log("[InvestorSociety] " + entry); }
     }
```

## Assets/Scripts/NightShift/LeadingRoomOrders.cs
변경 덩어리: 4

### private void Delisted(Stock stock)
원본
```csharp
    private void Delisted(Stock stock)
    {
        // Existing delisting policy removes all holdings without compensation. Settle the remaining acquired basis once.
        foreach (var plan in plans.Values)
        {
            if (plan.Command.Stock != stock || plan.Reported)
            { continue; }
            CancelPending(plan);
            if (plan.Remaining <= 0m)
            { plan.State = Stage.Canceled; continue; }
            plan.RealizedCost += plan.Basis; plan.Basis = plan.Remaining = 0m;
            plan.State = Stage.Completed; plan.Reported = true;
            society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time, "상장폐지 손실", plan.AssetBefore));
        }
        stock.OnMatchedTrade -= Fill; subscribed.Remove(stock);
    }
```
수정본
```csharp
    private void Delisted(Stock stock)
    {
        // Existing delisting policy removes all holdings without compensation. Settle the remaining acquired basis once.
        foreach (var plan in plans.Values)
        {
            if (plan.Command.Stock != stock || plan.Reported)
            { continue; }
            CancelPending(plan);
            if (plan.Remaining <= 0m)
            { plan.State = Stage.Canceled; continue; }
            plan.RealizedCost += plan.Basis; plan.Basis = plan.Remaining = 0m;
            plan.State = Stage.Completed; plan.Reported = true;
            society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, SessionClock.Time, "상장폐지 손실", plan.AssetBefore));
        }
        stock.OnMatchedTrade -= Fill; subscribed.Remove(stock);
    }
```

### private void ObserveCommands(Stock stock, decimal price)
원본
```csharp
    private void ObserveCommands(Stock stock, decimal price)
    {
        int day = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
        // 방 3개의 지시만 검사한다. 매 체결마다 전체 봇을 순회하지 않는다.
        foreach (var room in society.Rooms)
        {
            var command = room.Command;
            if (!room.PlayerOwned || command == null || command.Stock != stock || !command.ObserveTrade(price, day, MarketDayController.CanTrade))
            { continue; }
            long eventId = society.NextEventId();
            var observation = new InvestorForecastObservation(eventId, command.Id, command.Room, stock.symbol, command.Buy, command.Sell, Time.time);
            foreach (var plan in command.Observers)
            {
                var bot = plan.Bot;
                if (plan.State != Stage.Rejected || bot == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != command.Room || !bot.Social.Psychology.ActiveAt(Time.time))
                { continue; }
                if (observedResults.Count >= 256) { FlushObservedResults(1); }
                observedResults.Enqueue((bot, observation));
            }
            command.Observers.Clear();
        }
    }
```
수정본
```csharp
    private void ObserveCommands(Stock stock, decimal price)
    {
        int day = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
        // 방 3개의 지시만 검사한다. 매 체결마다 전체 봇을 순회하지 않는다.
        foreach (var room in society.Rooms)
        {
            var command = room.Command;
            if (!room.PlayerOwned || command == null || command.Stock != stock || !command.ObserveTrade(price, day, MarketDayController.CanTrade))
            { continue; }
            long eventId = society.NextEventId();
            var observation = new InvestorForecastObservation(eventId, command.Id, command.Room, stock.symbol, command.Buy, command.Sell, SessionClock.Time);
            foreach (var plan in command.Observers)
            {
                var bot = plan.Bot;
                if (plan.State != Stage.Rejected || bot == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != command.Room || !bot.Social.Psychology.ActiveAt(SessionClock.Time))
                { continue; }
                if (observedResults.Count >= 256) { FlushObservedResults(1); }
                observedResults.Enqueue((bot, observation));
            }
            command.Observers.Clear();
        }
    }
```

### private void Attribute(Plan plan, Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
원본
```csharp
    private void Attribute(Plan plan, Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
    {
        if (plan.Command.Stock != stock || plan.Reported)
        { return; }
        if (buyer == plan.Bot)
        {
            plan.Bought += volume; plan.Remaining += volume; plan.Basis += price * volume;
            if (plan.Bought >= plan.Goal) { plan.State = Stage.AwaitSell; }
        }
        else if (seller == plan.Bot && plan.Remaining > 0m)
        {
            decimal quantity = Math.Min(volume, plan.Remaining);
            decimal cost = plan.Basis * quantity / plan.Remaining;
            plan.Basis -= cost; plan.Remaining -= quantity; plan.RealizedCost += cost; plan.Proceeds += price * quantity;
            if (plan.Remaining <= 0m)
            {
                plan.Reported = true; plan.State = Stage.Completed;
                society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time, assetBefore: plan.AssetBefore));
            }
        }
    }
```
수정본
```csharp
    private void Attribute(Plan plan, Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
    {
        if (plan.Command.Stock != stock || plan.Reported)
        { return; }
        if (buyer == plan.Bot)
        {
            plan.Bought += volume; plan.Remaining += volume; plan.Basis += price * volume;
            if (plan.Bought >= plan.Goal) { plan.State = Stage.AwaitSell; }
        }
        else if (seller == plan.Bot && plan.Remaining > 0m)
        {
            decimal quantity = Math.Min(volume, plan.Remaining);
            decimal cost = plan.Basis * quantity / plan.Remaining;
            plan.Basis -= cost; plan.Remaining -= quantity; plan.RealizedCost += cost; plan.Proceeds += price * quantity;
            if (plan.Remaining <= 0m)
            {
                plan.Reported = true; plan.State = Stage.Completed;
                society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, SessionClock.Time, assetBefore: plan.AssetBefore));
            }
        }
    }
```

### public void ReconnectSession()
원본
```csharp
// 새 함수
```
수정본
```csharp
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks)
        { stock.OnMatchedTrade -= Fill; stock.OnMatchedTrade += Fill; }
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -65,6 +65,11 @@
         subscribed.Add(stock); stock.OnMatchedTrade += Fill;
     }
     public Plan Get(TradeBot bot) => plans.TryGetValue(bot, out var plan) ? plan : null;
+    public void ReconnectSession()
+    {
+        foreach (var stock in StockManager.Instance.stocks)
+        { stock.OnMatchedTrade -= Fill; stock.OnMatchedTrade += Fill; }
+    }
     public bool AllowsRegular(TradeBot bot, Stock stock)
     {
         return !plans.TryGetValue(bot, out var plan) || plan.Command.Stock != stock || submitting == plan ||
@@ -201,11 +206,11 @@
             if (!room.PlayerOwned || command == null || command.Stock != stock || !command.ObserveTrade(price, day, MarketDayController.CanTrade))
             { continue; }
             long eventId = society.NextEventId();
-            var observation = new InvestorForecastObservation(eventId, command.Id, command.Room, stock.symbol, command.Buy, command.Sell, Time.time);
+            var observation = new InvestorForecastObservation(eventId, command.Id, command.Room, stock.symbol, command.Buy, command.Sell, SessionClock.Time);
             foreach (var plan in command.Observers)
             {
                 var bot = plan.Bot;
-                if (plan.State != Stage.Rejected || bot == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != command.Room || !bot.Social.Psychology.ActiveAt(Time.time))
+                if (plan.State != Stage.Rejected || bot == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != command.Room || !bot.Social.Psychology.ActiveAt(SessionClock.Time))
                 { continue; }
                 if (observedResults.Count >= 256) { FlushObservedResults(1); }
                 observedResults.Enqueue((bot, observation));
@@ -242,7 +247,7 @@
             if (plan.Remaining <= 0m)
             {
                 plan.Reported = true; plan.State = Stage.Completed;
-                society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time, assetBefore: plan.AssetBefore));
+                society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, SessionClock.Time, assetBefore: plan.AssetBefore));
             }
         }
     }
@@ -263,7 +268,7 @@
             { plan.State = Stage.Canceled; continue; }
             plan.RealizedCost += plan.Basis; plan.Basis = plan.Remaining = 0m;
             plan.State = Stage.Completed; plan.Reported = true;
-            society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time, "상장폐지 손실", plan.AssetBefore));
+            society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, SessionClock.Time, "상장폐지 손실", plan.AssetBefore));
         }
         stock.OnMatchedTrade -= Fill; subscribed.Remove(stock);
     }
```

## Assets/Scripts/NightShift/MarketRhythmDirector.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -108,7 +108,7 @@
         stock.orderBook.CopyOwnerOrders(bot, orders);
         for (int i = orders.Count - 1; i >= 0; i--)
         {
-            if (Time.time - orders[i].createdAt > 18f)
+            if (SessionClock.Time - orders[i].createdAt > 18f)
             { stock.orderBook.CancelOrder(orders[i], stock); orders.RemoveAt(i); }
         }
         if (orders.Count >= 4)
```

## Assets/Scripts/NightShift/NpcLeadingRoom.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -40,7 +40,7 @@
     public string LeaderName => Leader == null || Leader.Social == null ? "운영자 대기" : Leader.Social.Nickname;
     public bool Available => Leader != null && Leader.isActiveAndEnabled && Leader.Social != null && Leader.Social.Psychology.Room == RoomId;
     public float NextObservationAt => nextObservation;
-    public bool HasSignal => Available && Current != null && Current.LeaderId == Leader.Social.Id && Time.time < Current.ExpiresAt && Current.Stock.AcceptsOrders;
+    public bool HasSignal => Available && Current != null && Current.LeaderId == Leader.Social.Id && SessionClock.Time < Current.ExpiresAt && Current.Stock.AcceptsOrders;
     private readonly HashSet<Stock> watched = new();
     private readonly List<Order> orders = new(32);
     private float nextObservation, nextMessage, lastBuyTime;
@@ -65,9 +65,9 @@
     }
     void Update()
     {
-        if (!MarketDayController.CanTrade || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.time < nextObservation)
-        { return; }
-        Observe(Time.time);
+        if (!MarketDayController.CanTrade || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || SessionClock.Time < nextObservation)
+        { return; }
+        Observe(SessionClock.Time);
     }
     public void Observe(float now)
     {
@@ -137,7 +137,7 @@
             RealizedProfit += (price - basis) * quantity;
         }
         else
-        { lastBought = stock; lastBuyPrice = price; lastBuyTime = Time.time; }
+        { lastBought = stock; lastBuyPrice = price; lastBuyTime = SessionClock.Time; }
     }
     private void PublishObservation(float now)
     {
```

## Assets/Scripts/NightShift/PhoneCallController.cs
변경 덩어리: 5

### void Update()
원본
```csharp
    void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            GameLocalizedText.Set(title,Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록");
            GameLocalizedText.Set(body,Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message);
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
```
수정본
```csharp
    void Update()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && SessionClock.Time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && SessionClock.Time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            GameLocalizedText.Set(title,Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록");
            GameLocalizedText.Set(body,Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message);
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
```

### void Start()
원본
```csharp
    void Start()
    {
        authoredScale = grip.localScale; authoredPosition = grip.localPosition;
        grip.gameObject.SetActive(false);
        answer.onClick.AddListener(Answer);
        decline.onClick.AddListener(Decline);
        close.onClick.AddListener(Lower);
        nextCall = Time.time + 35f;
    }
```
수정본
```csharp
    void Start()
    {
        authoredScale = grip.localScale; authoredPosition = grip.localPosition;
        grip.gameObject.SetActive(false);
        answer.onClick.AddListener(Answer);
        decline.onClick.AddListener(Decline);
        close.onClick.AddListener(Lower);
        nextCall = SessionClock.Time + 35f;
    }
```

### public void Ring(Caller caller)
원본
```csharp
    public void Ring(Caller caller)
    {
        if (Ringing)
        { return; }
        CurrentCaller = caller; Ringing = true; expires = Time.time + 70f;
    }
```
수정본
```csharp
    public void Ring(Caller caller)
    {
        if (Ringing)
        { return; }
        CurrentCaller = caller; Ringing = true; expires = SessionClock.Time + 70f;
    }
```

### private void Resolve()
원본
```csharp
    private void Resolve() { Ringing = false; ResolvedCalls++; nextCall = Time.time + random.Next(210, 360); }
```
수정본
```csharp
    private void Resolve() { Ringing = false; ResolvedCalls++; nextCall = SessionClock.Time + random.Next(210, 360); }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -12,7 +12,7 @@
     public TextMeshProUGUI title, body, alert;
     public Button answer, decline, close;
     public PhoneScreenUI screenUI;
-    public int RemainingSeconds => Mathf.CeilToInt(Mathf.Max(0f, expires - Time.time));
+    public int RemainingSeconds => Mathf.CeilToInt(Mathf.Max(0f, expires - SessionClock.Time));
     public string LastMessage => message;
     public bool Ringing { get; private set; }
     public Caller CurrentCaller { get; private set; }
@@ -30,10 +30,11 @@
         answer.onClick.AddListener(Answer);
         decline.onClick.AddListener(Decline);
         close.onClick.AddListener(Lower);
-        nextCall = Time.time + 35f;
+        nextCall = SessionClock.Time + 35f;
     }
     void Update()
     {
+        if (PauseMenuController.BlocksInput) { return; }
         if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
         { return; }
         if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
@@ -41,8 +42,8 @@
         if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
         if (MarketDayController.CanTrade)
         {
-            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
-            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
+            if (!Ringing && SessionClock.Time >= nextCall) { Ring((Caller)random.Next(3)); }
+            if (Ringing && SessionClock.Time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
         }
         if (Time.unscaledTime < nextUi)
         { return; }
@@ -60,7 +61,7 @@
     {
         if (Ringing)
         { return; }
-        CurrentCaller = caller; Ringing = true; expires = Time.time + 70f;
+        CurrentCaller = caller; Ringing = true; expires = SessionClock.Time + 70f;
     }
     public void Answer()
     {
@@ -82,7 +83,7 @@
         message = "전화를 거절했습니다. 시장은 계속 움직입니다.";
         Resolve();
     }
-    private void Resolve() { Ringing = false; ResolvedCalls++; nextCall = Time.time + random.Next(210, 360); }
+    private void Resolve() { Ringing = false; ResolvedCalls++; nextCall = SessionClock.Time + random.Next(210, 360); }
     public void Raise()
     {
         DeskViewController.Instance?.ExitView(true);
```

## Assets/Scripts/NightShift/TabletAccountUI.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -86,7 +86,7 @@
         { return; }
         next = Time.unscaledTime + .3f;
         var day = MarketDayController.Instance;
-        DeviceTerminalUI.Set(clock,day == null ? "DAY —  —:—" : "DAY " + day.Day + "  " + DeviceMarketJournal.Stamp());
+        DeviceTerminalUI.Set(clock,day == null ? "DAY —  —:—" : "DAY " + day.CalendarDay + "  " + DeviceMarketJournal.Stamp());
         transferButton.interactable = accounts.AutomatedUnlocked;
         tabs[3].interactable = dashboard.Selected != null && account.GetOwnedVolume(dashboard.Selected) > 0m;
         if (CurrentPage != Page.Records || seenVersion == events.records.Version)
```

## Assets/Scripts/NightShift/TradingOperationController.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -56,6 +56,11 @@
         { enabled = false; return; }
         Instance = this;
     }
+    public void ReconnectSession()
+    {
+        foreach (var stock in StockManager.Instance.stocks) { stock.OnMatchedTrade -= OnFill; }
+        if (Target != null && Active) { Target.OnMatchedTrade += OnFill; }
+    }
     void Start()
     {
         if (account == null && StockManager.Instance != null)
@@ -102,7 +107,7 @@
         { return; }
         CancelPending();
         CurrentMode = mode;
-        nextOrder = Time.time;
+        nextOrder = SessionClock.Time;
         Feedback = mode == Mode.Paused ? "내 미체결 주문을 회수했습니다. 시장은 계속 움직입니다." : ModeName + " 실행. 주문·체결·잔여 물량을 확인하세요.";
     }
     void Update()
@@ -124,9 +129,9 @@
         if (CurrentMode == Mode.Prepared)
         { return; }
         Elapsed += Time.deltaTime;
-        if (CurrentMode == Mode.Paused || Time.time < nextOrder)
-        { return; }
-        nextOrder = Time.time + (CurrentMode == Mode.Drive ? 2f : 3.5f);
+        if (CurrentMode == Mode.Paused || SessionClock.Time < nextOrder)
+        { return; }
+        nextOrder = SessionClock.Time + (CurrentMode == Mode.Drive ? 2f : 3.5f);
         CancelPending();
         SubmitSlice();
     }
```

## Assets/Scripts/NightShift/WorldDramaDirector.cs
변경 덩어리: 8

### private void EvaluateMemory(Fund fund)
원본
```csharp
    private void EvaluateMemory(Fund fund)
    {
        decimal change = fund.EncounterStock.currentPrice / Math.Max(1m, fund.EncounterPrice) - 1m;
        float outcome = Mathf.Clamp((float)change * (fund.Bought ? 100f : -100f), -20f, 20f);
        fund.Relationships.TryGetValue(fund.Counterparty, out float score);
        fund.Relationships[fund.Counterparty] = Mathf.Clamp(score + outcome, -100f, 100f);
        MemoryCount++;
        if (Mathf.Abs(outcome) >= 4f && Time.time > fund.NextNotice)
        {
            string other = PlayerAccounts.IsPlayer(fund.Counterparty) ? "플레이어" : fund.Counterparty is TradeBot bot ? bot.Social.Nickname : "맞은편 계좌";
            MarketDayController.Instance.PublishParticipant(fund.Account, other + (outcome < 0f ? "와 거래한 뒤 흐름이 불리해졌네요. 다음에는 경계하겠습니다." : "와 거래한 뒤 흐름이 나쁘지 않았습니다. 다음 움직임도 지켜보죠."));
            fund.NextNotice = Time.time + 120f;
        }
        fund.Counterparty = null;
    }
```
수정본
```csharp
    private void EvaluateMemory(Fund fund)
    {
        decimal change = fund.EncounterStock.currentPrice / Math.Max(1m, fund.EncounterPrice) - 1m;
        float outcome = Mathf.Clamp((float)change * (fund.Bought ? 100f : -100f), -20f, 20f);
        fund.Relationships.TryGetValue(fund.Counterparty, out float score);
        fund.Relationships[fund.Counterparty] = Mathf.Clamp(score + outcome, -100f, 100f);
        MemoryCount++;
        if (Mathf.Abs(outcome) >= 4f && SessionClock.Time > fund.NextNotice)
        {
            string other = PlayerAccounts.IsPlayer(fund.Counterparty) ? "플레이어" : fund.Counterparty is TradeBot bot ? bot.Social.Nickname : "맞은편 계좌";
            MarketDayController.Instance.PublishParticipant(fund.Account, other + (outcome < 0f ? "와 거래한 뒤 흐름이 불리해졌네요. 다음에는 경계하겠습니다." : "와 거래한 뒤 흐름이 나쁘지 않았습니다. 다음 움직임도 지켜보죠."));
            fund.NextNotice = SessionClock.Time + 120f;
        }
        fund.Counterparty = null;
    }
```

### void Update()
원본
```csharp
    void Update()
    {
        if (!MarketDayController.CanTrade || Time.time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }
        nextUpdate = Time.time + 2f;
        if (Funds.Count == 0)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (!bot.IsCompanyOwner || bot.IsExchangeParticipant || bot.Social == null)
                { continue; }
                var fund = new Fund { Account = bot, OpeningEquity = bot.Equity(), PeakEquity = bot.Equity() };
                Funds.Add(fund); byAccount[bot] = fund;
            }
        }
        foreach (var fund in Funds)
        {
            if (fund.Account == null || fund.Closed)
            { continue; }
            decimal equity = fund.Account.Equity();
            fund.PeakEquity = Math.Max(fund.PeakEquity, equity);
            if (!fund.Reducing && fund.PeakEquity > 0m && equity < fund.PeakEquity * .45m)
            {
                fund.Reducing = true;
                InvestorSociety.Instance?.Leave(fund.Account, "운용자산 축소");
                Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 고점 대비 운용자산 55% 이상 감소. 자산 축소에 돌입.");
            }
            if (fund.Counterparty != null && Time.time >= fund.EvaluateAt)
            { EvaluateMemory(fund); }
            if (fund.Reducing) { Liquidate(fund); }
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (!MarketDayController.CanTrade || SessionClock.Time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }
        nextUpdate = SessionClock.Time + 2f;
        if (Funds.Count == 0)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (!bot.IsCompanyOwner || bot.IsExchangeParticipant || bot.Social == null)
                { continue; }
                var fund = new Fund { Account = bot, OpeningEquity = bot.Equity(), PeakEquity = bot.Equity() };
                Funds.Add(fund); byAccount[bot] = fund;
            }
        }
        foreach (var fund in Funds)
        {
            if (fund.Account == null || fund.Closed)
            { continue; }
            decimal equity = fund.Account.Equity();
            fund.PeakEquity = Math.Max(fund.PeakEquity, equity);
            if (!fund.Reducing && fund.PeakEquity > 0m && equity < fund.PeakEquity * .45m)
            {
                fund.Reducing = true;
                InvestorSociety.Instance?.Leave(fund.Account, "운용자산 축소");
                Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 고점 대비 운용자산 55% 이상 감소. 자산 축소에 돌입.");
            }
            if (fund.Counterparty != null && SessionClock.Time >= fund.EvaluateAt)
            { EvaluateMemory(fund); }
            if (fund.Reducing) { Liquidate(fund); }
        }
    }
```

### private void Liquidate(Fund fund)
원본
```csharp
    private void Liquidate(Fund fund)
    {
        bool sharesRemain = false;
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CopyOwnerOrders(fund.Account, ownOrders);
            foreach (var order in ownOrders) { if (order.isBuy || Time.time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
            if (fund.Account.GetOwnedVolume(stock) < 1m)
            { continue; }
            sharesRemain = true;
            stock.orderBook.GetTopBids(levels, 1);
            if (levels.Count > 0 && fund.Account.GetHolding(stock) > 0m)
            { fund.Account.SellLimit(levels[0].price, Math.Min(1000m, fund.Account.GetHolding(stock)), 0, stock); }
            break;
        }
        if (!sharesRemain)
        {
            fund.Closed = true; fund.Account.enabled = false;
            Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 자산 정리 완료, 운용 중단. 남은 현금 " + fund.Account.cash.ToString("N0") + "원.");
        }
    }
```
수정본
```csharp
    private void Liquidate(Fund fund)
    {
        bool sharesRemain = false;
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CopyOwnerOrders(fund.Account, ownOrders);
            foreach (var order in ownOrders) { if (order.isBuy || SessionClock.Time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
            if (fund.Account.GetOwnedVolume(stock) < 1m)
            { continue; }
            sharesRemain = true;
            stock.orderBook.GetTopBids(levels, 1);
            if (levels.Count > 0 && fund.Account.GetHolding(stock) > 0m)
            { fund.Account.SellLimit(levels[0].price, Math.Min(1000m, fund.Account.GetHolding(stock)), 0, stock); }
            break;
        }
        if (!sharesRemain)
        {
            fund.Closed = true; fund.Account.enabled = false;
            Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 자산 정리 완료, 운용 중단. 남은 현금 " + fund.Account.cash.ToString("N0") + "원.");
        }
    }
```

### public bool TryFundDecision(TradeBot bot, Stock stock)
원본
```csharp
    public bool TryFundDecision(TradeBot bot, Stock stock)
    {
        if (!byAccount.TryGetValue(bot, out var fund) || bot is WhaleBot whale && whale.PreDisposal)
        { return false; }
        if (fund.Reducing || fund.Closed)
        { return true; }
        float caution = 0f;
        float relation = 0f;
        fund.Relationships.TryGetValue(StockManager.Instance.playerAccount, out relation);
        if (PlayerAccounts.Instance != null && fund.Relationships.TryGetValue(PlayerAccounts.Instance.automated, out float otherRelation))
        { relation += otherRelation; }
        if (relation != 0f)
        { caution = Mathf.Clamp(-relation / 100f, 0f, 1f); }
        decimal basis = bot.GetOwnedAverage(stock);
        bool sell = bot.GetHolding(stock) > 0m && (basis > 0m && stock.currentPrice > basis * (decimal)(1.04f + bot.Social.Greed * .1f) || bot.Social.Stress > 65f || caution > .35f);
        bool buy = !sell && bot.cash - bot.ReservedCash > bot.Equity() * .03m && stock.currentPrice < stock.fakeValue * (decimal)(1.03f + bot.Social.Greed * .08f);
        if (!sell && !buy)
        { return true; }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        foreach (var order in ownOrders)
        { if (Time.time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        if (ownOrders.Count >= 3)
        { return true; }
        if (buy) { stock.orderBook.GetTopAsks(levels, 1); } else { stock.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { return true; }
        decimal price = levels[0].price;
        decimal quantity = decimal.Floor(Math.Min(1000m, buy ? Math.Max(0m, bot.cash - bot.ReservedCash) * .005m / price : bot.GetHolding(stock) * .01m));
        if (quantity >= 1m)
        { if (buy) { bot.BuyLimit(price, quantity, 0, stock); } else { bot.SellLimit(price, quantity, 0, stock); } }
        return true;
    }
```
수정본
```csharp
    public bool TryFundDecision(TradeBot bot, Stock stock)
    {
        if (!byAccount.TryGetValue(bot, out var fund) || bot is WhaleBot whale && whale.PreDisposal)
        { return false; }
        if (fund.Reducing || fund.Closed)
        { return true; }
        float caution = 0f;
        float relation = 0f;
        fund.Relationships.TryGetValue(StockManager.Instance.playerAccount, out relation);
        if (PlayerAccounts.Instance != null && fund.Relationships.TryGetValue(PlayerAccounts.Instance.automated, out float otherRelation))
        { relation += otherRelation; }
        if (relation != 0f)
        { caution = Mathf.Clamp(-relation / 100f, 0f, 1f); }
        decimal basis = bot.GetOwnedAverage(stock);
        bool sell = bot.GetHolding(stock) > 0m && (basis > 0m && stock.currentPrice > basis * (decimal)(1.04f + bot.Social.Greed * .1f) || bot.Social.Stress > 65f || caution > .35f);
        bool buy = !sell && bot.cash - bot.ReservedCash > bot.Equity() * .03m && stock.currentPrice < stock.fakeValue * (decimal)(1.03f + bot.Social.Greed * .08f);
        if (!sell && !buy)
        { return true; }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        foreach (var order in ownOrders)
        { if (SessionClock.Time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        if (ownOrders.Count >= 3)
        { return true; }
        if (buy) { stock.orderBook.GetTopAsks(levels, 1); } else { stock.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { return true; }
        decimal price = levels[0].price;
        decimal quantity = decimal.Floor(Math.Min(1000m, buy ? Math.Max(0m, bot.cash - bot.ReservedCash) * .005m / price : bot.GetHolding(stock) * .01m));
        if (quantity >= 1m)
        { if (buy) { bot.BuyLimit(price, quantity, 0, stock); } else { bot.SellLimit(price, quantity, 0, stock); } }
        return true;
    }
```

### private void Remember(Trader actor, Trader other, Stock stock, decimal price, decimal volume, bool bought)
원본
```csharp
    private void Remember(Trader actor, Trader other, Stock stock, decimal price, decimal volume, bool bought)
    {
        if (!byAccount.TryGetValue(actor, out var fund) || (!PlayerAccounts.IsPlayer(other) && !byAccount.ContainsKey(other)) || fund.Counterparty != null)
        { return; }
        fund.Counterparty = other; fund.EncounterStock = stock; fund.EncounterPrice = price;
        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = Time.time + 20f;
    }
```
수정본
```csharp
    private void Remember(Trader actor, Trader other, Stock stock, decimal price, decimal volume, bool bought)
    {
        if (!byAccount.TryGetValue(actor, out var fund) || (!PlayerAccounts.IsPlayer(other) && !byAccount.ContainsKey(other)) || fund.Counterparty != null)
        { return; }
        fund.Counterparty = other; fund.EncounterStock = stock; fund.EncounterPrice = price;
        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = SessionClock.Time + 20f;
    }
```

### public bool BoardDecision(Stock stock, int action, Trader payer = null)
원본
```csharp
    public bool BoardDecision(Stock stock, int action, Trader payer = null)
    {
        if (!MarketDayController.CanTrade || !Controls(stock))
        { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
        if (boardCooldown.TryGetValue(stock, out float next) && next > Time.time)
        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - Time.time)}초"; return false; }
        var player = payer != null ? payer : StockManager.Instance.playerAccount;
        decimal cost = action == 2 ? stock.issuedShares : Math.Max(10000m, decimal.Floor(player.cash * .02m));
        if (player.cash - player.ReservedCash < cost)
        { BoardResult = "가용 현금이 부족합니다."; return false; }
        // The action payment is transferred to an existing counterparty, never destroyed or minted.
        if (action == 2)
        {
            decimal payout = 0m;
            foreach (var holder in Trader.Registry) { payout += holder.GetOwnedVolume(stock); }
            if (payout > cost)
            { BoardResult = "주주 원장을 확인할 수 없습니다."; return false; }
            player.ApplyTradeCash(-payout);
            foreach (var holder in Trader.Registry) { holder.ApplyTradeCash(holder.GetOwnedVolume(stock)); }
            cost = payout;
        }
        else { player.ApplyTradeCash(-cost); ExchangeBot.Instance.ApplyTradeCash(cost); }
        boardActions.TryGetValue(stock, out int revision);
        boardActions[stock] = ++revision; boardCooldown[stock] = Time.time + 120f;
        float quality = Mathf.PerlinNoise(revision * .7f, stock.symbol.Length * .31f);
        decimal effect = action == 0 ? .98m + (decimal)quality * .06m : action == 1 ? .95m + (decimal)quality * .12m : 1m;
        stock.SetRealValue(Math.Max(1m, stock.realValue * effect));
        string decision = action == 0 ? "경영진 교체" : action == 1 ? "신사업 투자" : "대주주 자비 특별배당 (주당 1원)";
        BoardResult = decision + " 완료. 시장 평가는 참여자들의 판단에 달려 있습니다.";
        Publish(stock, stock.stockName + ": 플레이어 주도 " + decision + ". 집행 비용 " + cost.ToString("N0") + "원.");
        return true;
    }
```
수정본
```csharp
    public bool BoardDecision(Stock stock, int action, Trader payer = null)
    {
        if (!MarketDayController.CanTrade || !Controls(stock))
        { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
        if (boardCooldown.TryGetValue(stock, out float next) && next > SessionClock.Time)
        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - SessionClock.Time)}초"; return false; }
        var player = payer != null ? payer : StockManager.Instance.playerAccount;
        decimal cost = action == 2 ? stock.issuedShares : Math.Max(10000m, decimal.Floor(player.cash * .02m));
        if (player.cash - player.ReservedCash < cost)
        { BoardResult = "가용 현금이 부족합니다."; return false; }
        // The action payment is transferred to an existing counterparty, never destroyed or minted.
        if (action == 2)
        {
            decimal payout = 0m;
            foreach (var holder in Trader.Registry) { payout += holder.GetOwnedVolume(stock); }
            if (payout > cost)
            { BoardResult = "주주 원장을 확인할 수 없습니다."; return false; }
            player.ApplyTradeCash(-payout);
            foreach (var holder in Trader.Registry) { holder.ApplyTradeCash(holder.GetOwnedVolume(stock)); }
            cost = payout;
        }
        else { player.ApplyTradeCash(-cost); ExchangeBot.Instance.ApplyTradeCash(cost); }
        boardActions.TryGetValue(stock, out int revision);
        boardActions[stock] = ++revision; boardCooldown[stock] = SessionClock.Time + 120f;
        float quality = Mathf.PerlinNoise(revision * .7f, stock.symbol.Length * .31f);
        decimal effect = action == 0 ? .98m + (decimal)quality * .06m : action == 1 ? .95m + (decimal)quality * .12m : 1m;
        stock.SetRealValue(Math.Max(1m, stock.realValue * effect));
        string decision = action == 0 ? "경영진 교체" : action == 1 ? "신사업 투자" : "대주주 자비 특별배당 (주당 1원)";
        BoardResult = decision + " 완료. 시장 평가는 참여자들의 판단에 달려 있습니다.";
        Publish(stock, stock.stockName + ": 플레이어 주도 " + decision + ". 집행 비용 " + cost.ToString("N0") + "원.");
        return true;
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -45,9 +45,9 @@
     }
     void Update()
     {
-        if (!MarketDayController.CanTrade || Time.time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
-        { return; }
-        nextUpdate = Time.time + 2f;
+        if (!MarketDayController.CanTrade || SessionClock.Time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
+        { return; }
+        nextUpdate = SessionClock.Time + 2f;
         if (Funds.Count == 0)
         {
             foreach (var bot in MarketDayController.Instance.Bots)
@@ -70,7 +70,7 @@
                 InvestorSociety.Instance?.Leave(fund.Account, "운용자산 축소");
                 Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 고점 대비 운용자산 55% 이상 감소. 자산 축소에 돌입.");
             }
-            if (fund.Counterparty != null && Time.time >= fund.EvaluateAt)
+            if (fund.Counterparty != null && SessionClock.Time >= fund.EvaluateAt)
             { EvaluateMemory(fund); }
             if (fund.Reducing) { Liquidate(fund); }
         }
@@ -88,7 +88,7 @@
         if (!byAccount.TryGetValue(actor, out var fund) || (!PlayerAccounts.IsPlayer(other) && !byAccount.ContainsKey(other)) || fund.Counterparty != null)
         { return; }
         fund.Counterparty = other; fund.EncounterStock = stock; fund.EncounterPrice = price;
-        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = Time.time + 20f;
+        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = SessionClock.Time + 20f;
     }
     private void EvaluateMemory(Fund fund)
     {
@@ -97,11 +97,11 @@
         fund.Relationships.TryGetValue(fund.Counterparty, out float score);
         fund.Relationships[fund.Counterparty] = Mathf.Clamp(score + outcome, -100f, 100f);
         MemoryCount++;
-        if (Mathf.Abs(outcome) >= 4f && Time.time > fund.NextNotice)
+        if (Mathf.Abs(outcome) >= 4f && SessionClock.Time > fund.NextNotice)
         {
             string other = PlayerAccounts.IsPlayer(fund.Counterparty) ? "플레이어" : fund.Counterparty is TradeBot bot ? bot.Social.Nickname : "맞은편 계좌";
             MarketDayController.Instance.PublishParticipant(fund.Account, other + (outcome < 0f ? "와 거래한 뒤 흐름이 불리해졌네요. 다음에는 경계하겠습니다." : "와 거래한 뒤 흐름이 나쁘지 않았습니다. 다음 움직임도 지켜보죠."));
-            fund.NextNotice = Time.time + 120f;
+            fund.NextNotice = SessionClock.Time + 120f;
         }
         fund.Counterparty = null;
     }
@@ -126,7 +126,7 @@
         { return true; }
         stock.orderBook.CopyOwnerOrders(bot, ownOrders);
         foreach (var order in ownOrders)
-        { if (Time.time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
+        { if (SessionClock.Time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
         stock.orderBook.CopyOwnerOrders(bot, ownOrders);
         if (ownOrders.Count >= 3)
         { return true; }
@@ -145,7 +145,7 @@
         foreach (var stock in StockManager.Instance.stocks)
         {
             stock.orderBook.CopyOwnerOrders(fund.Account, ownOrders);
-            foreach (var order in ownOrders) { if (order.isBuy || Time.time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
+            foreach (var order in ownOrders) { if (order.isBuy || SessionClock.Time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
             if (fund.Account.GetOwnedVolume(stock) < 1m)
             { continue; }
             sharesRemain = true;
@@ -186,8 +186,8 @@
     {
         if (!MarketDayController.CanTrade || !Controls(stock))
         { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
-        if (boardCooldown.TryGetValue(stock, out float next) && next > Time.time)
-        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - Time.time)}초"; return false; }
+        if (boardCooldown.TryGetValue(stock, out float next) && next > SessionClock.Time)
+        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - SessionClock.Time)}초"; return false; }
         var player = payer != null ? payer : StockManager.Instance.playerAccount;
         decimal cost = action == 2 ? stock.issuedShares : Math.Max(10000m, decimal.Floor(player.cash * .02m));
         if (player.cash - player.ReservedCash < cost)
@@ -205,7 +205,7 @@
         }
         else { player.ApplyTradeCash(-cost); ExchangeBot.Instance.ApplyTradeCash(cost); }
         boardActions.TryGetValue(stock, out int revision);
-        boardActions[stock] = ++revision; boardCooldown[stock] = Time.time + 120f;
+        boardActions[stock] = ++revision; boardCooldown[stock] = SessionClock.Time + 120f;
         float quality = Mathf.PerlinNoise(revision * .7f, stock.symbol.Length * .31f);
         decimal effect = action == 0 ? .98m + (decimal)quality * .06m : action == 1 ? .95m + (decimal)quality * .12m : 1m;
         stock.SetRealValue(Math.Max(1m, stock.realValue * effect));
```

## Assets/Scripts/StockListUI/StockListItem.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -226,7 +226,7 @@
             string label = stock.RiskLabel;
             if (stock.IsTradingHalted)
             {
-                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - Time.time)) + "초";
+                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - SessionClock.Time)) + "초";
             }
             if (!GameLocalizedText.Matches(textRisk,label))
             {
```

## Assets/Scripts/StockListUI/StockListUIManager.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -76,6 +76,8 @@
         BuildList();
     }
 
+    public void ReconnectSession() { BuildList(); }
+
     void BuildList()
     {
         if (StockManager.Instance == null)
```

## Assets/Scripts/TradeBot/ExchangeBot.cs
변경 덩어리: 7

### private void Register(Stock stock, bool listing)
원본
```csharp
    private void Register(Stock stock, bool listing)
    {
        if (byStock.ContainsKey(stock))
        { return; }

        var state = new StockState
        {
            Stock = stock,
            Reference = stock.currentPrice,
            InitialInventory = GetOwnedVolume(stock),
            NewListing = listing,
            ListingDay = MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day,
            NextReference = Time.time + Mathf.Max(0.1f, referenceInterval),
            NeedsOpening = true,
            Random = new System.Random(CandleHistoryManager.StableSeed(referenceSeed, stock.symbol))
        };
        states.Add(state);
        byStock.Add(stock, state);
        if (earlyNews.TryGetValue(stock, out var news))
        {
            state.LastNewsId = news.id;
            state.PendingNews = news.multiplier;
            earlyNews.Remove(stock);
        }

        debugStocks.Add(new StockDebug { symbol = stock.symbol });
        stock.OnMatchedTrade += OnMatched;
        if (listing)
        {
            decimal perLevel = decimal.Floor(state.InitialInventory * Ratio(initialListingSupplyRatio) / 50m);
            for (int i = 0; i < 50; i++)
            {
                decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + 9m * i / 49m));
                int destination = i;
                for (int n = 0; n < i; n++)
                {
                    if (state.InitialPrices[n] == price)
                    {
                        destination = n;
                        break;
                    }
                }

                state.InitialPrices[destination] = price;
                state.InitialRemaining[destination] += perLevel;
            }
        }

        RefreshBand(state);
        InitializeGrade(state);
    }
```
수정본
```csharp
    private void Register(Stock stock, bool listing)
    {
        if (byStock.ContainsKey(stock))
        { return; }

        var state = new StockState
        {
            Stock = stock,
            Reference = stock.currentPrice,
            InitialInventory = GetOwnedVolume(stock),
            NewListing = listing,
            ListingDay = MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day,
            NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval),
            NeedsOpening = true,
            Random = new System.Random(CandleHistoryManager.StableSeed(referenceSeed, stock.symbol))
        };
        states.Add(state);
        byStock.Add(stock, state);
        if (earlyNews.TryGetValue(stock, out var news))
        {
            state.LastNewsId = news.id;
            state.PendingNews = news.multiplier;
            earlyNews.Remove(stock);
        }

        debugStocks.Add(new StockDebug { symbol = stock.symbol });
        stock.OnMatchedTrade += OnMatched;
        if (listing)
        {
            decimal perLevel = decimal.Floor(state.InitialInventory * Ratio(initialListingSupplyRatio) / 50m);
            for (int i = 0; i < 50; i++)
            {
                decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + 9m * i / 49m));
                int destination = i;
                for (int n = 0; n < i; n++)
                {
                    if (state.InitialPrices[n] == price)
                    {
                        destination = n;
                        break;
                    }
                }

                state.InitialPrices[destination] = price;
                state.InitialRemaining[destination] += perLevel;
            }
        }

        RefreshBand(state);
        InitializeGrade(state);
    }
```

### private void OnCandleCompleted(Stock stock, CandleData candle, int count)
원본
```csharp
    private void OnCandleCompleted(Stock stock, CandleData candle, int count)
    {
        var state = GetState(stock);
        if (state == null)
        { return; }

        bool previouslyRisky = state.Risky;
        state.CompletedCount = count;
        RefreshBand(state);
        decimal correction = (state.Middle - state.Reference) * Ratio(marketCorrectionFraction);
        if (state.Middle > 0m)
        {
            decimal max = state.Reference * Math.Max(0m, (decimal)marketCorrectionMaxPercent / 100m);
            state.Reference = Positive(state.Reference + Math.Max(-max, Math.Min(max, correction)));
        }

        state.RecentActualTrade = false;
        var completed = CandleHistoryManager.Instance.Completed(stock);
        for (int i = Math.Max(0, completed.Count - 5); i < completed.Count; i++)
        {
            if (!completed[i].historical && completed[i].volume > 0f)
            {
                state.RecentActualTrade = true;
            }
        }

        RecordStableCandle(state);
        if (state.RecoveryNeedsTrade && !state.Risky && state.RecentActualTrade)
        {
            state.RecoveryNeedsTrade = false;
        }

        if (state.NewListing && count == 60)
        {
            CancelInitial(state);
            state.NeedsOpening = false;
            state.NextRegular = Time.time;
        }

        if (!previouslyRisky && state.Risky)
        {
            CancelState(state);
        }
    }
```
수정본
```csharp
    private void OnCandleCompleted(Stock stock, CandleData candle, int count)
    {
        var state = GetState(stock);
        if (state == null)
        { return; }

        bool previouslyRisky = state.Risky;
        state.CompletedCount = count;
        RefreshBand(state);
        decimal correction = (state.Middle - state.Reference) * Ratio(marketCorrectionFraction);
        if (state.Middle > 0m)
        {
            decimal max = state.Reference * Math.Max(0m, (decimal)marketCorrectionMaxPercent / 100m);
            state.Reference = Positive(state.Reference + Math.Max(-max, Math.Min(max, correction)));
        }

        state.RecentActualTrade = false;
        var completed = CandleHistoryManager.Instance.Completed(stock);
        for (int i = Math.Max(0, completed.Count - 5); i < completed.Count; i++)
        {
            if (!completed[i].historical && completed[i].volume > 0f)
            {
                state.RecentActualTrade = true;
            }
        }

        RecordStableCandle(state);
        if (state.RecoveryNeedsTrade && !state.Risky && state.RecentActualTrade)
        {
            state.RecoveryNeedsTrade = false;
        }

        if (state.NewListing && count == 60)
        {
            CancelInitial(state);
            state.NeedsOpening = false;
            state.NextRegular = SessionClock.Time;
        }

        if (!previouslyRisky && state.Risky)
        {
            CancelState(state);
        }
    }
```

### void Update()
원본
```csharp
    void Update()
    {
        if (Time.timeScale <= 0f)
        { return; }
        TickFutureNews();
        TickRiskGrades();
        if (AssetsInitialized && Time.time >= nextDebug)
        {
            UpdateDebug();
            nextDebug = Time.time + 1f;
        }

        if (!AssetsInitialized || !MarketDayController.CanTrade || states.Count == 0)
        { return; }

        DoInitialPlacement();
        ordersLeft = Mathf.Max(6, ordersPerFrame);
        int count = Mathf.Min(states.Count, Mathf.Max(1, stocksPerFrame), Mathf.Max(1, ordersLeft / 6));
        for (int i = 0; i < count; i++)
        {
            if (cursor >= states.Count)
            {
                cursor = 0;
            }

            var state = states[cursor++];
            if (state.Stock.IsDelisting || state.Stock.IsTradingHalted)
            {
                CancelState(state);
                continue;
            }

            float now = Time.time;
            if (now >= state.NextReference)
            {
                using (ReferenceMarker.Auto())
                {
                    AdvanceReference(state);
                    TouchPressure(state, now);
                }

                state.NextReference = now + Mathf.Max(0.1f, referenceInterval);
            }

            if (state.PendingNews != 1m)
            {
                state.Reference = Positive(state.Reference * state.PendingNews);
                state.PendingNews = 1m;
            }

            if (state.NewListing && state.CompletedCount < 60)
            {
                if (state.NeedsOpening)
                {
                    state.NeedsOpening = !PlaceInitial(state);
                }

                continue;
            }

            if (state.NeedsOpening || now >= state.NextRegular)
            {
                using (DefenseMarker.Auto())
                {
                    Reevaluate(state.Stock, now);
                }
            }
        }
    }
```
수정본
```csharp
    void Update()
    {
        if (Time.timeScale <= 0f)
        { return; }
        TickFutureNews();
        TickRiskGrades();
        if (AssetsInitialized && SessionClock.Time >= nextDebug)
        {
            UpdateDebug();
            nextDebug = SessionClock.Time + 1f;
        }

        if (!AssetsInitialized || !MarketDayController.CanTrade || states.Count == 0)
        { return; }

        DoInitialPlacement();
        ordersLeft = Mathf.Max(6, ordersPerFrame);
        int count = Mathf.Min(states.Count, Mathf.Max(1, stocksPerFrame), Mathf.Max(1, ordersLeft / 6));
        for (int i = 0; i < count; i++)
        {
            if (cursor >= states.Count)
            {
                cursor = 0;
            }

            var state = states[cursor++];
            if (state.Stock.IsDelisting || state.Stock.IsTradingHalted)
            {
                CancelState(state);
                continue;
            }

            float now = SessionClock.Time;
            if (now >= state.NextReference)
            {
                using (ReferenceMarker.Auto())
                {
                    AdvanceReference(state);
                    TouchPressure(state, now);
                }

                state.NextReference = now + Mathf.Max(0.1f, referenceInterval);
            }

            if (state.PendingNews != 1m)
            {
                state.Reference = Positive(state.Reference * state.PendingNews);
                state.PendingNews = 1m;
            }

            if (state.NewListing && state.CompletedCount < 60)
            {
                if (state.NeedsOpening)
                {
                    state.NeedsOpening = !PlaceInitial(state);
                }

                continue;
            }

            if (state.NeedsOpening || now >= state.NextRegular)
            {
                using (DefenseMarker.Auto())
                {
                    Reevaluate(state.Stock, now);
                }
            }
        }
    }
```

### public void DoInitialPlacement()
원본
```csharp
    public void DoInitialPlacement()
    {
        if (!AssetsInitialized || !MarketDayController.CanTrade || openingSession == MarketDayController.Session)
        { return; }

        openingSession = MarketDayController.Session;
        foreach (var state in states)
        {
            state.NeedsOpening = true;
            state.NextReference = Time.time + Mathf.Max(0.1f, referenceInterval);
        }
    }
```
수정본
```csharp
    public void DoInitialPlacement()
    {
        if (!AssetsInitialized || !MarketDayController.CanTrade || openingSession == MarketDayController.Session)
        { return; }

        openingSession = MarketDayController.Session;
        foreach (var state in states)
        {
            state.NeedsOpening = true;
            state.NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval);
        }
    }
```

### private void OnMatched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
원본
```csharp
    private void OnMatched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool bought = ReferenceEquals(buyer, this), sold = ReferenceEquals(seller, this);
        if (!bought && !sold)
        { return; }

        var state = GetState(stock);
        if (state == null)
        { return; }

        bool maker = resting != null && ReferenceEquals(resting.owner, this);
        decimal beforeInventory = GetOwnedVolume(stock) + (maker ? sold ? volume : -volume : 0m);
        int index = TouchPressure(state, Time.time, beforeInventory);
        var bucket = state.Pressure[index];
        bucket.NetSold += sold ? volume : -volume;
        bucket.NetSpend += bought ? price * volume : -price * volume;
        state.Pressure[index] = bucket;
        if (sold)
        {
            int slot = resting != null && ReferenceEquals(resting.owner, this) && resting.purpose == OrderPurpose.ExchangeInitialSupply ? resting.purposeSlot : submittingState == state ? submittingInitialSlot : -1;
            if (slot >= 0 && slot < 50)
            {
                state.InitialRemaining[slot] = Math.Max(0m, state.InitialRemaining[slot] - volume);
            }
        }
    }
```
수정본
```csharp
    private void OnMatched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool bought = ReferenceEquals(buyer, this), sold = ReferenceEquals(seller, this);
        if (!bought && !sold)
        { return; }

        var state = GetState(stock);
        if (state == null)
        { return; }

        bool maker = resting != null && ReferenceEquals(resting.owner, this);
        decimal beforeInventory = GetOwnedVolume(stock) + (maker ? sold ? volume : -volume : 0m);
        int index = TouchPressure(state, SessionClock.Time, beforeInventory);
        var bucket = state.Pressure[index];
        bucket.NetSold += sold ? volume : -volume;
        bucket.NetSpend += bought ? price * volume : -price * volume;
        state.Pressure[index] = bucket;
        if (sold)
        {
            int slot = resting != null && ReferenceEquals(resting.owner, this) && resting.purpose == OrderPurpose.ExchangeInitialSupply ? resting.purposeSlot : submittingState == state ? submittingInitialSlot : -1;
            if (slot >= 0 && slot < 50)
            {
                state.InitialRemaining[slot] = Math.Max(0m, state.InitialRemaining[slot] - volume);
            }
        }
    }
```

### private void UpdateDebug()
원본
```csharp
    private void UpdateDebug()
    {
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            var d = debugStocks[i];
            d.reference = (double)s.Reference;
            d.current = (double)s.Stock.currentPrice;
            d.middle = (double)s.Middle;
            d.upper = (double)(s.Middle + 2m * s.Deviation);
            d.lower = (double)(s.Middle - 2m * s.Deviation);
            d.risk = (double)s.Risk;
            d.grade = s.Stock.RiskLabel;
            d.gradePending = s.Stock.RiskPending;
            d.nextGradeEvaluation = Math.Max(0f, s.NextGradeEvaluation - Time.time);
            d.haltRemaining = s.Stock.IsTradingHalted ? Math.Max(0f, s.Stock.HaltUntil - Time.time) : 0;
            int gradeIndex = Math.Min(3, (int)s.Stock.RiskGrade);
            d.cautiousSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.cautiousWeights, gradeIndex);
            d.neutralSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.neutralWeights, gradeIndex);
            d.speculativeSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.speculativeWeights, gradeIndex);
            d.nextReevaluation = Math.Max(0f, s.NextRegular - Time.time);
            d.targetBuy = (double)s.TargetBuy;
            d.targetSell = (double)s.TargetSell;
            d.reservedBuy = (double)ReservedBuy(s);
            d.reservedSell = (double)(ReservedSell(s) + ReservedSell(s, true));
            d.availableCash = (double)(cash - reservedCash);
            d.availableShares = (double)GetHolding(s.Stock);
            d.totalShares = (double)GetOwnedVolume(s.Stock);
            d.initialUnsold = (double)Sum(s.InitialRemaining);
            d.completedCandles = s.CompletedCount;
            d.listingDay = s.ListingDay;
            d.state = s.Stock.IsTradingHalted ? "거래중지" : !MarketDayController.CanTrade ? "장외" : s.Risky ? s.Stock.RiskLabel : s.NewListing && s.CompletedCount < 60 ? "신규 초기 공급" : s.RecoveryStage < 3 ? "단계 복귀" : "정상";
            d.limitReason = s.LimitReason;
            d.buyProtected = s.BuyProtected;
            d.sellProtected = s.SellProtected;
            d.recoveryStep = s.RecoveryStage;
        }
    }
```
수정본
```csharp
    private void UpdateDebug()
    {
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            var d = debugStocks[i];
            d.reference = (double)s.Reference;
            d.current = (double)s.Stock.currentPrice;
            d.middle = (double)s.Middle;
            d.upper = (double)(s.Middle + 2m * s.Deviation);
            d.lower = (double)(s.Middle - 2m * s.Deviation);
            d.risk = (double)s.Risk;
            d.grade = s.Stock.RiskLabel;
            d.gradePending = s.Stock.RiskPending;
            d.nextGradeEvaluation = Math.Max(0f, s.NextGradeEvaluation - SessionClock.Time);
            d.haltRemaining = s.Stock.IsTradingHalted ? Math.Max(0f, s.Stock.HaltUntil - SessionClock.Time) : 0;
            int gradeIndex = Math.Min(3, (int)s.Stock.RiskGrade);
            d.cautiousSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.cautiousWeights, gradeIndex);
            d.neutralSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.neutralWeights, gradeIndex);
            d.speculativeSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.speculativeWeights, gradeIndex);
            d.nextReevaluation = Math.Max(0f, s.NextRegular - SessionClock.Time);
            d.targetBuy = (double)s.TargetBuy;
            d.targetSell = (double)s.TargetSell;
            d.reservedBuy = (double)ReservedBuy(s);
            d.reservedSell = (double)(ReservedSell(s) + ReservedSell(s, true));
            d.availableCash = (double)(cash - reservedCash);
            d.availableShares = (double)GetHolding(s.Stock);
            d.totalShares = (double)GetOwnedVolume(s.Stock);
            d.initialUnsold = (double)Sum(s.InitialRemaining);
            d.completedCandles = s.CompletedCount;
            d.listingDay = s.ListingDay;
            d.state = s.Stock.IsTradingHalted ? "거래중지" : !MarketDayController.CanTrade ? "장외" : s.Risky ? s.Stock.RiskLabel : s.NewListing && s.CompletedCount < 60 ? "신규 초기 공급" : s.RecoveryStage < 3 ? "단계 복귀" : "정상";
            d.limitReason = s.LimitReason;
            d.buyProtected = s.BuyProtected;
            d.sellProtected = s.SellProtected;
            d.recoveryStep = s.RecoveryStage;
        }
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -204,7 +204,7 @@
             InitialInventory = GetOwnedVolume(stock),
             NewListing = listing,
             ListingDay = MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day,
-            NextReference = Time.time + Mathf.Max(0.1f, referenceInterval),
+            NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval),
             NeedsOpening = true,
             Random = new System.Random(CandleHistoryManager.StableSeed(referenceSeed, stock.symbol))
         };
@@ -279,10 +279,10 @@
         { return; }
         TickFutureNews();
         TickRiskGrades();
-        if (AssetsInitialized && Time.time >= nextDebug)
+        if (AssetsInitialized && SessionClock.Time >= nextDebug)
         {
             UpdateDebug();
-            nextDebug = Time.time + 1f;
+            nextDebug = SessionClock.Time + 1f;
         }
 
         if (!AssetsInitialized || !MarketDayController.CanTrade || states.Count == 0)
@@ -305,7 +305,7 @@
                 continue;
             }
 
-            float now = Time.time;
+            float now = SessionClock.Time;
             if (now >= state.NextReference)
             {
                 using (ReferenceMarker.Auto())
@@ -352,7 +352,7 @@
         foreach (var state in states)
         {
             state.NeedsOpening = true;
-            state.NextReference = Time.time + Mathf.Max(0.1f, referenceInterval);
+            state.NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval);
         }
     }
 
@@ -417,7 +417,7 @@
         {
             CancelInitial(state);
             state.NeedsOpening = false;
-            state.NextRegular = Time.time;
+            state.NextRegular = SessionClock.Time;
         }
 
         if (!previouslyRisky && state.Risky)
@@ -478,7 +478,7 @@
 
         bool maker = resting != null && ReferenceEquals(resting.owner, this);
         decimal beforeInventory = GetOwnedVolume(stock) + (maker ? sold ? volume : -volume : 0m);
-        int index = TouchPressure(state, Time.time, beforeInventory);
+        int index = TouchPressure(state, SessionClock.Time, beforeInventory);
         var bucket = state.Pressure[index];
         bucket.NetSold += sold ? volume : -volume;
         bucket.NetSpend += bought ? price * volume : -price * volume;
@@ -1066,13 +1066,13 @@
             d.risk = (double)s.Risk;
             d.grade = s.Stock.RiskLabel;
             d.gradePending = s.Stock.RiskPending;
-            d.nextGradeEvaluation = Math.Max(0f, s.NextGradeEvaluation - Time.time);
-            d.haltRemaining = s.Stock.IsTradingHalted ? Math.Max(0f, s.Stock.HaltUntil - Time.time) : 0;
+            d.nextGradeEvaluation = Math.Max(0f, s.NextGradeEvaluation - SessionClock.Time);
+            d.haltRemaining = s.Stock.IsTradingHalted ? Math.Max(0f, s.Stock.HaltUntil - SessionClock.Time) : 0;
             int gradeIndex = Math.Min(3, (int)s.Stock.RiskGrade);
             d.cautiousSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.cautiousWeights, gradeIndex);
             d.neutralSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.neutralWeights, gradeIndex);
             d.speculativeSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.speculativeWeights, gradeIndex);
-            d.nextReevaluation = Math.Max(0f, s.NextRegular - Time.time);
+            d.nextReevaluation = Math.Max(0f, s.NextRegular - SessionClock.Time);
             d.targetBuy = (double)s.TargetBuy;
             d.targetSell = (double)s.TargetSell;
             d.reservedBuy = (double)ReservedBuy(s);
```

## Assets/Scripts/TradeBot/ExchangeBot.Future.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -235,9 +235,9 @@
             }
         }
 
-        if (Time.time >= nextFutureDebug)
-        {
-            nextFutureDebug = Time.time + 1f;
+        if (SessionClock.Time >= nextFutureDebug)
+        {
+            nextFutureDebug = SessionClock.Time + 1f;
             for (int i = 0; i < scheduleOrder.Count; i++)
             {
                 var schedule = scheduleOrder[i];
```

## Assets/Scripts/TradeBot/ExchangeBot.Risk.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -17,7 +17,7 @@
 
     private void InitializeGrade(StockState state)
     {
-        state.NextGradeEvaluation = Time.time + Mathf.Max(1f, Settings.gradeInterval) + (states.Count - 1) * 0.1f;
+        state.NextGradeEvaluation = SessionClock.Time + Mathf.Max(1f, Settings.gradeInterval) + (states.Count - 1) * 0.1f;
         if (CandleHistoryManager.Instance == null || CandleHistoryManager.Instance.Completed(state.Stock).Count < 20)
         {
             state.Stock.RiskPending = true;
@@ -25,7 +25,7 @@
         }
 
         state.Stock.RiskPending = false;
-        ApplyGrade(state, GradeFor(state.Risk), Time.time);
+        ApplyGrade(state, GradeFor(state.Risk), SessionClock.Time);
     }
 
     public StockRiskGrade GradeFor(decimal indicator)
@@ -148,17 +148,17 @@
                 if (stock.IsTradingHalted)
                 {
                     stock.orderBook.CancelAllBudgeted(stock, Math.Max(1, Settings.cancelOrdersPerFrame));
-                    if (Time.time >= stock.HaltUntil && !stock.orderBook.HasOrders)
+                    if (SessionClock.Time >= stock.HaltUntil && !stock.orderBook.HasOrders)
                     {
-                        ApplyGrade(state, StockRiskGrade.HighRisk, Time.time);
+                        ApplyGrade(state, StockRiskGrade.HighRisk, SessionClock.Time);
                         state.GraceCandles = Math.Max(1, Settings.haltGraceCandles);
-                        state.NextGradeEvaluation = Time.time + Mathf.Max(1f, Settings.gradeInterval);
+                        state.NextGradeEvaluation = SessionClock.Time + Mathf.Max(1f, Settings.gradeInterval);
                         state.NeedsOpening = state.NewListing && state.CompletedCount < 60;
                     }
                 }
                 else
                 {
-                    EvaluateGrade(stock, Time.time);
+                    EvaluateGrade(stock, SessionClock.Time);
                 }
             }
         }
```

## Assets/Scripts/TradeBot/FomoBot.cs
변경 덩어리: 2

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -182,7 +182,7 @@
         }
 
         _surgeTargetStock = targetStock;
-        _surgeTargetTime = Time.time + Random.Range(5f, 15f);
+        _surgeTargetTime = SessionClock.Time + Random.Range(5f, 15f);
     }
 
     protected override Stock PickStock()
@@ -190,7 +190,7 @@
         if (_isSingleStockTrader && _specializedStock != null)
         { return _specializedStock; }
 
-        if (_surgeTargetStock != null && Time.time < _surgeTargetTime)
+        if (_surgeTargetStock != null && SessionClock.Time < _surgeTargetTime)
         { return _surgeTargetStock; }
 
         _surgeTargetStock = null;
```

## Assets/Scripts/TradeBot/MonitorBot.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -118,7 +118,7 @@
         }
 
         _lastPrice[stock] = stock.currentPrice;
-        _nextCheckTime[stock] = Time.time + Random.Range(checkIntervalMin, checkIntervalMax);
+        _nextCheckTime[stock] = SessionClock.Time + Random.Range(checkIntervalMin, checkIntervalMax);
     }
 
     public void ExitOperation()
@@ -226,7 +226,7 @@
             }
         }
 
-        float now = Time.time;
+        float now = SessionClock.Time;
         if (!_nextCheckTime.TryGetValue(stock, out float next) || now < next)
         {
             if (!_nextCheckTime.ContainsKey(stock))
@@ -321,6 +321,6 @@
 
     void ScheduleNext(Stock stock)
     {
-        _nextCheckTime[stock] = Time.time + Random.Range(checkIntervalMin, checkIntervalMax);
+        _nextCheckTime[stock] = SessionClock.Time + Random.Range(checkIntervalMin, checkIntervalMax);
     }
 }
```

## Assets/Scripts/TradeBot/ScalperBot.cs
변경 덩어리: 1

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -158,12 +158,12 @@
         }
 
         _surgeTargetStock = targetStock;
-        _surgeTargetTime = Time.time + Random.Range(10f, 30f);
+        _surgeTargetTime = SessionClock.Time + Random.Range(10f, 30f);
     }
 
     protected override Stock PickStock()
     {
-        if (_surgeTargetStock != null && Time.time < _surgeTargetTime)
+        if (_surgeTargetStock != null && SessionClock.Time < _surgeTargetTime)
         { return _surgeTargetStock; }
 
         _surgeTargetStock = null;
```

## Assets/Scripts/TradeBot/TradeBot.cs
변경 덩어리: 8

### protected void RegisterTargetSell(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
원본
```csharp
    protected void RegisterTargetSell(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanSellStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
    }
```
수정본
```csharp
    protected void RegisterTargetSell(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanSellStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
    }
```

### protected void RegisterTargetBuy(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
원본
```csharp
    protected void RegisterTargetBuy(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanBuyStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
    }
```
수정본
```csharp
    protected void RegisterTargetBuy(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanBuyStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
    }
```

### protected virtual Stock PickStock()
원본
```csharp
    protected virtual Stock PickStock()
    {
        if (_isSingleStockTrader)
        { return _specializedStock != null && _specializedStock.AcceptsOrders ? _specializedStock : null; }

        if (Social != null && Social.Psychology.HasFomo && StockManager.Instance != null && Random.value < ChatSocialConfig.Current.Get("fomoPickChance"))
        {
            foreach (var candidate in StockManager.Instance.stocks)
            { if (candidate.AcceptsOrders && Social.Psychology.HasFomoFor(candidate)) { return candidate; } }
        }

        if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && Time.time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
        { return Social.InfluencedStock; }

        if (StockInterestManager.Instance != null)
        { return StockInterestManager.Instance.PickStockByWeightForBot(this); }

        if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
        { return StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)]; }

        return null;
    }
```
수정본
```csharp
    protected virtual Stock PickStock()
    {
        if (_isSingleStockTrader)
        { return _specializedStock != null && _specializedStock.AcceptsOrders ? _specializedStock : null; }

        if (Social != null && Social.Psychology.HasFomo && StockManager.Instance != null && Random.value < ChatSocialConfig.Current.Get("fomoPickChance"))
        {
            foreach (var candidate in StockManager.Instance.stocks)
            { if (candidate.AcceptsOrders && Social.Psychology.HasFomoFor(candidate)) { return candidate; } }
        }

        if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && SessionClock.Time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
        { return Social.InfluencedStock; }

        if (StockInterestManager.Instance != null)
        { return StockInterestManager.Instance.PickStockByWeightForBot(this); }

        if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
        { return StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)]; }

        return null;
    }
```

### private void RefreshLiquidityOrders(Stock stock)
원본
```csharp
    private void RefreshLiquidityOrders(Stock stock)
    {
        if (IsExchangeParticipant || IsCompanyOwner || !(UseStockPreference() || this is ScalperBot))
        { return; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        int cancelled = 0;
        foreach (var order in thinBookOwnedOrders)
        {
            if (order.purpose != OrderPurpose.Ordinary)
            { continue; }
            float lifetime = this is ScalperBot ? 20f : 60f;
            if (!order.isBuy && order.price > stock.currentPrice * 1.15m)
            {
                lifetime = 120f;
            }
            if (Time.time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
            {
                cancelled++;
                if (cancelled >= 2)
                { break; }
            }
        }
    }
```
수정본
```csharp
    private void RefreshLiquidityOrders(Stock stock)
    {
        if (IsExchangeParticipant || IsCompanyOwner || !(UseStockPreference() || this is ScalperBot))
        { return; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        int cancelled = 0;
        foreach (var order in thinBookOwnedOrders)
        {
            if (order.purpose != OrderPurpose.Ordinary)
            { continue; }
            float lifetime = this is ScalperBot ? 20f : 60f;
            if (!order.isBuy && order.price > stock.currentPrice * 1.15m)
            {
                lifetime = 120f;
            }
            if (SessionClock.Time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
            {
                cancelled++;
                if (cancelled >= 2)
                { break; }
            }
        }
    }
```

### public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
원본
```csharp
    public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
        { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        {
            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, Time.time);
            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, Time.time);
            volume = decimal.Floor(volume * (decimal)(riskScale * newsScale));
            if (volume < 1m) { return; }
        }
        base.BuyLimit(price, volume, subTickSteps, stock);
    }
```
수정본
```csharp
    public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
        { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        {
            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, SessionClock.Time);
            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, SessionClock.Time);
            volume = decimal.Floor(volume * (decimal)(riskScale * newsScale));
            if (volume < 1m) { return; }
        }
        base.BuyLimit(price, volume, subTickSteps, stock);
    }
```

### public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
원본
```csharp
    public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, Time.time)); }
        base.SellLimit(price, volume, subTickSteps, stock);
    }
```
수정본
```csharp
    public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, SessionClock.Time)); }
        base.SellLimit(price, volume, subTickSteps, stock);
    }
```

### private bool TryThinBookSellOffer(Stock stock)
원본
```csharp
    private bool TryThinBookSellOffer(Stock stock)
    {
        if (IsExchangeParticipant || !CanSellStock(stock) || Time.time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
        { return false; }
        decimal holding = GetHolding(stock);
        if (holding < minOrderVolume || stock.currentPrice < 1m)
        { return false; }
        stock.orderBook.GetTopAsks(thinBookAsks, 3);
        bool thin = thinBookAsks.Count < 3 || thinBookAsks[0].price > stock.currentPrice * 1.08m;
        if (!thin)
        { return false; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        foreach (var order in thinBookOwnedOrders)
        {
            if (!order.isBuy)
            { return false; }
        }
        float risk = Social == null ? 0.5f : Social.Risk;
        float stress = Social == null ? 0f : Mathf.Clamp01(Social.Stress / 100f);
        if (Random.value > 0.15f + risk * 0.2f + stress * 0.1f)
        { return false; }
        stock.orderBook.GetTopBids(thinBookBids, 1);
        decimal basis = stock.currentPrice;
        if (thinBookBids.Count > 0)
        {
            basis = System.Math.Max(basis, thinBookBids[0].price);
        }
        // Most offers are nearby; a few holders test a deliberately ambitious price.
        float premium = Random.value < 0.15f + risk * 0.15f ? Random.Range(0.15f, 0.65f) : Random.Range(0.01f, 0.08f);
        premium *= 1f - stress * 0.5f;
        decimal price = PriceTickPolicy.CeilToTick(basis * (1m + (decimal)premium));
        decimal ratio = System.Math.Min(0.05m, sellHoldingRatio * (decimal)Random.Range(0.1f, 0.5f));
        decimal volume = ClampMinVolume(holding * ratio);
        if (volume < 1m)
        { return false; }
        nextThinBookOffer = Time.time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
        DelayedSellLimit(price, volume, 0, stock);
        return true;
    }
```
수정본
```csharp
    private bool TryThinBookSellOffer(Stock stock)
    {
        if (IsExchangeParticipant || !CanSellStock(stock) || SessionClock.Time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
        { return false; }
        decimal holding = GetHolding(stock);
        if (holding < minOrderVolume || stock.currentPrice < 1m)
        { return false; }
        stock.orderBook.GetTopAsks(thinBookAsks, 3);
        bool thin = thinBookAsks.Count < 3 || thinBookAsks[0].price > stock.currentPrice * 1.08m;
        if (!thin)
        { return false; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        foreach (var order in thinBookOwnedOrders)
        {
            if (!order.isBuy)
            { return false; }
        }
        float risk = Social == null ? 0.5f : Social.Risk;
        float stress = Social == null ? 0f : Mathf.Clamp01(Social.Stress / 100f);
        if (Random.value > 0.15f + risk * 0.2f + stress * 0.1f)
        { return false; }
        stock.orderBook.GetTopBids(thinBookBids, 1);
        decimal basis = stock.currentPrice;
        if (thinBookBids.Count > 0)
        {
            basis = System.Math.Max(basis, thinBookBids[0].price);
        }
        // Most offers are nearby; a few holders test a deliberately ambitious price.
        float premium = Random.value < 0.15f + risk * 0.15f ? Random.Range(0.15f, 0.65f) : Random.Range(0.01f, 0.08f);
        premium *= 1f - stress * 0.5f;
        decimal price = PriceTickPolicy.CeilToTick(basis * (1m + (decimal)premium));
        decimal ratio = System.Math.Min(0.05m, sellHoldingRatio * (decimal)Random.Range(0.1f, 0.5f));
        decimal volume = ClampMinVolume(holding * ratio);
        if (volume < 1m)
        { return false; }
        nextThinBookOffer = SessionClock.Time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
        DelayedSellLimit(price, volume, 0, stock);
        return true;
    }
```

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -225,8 +225,8 @@
         MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
         if (!IsExchangeParticipant && Social != null)
         {
-            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, Time.time);
-            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, Time.time);
+            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, SessionClock.Time);
+            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, SessionClock.Time);
             volume = decimal.Floor(volume * (decimal)(riskScale * newsScale));
             if (volume < 1m) { return; }
         }
@@ -245,7 +245,7 @@
         { return; }
         MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
         if (!IsExchangeParticipant && Social != null)
-        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, Time.time)); }
+        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, SessionClock.Time)); }
         base.SellLimit(price, volume, subTickSteps, stock);
     }
 
@@ -382,7 +382,7 @@
             {
                 lifetime = 120f;
             }
-            if (Time.time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
+            if (SessionClock.Time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
             {
                 cancelled++;
                 if (cancelled >= 2)
@@ -393,7 +393,7 @@
 
     private bool TryThinBookSellOffer(Stock stock)
     {
-        if (IsExchangeParticipant || !CanSellStock(stock) || Time.time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
+        if (IsExchangeParticipant || !CanSellStock(stock) || SessionClock.Time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
         { return false; }
         decimal holding = GetHolding(stock);
         if (holding < minOrderVolume || stock.currentPrice < 1m)
@@ -426,7 +426,7 @@
         decimal volume = ClampMinVolume(holding * ratio);
         if (volume < 1m)
         { return false; }
-        nextThinBookOffer = Time.time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
+        nextThinBookOffer = SessionClock.Time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
         DelayedSellLimit(price, volume, 0, stock);
         return true;
     }
@@ -479,7 +479,7 @@
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
         { return; }
 
-        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
+        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
     }
 
     /// <summary>목표가 매도 주문 등록. 현재가가 목표가 근처에 도달하면 실행됨.</summary>
@@ -491,7 +491,7 @@
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
         { return; }
 
-        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
+        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
     }
 
     /// <summary>해당 종목의 대기 주문이 있는지 확인</summary>
@@ -521,7 +521,7 @@
             { if (candidate.AcceptsOrders && Social.Psychology.HasFomoFor(candidate)) { return candidate; } }
         }
 
-        if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && Time.time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
+        if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && SessionClock.Time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
         { return Social.InfluencedStock; }
 
         if (StockInterestManager.Instance != null)
```

## Assets/Scripts/TradeBot/TradeBot.Risk.cs
변경 덩어리: 2

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -47,7 +47,7 @@
             response.ApplyAt = stock.RiskChangedAt + hash / (float)uint.MaxValue * (StockManager.Instance == null ? 30f : Mathf.Max(0f, StockManager.Instance.economy.reactionDelayMax));
         }
 
-        if (Time.time >= response.ApplyAt)
+        if (SessionClock.Time >= response.ApplyAt)
         {
             response.Applied = stock.RiskGrade;
         }
@@ -75,7 +75,7 @@
     }
 
     protected virtual bool CanBuyStock(Stock stock) => stock != null && stock.AcceptsOrders &&
-        (IsExchangeParticipant || Social == null || Social.Psychology.MarketRiskBuyScale(stock.symbol, Time.time) > 0);
+        (IsExchangeParticipant || Social == null || Social.Psychology.MarketRiskBuyScale(stock.symbol, SessionClock.Time) > 0);
     protected virtual bool CanSellStock(Stock stock) => stock != null && stock.AcceptsOrders;
     private int IntentVersion(Stock stock) => stock == null ? -1 : stock.OrderEpoch + (intentVersions.TryGetValue(stock, out int version) ? version : 0);
     public void InvalidateStockIntents(Stock stock)
```

## Assets/Scripts/TradeBot/WhaleBot.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -96,7 +96,7 @@
             reservedCash = 0m;
         }
 
-        ScheduleNextAction(Time.time);
+        ScheduleNextAction(SessionClock.Time);
     }
 
     void Update()
@@ -121,7 +121,7 @@
             RefreshFollowersIfNeeded();
         }
 
-        float now = Time.time;
+        float now = SessionClock.Time;
         // 0.1초마다 FakePrice 모니터링: 목표 도달 시 fakeValue = realValue 로 맞춤
         if (now - _lastPumpMonitorTime >= pumpMonitorInterval)
         {
@@ -179,10 +179,10 @@
         if (followers.Count > 0)
         { return; }
 
-        if (Time.time < _nextFollowerSearch)
-        { return; }
-
-        _nextFollowerSearch = Time.time + 5f;
+        if (SessionClock.Time < _nextFollowerSearch)
+        { return; }
+
+        _nextFollowerSearch = SessionClock.Time + 5f;
         foreach (var follower in WhaleFollowerBot.RegisteredFollowers)
         {
             if (follower != null && follower.TryAssignCommander(this))
```

## Assets/Scripts/TradeBot/WhaleBot.Owner.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -78,7 +78,7 @@
             }
         }
 
-        nextDisposal = Time.time + InitialAccountId % 10 * 0.1f;
+        nextDisposal = SessionClock.Time + InitialAccountId % 10 * 0.1f;
     }
 
     private decimal OwnSellOutstanding()
@@ -122,10 +122,10 @@
     {
         disposalRemainingSeconds = Math.Max(0, DisposalNewsAt - (ExchangeBot.Instance == null ? 0 : ExchangeBot.Instance.NewsClock));
         disposalSoldShares = (double)DisposalSold;
-        if (OwnedCompany == null || OwnedCompany.IsDelisting || OwnedCompany.IsDelisted || !MarketDayController.CanTrade || !OwnedCompany.AcceptsOrders || Time.time < nextDisposal)
+        if (OwnedCompany == null || OwnedCompany.IsDelisting || OwnedCompany.IsDelisted || !MarketDayController.CanTrade || !OwnedCompany.AcceptsOrders || SessionClock.Time < nextDisposal)
         { return; }
 
-        nextDisposal = Time.time + Mathf.Max(0.1f, OwnerSettings.disposalInterval);
+        nextDisposal = SessionClock.Time + Mathf.Max(0.1f, OwnerSettings.disposalInterval);
         using (DisposalMarker.Auto())
         {
             decimal outstanding = OwnSellOutstanding();
@@ -136,7 +136,7 @@
             for (int i = 0; i < ownOrders.Count; i++)
             {
                 var order = ownOrders[i];
-                if (order.isBuy || Time.time - order.createdAt < OwnerSettings.staleSellSeconds)
+                if (order.isBuy || SessionClock.Time - order.createdAt < OwnerSettings.staleSellSeconds)
                 { continue; }
 
                 decimal remaining = order.volume;
```

## Assets/Scripts/TradeBot/WhaleFollowerBot.cs
변경 덩어리: 3

전체 필드·속성 및 호출부 차이
```diff
--- before
+++ after
@@ -150,10 +150,10 @@
         operationStock = stock;
         _whaleTargetPrice = target > 0m ? target : stock.fakeValue;
         entryAvgPrice = 0m;
-        joinTime = Time.time;
+        joinTime = SessionClock.Time;
         mode = Mode.FomoBuy;
-        _nextActionTime = Time.time + Random.Range(0.1f, 1f);
-        _buyEndTime = Time.time + Random.Range(buyDurationMin, buyDurationMax);
+        _nextActionTime = SessionClock.Time + Random.Range(0.1f, 1f);
+        _buyEndTime = SessionClock.Time + Random.Range(buyDurationMin, buyDurationMax);
         _peakPrice = (float)stock.currentPrice;
     }
 
@@ -217,7 +217,7 @@
         if (stock == null || operationStock == null || stock != operationStock)
         { return; }
 
-        float now = Time.time;
+        float now = SessionClock.Time;
         if (now < _nextActionTime)
         { return; }
 
@@ -432,11 +432,11 @@
         if (sellVol >= holding * 0.8m)
         {
             mode = Mode.Done;
-            _nextActionTime = Time.time + 2f;
+            _nextActionTime = SessionClock.Time + 2f;
         }
         else
         {
-            _nextActionTime = Time.time + Random.Range(2f, 5f);
+            _nextActionTime = SessionClock.Time + Random.Range(2f, 5f);
         }
     }
 
```
