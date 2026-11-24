using System;
using System.Collections.Generic;

/// <summary>Display-only OHLCV aggregation. Never feeds orders or strategy indicators.</summary>
public static class ChartHistorySeries
{
    public static void RecordTrade(List<CandleData> minutes, int day, int minute, decimal price, decimal volume, bool organic, int capacity)
    {
        if (price <= 0m || volume <= 0m)
        { return; }
        bool append = minutes.Count == 0 || minutes[minutes.Count - 1].marketDay != day || minutes[minutes.Count - 1].marketMinute != minute;
        var candle = append ? new CandleData { marketDay = day, marketMinute = minute, open = (float)price, high = (float)price, low = (float)price } : minutes[minutes.Count - 1];
        candle.high = Math.Max(candle.high, (float)price); candle.low = Math.Min(candle.low, (float)price);
        candle.close = (float)price; candle.exactClose = price; candle.volume += (float)volume;
        if (organic)
        { candle.nonExchangeVolume += volume; }
        if (append)
        { minutes.Add(candle); }
        else
        { minutes[minutes.Count - 1] = candle; }
        if (minutes.Count > Math.Max(1, capacity))
        { minutes.RemoveAt(0); }
    }
    /// <summary>
    /// 게임 시각이 적힌 봉인가. 실제 체결로 생긴 봉(1일부터)과, 새 게임의 지난 시세로 미리 만든 분봉(0일, -1일, …의 장중 시각)이 그렇다.
    /// 기본 보기의 지난 봉 60개는 시각이 없다(날짜와 분이 모두 0).
    /// </summary>
    public static bool Timed(CandleData candle) => candle.marketDay >= 1 || candle.historical && candle.marketMinute > 0;
    public static long Key(CandleData candle, int minutes)
    {
        if (minutes <= 0 || !Timed(candle))
        { return BitConverter.DoubleToInt64Bits(candle.completedTime); }
        // 지난 시세의 날짜는 0 이하다. 묶음의 경계가 어긋나지 않게 넉넉히 밀어서 센다.
        return (((long)candle.marketDay + 100000L) * 1440 + candle.marketMinute) / minutes;
    }
    public static void Build(IReadOnlyList<CandleData> closed, CandleData forming, int minutes, List<CandleData> output, bool includeForming = true)
    {
        output.Clear();
        for (int i = 0; i < closed.Count + (includeForming ? 1 : 0); i++)
        {
            var next = i == closed.Count ? forming : closed[i];
            // 시각이 없는 봉은 분 단위로 묶을 수 없다. 미리 만든 지난 분봉에는 시각이 있어서 함께 묶인다.
            if (minutes > 0 && !Timed(next))
            { continue; }
            // 일봉으로만 만들어 둔 먼 지난 날은 1일 봉 보기에서만 그린다(분·시간 보기에 끼면 하루짜리 봉 하나가 덩그러니 선다).
            if (minutes < 1440 && next.historical && next.completedTime == CandleHistoryManager.DailyOnly)
            { continue; }
            if (output.Count == 0 || minutes <= 0 || Key(output[output.Count - 1], minutes) != Key(next, minutes))
            { output.Add(next); continue; }
            var merged = output[output.Count - 1];
            merged.high = Math.Max(merged.high, next.high);
            merged.low = Math.Min(merged.low, next.low);
            merged.close = next.close; merged.exactClose = next.exactClose;
            merged.volume += next.volume; merged.nonExchangeVolume += next.nonExchangeVolume;
            output[output.Count - 1] = merged;
        }
    }
}
