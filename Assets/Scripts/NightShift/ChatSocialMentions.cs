using System;
using System.Collections.Generic;
using System.Text;

/// <summary>UI-only nickname highlighting. A roster trie avoids scanning every bot for each message.</summary>
public sealed class ChatSocialMentions
{
    public const string MarkOpen = "<mark=#D4E1E832>";
    public const string StockMarkOpen = "<mark=#25AFC02B><color=#81D5DE>";
    private sealed class Node
    {
        public readonly Dictionary<char, Node> Children = new();
        public bool Terminal;
        public string StockSymbol;
    }
    private readonly Node root = new();
    private static readonly string[] Particles = { "님", "한테", "에게", "이랑", "에서", "으로", "보다", "처럼", "까지", "부터", "만", "은", "는", "이", "가", "을", "를", "랑", "도", "의", "아", "야", "さん", "は", "が", "を", "の", "に", "も", "と", "より" };
    public ChatSocialMentions(IEnumerable<string> nicknames, IEnumerable<KeyValuePair<string, string>> stocks = null)
    {
        foreach (string name in nicknames)
        {
            if (string.IsNullOrWhiteSpace(name))
            { continue; }
            var node = root;
            foreach (char letter in name)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.Terminal = true;
        }
        if (stocks == null) { return; }
        foreach (var stock in stocks)
        {
            if (string.IsNullOrWhiteSpace(stock.Key) || string.IsNullOrWhiteSpace(stock.Value)) { continue; }
            var node = root;
            foreach (char letter in stock.Key)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.StockSymbol = stock.Value;
        }
    }
    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            string symbol = null;
            int end = index == 0 || !Word(text[index - 1]) || Japanese(text[index - 1]) ? MatchEnd(text, start, text[index] == '@', out symbol) : -1;
            if (end > start)
            {
                if (symbol != null) { result.Append("<link=\"stock:").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(symbol))).Append("\">").Append(StockMarkOpen); }
                else { result.Append(MarkOpen); }
                AppendLiteral(result, text, index, end);
                result.Append(symbol == null ? "</mark>" : "</color></mark></link>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
    public static string Literal(string text)
    {
        if (string.IsNullOrEmpty(text)) { return ""; }
        var result = new StringBuilder(text.Length);
        AppendLiteral(result, text, 0, text.Length); return result.ToString();
    }
    public static bool TryStockSymbol(string link, out string symbol)
    {
        symbol = null;
        if (string.IsNullOrEmpty(link) || !link.StartsWith("stock:", StringComparison.Ordinal) || link.Length > 256) { return false; }
        try { symbol = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(link.Substring(6))); }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException) { return false; }
        return !string.IsNullOrWhiteSpace(symbol);
    }
    private int MatchEnd(string text, int start, bool nicknameOnly, out string symbol)
    {
        symbol = null;
        var node = root; int end = -1;
        for (int i = start; i < text.Length && node.Children.TryGetValue(text[i], out node); i++)
        {
            if ((node.Terminal || !nicknameOnly && node.StockSymbol != null) && BoundaryAfter(text, i + 1))
            { end = i + 1; symbol = nicknameOnly ? null : node.StockSymbol; }
        }
        return end;
    }
    private static bool Word(char letter) => char.IsLetterOrDigit(letter) || letter == '_';
    private static bool Japanese(char letter) => letter >= '\u3040' && letter <= '\u30ff' || letter >= '\u3400' && letter <= '\u9fff';
    private static bool BoundaryAfter(string text, int index)
    {
        if (index >= text.Length || !Word(text[index]))
        { return true; }
        foreach (string particle in Particles)
        {
            if (index + particle.Length > text.Length || string.CompareOrdinal(text, index, particle, 0, particle.Length) != 0)
            { continue; }
            int end = index + particle.Length;
            // Japanese has no space between a particle and the following clause.
            if (Japanese(particle[0])) { return true; }
            if (end == text.Length || !Word(text[end])) { return true; }
            if (particle == "님")
            {
                foreach (string suffix in Particles)
                {
                    int tail = end + suffix.Length;
                    if (tail <= text.Length && string.CompareOrdinal(text, end, suffix, 0, suffix.Length) == 0 && (tail == text.Length || !Word(text[tail]))) { return true; }
                }
            }
        }
        return false;
    }
    private static void AppendLiteral(StringBuilder result, string text, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            // Quote each '<' separately: even user-supplied </noparse> cannot escape into a TMP tag.
            if (text[i] == '<') { result.Append("<noparse><</noparse>"); }
            else { result.Append(text[i]); }
        }
    }
}
