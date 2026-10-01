using System.Buffers;

namespace Anonymizer.Core;

public sealed class LinuxPathAnonymizer : IAnonymizer
{
    private static readonly SearchValues<char> StartBoundary = SearchValues.Create("\"'(=[,<");
    private static readonly SearchValues<char> EndChars = SearchValues.Create("\"'<>|*?");
    private static readonly SearchValues<char> TrailingPunctuation = SearchValues.Create(".,;:)]'\"");

    public string Name => "linux_path";

    public string Triggers => "/";

    public bool TryMatch(ReadOnlySpan<char> text, int triggerIndex, int cursor, out PathMatch match)
    {
        match = default;
        if (triggerIndex > 0)
        {
            var prev = text[triggerIndex - 1];
            if (!char.IsWhiteSpace(prev) && !StartBoundary.Contains(prev)) return false;
        }

        var end = triggerIndex;
        var slashes = 0;
        var lastSlash = triggerIndex;
        for (; end < text.Length; end++)
        {
            var c = text[end];
            if (char.IsWhiteSpace(c) || EndChars.Contains(c)) break;
            if (c == '/') { slashes++; lastSlash = end; }
        }

        while (end > triggerIndex && TrailingPunctuation.Contains(text[end - 1])) end--;

        if (slashes < 2 || lastSlash >= end - 1) return false;
        match = new PathMatch(triggerIndex, end - triggerIndex, triggerIndex, lastSlash - triggerIndex + 1);
        return true;
    }

    public string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map)
    {
        var dir = text.Slice(match.DirStart, match.DirLength);
        var file = text.Slice(match.DirStart + match.DirLength, match.Length - match.DirLength);
        return string.Concat("/", map.GetOrCreate(Name, dir), "/", file);
    }
}
