using System;
using System.Collections.Generic;

/// <summary>봉, 눈금, 커서가 함께 사용하는 원화 가격 좌표. 매매 호가 규칙과는 별개다.</summary>
public readonly struct ChartPriceScale
{
    /// <summary>Minimum·Maximum은 화면에 보이는 봉의 최저가·최고가 그대로다(여유를 두거나 반올림하지 않는다).</summary>
    public readonly double Minimum, Maximum, Step, BaseStep, FirstLine;
    /// <summary>보조선의 수. 보조선은 Step의 배수인 가격에만 그어진다.</summary>
    public readonly int Lines;
    public double Range => Maximum - Minimum;

    private ChartPriceScale(double minimum, double maximum, double step, double baseStep)
    {
        Minimum = minimum; Maximum = maximum; Step = step; BaseStep = baseStep;
        FirstLine = Math.Ceiling(minimum / step - .000000001d) * step;
        Lines = Math.Max(0, (int)Math.Floor((maximum - FirstLine) / step + .000000001d) + 1);
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

    /// <summary>
    /// 보조선 단위는 (최고 - 최저)로 정한다: 그 차이가 100단위(100~999)면 10원, 1,000단위면 100원, 10,000단위면 1,000원 간격이다. 100 미만이면 1원.
    /// 그 간격으로 그으면 선이 maxLines보다 많아질 때만 같은 자리의 2배, 5배, 10배(20·50·100원처럼)로 넓힌다.
    /// </summary>
    public static ChartPriceScale Create(double minimum, double maximum, int maxLines = 10)
    {
        if (!Finite(minimum) || !Finite(maximum) || maximum <= 0d || minimum > maximum)
        { return new ChartPriceScale(0d, 10d, 1d, 1d); }
        minimum = Math.Max(0d, minimum);
        maxLines = Math.Max(2, Math.Min(12, maxLines));
        if (maximum - minimum <= 0d)
        {
            // 고점과 저점이 같은 평평한 봉: 위아래로 한 단위씩 벌려야 그릴 수 있다.
            double unit = UnitFor(maximum);
            minimum = Math.Max(0d, minimum - unit); maximum += unit;
        }
        double range = maximum - minimum;
        double basis = Math.Max(1d, Math.Pow(10d, Math.Floor(Math.Log10(range) + .000000001d) - 1d));
        foreach (double multiple in new[] { 1d, 2d, 5d, 10d })
        {
            var scale = new ChartPriceScale(minimum, maximum, basis * multiple, basis);
            if (scale.Lines <= maxLines) { return scale; }
        }
        return new ChartPriceScale(minimum, maximum, basis * 20d, basis);
    }

    /// <summary>index번째 보조선의 가격(아래에서부터).</summary>
    public double Line(int index) => FirstLine + Step * Math.Max(0, Math.Min(Math.Max(0, Lines - 1), index));
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

    private static void Include(double value, ref double minimum, ref double maximum)
    {
        if (!Finite(value) || value <= 0d)
        { return; }
        minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
    }
    private static double Clamp01(double value) => Finite(value) ? Math.Max(0d, Math.Min(1d, value)) : 0d;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
