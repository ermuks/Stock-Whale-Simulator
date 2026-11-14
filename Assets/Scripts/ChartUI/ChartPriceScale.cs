using System;
using System.Collections.Generic;

/// <summary>봉, 눈금, 커서가 함께 사용하는 원화 가격 좌표. 매매 호가 규칙과는 별개다.</summary>
public readonly struct ChartPriceScale
{
    public readonly double Minimum, Maximum, Step, BaseStep;
    public readonly int Intervals;
    public double Range => Maximum - Minimum;

    private ChartPriceScale(double minimum, double maximum, double step, double baseStep)
    {
        Minimum = minimum; Maximum = maximum; Step = step; BaseStep = baseStep;
        Intervals = Math.Max(1, (int)Math.Round((maximum - minimum) / step));
    }

    public static double UnitFor(double price)
    {
        if (!Finite(price) || price < 100d)
        { return 1d; }
        return Math.Pow(10d, Math.Floor(Math.Log10(price)) - 1d);
    }

    public static ChartPriceScale FromCandles(IReadOnlyList<CandleData> candles, int maxIntervals = 10)
    {
        double minimum = double.PositiveInfinity, maximum = 0d;
        if (candles != null)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                Include(c.low, ref minimum, ref maximum); Include(c.high, ref minimum, ref maximum);
                Include(c.open, ref minimum, ref maximum); Include(c.close, ref minimum, ref maximum);
            }
        }
        return Create(minimum, maximum, maxIntervals);
    }

    public static ChartPriceScale Create(double minimum, double maximum, int maxIntervals = 10)
    {
        if (!Finite(minimum) || !Finite(maximum) || maximum <= 0d || minimum > maximum)
        { return new ChartPriceScale(0d, 10d, 1d, 1d); }
        minimum = Math.Max(0d, minimum);
        maxIntervals = Math.Max(2, Math.Min(12, maxIntervals));
        double unit = UnitFor(maximum);
        // 평평한 봉도 표시하고, 최저/최고 심지가 경계에 붙지 않도록 여유를 둔다.
        double padding = Math.Max(unit * .25d, (maximum - minimum) * .04d);
        double low = Math.Max(0d, minimum - padding), high = maximum + padding;
        double step = unit;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            double bottom = Math.Floor(low / step) * step, top = Math.Ceiling(high / step) * step;
            if ((top - bottom) / step <= maxIntervals + .000001d)
            { return new ChartPriceScale(bottom, Math.Max(bottom + step, top), step, unit); }
            step = NextStep(step, unit);
        }
        return new ChartPriceScale(0d, step, step, unit);
    }

    public double Tick(int index) => Minimum + Step * Math.Max(0, Math.Min(Intervals, index));
    public double PriceAt(double normalized) => Minimum + Clamp01(normalized) * Range;
    public double Normalize(double price) => Range > 0d && Finite(price) ? Clamp01((price - Minimum) / Range) : 0d;

    public static bool TryCandleIndex(double x, double stride, int count, out int index)
    {
        index = -1;
        if (!Finite(x) || !Finite(stride) || x < 0d || stride <= 0d || count <= 0)
        { return false; }
        double slot = Math.Floor(x / stride);
        if (slot >= count)
        { return false; }
        index = (int)slot;
        return true;
    }

    private static double NextStep(double step, double unit)
    {
        double magnitude = Math.Pow(10d, Math.Floor(Math.Log10(step / unit) + .000000001d));
        double fraction = step / (unit * magnitude);
        return unit * magnitude * (fraction < 1.5d ? 2d : fraction < 3.5d ? 5d : 10d);
    }
    private static void Include(double value, ref double minimum, ref double maximum)
    {
        if (!Finite(value) || value <= 0d)
        { return; }
        minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
    }
    private static double Clamp01(double value) => Finite(value) ? Math.Max(0d, Math.Min(1d, value)) : 0d;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
