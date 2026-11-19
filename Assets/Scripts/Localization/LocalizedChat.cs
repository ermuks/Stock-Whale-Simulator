using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Re-renders the chosen historical utterance; never rerolls speech or reads a current account balance.</summary>
public static class LocalizedChat
{
    public static string Text(BotChatMessage message)
    { return Text(message.Social,message.Text); }
    public static string Text(SocialMessage social,string source)
    {
        if (GameLocale.Code == "ko" || social == null || string.IsNullOrEmpty(social.localizedPhrase) || social.localizedValues == null)
        { return GameLocale.Text(source); }
        var values = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var slot in social.localizedValues)
        {
            string value = slot.value ?? "";
            if (slot.name == "target" || slot.name == "departedName")
            { value = slot.value == "방장" ? GameLocale.Text("방장") : value; }
            else if (slot.name == "gain" || slot.name == "loss")
            { value = GameLocale.Code == "ja" ? Amount(slot.raw,false) + "ウォン" : Amount(slot.raw,false).Replace("~","~₩"); }
            else if (slot.name == "return" || slot.name == "moveTalk") { value = Percent(slot.raw); }
            else if (slot.approximate && (slot.name == "amount" || slot.name == "memAmount")) { value = Amount(slot.raw); }
            else if (slot.approximate && (slot.name == "pct" || slot.name == "memPct"))
            { value = Percent(slot.raw).Replace("%",""); }
            else if (!string.IsNullOrEmpty(slot.raw) && (slot.name == "amount" || slot.name == "memAmount" || slot.name == "pct" || slot.name == "memPct"))
            { value = slot.raw; }
            else { value = GameLocale.Text(value); }
            values[slot.name] = value;
        }
        string text = GameLocale.Template(social.localizedPhrase,values);
        // 영어 문장은 "₩{amount}" 꼴이라 부호·어림 표시가 통화 기호 뒤에 붙는다("₩−~600k"). 부호와 어림 표시를 기호 앞으로 옮긴다("−~₩600k").
        if (GameLocale.Code == "en" && text.IndexOf('₩') >= 0)
        { text = System.Text.RegularExpressions.Regex.Replace(text,"₩([−+-]?~?)(?=[0-9])","$1₩"); }
        string prefix = string.IsNullOrEmpty(social.localizedTarget) ? "" : "@" +
            (social.localizedTarget == "방장" ? GameLocale.Text("방장") : BotNicknames.Localize(social.localizedTarget)) + " ";
        return prefix + text + GameLocale.Text(social.localizedSuffix);
    }
    private static string Percent(string raw)
    {
        string result = ChatSocialNumbers.Return(raw,1);
        return result.Contains("미만") ? "<" + result.Replace(" 미만","") : result;
    }
    private static string Amount(string raw,bool signed = true)
    {
        if (!decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out decimal amount)) { return raw ?? ""; }
        decimal value = Math.Abs(amount), step = value >= 10000m ? 10000m : value >= 1000m ? 1000m : value >= 100m ? 100m : 1m;
        while (step >= 10000m && step <= value / 10m) { step *= 10m; }
        value = Math.Floor(value / step) * step;
        string sign = !signed ? "" : amount < 0 ? "−" : amount > 0 ? "+" : "";
        if (GameLocale.Code == "ja")
        {
            decimal unit = value >= 100000000m ? 100000000m : value >= 10000m ? 10000m : 1m;
            string suffix = unit == 100000000m ? "億" : unit == 10000m ? "万" : "";
            return sign + "約" + (value / unit).ToString("0.#",CultureInfo.InvariantCulture) + suffix;
        }
        decimal scale = value >= 1000000000m ? 1000000000m : value >= 1000000m ? 1000000m : value >= 1000m ? 1000m : 1m;
        string label = scale == 1000000000m ? "b" : scale == 1000000m ? "m" : scale == 1000m ? "k" : "";
        return sign + "~" + (value / scale).ToString("0.##",CultureInfo.InvariantCulture) + label;
    }
}
