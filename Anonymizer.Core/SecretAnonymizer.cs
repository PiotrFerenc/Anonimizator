using System.Buffers;

namespace Anonymizer.Core;

/// <summary>
/// Secrets and personal data: only the value is replaced (key/scheme stays), same value = same token.
/// Detects: Authorization header value, glpat- tokens, password/passwd/token/secret/apikey/api_key/access_key pairs
/// (URL, env vars, connection strings), e-mail addresses, IPv4.
/// </summary>
public sealed class SecretAnonymizer : IAnonymizer
{
    private static readonly string[] Keys = ["password", "passwd", "token", "secret", "apikey", "api_key", "access_key"];
    private static readonly string[] Schemes = ["Bearer", "Basic", "Token"];
    private static readonly SearchValues<char> KeyChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-");
    private static readonly SearchValues<char> ValueEnd = SearchValues.Create("\"'<>&;, \t\r\n\v\f");
    private static readonly SearchValues<char> TokenChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-");
    private static readonly SearchValues<char> LocalChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._%+-");
    private static readonly SearchValues<char> DomainChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-");

    public string Name => "secrets";
    public string Triggers => "=:-@.";

    public bool TryMatch(ReadOnlySpan<char> text, int i, int cursor, out PathMatch match)
    {
        match = default;
        int start, end;
        switch (text[i])
        {
            case '=': if (!KeyValue(text, i, cursor, out start, out end)) return false; break;
            case ':': if (!Authorization(text, i, cursor, out start, out end)) return false; break;
            case '-': if (!Glpat(text, i, cursor, out start, out end)) return false; break;
            case '@': if (!Email(text, i, cursor, out start, out end)) return false; break;
            default: if (!IPv4(text, i, cursor, out start, out end)) return false; break;
        }
        match = new PathMatch(start, end - start, start, end - start);
        return true;
    }

    public string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map)
        => map.GetOrCreate(Name, text.Slice(match.Start, match.Length));

    private static bool KeyValue(ReadOnlySpan<char> text, int i, int cursor, out int start, out int end)
    {
        start = end = 0;
        var k = i;
        while (k > cursor && KeyChars.Contains(text[k - 1])) k--;
        var key = text[k..i];
        var ok = false;
        foreach (var name in Keys)
            if (key.EndsWith(name, StringComparison.OrdinalIgnoreCase)) { ok = true; break; }
        if (!ok) return false;

        start = i + 1;
        if (start < text.Length && text[start] is '"' or '\'') start++;
        var len = text[start..].IndexOfAny(ValueEnd);
        end = len < 0 ? text.Length : start + len;
        return end > start;
    }

    private static bool Authorization(ReadOnlySpan<char> text, int i, int cursor, out int start, out int end)
    {
        start = end = 0;
        var k = i;
        if (k > cursor && text[k - 1] is '"' or '\'') k--;
        const string h = "Authorization";
        if (k - h.Length < cursor || !text.Slice(k - h.Length, h.Length).Equals(h, StringComparison.OrdinalIgnoreCase)) return false;

        var p = i + 1;
        while (p < text.Length && text[p] is ' ' or '\t' or '"' or '\'') p++;
        var rest = text[p..];
        foreach (var s in Schemes)
        {
            if (!rest.StartsWith(s, StringComparison.OrdinalIgnoreCase)) continue;
            var q = p + s.Length;
            if (q >= text.Length || text[q] is not (' ' or '\t')) continue;
            while (q < text.Length && text[q] is ' ' or '\t') q++;
            var len = text[q..].IndexOfAny(" \t\r\n\"'");
            start = q;
            end = len < 0 ? text.Length : q + len;
            return end > start;
        }
        return false;
    }

    private static bool Glpat(ReadOnlySpan<char> text, int i, int cursor, out int start, out int end)
    {
        start = i - 5;
        end = i + 1;
        if (start < cursor || !text.Slice(start, 5).SequenceEqual("glpat")) return false;
        if (start > 0 && char.IsAsciiLetterOrDigit(text[start - 1])) return false;
        while (end < text.Length && TokenChars.Contains(text[end])) end++;
        return end - i - 1 >= 10;
    }

    private static bool Email(ReadOnlySpan<char> text, int i, int cursor, out int start, out int end)
    {
        start = i;
        end = i + 1;
        while (start > cursor && LocalChars.Contains(text[start - 1])) start--;
        if (start == i) return false;
        while (end < text.Length && DomainChars.Contains(text[end])) end++;
        while (end > i + 1 && text[end - 1] is '.' or '-') end--;
        var domain = text[(i + 1)..end];
        var dot = domain.LastIndexOf('.');
        if (dot < 1 || domain.Length - dot - 1 < 2) return false;
        return !domain[(dot + 1)..].ContainsAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    private static bool IPv4(ReadOnlySpan<char> text, int i, int cursor, out int start, out int end)
    {
        start = i;
        end = i;
        while (start > cursor && char.IsAsciiDigit(text[start - 1])) start--;
        if (start == i || i - start > 3) return false;
        if (start > cursor && (char.IsAsciiLetterOrDigit(text[start - 1]) || text[start - 1] == '.')) return false;
        var p = start;
        for (var octet = 0; octet < 4; octet++)
        {
            if (octet > 0)
            {
                if (p >= text.Length || text[p] != '.') return false;
                p++;
            }
            var d = p;
            while (p < text.Length && char.IsAsciiDigit(text[p])) p++;
            if (p == d || p - d > 3 || int.Parse(text[d..p]) > 255) return false;
        }
        if (p < text.Length && (char.IsAsciiLetterOrDigit(text[p]) || (text[p] == '.' && p + 1 < text.Length && char.IsAsciiDigit(text[p + 1])))) return false;
        end = p;
        return true;
    }
}
