namespace Anonymizer.Core;

/// <summary>Per-call map: same key of the same anonymizer always gets the same token; tokens never repeat across keys.</summary>
public sealed class ReplacementMap(Random random)
{
    private const int MaxAttempts = 8;
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private int _length = 4;

    /// <param name="anonymizerName">Namespace of the key (separate dictionary per anonymizer).</param>
    /// <param name="key">e.g. "/etc/projekty/". Compared ordinally; callers normalize case themselves if needed.</param>
    public string GetOrCreate(string anonymizerName, string key)
    {
        var composite = string.Concat(anonymizerName, "\0", key);
        if (_map.TryGetValue(composite, out var token))
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
        _map[composite] = token;
        return token;
    }
}
