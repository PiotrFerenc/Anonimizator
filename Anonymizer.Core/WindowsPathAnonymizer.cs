using System.Buffers;

namespace Anonymizer.Core;

public sealed class WindowsPathAnonymizer : IAnonymizer
{
    private static readonly SearchValues<char> Terminators = SearchValues.Create("\"<>|*? \t\r\n\v\f");
    private static readonly SearchValues<char> Trailing = SearchValues.Create(".,;:)]'\"");

    public string Name => "windows_path";
    public string Triggers => ":";

    public bool TryMatch(ReadOnlySpan<char> text, int triggerIndex, int cursor, out PathMatch match)
    {
        match = default;
        var start = triggerIndex - 1;
        if (start < cursor || !char.IsAsciiLetter(text[start])) return false;
        if (start > 0 && char.IsAsciiLetterOrDigit(text[start - 1])) return false;
        if (triggerIndex + 1 >= text.Length || text[triggerIndex + 1] != '\\') return false;

        var rest = text[(triggerIndex + 1)..];
        var len = rest.IndexOfAny(Terminators);
        if (len < 0) len = rest.Length;
        rest = rest[..len];
        var lastNot = rest.LastIndexOfAnyExcept(Trailing);
        if (lastNot < 0) return false;
        rest = rest[..(lastNot + 1)];

        var lastBs = rest.LastIndexOf('\\'); // relative to triggerIndex+1
        var dirStart = triggerIndex + 2;
        var dirLength = triggerIndex + 1 + lastBs - dirStart + 1;
        if (dirLength < 2 || lastBs + 1 >= rest.Length) return false;
        if (text.Slice(dirStart, dirLength).IndexOfAnyExcept('\\') < 0) return false;

        match = new PathMatch(start, triggerIndex + 1 + rest.Length - start, dirStart, dirLength);
        return true;
    }

    public string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map)
    {
        var token = map.GetOrCreate(Name, text.Slice(match.DirStart, match.DirLength), ignoreCase: true);
        var fileStart = match.DirStart + match.DirLength;
        return string.Concat(text.Slice(match.Start, 3), token, "\\", text[fileStart..(match.Start + match.Length)]);
    }
}
