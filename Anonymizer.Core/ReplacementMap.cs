namespace Anonymizer.Core;

/// <summary>Per-call map: same key of the same anonymizer always gets the same token; tokens never repeat across keys.</summary>
public sealed class ReplacementMap(Random random)
{
    private const int MaxAttempts = 8;
    private readonly Dictionary<string, Dictionary<string, string>> _maps = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private int _length = 4;

    /// <param name="anonymizerName">Namespace of the key (separate dictionary per anonymizer).</param>
    /// <param name="key">e.g. "/etc/projekty/". A hit does not allocate.</param>
    /// <param name="ignoreCase">Case-insensitive keys (Windows); fixed by the first call for a given name.</param>
    public string GetOrCreate(string anonymizerName, ReadOnlySpan<char> key, bool ignoreCase = false)
    {
        if (!_maps.TryGetValue(anonymizerName, out var map))
            _maps[anonymizerName] = map = new(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var lookup = map.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(key, out var token))
            return token;

        for (var attempt = 0; ; attempt++)
        {
            if (attempt == MaxAttempts) { _length++; attempt = 0; }
            token = string.Create(_length, random, static (span, r) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = (char)('a' + r.Next(26));
            });
            if (_used.Add(token)) break;
        }
        map[key.ToString()] = token;
        return token;
    }
}
