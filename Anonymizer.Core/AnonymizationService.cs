using System.Buffers;
using System.Text;

namespace Anonymizer.Core;

public sealed class AnonymizationService : IAnonymizationService
{
    private readonly IAnonymizer[] _anonymizers;
    private readonly SearchValues<char> _allTriggers;
    private readonly Random _random;

    public AnonymizationService(IEnumerable<IAnonymizer> anonymizers, Random random)
    {
        ArgumentNullException.ThrowIfNull(anonymizers);
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _anonymizers = anonymizers.ToArray();

        var names = new HashSet<string>(StringComparer.Ordinal);
        var chars = new HashSet<char>();
        foreach (var a in _anonymizers)
        {
            if (!names.Add(a.Name))
                throw new InvalidOperationException($"Duplicate anonymizer name '{a.Name}'.");
            // Triggers is opaque; probe the BMP once at startup to build the union.
            for (var c = 0; c < char.MaxValue; c++)
                if (a.Triggers.Contains((char)c)) chars.Add((char)c);
        }
        _allTriggers = SearchValues.Create(chars.ToArray());
    }

    public AnonymizationResult Anonymize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var span = text.AsSpan();
        var map = new ReplacementMap(_random);
        List<AnonymizedItem>? items = null;
        List<string>? used = null;
        StringBuilder? sb = null;
        var cursor = 0; // end of last match / start of unflushed text
        var scan = 0;   // where to look for the next trigger

        while (scan < span.Length)
        {
            var rel = span[scan..].IndexOfAny(_allTriggers);
            if (rel < 0) break;
            var i = scan + rel;

            IAnonymizer? best = null;
            PathMatch bm = default;
            foreach (var a in _anonymizers)
            {
                if (!a.Triggers.Contains(span[i])) continue;
                if (!a.TryMatch(span, i, cursor, out var m)) continue;
                if (best is null || m.Start < bm.Start || (m.Start == bm.Start && m.Length > bm.Length))
                {
                    best = a;
                    bm = m;
                }
            }

            if (best is null) { scan = i + 1; continue; }

            sb ??= new StringBuilder(text.Length);
            items ??= new List<AnonymizedItem>();
            used ??= new List<string>();
            sb.Append(span[cursor..bm.Start]);
            var replaced = best.Replace(bm, span, map);
            sb.Append(replaced);
            items.Add(new AnonymizedItem(text.Substring(bm.Start, bm.Length), replaced, best.Name));
            if (!used.Contains(best.Name)) used.Add(best.Name);
            cursor = scan = bm.Start + bm.Length;
        }

        if (sb is null)
            return new AnonymizationResult(text, text, [], []);

        sb.Append(span[cursor..]);
        return new AnonymizationResult(text, sb.ToString(), used!, items!);
    }
}
