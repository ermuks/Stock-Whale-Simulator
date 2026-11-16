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
    public static long Key(CandleData candle, int minutes)
    {
        if (minutes <= 0 || candle.historical || candle.marketDay < 1)
        { return BitConverter.DoubleToInt64Bits(candle.completedTime); }
        return ((long)candle.marketDay * 1440 + candle.marketMinute) / minutes;
    }
    public static void Build(IReadOnlyList<CandleData> closed, CandleData forming, int minutes, List<CandleData> output, bool includeForming = true)
    {
        output.Clear();
        for (int i = 0; i < closed.Count + (includeForming ? 1 : 0); i++)
        {
            var next = i == closed.Count ? forming : closed[i];
            // Seeded prices have no real game timestamp. Do not invent a time bucket for them.
            if (minutes > 0 && (next.historical || next.marketDay < 1))
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
