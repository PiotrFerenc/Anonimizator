namespace Anonymizer.Core;

public sealed record AnonymizedItem(string Original, string Anonymized, string Anonymizer);

public sealed record AnonymizationResult(
    string Original,
    string AnonymizedDocument,
    IReadOnlyList<string> Anonymizers,
    IReadOnlyList<AnonymizedItem> Anonymized);
