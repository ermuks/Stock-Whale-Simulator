using System;
using System.Globalization;

/// <summary>Presentation only. Raw social facts and account values are never modified.</summary>
public static class ChatSocialNumbers
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    public static string Amount(string raw, int dialect, bool signed = false, bool compactNoun = false)
    {
        if (!decimal.TryParse(raw, NumberStyles.Number, Culture, out decimal value))
        { return raw; }
        decimal magnitude = Math.Abs(value), rounded = magnitude;
        if (magnitude >= 10000m)
        {
            decimal step = 10000m;
            while (step <= magnitude / 10m) { step *= 10m; }
            rounded = Math.Floor(magnitude / step) * step;
        }
        else if (magnitude >= 1000m) { rounded = Math.Floor(magnitude / 1000m) * 1000m; }
        else if (magnitude >= 100m) { rounded = Math.Floor(magnitude / 100m) * 100m; }
        else { rounded = Math.Floor(magnitude); }
        string sign = signed && value != 0 ? value > 0 ? "+" : "−" : "";
        if (magnitude > 0 && rounded == 0)
        { return sign + "1원 미만"; }
        if (rounded == 0)
        { return "0원"; }
        if (rounded >= 100000000m)
        { return sign + (rounded / 100000000m).ToString("0.#", Culture) + (compactNoun ? "억원쯤" : "억 정도"); }
        if (rounded >= 10000m)
        {
            string units = (rounded / 10000m).ToString("#,##0", Culture);
            if (compactNoun) { return sign + units + "만원쯤"; }
            // One '장' always means 10,000 won; 5,284,003 won must never become '5장'.
            return sign + units + (dialect == 1 ? "장" : dialect == 2 ? " 정도" : "만 정도");
        }
        if (rounded >= 1000m)
        { return sign + (rounded / 1000m).ToString("0", Culture) + (compactNoun ? "천원쯤" : "천 원 정도"); }
        return sign + rounded.ToString("0", Culture) + (compactNoun ? "원쯤" : "원 정도");
    }
    public static string Return(string raw, int dialect)
    {
        if (!decimal.TryParse(raw, NumberStyles.Number, Culture, out decimal value))
        { return raw; }
        decimal magnitude = Math.Abs(value);
        decimal rounded = Math.Round(magnitude, magnitude < 1m ? 2 : magnitude < 10m ? 1 : 0, MidpointRounding.AwayFromZero);
        string sign = value < 0 ? "−" : "";
        if (magnitude > 0 && rounded == 0)
        { return sign + "0.01" + (dialect == 1 ? "% 미만" : "퍼 미만"); }
        return sign + rounded.ToString("0.##", Culture) + (dialect == 1 ? "%" : "퍼");
    }
}
