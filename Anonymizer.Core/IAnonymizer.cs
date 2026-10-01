namespace Anonymizer.Core;

/// <summary>Absolute positions in the scanned text: whole path and its directory part to replace.</summary>
public readonly record struct PathMatch(int Start, int Length, int DirStart, int DirLength);

/// <summary>
/// Strategy for one kind of sensitive fragment. Register in DI as <see cref="IAnonymizer"/>; no change in the service needed.
/// </summary>
public interface IAnonymizer
{
    /// <summary>Unique snake_case name, e.g. "linux_path". Duplicates make the service throw at startup.</summary>
    string Name { get; }

    /// <summary>Characters on which a match may be detected.</summary>
    string Triggers { get; }

    /// <summary>
    /// <c>text[triggerIndex]</c> is one of <see cref="Triggers"/>. Must not allocate and must not return <c>Start &lt; cursor</c>.
    /// </summary>
    bool TryMatch(ReadOnlySpan<char> text, int triggerIndex, int cursor, out PathMatch match);

    /// <summary>Builds the anonymized fragment for <paramref name="match"/> (the whole path, not only the directory part).</summary>
    string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map);
}
