using System;
using System.Collections.Generic;

/// <summary>한 언어의 끊어 치기 규칙. 언어마다 따로 쓴다(한국어 규칙을 옮긴 것이 아니다).</summary>
[Serializable] public sealed class ChatBreathRules
{
    /// <summary>단어 사이를 띄어 쓰는 언어인가. 일본어는 false.</summary>
    public bool spaced = true;
    /// <summary>after 낱말이 독립된 단어여야 하는가(영어 "tho"). false면 어미처럼 단어 끝에 붙는다(한국어 "는데").</summary>
    public bool afterWholeWord;
    public int minPiece = 4, minTotal = 12;
    public float gapBaseSec = .6f, gapPerCharSec = .07f, gapMaxSec = 2.4f;
    /// <summary>끊어 치는 사람이 조각 끝에서 생략하는 문자(마침표 등).</summary>
    public string dropTail = ".";
    /// <summary>openers: 말머리. after: 이 말 뒤에서 숨을 쉰다. before: 이 말 앞에서 숨을 쉰다.</summary>
    public string[] openers, after, before;
}

public enum ChatBreathMode
{
    /// <summary>문장이 끝나는 자리에서만.</summary>
    Sentences,
    /// <summary>문장 끝과 말머리 뒤.</summary>
    Light,
    /// <summary>연결 어미·접속어·쉼표까지 모든 숨 자리.</summary>
    Heavy
}

/// <summary>
/// 한 번에 보낼 수 있는 말을 숨 쉬는 자리에서 여러 번으로 나눈다.
/// 아무 데서나 자르지 않는다: 말머리 뒤, 절이 끝나는 자리, 문장이 끝나는 자리만 후보다.
/// </summary>
public static class ChatBreath
{
    private const string Enders = ".!?…。！？";
    private const string WideEnders = "。！？";
    private const string OpenerTails = ",、，…!?！？~";

    public static List<string> Split(string text, ChatBreathRules rules, int maxPieces, ChatBreathMode mode)
    {
        if (string.IsNullOrWhiteSpace(text) || rules == null || maxPieces < 2 || text.IndexOf('\n') >= 0) { return null; }
        string prefix = "";
        string body = text.Trim();
        if (body.StartsWith("@", StringComparison.Ordinal))
        {
            int space = body.IndexOf(' ');
            if (space < 0) { return null; }
            prefix = body.Substring(0, space + 1); body = body.Substring(space + 1).TrimStart();
        }
        if (body.Length < rules.minTotal) { return null; }

        var breaks = new Dictionary<int, int>();
        int openerEnd = OpenerEnd(body, rules);
        if (openerEnd > 0 && mode != ChatBreathMode.Sentences) { Offer(breaks, openerEnd, 2); }
        for (int i = 0; i < body.Length; i++)
        {
            if (Enders.IndexOf(body[i]) < 0) { continue; }
            int end = i; bool wide = false;
            while (end < body.Length && (Enders.IndexOf(body[end]) >= 0 || body[end] == '"' || body[end] == '」' || body[end] == ')')) { wide |= WideEnders.IndexOf(body[end]) >= 0; end++; }
            // "3.5%"나 "1.2배"의 점은 문장 끝이 아니다. 뒤에 공백이 오거나 전각 문장부호일 때만 끝으로 본다.
            if (end < body.Length && (char.IsWhiteSpace(body[end]) || wide)) { Offer(breaks, Skip(body, end), 3); }
            i = end - 1;
        }
        if (mode == ChatBreathMode.Heavy)
        {
            if (rules.after != null) { foreach (string token in rules.after) { OfferAfter(breaks, body, token, rules); } }
            if (rules.before != null && rules.spaced) { foreach (string token in rules.before) { OfferBefore(breaks, body, token); } }
            for (int i = 1; i < body.Length - 1; i++)
            {
                if (body[i] == '、' || body[i] == '，' || body[i] == ',' && char.IsWhiteSpace(body[i + 1])) { Offer(breaks, Skip(body, i + 1), 1); }
            }
        }
        if (breaks.Count == 0) { return null; }

        // 센 자리부터 받아들인다. 조각이 너무 짧아지는 자리는 버린다(말머리 조각만 짧아도 된다).
        var order = new List<KeyValuePair<int, int>>(breaks);
        order.Sort((a, b) => { int strength = b.Value.CompareTo(a.Value); return strength != 0 ? strength : a.Key.CompareTo(b.Key); });
        var cuts = new List<int>();
        foreach (var candidate in order)
        {
            if (cuts.Count >= maxPieces - 1) { break; }
            int position = candidate.Key;
            if (position <= 0 || position >= body.Length) { continue; }
            int left = 0, right = body.Length;
            foreach (int cut in cuts) { if (cut < position && cut > left) { left = cut; } if (cut > position && cut < right) { right = cut; } }
            bool openerPiece = left == 0 && position == openerEnd;
            if (!openerPiece && Visible(body, left, position) < rules.minPiece || Visible(body, position, right) < rules.minPiece) { continue; }
            cuts.Add(position);
        }
        if (cuts.Count == 0) { return null; }
        cuts.Sort();
        var pieces = new List<string>(); int start = 0;
        foreach (int cut in cuts) { Add(pieces, body.Substring(start, cut - start), rules); start = cut; }
        Add(pieces, body.Substring(start), rules);
        if (pieces.Count < 2) { return null; }
        pieces[0] = prefix + pieces[0];
        return pieces;
    }

    /// <summary>다음 조각을 치는 데 걸리는 시간. 길수록 오래 걸린다.</summary>
    public static double Gap(string piece, ChatBreathRules rules)
        => Math.Min(rules.gapMaxSec, rules.gapBaseSec + (piece?.Length ?? 0) * rules.gapPerCharSec);

    private static void Offer(Dictionary<int, int> breaks, int position, int strength)
    { if (!breaks.TryGetValue(position, out int known) || known < strength) { breaks[position] = strength; } }
    private static int Skip(string body, int index)
    { while (index < body.Length && char.IsWhiteSpace(body[index])) { index++; } return index; }
    private static int Visible(string body, int from, int to)
    { int count = 0; for (int i = from; i < to; i++) { if (!char.IsWhiteSpace(body[i])) { count++; } } return count; }
    private static void Add(List<string> pieces, string raw, ChatBreathRules rules)
    {
        string piece = raw.Trim().TrimEnd(',', '、', '，').TrimEnd();
        // "..."처럼 일부러 늘인 말줄임은 남기고, 문장을 닫는 한 글자만 뗀다.
        if (piece.Length > 1 && !string.IsNullOrEmpty(rules.dropTail) && rules.dropTail.IndexOf(piece[piece.Length - 1]) >= 0 && rules.dropTail.IndexOf(piece[piece.Length - 2]) < 0)
        { piece = piece.Substring(0, piece.Length - 1).TrimEnd(); }
        if (piece.Length > 0) { pieces.Add(piece); }
    }
    private static int OpenerEnd(string body, ChatBreathRules rules)
    {
        if (rules.openers == null) { return 0; }
        int best = 0;
        foreach (string opener in rules.openers)
        {
            if (string.IsNullOrEmpty(opener) || opener.Length <= best - 1 || body.Length <= opener.Length || !body.StartsWith(opener, StringComparison.OrdinalIgnoreCase)) { continue; }
            char next = body[opener.Length];
            bool boundary = char.IsWhiteSpace(next) || OpenerTails.IndexOf(next) >= 0 || !rules.spaced && opener.Length >= 3;
            if (!boundary) { continue; }
            int end = opener.Length;
            while (end < body.Length && OpenerTails.IndexOf(body[end]) >= 0) { end++; }
            end = Skip(body, end);
            if (end < body.Length && end > best) { best = end; }
        }
        return best;
    }
    private static void OfferAfter(Dictionary<int, int> breaks, string body, string token, ChatBreathRules rules)
    {
        if (string.IsNullOrEmpty(token)) { return; }
        for (int at = body.IndexOf(token, StringComparison.OrdinalIgnoreCase); at >= 0; at = body.IndexOf(token, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (rules.afterWholeWord ? at > 0 && !char.IsWhiteSpace(body[at - 1]) : at == 0 || char.IsWhiteSpace(body[at - 1])) { continue; }
            int end = at + token.Length;
            while (end < body.Length && (body[end] == ',' || body[end] == '、' || body[end] == '，' || body[end] == '?' || body[end] == '？' || body[end] == '!' || body[end] == '！')) { end++; }
            if (end >= body.Length) { continue; }
            if (rules.spaced && !char.IsWhiteSpace(body[end])) { continue; }
            Offer(breaks, Skip(body, end), 1);
        }
    }
    private static void OfferBefore(Dictionary<int, int> breaks, string body, string token)
    {
        if (string.IsNullOrEmpty(token)) { return; }
        string spaced = " " + token + " ";
        for (int at = body.IndexOf(spaced, StringComparison.OrdinalIgnoreCase); at >= 0; at = body.IndexOf(spaced, at + 1, StringComparison.OrdinalIgnoreCase))
        { Offer(breaks, at + 1, 1); }
    }
}
